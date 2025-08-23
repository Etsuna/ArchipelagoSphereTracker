using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.Net;
using System.Text.RegularExpressions;

public class UrlClass
{
    public static async Task<string> AddUrl(SocketSlashCommand command, IGuildUser? guildUser, string message, string channelId, string guildId, ITextChannel channel)
    {
        string baseUrl = "https://archipelago.gg";
        string? trackerUrl = null;
        string? sphereTrackerUrl = null;
        string port = string.Empty;
        var silent = command.Data.Options.ElementAtOrDefault(3)?.Value as bool? ?? false;

        bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

        async Task<bool> CanAddUrlAsync(string guildId, string channelId)
        {
            var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
            return !checkIfChannelExistsAsync;
        }

        async Task<(bool isValid, string pageContent, string message)> IsAllUrlIsValidAsync(string newUrl)
        {
            if(!await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId))
            {
                return (false, "", "You can't have more than 2 Threads. Delete one before adding a new Sphere_Tracker please.");
            }

            using HttpClient client = new();
            string pageContent = await client.GetStringAsync(newUrl);

            var portMatch = Regex.Match(pageContent, @"/connect archipelago\.gg:(\d+)");
            if (portMatch.Success)
            {
                port = portMatch.Groups[1].Value;
                Console.WriteLine(string.Format(Resource.HelperPort, port));
            }
            else
            {
                Console.WriteLine(Resource.HelperPortNotFound);
                return (false, pageContent, Resource.HelperPortNotFound);
            }

            trackerUrl = ExtractUrl(pageContent, "Multiworld Tracker");
            sphereTrackerUrl = ExtractUrl(pageContent, "Sphere Tracker");

            if (string.IsNullOrEmpty(trackerUrl) || string.IsNullOrEmpty(sphereTrackerUrl) || string.IsNullOrEmpty(port))
            {
                return (false, pageContent, Resource.UrlCanceled);
            }

            if (!trackerUrl.StartsWith("http"))
            {
                trackerUrl = baseUrl + trackerUrl;
            }
            if (!sphereTrackerUrl.StartsWith("http"))
            {
                sphereTrackerUrl = baseUrl + sphereTrackerUrl;
            }

            if(await TrackingDataManager.CheckMaxPlayersAsync(trackerUrl))
            {
                return (false, pageContent, $"You can't have more than {Declare.MaxPlayer} players");
            }

            return (true, pageContent, string.Empty);
        }

        string? ExtractUrl(string htmlContent, string linkText)
        {
            var match = Regex.Match(htmlContent, $@"<a[^>]*>.*{linkText}.*</a>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var hrefMatch = Regex.Match(match.Value, @"href=""(.*?)""");
                if (hrefMatch.Success)
                {
                    return hrefMatch.Groups[1].Value;
                }
            }
            return null;
        }


        if (await CanAddUrlAsync(guildId, channelId))
        {
            var newUrl = command.Data.Options.FirstOrDefault()?.Value as string;

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
                var (isValid, pageContent, errorMessage) = await IsAllUrlIsValidAsync(newUrl);

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

                    var rowsMatch = Regex.Matches(pageContent, @"<tr[^>]*>.*?</tr>", RegexOptions.Singleline);
                    var patchLinkList = new List<Patch>();

                    foreach (Match rowMatch in rowsMatch)
                    {
                        var columnsMatch = Regex.Matches(rowMatch.Value, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline);
                        if (columnsMatch.Count >= 4)
                        {
                            string gameAliasHtml = WebUtility.HtmlDecode(columnsMatch[1].Groups[1].Value.Trim());
                            var gameAliasMatch = Regex.Match(gameAliasHtml, @">([^<]+)<");
                            string gameAlias = gameAliasMatch.Success ? gameAliasMatch.Groups[1].Value : gameAliasHtml;

                            string gameName = WebUtility.HtmlDecode(columnsMatch[2].Groups[1].Value.Trim());

                            string downloadLinkHtml = WebUtility.HtmlDecode(columnsMatch[3].Groups[1].Value.Trim());
                            var downloadLinkMatch = Regex.Match(downloadLinkHtml, @"href=\""(.*?)\""");
                            string downloadLink = downloadLinkMatch.Success ? downloadLinkMatch.Groups[1].Value.Trim() : "No File";

                            if (string.IsNullOrEmpty(downloadLink) || downloadLink.Equals("No File"))
                            {
                                continue;
                            }

                            Console.WriteLine(string.Format(Resource.UrlGamePatch, gameAlias, downloadLink));

                            var patchLink = new Patch
                            {
                                GameAlias = gameAlias,
                                GameName = gameName,
                                PatchLink = baseUrl + downloadLink
                            };

                            patchLinkList.Add(patchLink);
                        }
                    }

                    if (!string.IsNullOrEmpty(trackerUrl) && !string.IsNullOrEmpty(sphereTrackerUrl))
                    {
                        Declare.AddedChannelId.Add(channelId);

                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, newUrl, trackerUrl, sphereTrackerUrl, silent);
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                        await TrackingDataManager.SetAliasAndGameStatusAsync(guildId, channelId, trackerUrl, silent);
                        await TrackingDataManager.CheckGameStatusAsync(guildId, channelId, trackerUrl, silent);
                        await TrackingDataManager.GetTableDataAsync(guildId, channelId, sphereTrackerUrl, silent);
                        await BotCommands.SendMessageAsync(Resource.URLBotReady, channelId);
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
