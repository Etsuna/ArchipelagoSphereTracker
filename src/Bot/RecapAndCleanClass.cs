using Discord.WebSocket;
using System.Text;

public class RecapAndCleanClass
{
    public static async Task<string> HandleRecapOrClean(
    SocketSlashCommand command,
    string message,
    string? alias,
    string channelId,
    string guildId,
    bool isAliasRequired,
    bool deleteAfter,
    bool includeAllAliases,
    bool returnRecap,
    Func<Dictionary<string, List<string>>, string, string, string?, string>? buildMessage)
    {
        var userId = command.User.Id.ToString();

        if (!await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable"))
            return "No URL registered for this channel or no alias recorded.";

        var userAliases = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);
        if (!userAliases.Any(x => x.Contains(userId)))
            return "You don’t have any registered alias. Use the /add-alias command to automatically generate a recap file.";

        if (isAliasRequired && string.IsNullOrWhiteSpace(alias))
            return "The alias cannot be empty.";

        var exists = includeAllAliases
            ? await RecapListCommands.CheckIfExistsWithoutAlias(guildId, channelId, userId)
            : await RecapListCommands.CheckIfExists(guildId, channelId, userId, alias!);

        if (!exists)
            return "There is no list.";

        var aliasesWithItems = includeAllAliases
            ? await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId)
            : await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId, alias!);

        if (!aliasesWithItems.Any())
            return $"The user <@{userId}> does not exist.";

        if (!includeAllAliases && !aliasesWithItems.ContainsKey(alias!))
            return $"The user <@{userId}> is not registered with the alias: {alias}.";

        if (returnRecap)
        {
            if (buildMessage is null)
                return "Internal error: buildMessage is null even though a recap was requested.";

            message = buildMessage(aliasesWithItems, userId, alias!, includeAllAliases ? null : alias);
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
                ? $"Clean All for <@{userId}> completed."
                : $"Clean for Alias {alias} completed";
        }

        return message;
    }

    public static async Task<string> Clean(SocketSlashCommand command, string message, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, message, alias, channelId, guildId, isAliasRequired: true, deleteAfter: true, includeAllAliases: false, returnRecap: false, buildMessage: null);
    }

    public static async Task<string> CleanAll(SocketSlashCommand command, string message, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, message, alias, channelId, guildId, isAliasRequired: false, deleteAfter: true, includeAllAliases: true, returnRecap: false, buildMessage: null);
    }

    public static async Task<string> Recap(SocketSlashCommand command, string message, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, message, alias, channelId, guildId, isAliasRequired: true, deleteAfter: false, includeAllAliases: false, returnRecap: true, buildMessage: BuildRecapMessage);
    }

    public static async Task<string> RecapAll(SocketSlashCommand command, string message, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, message, alias: null, channelId, guildId, isAliasRequired: false, deleteAfter: false, includeAllAliases: true, returnRecap: true, buildMessage: BuildRecapMessage);
    }
    public static async Task<string> RecapAndClean(SocketSlashCommand command, string message, string? alias, string channelId, string guildId)
    {
        return await HandleRecapOrClean(command, message, alias, channelId, guildId, isAliasRequired: true, deleteAfter: true, includeAllAliases: false, returnRecap: true, buildMessage: BuildRecapMessage);
    }

    public static string BuildRecapMessage(Dictionary<string, List<string>> data, string userId, string alias, string? filterAlias)
    {
        var sb = new StringBuilder($"Details for <@{userId}>:\n");

        var toProcess = filterAlias != null
            ? data.Where(d => d.Key == filterAlias)
            : data;

        foreach (var sub in toProcess)
        {
            var grouped = sub.Value != null && sub.Value.Any()
                ? string.Join(", ", sub.Value
                    .GroupBy(x => x)
                    .Select(g => g.Count() > 1 ? $"{g.Key} x {g.Count()}" : g.Key))
                : "No item";

            sb.AppendLine($"**{sub.Key}** : {grouped}");
        }

        return sb.ToString();
    }
}
