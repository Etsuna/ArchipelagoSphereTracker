using ArchipelagoSphereTracker.src.Resources;
using Discord.WebSocket;
using System.Text;

public class RecapAndCleanClass
{
    public static async Task<string> HandleRecapOrClean(
    SocketSlashCommand command,
    string? alias,
    string channelId,
    string guildId,
    bool isAliasRequired,
    bool deleteAfter,
    bool includeAllAliases,
    bool returnRecap,
    Func<SocketSlashCommand, Dictionary<string, List<(string Item, long? Flag)>>, string, string, string?, string>? buildMessage)
    {
        var message = string.Empty;
        var userId = command.User.Id.ToString();

        if (!await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable"))
            return Resource.RACNoUrlOrAlias;

        var userAliases = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);
        if (!userAliases.Any(x => x.Contains(userId)))
            return Resource.RACNoAliasRegistered;

        if (isAliasRequired && string.IsNullOrWhiteSpace(alias))
            return Resource.AliasEmpty;

        var exists = includeAllAliases
            ? await RecapListCommands.CheckIfExistsWithoutAlias(guildId, channelId, userId)
            : await RecapListCommands.CheckIfExists(guildId, channelId, userId, alias!);

        if (!exists)
            return Resource.RACNoList;

        var aliasesWithItems = includeAllAliases
            ? await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId)
            : await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId, alias!);

        if (!aliasesWithItems.Any())
            return string.Format(Resource.RACUserNotExists, userId);

        if (!includeAllAliases && !aliasesWithItems.ContainsKey(alias!))
            return string.Format(Resource.RACUserNoRegistredWithAlias, userId, alias);

        if (returnRecap)
        {
            if (buildMessage is null)
                return Resource.RACBuildMessageError;

            message = buildMessage(command, aliasesWithItems, userId, alias!, includeAllAliases ? null : alias);
        }

        if (deleteAfter)
        {
            if (includeAllAliases)
                await RecapListCommands.DeleteAliasAndItemsForUserIdAsync(guildId, channelId, userId);
            else
                await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias!);
        }

        if (!returnRecap)
        {
            message = includeAllAliases
                ? string.Format(Resource.RACCleanAll, userId)
                : string.Format(Resource.RACClean, alias);
        }

        return message;
    }

    public static async Task<string> Clean(SocketSlashCommand command, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, alias, channelId, guildId, isAliasRequired: true, deleteAfter: true, includeAllAliases: false, returnRecap: false, buildMessage: null);
    }

    public static async Task<string> CleanAll(SocketSlashCommand command, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, alias, channelId, guildId, isAliasRequired: false, deleteAfter: true, includeAllAliases: true, returnRecap: false, buildMessage: null);
    }

    public static async Task<string> Recap(SocketSlashCommand command, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, alias, channelId, guildId, isAliasRequired: true, deleteAfter: false, includeAllAliases: false, returnRecap: true, buildMessage: BuildRecapMessage);
    }

    public static async Task<string> RecapAll(SocketSlashCommand command, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, alias: null, channelId, guildId, isAliasRequired: false, deleteAfter: false, includeAllAliases: true, returnRecap: true, buildMessage: BuildRecapMessage);
    }
    public static async Task<string> RecapAndClean(SocketSlashCommand command, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, alias, channelId, guildId, isAliasRequired: true, deleteAfter: true, includeAllAliases: false, returnRecap: true, buildMessage: BuildRecapMessage);
    }

    public sealed record RecapItem(string Item, long? Flag);

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

    public static string BuildRecapMessage(
    SocketSlashCommand command,
    Dictionary<string, List<(string Item, long? Flag)>> data,
    string userId,
    string alias,
    string? filterAlias)
    {
        var sb = new StringBuilder(string.Format(Resource.RACDetailsForUser, userId));
        sb.AppendLine();

        var toProcess = filterAlias != null ? data.Where(d => d.Key == filterAlias) : data;
        bool firstSection = true;

        foreach (var sub in toProcess)
        {
            if (firstSection) sb.AppendLine();
            firstSection = false;

            sb.AppendLine($"## **{sub.Key}:**");

            var items = sub.Value ?? new List<(string, long?)>();

            if (items.Count == 0)
            {
                sb.AppendLine(Resource.HelperNoItems);
                continue;
            }

            var byFlag = items
            .GroupBy(x => x.Flag)
            .OrderBy(g => Rank(g.Key))
            .ThenBy(g => g.Key);

            bool firstFlag = true;

            foreach (var fg in byFlag)
            {
                if (!firstFlag) sb.AppendLine();
                firstFlag = false;

                var groupedItems = fg
                    .GroupBy(x => x.Item)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x {g.Count()}" : g.Key)
                    .ToList();

                var label = FlagLabel(fg.Key);
                if (!string.IsNullOrEmpty(label))
                {
                    sb.AppendLine($"### **{label}:**");
                }

                foreach (var s in groupedItems)
                {
                    sb.AppendLine($"> - {s}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
