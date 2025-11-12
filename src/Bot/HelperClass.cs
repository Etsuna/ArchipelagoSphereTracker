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
                        string percentText = percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                        
                        string gameStatus = checks != total
                            ? string.Format(Resource.HelperGameStatusInProgress, game.Name, game.Game, percentText) + "\n"
                            : string.Format(Resource.HelperGameStatusDone, game.Name, game.Game, percentText) + "\n";

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
                    string percentText = percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                    string gameStatus = checks != total
                        ? string.Format(Resource.HelperGameStatusInProgress, game.Name, game.Game, percentText) + "\n"
                        : string.Format(Resource.HelperGameStatusDone, game.Name, game.Game, percentText) + "\n";

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

    private static int? ToFlagCode(string? s) => int.TryParse(s, out var v) ? v : (int?)null;

    private static string FlagLabel(long? f) => f switch
    {
        0 => Resource.Filler,
        1 => Resource.Progression,
        2 => Resource.Useful,
        3 => Resource.Required,
        4 => Resource.Trap,
        null => string.Empty,
        _ => string.Format(Resource.Unknown, f)
    };

    private static int Rank(long? f) => f switch
    {
        3 => 0,
        1 => 1,
        2 => 2,
        0 => 3,
        4 => 4,
        null => int.MaxValue,
        _ => int.MaxValue - 1
    };

    public static async Task<string> ListItems(
    SocketSlashCommand command, string? userId, string message,
    string? alias, string channelId, string guildId)
    {
        bool listByLine = command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value as bool? ?? false;

        string BuildItemMessageByFlag(IEnumerable<DisplayedItem> items, bool listByLine)
        {
            var sb = new StringBuilder();

            var byFlag = items
             .GroupBy(i => ToFlagCode(i.Flag))
             .OrderBy(g => Rank(g.Key))
             .ThenBy(g => g.Key);

            bool firstFlag = true;
            foreach (var fg in byFlag)
            {
                if (!firstFlag) sb.AppendLine();
                firstFlag = false;

                if(FlagLabel(fg.Key) != string.Empty)
                {
                    sb.AppendLine($"**{FlagLabel(fg.Key)}:**");
                }

                var groupedItems = fg
                    .GroupBy(x => x.Item)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x {g.Count()}" : g.Key)
                    .ToList();

                if (groupedItems.Count == 0)
                {
                    sb.AppendLine(Resource.HelperNoItems);
                    continue;
                }

                if (listByLine)
                    groupedItems.ForEach(s => sb.AppendLine(s));
                else
                    sb.AppendLine(string.Join(", ", groupedItems));
            }

            return sb.ToString();
        }

        var checkIfChannelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "DisplayedItemTable");
        if (!checkIfChannelExists)
        {
            await command.FollowupAsync(Resource.NoUrlRegistered);
            return message;
        }

        if (string.IsNullOrWhiteSpace(alias))
            return Resource.AliasEmpty;

        var items = await DisplayItemCommands.GetUserItemsGroupedAsync(guildId, channelId, alias);

        if (items.Any())
        {
            var body = BuildItemMessageByFlag(items, listByLine);
            message = string.Format(Resource.HelperItemsFor, $"<@{userId}>") + "\n" + body;
        }
        else
        {
            await command.FollowupAsync(Resource.HelperNoItems);
        }

        return message;
    }

    public static string TranslateBool(bool value)
    {
        return value ? Resource.LanguageYes : Resource.LanguageNo;
    }
}
