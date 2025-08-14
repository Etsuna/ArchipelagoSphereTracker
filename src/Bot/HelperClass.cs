using Discord.WebSocket;
using System.Text;
using System.Text.RegularExpressions;

public class HelperClass
{
    public static async Task<string> GetPatch(SocketSlashCommand command, string message, string channelId, string guildId)
    {
        var userId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return "Receiver ID not specified.";

        }

        var getNameAndPatch = await ChannelsAndUrlsCommands.GetPatchAndGameNameForAlias(guildId, channelId, userId);

        if (!string.IsNullOrEmpty(getNameAndPatch))
        {
            message += $"Patch for {userId}, {getNameAndPatch}\n\n";
        }
        else
        {
            message = "No patch for this user.";
        }

        return message;
    }

    public static async Task<string> Info(string message, string channelId, string guildId)
    {
        (var urlTracker, var urlSphereTracker, var room, var silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);

        if (!string.IsNullOrEmpty(room))
        {
            using HttpClient client = new();
            string pageContent = await client.GetStringAsync(room);

            string? port = null;
            var match = Regex.Match(pageContent, @"/connect archipelago\.gg:(\d+)", RegexOptions.Singleline);

            message += "Info:\n";
            message += $"Room : {room}\n";
            message += $"Tracker : {urlTracker}\n";
            message += $"SphereTracker : {urlSphereTracker}\n";
            message += $"Silent : {silent}\n";

            if (match.Success)
            {
                port = match.Groups[1].Value;
                message += $"Port : {port}\n";
            }
            else
            {
                message += "Port not found.\n";
            }
        }
        else
        {
            message = "No URL registered for this channel.";
        }

        return message;
    }

    public static async Task<string> StatusGameList(string message, string channelId, string guildId)
    {
        var getGameStatusForGuildAndChannelAsync = await GameStatusCommands.GetGameStatusForGuildAndChannelAsync(guildId, channelId);
        var (urlTracker, urlSphereTracker, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);
        var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
        var getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

        if (silent)
        {
            message = "Status for all games, Thread is silent, Only for added aliases :\n";
            getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

            if (getReceiverAliases.Count == 0)
            {
                message += "No alias is registered.";
            }
            else
            {
                getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                if (getReceiverAliases.Count == 0)
                {
                    message += "No alias is registered.";
                }
                else
                {
                    var filteredGameStatus = getGameStatusForGuildAndChannelAsync
                     .Where(x =>
                     {
                         if (getReceiverAliases == null) return false;

                         var match = Regex.Match(x.Name, @"\(([^)]+)\)$");
                         var alias = match.Success ? match.Groups[1].Value : x.Name;

                         return getReceiverAliases.Contains(alias);
                     });

                    foreach (var game in filteredGameStatus)
                    {
                        string gameStatus = (game.Percent != "100.00")
                            ? $"**{game.Name} - {game.Game} - {game.Percent}%**\n"
                            : $"~~{game.Name} - {game.Game} - {game.Percent}%~~\n";
                        message += gameStatus;
                    }
                }
            }
        }
        else
        {
            message = "Status for all games :\n";

            if (getGameStatusForGuildAndChannelAsync.Any())
            {
                foreach (var game in getGameStatusForGuildAndChannelAsync)
                {
                    string gameStatus = (game.Percent != "100.00")
                        ? $"**{game.Name} - {game.Game} - {game.Percent}%**\n"
                        : $"~~{game.Name} - {game.Game} - {game.Percent}%~~\n";

                    message += gameStatus;
                }
            }
            else
            {
                message = "No URL registered for this channel.";
            }
        }

        return message;
    }

    public static async Task<string> ListItems(SocketSlashCommand command, string? userId, string message, string? alias, string channelId, string guildId)
    {
        bool listByLine = command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value as bool? ?? false;

        string BuildItemMessage(IEnumerable<IGrouping<string, DisplayedItem>> filteredItems, bool listByLine)
        {
            var messageBuilder = new StringBuilder();
            bool isFirst = true;

            foreach (var groupedItem in filteredItems)
            {
                if (!isFirst)
                {
                    messageBuilder.Append(listByLine ? "\n" : ", ");
                }
                messageBuilder.Append(groupedItem.Count() > 1
                    ? $"{groupedItem.Key} x {groupedItem.Count()}"
                    : groupedItem.Key);
                isFirst = false;
            }

            return messageBuilder.ToString();
        }

        var checkIfChannelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "DisplayedItemTable");

        if (checkIfChannelExists)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return "Alias error.";
            }

            var getGameStatusTextsAsync = await DisplayItemCommands.GetUserItemsGroupedAsync(guildId, channelId, alias);
            var filteredItems = getGameStatusTextsAsync
                        .GroupBy(item => item.Item)
                        .OrderBy(group => group.Key);


            if (filteredItems.Any())
            {
                message = $"Items for {userId} :\n{BuildItemMessage(filteredItems, listByLine)}";
            }
            else
            {
                await command.FollowupAsync("No items");
            }
        }
        else
        {
            await command.FollowupAsync("No URL registered for this channel.");
        }

        return message;
    }
}
