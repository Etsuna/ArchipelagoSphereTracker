using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
using Sprache;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using static GetData;
using static System.Net.Mime.MediaTypeNames;


public static class TrackingDataManager
{
    public static class RateLimitGuards
    {
        public static readonly SemaphoreSlim SendMessageGate = new(1, 1);
    }

    private static readonly ConcurrentDictionary<string, byte> InFlight = new();

    public record NetworkHintDto(
        int finding_player,
        int receiving_player,
        long item,
        int item_flags,
        long location,
        bool found,
        string? entrance
    );

    public static void StartTracking()
    {
        const int MaxGuildsParallel = 10;
        const int MaxChannelsParallel = 2;

        if (Declare.Cts != null)
        {
            Declare.Cts.Cancel();
        }

        Declare.Cts = new CancellationTokenSource();
        var token = Declare.Cts.Token;

        Task.Run(async () =>
        {
            try
            {
                var programID = await DatabaseCommands.ProgramIdentifier("ProgramIdTable");

                while (!token.IsCancellationRequested)
                {
                    var getAllGuild = await DatabaseCommands.GetAllGuildsAsync("ChannelsAndUrlsTable");
                    var uniqueGuilds = getAllGuild.Distinct().ToList();

                    await Telemetry.SendDailyTelemetryAsync(programID);

                    await Parallel.ForEachAsync(
                    uniqueGuilds,
                    new ParallelOptions { MaxDegreeOfParallelism = MaxGuildsParallel },
                    async (guild, ctGuild) =>
                    {
                        var guildCheck = Declare.Client.GetGuild(ulong.Parse(guild));
                        if (guildCheck == null)
                        {
                            Console.WriteLine(string.Format(Resource.TDMServerNotFound, guild));
                            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guild);
                            await DatabaseCommands.ReclaimSpaceAsync();
                            Console.WriteLine(Resource.TDMDeletionCompleted);
                            return;
                        }

                        var channelsRaw = await DatabaseCommands.GetAllChannelsAsync(guild, "ChannelsAndUrlsTable");
                        var uniqueChannels = channelsRaw.Distinct().ToList();

                        var channelsToProcess = uniqueChannels
                            .Where(ch => !Declare.AddedChannelId.Contains(ch))
                            .ToList();

                        await Parallel.ForEachAsync(
                            channelsToProcess,
                            new ParallelOptions { MaxDegreeOfParallelism = MaxChannelsParallel },
                            async (channel, ctChan) =>
                            {
                                var key = $"{guild}:{channel}";

                                if (!InFlight.TryAdd(key, 0))
                                {
                                    return;
                                }

                                try
                                {
                                    var (tracker, baseUrl, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guild, channel);
                                    var channelCheck = guildCheck.GetChannel(ulong.Parse(channel));

                                    if (channelCheck == null)
                                    {
                                        Console.WriteLine(string.Format(Resource.TDMChannelNoLongerExists, channel));
                                        await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                        await DatabaseCommands.ReclaimSpaceAsync();
                                        Console.WriteLine(Resource.TDMDeletionCompleted);
                                        return;
                                    }

                                    Console.WriteLine(string.Format(Resource.TDMChannelStillExists, channelCheck.Name));

                                    if (guildCheck.GetChannel(ulong.Parse(channel)) is SocketThreadChannel thread)
                                    {
                                        var messages = await thread.GetMessagesAsync(1).FlattenAsync();
                                        var lastMessage = messages.FirstOrDefault();

                                        DateTimeOffset lastActivity = lastMessage?.Timestamp ?? SnowflakeUtils.FromSnowflake(thread.Id);
                                        if (lastMessage == null)
                                            Console.WriteLine(string.Format(Resource.TDMNoMessageFound, lastActivity));

                                        double daysInactive = (DateTimeOffset.UtcNow - lastActivity).TotalDays;

                                        if (daysInactive < 6)
                                        {
                                            if (Declare.WarnedThreads.Contains(thread.Id.ToString()))
                                            {
                                                await RateLimitGuards.SendMessageGate.WaitAsync(ctChan);
                                                try
                                                {
                                                    await BotCommands.SendMessageAsync(string.Format(Resource.TDMNewMessageOnThread, thread.Name), thread.Id.ToString());
                                                }
                                                finally
                                                {
                                                    RateLimitGuards.SendMessageGate.Release();
                                                }
                                                Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            }
                                        }
                                        else if (daysInactive < 7)
                                        {
                                            if (!Declare.WarnedThreads.Contains(thread.Id.ToString()))
                                            {
                                                DateTimeOffset deletionDate = lastActivity.AddTicks(TimeSpan.TicksPerDay * 7);
                                                TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
                                                DateTimeOffset localDeletionDate = TimeZoneInfo.ConvertTime(deletionDate, frenchTimeZone);
                                                string formattedDeletionDate = localDeletionDate.ToString("dddd d MMMM yyyy à HH'h'mm", CultureInfo.GetCultureInfo("fr-FR"));

                                                await RateLimitGuards.SendMessageGate.WaitAsync(ctChan);
                                                try
                                                {
                                                    await BotCommands.SendMessageAsync(
                                                        string.Format(Resource.TDMNoMessage6Days, formattedDeletionDate, thread.Name),
                                                        thread.ParentChannel.Id.ToString());
                                                }
                                                finally
                                                {
                                                    RateLimitGuards.SendMessageGate.Release();
                                                }

                                                Declare.WarnedThreads.Add(thread.Id.ToString());
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine(string.Format(Resource.TDMLastActivity, lastActivity));
                                            Console.WriteLine(Resource.TDMNoActivity);
                                            await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                            await DatabaseCommands.ReclaimSpaceAsync();
                                            await thread.DeleteAsync();
                                            Console.WriteLine(Resource.TDMThreadDeleted);
                                            Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            return;
                                        }
                                    }
                                    Console.WriteLine(string.Format(Resource.TDMCheckingItems, channelCheck.Name));
                                    await GetTableDataAsync(guild, channel, baseUrl, room, silent);
                                }
                                finally
                                {
                                    InFlight.TryRemove(key, out _);
                                }
                            });
                    });
                    Console.WriteLine(Resource.TDMWaitingCheck);
                    await DatabaseCommands.ReclaimSpaceAsync();
                    await Task.Delay(300000);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine(Resource.TDMTrackingCanceled);
            }
        }, token);
    }

    public static async Task GetTableDataAsync(string guild, string channel, string baseUrl, string room, bool silent)
    {
        var receivedItem = await Data(baseUrl, room);

        if (receivedItem == null)
        {
            Console.WriteLine("Aucun item publié.");
            return;
        }

       /* if (hints == null)
        {
            Console.WriteLine("Aucun item publié.");
            return;
        }*/

       await ProcessItemsTableAsync(guild, channel, receivedItem, silent);
       //await ProcessHintTableAsync(guild, channel, hints, silent);
    }

    private static async Task<string> BuildMessageAsync(string guild, string channel, DisplayedItem item, bool silent)
    {
        if (!silent)
        {
            if (item.Finder == item.Receiver)
                return string.Empty;
        }
        var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, item.Receiver);
        if (userIds.Count > 0)
        {
            if (silent)
            {
                if (item.Finder == item.Receiver)
                    return string.Empty;
            }

            if (userIds.Any(x => x.IsEnabled.Equals(true)))
            {
                var getGameName = await AliasChoicesCommands.GetGameForAliasAsync(guild, channel, item.Receiver);

                if (!string.IsNullOrWhiteSpace(getGameName))
                {
                    var isFiller = await ItemsCommands.IsFillerAsync(guild, channel, getGameName, item.Item);

                    if (isFiller)
                    {
                        return string.Empty;
                    }
                    return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
                }
            }

            return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
        }

        if (silent)
        {
            return string.Empty;
        }
        return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
    }

    private static async Task ProcessItemsTableAsync(string guild, string channel, List<TrackerItemsEnricher.EnrichedTeamItems> receivedItem, bool silent)
    {
        var channelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "DisplayedItemTable");
        var existingKeys = new HashSet<string>(await DisplayItemCommands.GetExistingKeysAsync(guild, channel));
        var newItems = new List<DisplayedItem>();
        var ownItemCount = 0;

        foreach (var received in receivedItem)
        {
            foreach (var player in received.Players)
            {
                foreach (var it in player.Items)
                {
                    if(it.FromPlayerName == "Player0")
                    {
                        continue;
                    }

                    var di = new DisplayedItem
                    {
                        Finder = it.FromPlayerName,
                        Receiver = it.ToPlayerName,
                        Item = it.ItemName,
                        Location = it.LocationName,
                        Game = it.FromPlayerGame,
                        Flag = it.Flags.ToString(),
                    };

                    var key = $"{di.Finder}|{di.Receiver}|{di.Item}|{di.Location}|{di.Game}|{di.Flag}";
                    if (!existingKeys.Contains(key))
                    {
                        ownItemCount++;
                        newItems.Add(di);
                    }
                }
            }
        }

        if (newItems.Count != 0)
        {
            await DisplayItemCommands.AddItemsAsync(newItems, guild, channel);
            await RecapListCommands.AddOrEditRecapListItemsForAllAsync(guild, channel, newItems);

            if (channelExists)
            {
                var groupedByReceiver = newItems.GroupBy(item => item.Receiver ?? "Inconnu");

                foreach (var group in groupedByReceiver)
                {
                    var receiver = group.Key;

                    var messages = await Task.WhenAll(
                        group.Select(item => BuildMessageAsync(guild, channel, item, silent))
                    );

                    var withHeader = messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                    var chunks = ChunkMessages(withHeader).ToList();

                    var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, receiver);
                    var mentions = string.Join(" ", userIds.Select(x => x.UserId).Select(id => $"<@{id}>"));

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        string header = chunks.Count > 1
                            ? $"**{Resource.ItemFor} {receiver} {mentions} ({group.Count() - ownItemCount}) [{i + 1}/{chunks.Count}]:**"
                            : $"**{Resource.ItemFor} {receiver} {mentions} ({group.Count() - ownItemCount}):**";

                        string finalMessage = header + "\n" + chunks[i];

                        await RateLimitGuards.SendMessageGate.WaitAsync();
                        try
                        {
                            await BotCommands.SendMessageAsync(finalMessage, channel);
                        }
                        finally
                        {
                            RateLimitGuards.SendMessageGate.Release();
                        }

                        await Task.Delay(1100);
                    }
                }
            }
        }
    }

   /* private static async Task ProcessHintTableAsync(string guild, string channel, List<TrackerItemsEnricher.EnrichedTeamHints> hintsList, bool silent)
    {
        var existingList = await HintStatusCommands.GetHintStatus(guild, channel);
        var existingByKey = existingList.ToDictionary(MakeKey);

        var hintsToAdd = new List<HintStatus>();
        var hintsToUpdate = new List<HintStatus>();


        if (hintsList == null || hintsList.Count == 0)
        {
            Console.WriteLine("Aucun hint publié.");
            return;
        }

        Console.WriteLine($"\n=== {hintsList.Count} hint(s) publié(s) ===");
        foreach (var players in hintsList)
        {
            foreach(var hints in players.Players)
            {
                foreach(var h in hints.Hints)
                {
                    var hint = new HintStatus
                    {
                        Finder = h.FinderName,
                        Receiver = h.ReceiverName,
                        Item = h.ItemName,
                        Location = h.LocationName,
                        Game = h.FinderGame,
                        Entrance = h.Raw5 ?? string.Empty,
                        Flag = h.Found.ToString()
                    };

                    var key = MakeKey(hint);
                    if (existingByKey.TryGetValue(key, out var existing))
                    {
                        if (!string.Equals(existing.Flag, h.Found.ToString(), StringComparison.Ordinal))
                        {
                            existing.Flag = h.Found.ToString();
                            hintsToUpdate.Add(existing);
                        }
                    }
                    else
                    {
                        hintsToAdd.Add(hint);
                    }
                }
            }
        }

        if (hintsToAdd.Count > 0)
            await HintStatusCommands.AddHintStatusAsync(guild, channel, hintsToAdd);

        if (hintsToUpdate.Count > 0)
            await HintStatusCommands.UpdateHintStatusAsync(guild, channel, hintsToUpdate);
    }*/

    private static IEnumerable<string> ChunkMessages(IEnumerable<string> messages, int maxLength = 1900)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            if (string.IsNullOrWhiteSpace(msg)) continue;

            if (sb.Length + msg.Length + (sb.Length > 0 ? 1 : 0) > maxLength)
            {
                yield return sb.ToString();
                sb.Clear();
            }

            if (sb.Length > 0) sb.AppendLine();
            sb.Append(msg);
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    private static string Normalize(string s) =>
    new string((s ?? "").Trim().ToLowerInvariant()
        .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
        .ToArray());

    private static string Clean(HtmlNode? td)
    {
        if (td is null) return "";
        var t = WebUtility.HtmlDecode(td.InnerText)?.Trim();
        return string.IsNullOrEmpty(t) ? "" : t.Replace('\u00A0', ' ');
    }

    private static string MakeKey(HintStatus h) =>
        $"{h.Finder}|{h.Receiver}|{h.Item}|{h.Location}|{h.Game}|{h.Flag}";
}
