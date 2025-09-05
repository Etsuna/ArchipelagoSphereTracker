using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System;
using System.Net;
using System.Text.RegularExpressions;

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
        var uri = new Uri(newUrl);

        baseUrl = $"{uri.Scheme}://{uri.Authority}";

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        room = segments.Length > 1 ? segments[^1] : "";

        bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

        var roomInfo = await GetData.RoomInfo(baseUrl, room);

        if (roomInfo == null)
        {
            message = "Room Not Found";
            return message;
        }

        tracker = roomInfo.Tracker != null ? roomInfo.Tracker : tracker;
        port = !string.IsNullOrEmpty(roomInfo.LastPort.ToString()) ? roomInfo.LastPort.ToString() : port;

        async Task<bool> CanAddUrlAsync(string guildId, string channelId)
        {
            var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
            return !checkIfChannelExistsAsync;
        }

        async Task<(bool isValid, string message)> IsAllUrlIsValidAsync()
        {
            if (!await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId))
            {
                return (false, Resource.UrlCheckMaxTread);
            }

            var playersCount = roomInfo.Players.Count;

            if(playersCount > Declare.MaxPlayer)
            {
                return (false, string.Format(Resource.CheckPlayerMinMax, Declare.MaxPlayer));
            }

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
                        IGuildUser? user = command.User as IGuildUser;
                        if (user == null)
                        {
                            message = Resource.UrlPrivateThreadUserNotFound;
                        }
                        else
                        {
                            await thread.AddUserAsync(user);
                        }
                    }
                    else
                    {
                        await foreach (var memberBatch in channel.GetUsersAsync())
                        {
                            foreach (var member in memberBatch)
                            {
                                await thread.AddUserAsync(member);
                            }
                        }
                    }

                    

                    var patchLinkList = new List<Patch>();
                    var aliasList = new List<Dictionary<string, string>>();
                    var aliasDict = new Dictionary<string, string>();

                    foreach (var player in roomInfo.Players)
                    {
                        aliasDict = new Dictionary<string, string>
                            {
                                { player.Name, player.Game},
                            };

                        aliasList.Add(aliasDict);
                    }

                    foreach (var download in roomInfo.Downloads)
                    {
                        foreach (var slot in roomInfo.Players)
                        {
                            var patchLink = new Patch
                            {
                                GameAlias = slot.Name,
                                GameName = slot.Game,
                                PatchLink = baseUrl + download.Download,
                            };
                            patchLinkList.Add(patchLink);
                            Console.WriteLine(string.Format(Resource.UrlGamePatch, patchLink.GameAlias, patchLink.PatchLink));

                        }
                    }


                    if (!string.IsNullOrEmpty(tracker))
                    {
                        Declare.AddedChannelId.Add(channelId);

                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, baseUrl, room, tracker, silent);
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guildId, channelId, aliasList);
                        await BotCommands.SendMessageAsync(Resource.TDMAliasUpdated, channelId);
                        /*                        await TrackingDataManager.CheckGameStatusAsync(guildId, channelId, trackerUrl, silent);
                                                await TrackingDataManager.GetTableDataAsync(guildId, channelId, sphereTrackerUrl, silent);*/
                        await BotCommands.SendMessageAsync(Resource.URLBotReady, channelId);
                        await ChannelsAndUrlsCommands.SendAllPatchesFileForChannelAsync(guildId, channelId);
                        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);

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
        string message = string.Empty;

        if (string.IsNullOrEmpty(channelId))
        {
            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guildId);
            await DatabaseCommands.ReclaimSpaceAsync();
        }
        else
        {
            await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);
            await DatabaseCommands.ReclaimSpaceAsync();
        }

        message = Resource.URLDeleted;
        await BotCommands.RegisterCommandsAsync();
        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);
        return message;
    }
}
