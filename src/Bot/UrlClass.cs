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

        async Task<(bool isValid, string pageContent)> IsAllUrlIsValidAsync(string newUrl)
        {
            using HttpClient client = new();
            string pageContent = await client.GetStringAsync(newUrl);

            var portMatch = Regex.Match(pageContent, @"/connect archipelago\.gg:(\d+)");
            if (portMatch.Success)
            {
                port = portMatch.Groups[1].Value;
                Console.WriteLine($"Port found : {port}");
            }
            else
            {
                Console.WriteLine("Port not found.");
                return (false, pageContent);
            }

            trackerUrl = ExtractUrl(pageContent, "Multiworld Tracker");
            sphereTrackerUrl = ExtractUrl(pageContent, "Sphere Tracker");

            if (string.IsNullOrEmpty(trackerUrl) || string.IsNullOrEmpty(sphereTrackerUrl) || string.IsNullOrEmpty(port))
            {
                return (false, pageContent);
            }

            if (!trackerUrl.StartsWith("http"))
            {
                trackerUrl = baseUrl + trackerUrl;
            }
            if (!sphereTrackerUrl.StartsWith("http"))
            {
                sphereTrackerUrl = baseUrl + sphereTrackerUrl;
            }

            return (true, pageContent);
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
                message = "Empty URL not allowed.";
            }
            else if (!IsValidUrl(newUrl))
            {
                message = $"The link is incorrect; use the room URL.";
            }
            else
            {
                var (isValid, pageContent) = await IsAllUrlIsValidAsync(newUrl);

                if (!isValid)
                {
                    message = $"Sphere_Tracker, Tracker, or the port not found. Addition canceled.";
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

                    await thread.SendMessageAsync($"The thread has been created: {thread.Name}, wait for the bot to be ready.");

                    channelId = thread.Id.ToString();

                    if (type == ThreadType.PrivateThread)
                    {
                        IGuildUser? user = command.User as IGuildUser;
                        if (user == null)
                        {
                            message = "User not found for the private thread.";
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

                            Console.WriteLine($"Name: {gameAlias} | Download: {downloadLink}");

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
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, newUrl, trackerUrl, sphereTrackerUrl, silent);
                        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                        await TrackingDataManager.SetAliasAndGameStatusAsync(guildId, channelId, trackerUrl, silent);
                        await TrackingDataManager.CheckGameStatusAsync(guildId, channelId, trackerUrl, silent);
                        await TrackingDataManager.GetTableDataAsync(guildId, channelId, sphereTrackerUrl, silent);
                        await BotCommands.SendMessageAsync("BOT Ready!", channelId);
                        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);
                    }

                    message = $"URL set to {newUrl}. Messages configured for this channel. Please wait while the program retrieves all aliases.";
                }
            }
        }
        else
        {
            message = "URL already set for this channel. Remove the URL before adding a new one.";
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
        }
        else
        {
            await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);
        }

        message = "URL deleted.";
        await BotCommands.RegisterCommandsAsync();
        await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);
        return message;
    }
}
