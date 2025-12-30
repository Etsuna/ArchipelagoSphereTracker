using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using Discord;
using Discord.WebSocket;
using System.Net;
using System.Text;
using System.Text.Json;
using TrackerLib.Models;

public class UrlClass
{
    public static async Task<string> AddUrl(SocketSlashCommand command, IGuildUser? guildUser, string channelId, string guildId, ITextChannel channel)
    {
        string baseUrl = string.Empty;
        string? tracker = string.Empty;
        string? room = string.Empty;
        string port = string.Empty;

        var newUrl = command.Data.Options.ElementAt(0)?.Value as string;
        var threadTitle = command.Data.Options.ElementAt(1)?.Value as string ?? "Archipelago";
        var threadType = command.Data.Options.ElementAt(2)?.Value as string ?? "Private";
        var autoAddMembers = command.Data.Options.ElementAtOrDefault(3)?.Value as bool? ?? false;
        var silent = command.Data.Options.ElementAtOrDefault(4)?.Value as bool? ?? false;
        var checkFrequencyStr = command.Data.Options.ElementAtOrDefault(5)?.Value as string ?? "5m";
        var message = string.Empty;

        if (newUrl == null)
        {
            message = "Is Null";
            return message;
        }

        var uri = new Uri(newUrl);
        baseUrl = $"{uri.Scheme}://{uri.Authority}";
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        room = segments.Length > 1 ? segments[^1] : "";

        bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

        var roomInfo = await RoomInfo(baseUrl, room);
        if (roomInfo == null)
        {
            message = "Room Not Found";
            return message;
        }

        var existingChannelForRoom =  await ChannelsAndUrlsCommands.GetChannelIdForRoomAsync(guildId, baseUrl, room);

        if (!string.IsNullOrEmpty(existingChannelForRoom) && existingChannelForRoom != channelId)
        {
            message = string.Format(Resource.RoomAlreadyExistInOtherThread, existingChannelForRoom);
            return message;
        }

        tracker = roomInfo.Tracker ?? tracker;
        port = !string.IsNullOrEmpty(roomInfo.LastPort.ToString()) ? roomInfo.LastPort.ToString() : port;

        async Task<bool> CanAddUrlAsync(string gId, string cId)
        {
            var exists = await DatabaseCommands.CheckIfChannelExistsAsync(gId, cId, "ChannelsAndUrlsTable");
            return !exists;
        }

        async Task<(bool isValid, string message)> IsAllUrlIsValidAsync()
        {
            if (!await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId))
                return (false, Resource.UrlCheckMaxTread);

            var playersCount = roomInfo.Players.Count;
            
            if(playersCount <= 1)
            {
                return (false, Resource.CheckPlayerMin);
            }

            if (playersCount > Declare.MaxPlayer)
                return (false, string.Format(Resource.CheckPlayerMax, Declare.MaxPlayer));

            return (true, string.Empty);
        }

        if (await CanAddUrlAsync(guildId, channelId))
        {
            if (string.IsNullOrEmpty(newUrl))
            {
                message = Resource.URLEmpty;
            }
            else if (!IsValidUrl(newUrl))
            {
                message = Resource.URLNotValid;
            }
            else
            {
                var (isValid, errorMessage) = await IsAllUrlIsValidAsync();
                if (!isValid)
                {
                    message = errorMessage;
                }
                else
                {
                    ThreadType type = threadType switch
                    {
                        "Private" => ThreadType.PrivateThread,
                        "Public" => ThreadType.PublicThread,
                        _ => ThreadType.PrivateThread
                    };

                    var thread = await channel.CreateThreadAsync(
                        threadTitle,
                        autoArchiveDuration: ThreadArchiveDuration.OneWeek,
                        type: type
                    );

                    await thread.SendMessageAsync(string.Format(Resource.UrlThredCreated, thread.Name));
                    channelId = thread.Id.ToString();

                    if (type == ThreadType.PrivateThread)
                    {
                        if (command.User is IGuildUser user)
                            await thread.AddUserAsync(user);
                        else
                            message = Resource.UrlPrivateThreadUserNotFound;
                    }
                    else
                    {
                        if(autoAddMembers)
                        {
                            await foreach (var memberBatch in channel.GetUsersAsync())
                            {
                                foreach (var member in memberBatch)
                                    await thread.AddUserAsync(member);
                            }
                        }
                        else
                        {
                            if (command.User is IGuildUser user)
                                await thread.AddUserAsync(user);
                            else
                                message = Resource.UrlPrivateThreadUserNotFound;
                        }
                    }

                    var patchLinkList = new List<Patch>();
                    var aliasList = new List<(int slot, string alias, string game)>();
                    var aliasSlot = 1;

                    foreach (var player in roomInfo.Players)
                    {
                        aliasList.Add((aliasSlot, player.Name, player.Game));
                        aliasSlot++;
                    }

                    foreach (var download in roomInfo.Downloads)
                    {
                        aliasList.Where(x => x.slot == download.Slot).ToList().ForEach(slot =>
                        {
                            var patchLink = new Patch
                            {
                                GameAlias = slot.alias,
                                GameName = slot.game,
                                PatchLink = baseUrl + download.Download,
                            };
                            patchLinkList.Add(patchLink);
                            Console.WriteLine(string.Format(Resource.UrlGamePatch, patchLink.GameAlias, patchLink.PatchLink));
                        });
                    }

                    if (!string.IsNullOrEmpty(tracker))
                    {
                        Declare.AddedChannelId.Add(channelId);

                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, baseUrl, room, tracker, silent, checkFrequencyStr, port);
                        var rootTracker = await TrackerDatapackageFetcher.getRoots(baseUrl, tracker, TrackingDataManager.Http);
                        var checksums = TrackerDatapackageFetcher.GetDatapackageChecksums(rootTracker);
                        await TrackerDatapackageFetcher.SeedDatapackagesFromTrackerAsync(baseUrl, guildId, channelId, rootTracker);
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guildId, channelId, aliasList);
                        await BotCommands.SendMessageAsync(Resource.TDMAliasUpdated, channelId);
                        var info = await HelperClass.Info(channelId, guildId);
                        await BotCommands.SendMessageAsync(info, channelId);
                        using MemoryStream playersStream = await SendPlayersInfoAsync(channelId, thread, aliasList, roomInfo, room);
                        await ChannelsAndUrlsCommands.SendAllPatchesFileForChannelAsync(guildId, channelId);
                        await TrackingDataManager.GetTableDataAsync(guildId, channelId, baseUrl, tracker, silent, true);
                        await Telemetry.SendTelemetryAsync(Declare.ProgramID, false);
                        await ChannelsAndUrlsCommands.UpdateLastCheckAsync(guildId, channelId);
                        
                        if (Declare.TelemetryName != "AST")
                        {
                            await UpdateReminder.MaybeNotifyDailyAsync(guildId, channelId, "Etsuna", "ArchipelagoSphereTracker", CancellationToken.None);
                        }
                        await BotCommands.SendMessageAsync(Resource.Discord, channelId);
                        await BotCommands.SendMessageAsync(Resource.URLBotReady, channelId);


                        Declare.AddedChannelId.Remove(channelId);
                    }
                    message = string.Format(Resource.URLSet, newUrl);
                }
            }
        }
        else
        {
            message = Resource.URLAlreadySet;
        }

        return message;

        static async Task<MemoryStream> SendPlayersInfoAsync(string channelId, IThreadChannel thread, List<(int slot, string alias, string game)> aliasList, RoomStatus roomInfo, string room)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Resource.PlayerAndGameList);
            sb.AppendLine();

            foreach (var (slot, alias, game) in aliasList)
            {
                sb.AppendLine($"• {alias} - {game}");
            }

            var playersTxt = sb.ToString();

            var playersBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(playersTxt);
            var playersStream = new MemoryStream(playersBytes);

            var playersFileName = $"players_{channelId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

            var playersMessage = await thread.SendFileAsync(
                playersStream,
                playersFileName,
                Resource.PlayerList
            );
            return playersStream;
        }
    }

    public static async Task<string> DeleteUrl(IGuildUser? guildUser, string channelId, string guildId)
    {
        var message = await DeleteChannelAndUrl(channelId, guildId);
        return message;
    }

    public static async Task<string> DeleteChannelAndUrl(string? channelId, string guildId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guildId);

            await DatabaseCommands.ReclaimSpaceAsync();
        }
        else
        {
            await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);
            await DatabaseCommands.ReclaimSpaceAsync();
            ChannelConfigCache.Remove(guildId, channelId);

            var playersPath = Path.Combine(Declare.PlayersPath, channelId);
            if (Directory.Exists(playersPath))
                Directory.Delete(playersPath, true);

            var outputPath = Path.Combine(Declare.OutputPath, channelId);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }

        var message = Resource.URLDeleted;
        await Task.WhenAll(
            BotCommands.RegisterCommandsAsync(),
            Telemetry.SendTelemetryAsync(Declare.ProgramID, false)
        ).ConfigureAwait(false);

        return message;
    }

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly TimeSpan MinSpacingPerHost = TimeSpan.FromSeconds(3);

    public static async Task<RoomStatus?> RoomInfo(string baseUrl, string roomId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(roomId))
            return null;

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            return null;

        var url = new Uri(baseUri, $"api/room_status/{roomId.Trim()}");

        string? json;
        try
        {
            json = await HttpThrottle.GetStringThrottledAsync(
                Http,
                url.ToString(),
                minSpacingPerHost: MinSpacingPerHost,
                ct: ct,
                maxAttempts: 3,
                log: Console.WriteLine
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RoomStatus>(json);
        }
        catch
        {
            return null;
        }
    }
}
