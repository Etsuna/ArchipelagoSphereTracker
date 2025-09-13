using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System;
using System.Text.Json;
using TrackerLib.Models;
using TrackerLib.Services;


public class UrlClass
{
    public static async Task<string> AddUrl(SocketSlashCommand command, IGuildUser? guildUser, string message, string channelId, string guildId, ITextChannel channel)
    {
        string baseUrl = string.Empty;
        string? tracker = string.Empty;
        string? room = string.Empty;
        string port = string.Empty;

        var silent = command.Data.Options.ElementAtOrDefault(3)?.Value as bool? ?? false;
        var newUrl = command.Data.Options.FirstOrDefault()?.Value as string;
        var checkFrequencyStr = command.Data.Options.ElementAtOrDefault(4)?.Value as string ?? "5m";

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
            if (playersCount > Declare.MaxPlayer)
                return (false, string.Format(Resource.CheckPlayerMinMax, Declare.MaxPlayer));

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
                    string? threadTitle = command.Data.Options.ElementAt(1).Value.ToString();
                    string? threadType = command.Data.Options.ElementAt(2).Value.ToString();

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
                        await foreach (var memberBatch in channel.GetUsersAsync())
                        {
                            foreach (var member in memberBatch)
                                await thread.AddUserAsync(member);
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

                    var rootTracker = await TrackerDatapackageFetcher.getRoots(baseUrl, tracker);
                    var checksums = TrackerDatapackageFetcher.GetDatapackageChecksums(rootTracker);
                    await TrackerDatapackageFetcher.SeedDatapackagesFromTrackerAsync(baseUrl, guildId, channelId, rootTracker);

                    if (!string.IsNullOrEmpty(tracker))
                    {
                        Declare.AddedChannelId.Add(channelId);

                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, baseUrl, room, tracker, silent, checkFrequencyStr);
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guildId, channelId, aliasList);
                        await BotCommands.SendMessageAsync(Resource.TDMAliasUpdated, channelId);
                        await TrackingDataManager.GetTableDataAsync(guildId, channelId, baseUrl, tracker, silent);
                        await BotCommands.SendMessageAsync(Resource.URLBotReady, channelId);
                        await ChannelsAndUrlsCommands.SendAllPatchesFileForChannelAsync(guildId, channelId);
                        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);
                        await ChannelsAndUrlsCommands.UpdateLastCheckAsync(guildId, channelId);

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
    }

    public static async Task<string> DeleteUrl(IGuildUser? guildUser, string message, string channelId, string guildId)
    {
        message = await DeleteChannelAndUrl(channelId, guildId);
        return message;
    }

    public static async Task<string> DeleteChannelAndUrl(string? channelId, string guildId)
    {
        string message;

        if (string.IsNullOrEmpty(channelId))
        {
            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guildId);
            if (channelId != null)
            {
                var playersPath = Path.Combine(Declare.PlayersPath, channelId);
                if (Directory.Exists(playersPath)) Directory.Delete(playersPath, true);
                var outputPath = Path.Combine(Declare.OutputPath, channelId);
                if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
            }
            await DatabaseCommands.ReclaimSpaceAsync();
        }
        else
        {
            await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);
            await DatabaseCommands.ReclaimSpaceAsync();
            ChannelConfigCache.Remove(guildId, channelId);
        }

        message = Resource.URLDeleted;
        await BotCommands.RegisterCommandsAsync();
        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);
        return message;
    }

    public static async Task<RoomStatus?> RoomInfo(string baseUrl, string roomId)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/room_status/{roomId}";
        using var http = new HttpClient();
        var json = await http.GetStringAsync(url);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var room = JsonSerializer.Deserialize<RoomStatus>(json, options);
        if (room is null)
        {
            Console.WriteLine("Failed to fetch or parse room status.");
            return null;
        }
        return room;
    }
}
