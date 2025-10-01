using ArchipelagoSphereTracker.src.Resources;
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
            return Resource.HelperNoId;
        }

        var getNameAndPatch = await ChannelsAndUrlsCommands.GetPatchAndGameNameForAlias(guildId, channelId, userId);

        if (!string.IsNullOrEmpty(getNameAndPatch))
        {
            message += string.Format(Resource.HelperPatch, userId, getNameAndPatch) + "\n\n";
        }
        else
        {
            message = Resource.HelperNoPatch;
        }
        return message;
    }

    public static async Task<string> Info(string message, string channelId, string guildId)
    {
        (var tracker, var baseUrl, var room, var silent, var CheckFrequency, var LastCheck) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);

        var roomInfo = await UrlClass.RoomInfo(baseUrl, room);

        if (roomInfo != null)
        {
            message += Resource.HelperInfos + "\n";
            message += string.Format(Resource.HelperRoom, $"{baseUrl}/room/{room}") + "\n";
            message += string.Format(Resource.HelperUrlTracker, $"{baseUrl}/tracker/{tracker}") + "\n";
            message += string.Format(Resource.HelperUrlSphereTracker, $"{baseUrl}/sphere_tracker/{tracker}") + "\n";
            message += string.Format(Resource.HelperSilent, TranslateBool(silent)) + "\n";
            message += string.Format(Resource.HelperCheckFrequency, CheckFrequency) + "\n";
            message += string.Format(Resource.HelperLastCheck, LastCheck) + "\n";
            message += string.Format(Resource.HelperPort, roomInfo.LastPort) + "\n";
        }
        else
        {
            message = Resource.NoUrlRegistered;
        }
        return message;
    }

    public static async Task<string> StatusGameList(string message, string channelId, string guildId)
    {
        var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
        if (!checkChannel)
        {
            message = Resource.NoUrlRegistered;
            return message;
        }

        var getGameStatusForGuildAndChannelAsync = await GameStatusCommands.GetGameStatusForGuildAndChannelAsync(guildId, channelId);
        var (urlTracker, urlSphereTracker, room, silent, CheckFrequency, LastCheck) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);
        var getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

        if (silent)
        {
            message = Resource.HelperGameStatusWithSilent + "\n";
            getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

            if (getReceiverAliases.Count == 0)
            {
                message += Resource.AliasNotRegistered;
            }
            else
            {
                getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                if (getReceiverAliases.Count == 0)
                {
                    message += Resource.AliasNotRegistered;
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
                        int checks = int.TryParse(game.Checks, out var c) ? c : 0;
                        int total = int.TryParse(game.Total, out var t) ? t : 0;
                        double percent = total > 0
                            ? (double)checks / total * 100.0
                            : 0.0;
                        string gameStatus = checks != total
                            ? string.Format(Resource.HelperGameStatusInProgress, game.Name, game.Game, percent) + "\n"
                            : string.Format(Resource.HelperGameStatusDone, game.Name, game.Game, percent) + "\n";

                        message += gameStatus;
                    }
                }
            }
        }
        else
        {
            message = $"{Resource.HelperStatusAllGames}\n";

            if (getGameStatusForGuildAndChannelAsync.Any())
            {
                foreach (var game in getGameStatusForGuildAndChannelAsync)
                {
                    int checks = int.TryParse(game.Checks, out var c) ? c : 0;
                    int total = int.TryParse(game.Total, out var t) ? t : 0;
                    double percent = total > 0
                        ? (double)checks / total * 100.0
                        : 0.0;
                    string gameStatus = checks != total
                        ? string.Format(Resource.HelperGameStatusInProgress, game.Name, game.Game, percent) + "\n"
                        : string.Format(Resource.HelperGameStatusDone, game.Name, game.Game, percent) + "\n";

                    message += gameStatus;
                }
            }
            else
            {
                message = Resource.NoUrlRegistered;
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
                return Resource.AliasEmpty;
            }

            var getGameStatusTextsAsync = await DisplayItemCommands.GetUserItemsGroupedAsync(guildId, channelId, alias);
            var filteredItems = getGameStatusTextsAsync
                        .GroupBy(item => item.Item)
                        .OrderBy(group => group.Key);


            if (filteredItems.Any())
            {
                message = string.Format(Resource.HelperItemsFor, userId) + $"\n{BuildItemMessage(filteredItems, listByLine)}";
            }
            else
            {
                await command.FollowupAsync(Resource.HelperNoItems);
            }
        }
        else
        {
            await command.FollowupAsync(Resource.NoUrlRegistered);
        }
        return message;
    }

    public static string TranslateBool(bool value)
    {
        return value ? Resource.LanguageYes : Resource.LanguageNo;
    }
}
