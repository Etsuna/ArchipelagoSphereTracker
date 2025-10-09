using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using Discord;
using Discord.WebSocket;
using Sprache;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class TrackingDataManager
{
    public static class RateLimitGuards
    {
        private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSendGates = new();
        public static SemaphoreSlim GetGuildSendGate(ulong guildId, int parallelismPerGuild = 2)
            => _guildSendGates.GetOrAdd(guildId, _ => new SemaphoreSlim(parallelismPerGuild, parallelismPerGuild));
    }

    private static readonly ConcurrentDictionary<string, byte> InFlight = new();

    public static void StartTracking()
    {
        const int MaxGuildsParallel = 24;
        const int MaxChannelsParallel = 6;

        if (Declare.Cts != null)
            Declare.Cts.Cancel();

        Declare.Cts = new CancellationTokenSource();
        var token = Declare.Cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await ChannelConfigCache.LoadAllAsync();

                while (!token.IsCancellationRequested)
                {
                    if (Declare.Client.ConnectionState != ConnectionState.Connected)
                    {
                        await Task.Delay(5000, token);
                        continue;
                    }

                    var getAllGuild = await DatabaseCommands.GetAllGuildsAsync("ChannelsAndUrlsTable");
                    var uniqueGuilds = getAllGuild.Distinct().ToList();

                    await Parallel.ForEachAsync(
                        uniqueGuilds,
                        new ParallelOptions { MaxDegreeOfParallelism = MaxGuildsParallel, CancellationToken = token },
                        async (guild, ctGuild) =>
                        {
                            var guildId = ulong.Parse(guild);

                            var guildCheck = Declare.Client.GetGuild(guildId);
                            if (guildCheck == null)
                            {
                                var restGuild = await Declare.Client.Rest.GetGuildAsync(guildId);
                                if (restGuild == null)
                                {
                                    Console.WriteLine(string.Format(Resource.TDMServerNotFound, guild));
                                    await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guild);
                                    await DatabaseCommands.ReclaimSpaceAsync();
                                    Console.WriteLine(Resource.TDMDeletionCompleted);
                                }
                                return;
                            }

                            var channelsRaw = await DatabaseCommands.GetAllChannelsAsync(guild, "ChannelsAndUrlsTable");
                            var uniqueChannels = channelsRaw.Distinct().ToList();

                            var channelsToProcess = uniqueChannels
                                .Where(ch => !Declare.AddedChannelId.Contains(ch))
                                .ToList();

                            await Parallel.ForEachAsync(
                                channelsToProcess,
                                new ParallelOptions { MaxDegreeOfParallelism = MaxChannelsParallel, CancellationToken = token },
                                async (channel, ctChan) =>
                                {
                                    var key = $"{guild}:{channel}";
                                    if (!InFlight.TryAdd(key, 0))
                                        return;

                                    try
                                    {
                                        if (!ChannelConfigCache.TryGet(guild, channel, out var cfg))
                                        {
                                            var (tracker, baseUrl, room, silent, checkFrequencyStr, lastCheckStr)
                                                = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guild, channel);

                                            if (string.IsNullOrWhiteSpace(tracker) || string.IsNullOrWhiteSpace(baseUrl))
                                                return;

                                            var checkFrequency = CheckFrequencyParser.ParseOrDefault(
                                                checkFrequencyStr,
                                                TimeSpan.FromMinutes(5),
                                                TimeSpan.FromMinutes(5),
                                                null);

                                            DateTimeOffset? last = null;
                                            if (!string.IsNullOrWhiteSpace(lastCheckStr) &&
                                                DateTimeOffset.TryParse(lastCheckStr, CultureInfo.InvariantCulture,
                                                                        DateTimeStyles.AssumeUniversal, out var dt))
                                            {
                                                last = dt;
                                            }

                                            cfg = new ChannelConfig(tracker, baseUrl, room, silent, checkFrequency, last);
                                            ChannelConfigCache.Upsert(guild, channel, cfg);
                                        }

                                        var channelId = ulong.Parse(channel);
                                        var guildChannel = guildCheck.GetChannel(channelId) as SocketGuildChannel; 
                                        var thread = guildCheck.ThreadChannels.FirstOrDefault(t => t.Id == channelId);

                                        if (guildChannel is null && thread is null)
                                        {
                                            var restChan = await Declare.Client.Rest.GetChannelAsync(channelId);
                                            if (restChan == null)
                                            {
                                                Console.WriteLine(string.Format(Resource.TDMChannelNoLongerExists, channel));
                                                await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                                await DatabaseCommands.ReclaimSpaceAsync();
                                                Console.WriteLine(Resource.TDMDeletionCompleted);
                                                ChannelConfigCache.Remove(guild, channel);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"[TDM] REST confirme l'existence du canal {channelId}, on saute cette passe.");
                                            }
                                            return;
                                        }

                                        var nameForLog = thread?.Name ?? guildChannel!.Name;
                                        Console.WriteLine(string.Format(Resource.TDMChannelStillExists, nameForLog));

                                        if (thread != null)
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
                                                    await RateLimitGuards.GetGuildSendGate(guildCheck.Id).WaitAsync(ctChan);
                                                    try
                                                    {
                                                        await BotCommands.SendMessageAsync(string.Format(Resource.TDMNewMessageOnThread, thread.Name), thread.Id.ToString());
                                                    }
                                                    finally
                                                    {
                                                        RateLimitGuards.GetGuildSendGate(guildCheck.Id).Release();
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

                                                    await RateLimitGuards.GetGuildSendGate(guildCheck.Id).WaitAsync(ctChan);
                                                    try
                                                    {
                                                        await BotCommands.SendMessageAsync(
                                                            string.Format(Resource.TDMNoMessage6Days, formattedDeletionDate, thread.Name),
                                                            thread.ParentChannel.Id.ToString());
                                                    }
                                                    finally
                                                    {
                                                        RateLimitGuards.GetGuildSendGate(guildCheck.Id).Release();
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
                                                ChannelConfigCache.Remove(guild, channel);
                                                return;
                                            }
                                        }

                                        var (shouldRun, checkFrequencyTs) = ChannelConfigCache.ShouldRunChecks(cfg);
                                        if (!shouldRun)
                                        {
                                            Console.WriteLine(string.Format(Resource.TDMSkippingCheck, nameForLog, checkFrequencyTs.TotalMinutes));
                                            return;
                                        }

                                        Console.WriteLine(string.Format(Resource.TDMCheckingItems, nameForLog));
                                        await GetTableDataAsync(guild, channel, cfg.BaseUrl, cfg.Tracker, cfg.Silent, ctChan);

                                        await ChannelsAndUrlsCommands.UpdateLastCheckAsync(guild, channel);
                                    }
                                    finally
                                    {
                                        InFlight.TryRemove(key, out _);
                                    }
                                });
                        });

                    Console.WriteLine(Resource.TDMWaitingCheck);
                    await Telemetry.SendTelemetryAsync(Declare.ProgramID);
                    await DatabaseCommands.ReclaimSpaceAsync();
                    await Task.Delay(60000, token); 
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine(Resource.TDMTrackingCanceled);
            }
        }, token);
    }

    internal static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static Task GetTableDataAsync(string guild, string channel, string baseUrl, string tracker, bool silent, bool sendAsTextFile)
        => GetTableDataAsync(guild, channel, baseUrl, tracker, silent, CancellationToken.None, sendAsTextFile);

    public static async Task GetTableDataAsync(string guild, string channel, string baseUrl, string tracker, bool silent, CancellationToken ctChan, bool sendAsTextFile = false)
    {
        var ctx = await ProcessingContextLoader.LoadOneShotAsync(guild, channel, silent).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctChan);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{tracker}";
        var urlStatic = $"{baseUrl.TrimEnd('/')}/api/static_tracker/{tracker}";

        string json, jsonStatic;
        try
        {
            json = await Http.GetStringAsync(url, cts.Token);
            jsonStatic = await Http.GetStringAsync(urlStatic, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[TDM] Serveur indisponible ou lent pour {baseUrl}. Abandon de la passe.");
            return;
        }
        catch (HttpRequestException hre)
        {
            Console.WriteLine($"[TDM] Erreur HTTP pour {url}: {hre.Message}");
            return;
        }

        var items = TrackerStreamParser.ParseItems(ctx, json);
        var hints = TrackerStreamParser.ParseHints(ctx, json);
        var statuses = TrackerStreamParser.ParseGameStatus(ctx, json, jsonStatic);
        if (items.Count == 0 && hints.Count == 0 && statuses.Count == 0) return;

        if (statuses.Count > 0) await ProcessGameStatusTableAsync(guild, channel, statuses, silent, ctChan).ConfigureAwait(false);
        if (items.Count > 0) await ProcessItemsTableAsync(guild, channel, items, silent, ctChan).ConfigureAwait(false);
        if (hints.Count > 0) await ProcessHintTableAsync(guild, channel, hints, silent, ctChan, sendAsTextFile).ConfigureAwait(false);
    }

    private static async Task<string> BuildMessageAsync(string guild, string channel, DisplayedItem item, bool silent)
    {
        if (!silent)
        {
            if (item.Finder == item.Receiver)
                return string.Empty;
        }

        if (int.TryParse(item.Location, out var loc) && loc < 0 || int.TryParse(item.Item, out var itm) && itm < 0)
            return string.Empty;

        var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, item.Receiver);
        if (userIds.Count > 0)
        {
            if (silent && item.Finder == item.Receiver)
                return string.Empty;

            if (userIds.Any(x => x.IsEnabled.Equals(true)))
            {
                var getGameName = await AliasChoicesCommands.GetGameForAliasAsync(guild, channel, item.Receiver);
                if (!string.IsNullOrWhiteSpace(getGameName))
                {
                    if (item.Flag is "0")
                        return string.Empty;

                    return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
                }
            }

            return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
        }

        if (silent)
            return string.Empty;

        return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
    }

    private static async Task ProcessItemsTableAsync(string guild, string channel, List<DisplayedItem> receivedItem, bool silent, CancellationToken ctChan)
    {
        var channelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "DisplayedItemTable");
        var existingKeys = new HashSet<string>(await DisplayItemCommands.GetExistingKeysAsync(guild, channel));
        var newItems = new List<DisplayedItem>();

        foreach (var di in receivedItem)
        {
            var key = $"{di.Finder}|{di.Receiver}|{di.Item}|{di.Location}|{di.Game}|{di.Flag}";
            if (!existingKeys.Contains(key))
                newItems.Add(di);
        }

        if (newItems.Count != 0)
        {
            await DisplayItemCommands.AddItemsAsync(newItems, guild, channel);
            await RecapListCommands.AddOrEditRecapListItemsForAllAsync(guild, channel, newItems);

            if (channelExists)
            {
                ulong guildIdLong = ulong.Parse(guild);

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
                            ? $"**{Resource.ItemFor} {receiver} {mentions} ({withHeader.Count}) [{i + 1}/{chunks.Count}]:**"
                            : $"**{Resource.ItemFor} {receiver} {mentions} ({withHeader.Count}):**";

                        string finalMessage = header + "\n" + chunks[i];

                        await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                        try
                        {
                            await BotCommands.SendMessageAsync(finalMessage, channel);
                        }
                        finally
                        {
                            RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                        }

                        await Task.Delay(1100, ctChan);
                    }
                }
            }
        }
    }

    private static async Task ProcessHintTableAsync(string guild, string channel, List<HintStatus> hintsList, bool silent, CancellationToken ctChan = default, bool sendAsTextFile = false)
    {
        var existingList = await HintStatusCommands.GetHintStatus(guild, channel);
        var existingByKey = existingList.ToDictionary(MakeKey);

        var hintsToAdd = new List<HintStatus>();
        var hintsToUpdate = new List<HintStatus>();

        if (hintsList == null || hintsList.Count == 0)
            return;

        foreach (var hint in hintsList)
        {
            var key = MakeKey(hint);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Flag, hint.Flag.ToString(), StringComparison.Ordinal))
                {
                    existing.Flag = hint.Flag.ToString();
                    hintsToUpdate.Add(existing);
                }
            }
            else
            {
                hintsToAdd.Add(hint);
            }
        }

        if (hintsToAdd.Count > 0)
        {
            await HintStatusCommands.AddHintStatusAsync(guild, channel, hintsToAdd);

            if (!silent)
            {
                ulong guildIdLong = ulong.Parse(guild);

                var eligible = hintsToAdd.Where(h => h.Finder != h.Receiver).ToList();
                if (eligible.Count > 0)
                {
                    static IEnumerable<string> BuildUnifiedLines(IEnumerable<HintStatus> hints)
                    {
                        foreach (var g in hints.GroupBy(h => h.Receiver))
                        {
                            yield return $"{Resource.HintNew}: {g.Key}:"; 
                            foreach (var h in g)
                                yield return string.Format(Resource.HintItemNew, h.Item, h.Location, h.Finder);
                            yield return string.Empty;
                        }
                    }

                    var allLines = BuildUnifiedLines(eligible);

                    if (sendAsTextFile)
                    {
                        string content = string.Join("\n", allLines);
                        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content.Replace("**", "")));
                        string fileName = $"hints_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

                        await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                        try
                        {
                            await BotCommands.SendFileAsync(channel, ms, fileName);
                        }
                        finally
                        {
                            RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                        }
                        await Task.Delay(1100, ctChan);
                    }
                    else
                    {
                        foreach (var chunk in ChunkMessages(allLines, 1900))
                        {
                            await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                            try
                            {
                                await BotCommands.SendMessageAsync(chunk, channel);
                            }
                            finally
                            {
                                RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                            }
                            await Task.Delay(1100, ctChan);
                        }
                    }
                }
            }
        }

        // --- UPDATED HINTS ---
        if (hintsToUpdate.Count > 0)
        {
            await HintStatusCommands.UpdateHintStatusAsync(guild, channel, hintsToUpdate);

            if (!silent)
            {
                ulong guildIdLong = ulong.Parse(guild);

                var eligible = hintsToUpdate.Where(h => h.Finder != h.Receiver).ToList();
                if (eligible.Count > 0)
                {
                    static IEnumerable<string> BuildUnifiedLinesUpdated(IEnumerable<HintStatus> hints)
                    {
                        foreach (var g in hints.GroupBy(h => h.Receiver))
                        {
                            yield return $"{Resource.HintUpdated}: {g.Key}'s :";
                            foreach (var h in g)
                                yield return string.Format(Resource.HintItemNew, h.Item, h.Location, h.Finder);
                            yield return string.Empty;
                        }
                    }

                    var allLines = BuildUnifiedLinesUpdated(eligible);

                    if (sendAsTextFile)
                    {
                        string content = string.Join("\n", allLines);
                        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
                        string fileName = $"hints_updated_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

                        await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                        try
                        {
                            await BotCommands.SendFileAsync(channel, ms, fileName);
                        }
                        finally
                        {
                            RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                        }
                        await Task.Delay(1100, ctChan);
                    }
                    else
                    {
                        foreach (var chunk in ChunkMessages(allLines, 1900))
                        {
                            await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                            try
                            {
                                await BotCommands.SendMessageAsync(chunk, channel);
                            }
                            finally
                            {
                                RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                            }
                            await Task.Delay(1100, ctChan);
                        }
                    }
                }
            }
        }
    }


    private static async Task ProcessGameStatusTableAsync(string guild, string channel, List<GameStatus> statuses, bool silent, CancellationToken ctChan)
    {
        if (statuses == null || statuses.Count == 0)
            return;

        var previous = await GameStatusCommands.GetGameStatusForGuildAndChannelAsync(guild, channel).ConfigureAwait(false);
        var prevByKey = previous.ToDictionary(
            x => MakeKey(x.Name, x.Game),
            x => x,
            StringComparer.Ordinal);

        var newlyCompleted = new List<GameStatus>();

        foreach (var cur in statuses)
        {
            var key = MakeKey(cur.Name, cur.Game);

            bool prevComplete = prevByKey.TryGetValue(key, out var prev)
                && IsComplete(prev.Checks, prev.Total);

            bool nowComplete = IsComplete(cur.Checks, cur.Total);

            if (!prevComplete && nowComplete)
            {
                newlyCompleted.Add(cur);
            }
        }

        await GameStatusCommands.UpdateGameStatusBatchAsync(guild, channel, statuses).ConfigureAwait(false);

        if (newlyCompleted.Count > 0)
        {
            foreach (var done in newlyCompleted)
            {
                bool canAnnounce = !silent;

                if (silent)
                {
                    var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, done.Name);
                    canAnnounce = userIds.Count > 0;
                }

                if (previous.Count == 0)
                    canAnnounce = false;

                if (canAnnounce)
                {
                    ulong guildIdLong = ulong.Parse(guild);
                    string text = string.Format(Resource.TDMGoalComplete, done.Name, done.Game);

                    await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                    try
                    {
                        await BotCommands.SendMessageAsync(text, channel);
                    }
                    finally
                    {
                        RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                    }

                    await Task.Delay(1100, ctChan);
                }
            }
        }
    }

    private static string MakeKey(string? name, string? game)
        => $"{name ?? ""}|{game ?? ""}";

    private static bool IsComplete(string? checksStr, string? totalStr)
    {
        if (!int.TryParse(checksStr, out var checks)) checks = 0;
        if (!int.TryParse(totalStr, out var total)) total = 0;
        return total > 0 && checks >= total;
    }

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

    /// <summary>
    /// Parse des durées type "12h", "5m", "1d2h30m", "90m", etc.
    /// Min garanti à 5 minutes si tu le passes en paramètre.
    /// </summary>
    public static class CheckFrequencyParser
    {
        private static readonly Regex TokenRegex = new(
            @"(?ix)(?<num>\d+)\s*(?<unit>d|h|m|s)", RegexOptions.Compiled);

        public static bool TryParse(string? input, out TimeSpan result,
                                    TimeSpan? min = null, TimeSpan? max = null)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            long totalSeconds = 0;
            foreach (Match m in TokenRegex.Matches(input))
            {
                if (!long.TryParse(m.Groups["num"].Value, out var n) || n < 0) return false;
                var unit = m.Groups["unit"].Value.ToLowerInvariant();
                long factor = unit switch
                {
                    "d" => 86400,
                    "h" => 3600,
                    "m" => 60,
                    "s" => 1,
                    _ => 0
                };
                if (factor == 0) return false;
                checked { totalSeconds += n * factor; }
            }
            if (totalSeconds <= 0) return false;

            var ts = TimeSpan.FromSeconds(totalSeconds);
            if (min is not null && ts < min.Value) return false;
            if (max is not null && ts > max.Value) return false;

            result = ts;
            return true;
        }

        public static TimeSpan ParseOrDefault(string? input, TimeSpan @default,
                                              TimeSpan? min = null, TimeSpan? max = null)
            => TryParse(input, out var ts, min, max) ? ts : @default;
    }

    private static string MakeKey(HintStatus h) =>
        $"{h.Finder}|{h.Receiver}|{h.Item}|{h.Location}|{h.Game}";
}
