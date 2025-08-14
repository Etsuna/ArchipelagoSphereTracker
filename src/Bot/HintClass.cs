using System.Text;

public class HintClass
{
    public static async Task<string> HintHandler(
    string message,
    string? realAlias,
    string channelId,
    string guildId,
    Func<string, string, string, Task<List<HintStatus>>> fetchHintsFunc,
    Func<HintStatus, string?, bool> filterFunc,
    string headerTemplate,
    string noHintMessage)
    {
        if (string.IsNullOrEmpty(realAlias))
        {
            return "Alias not specified.";
        }

        var hints = await fetchHintsFunc(guildId, channelId, realAlias);

        if (hints.Any())
        {
            var filteredHints = hints.Where(h => filterFunc(h, realAlias)).ToList();

            message = filteredHints.Count > 0
                ? BuildHintMessage(string.Format(headerTemplate, realAlias), filteredHints)
                : noHintMessage;
        }
        else
        {
            message = "No URL registered for this channel. ou Aucun hint.";
        }

        return message;
    }


    public static async Task<string> HintForReceiver(string message, string? realAlias, string channelId, string guildId)
    {
        return await HintHandler(
            message,
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForReceiver,
            (hint, alias) => hint.Receiver == alias,
            "Item for {0} :",
            "No hint found for this receiver"
        );
    }

    public static async Task<string> HintForFinder(string message, string? realAlias, string channelId, string guildId)
    {
        return await HintHandler(
            message,
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForFinder,
            (hint, alias) => hint.Finder == alias,
            "Item from {0} :",
            "No hint found for this finder"
        );
    }

    public static string BuildHintMessage(string header, IEnumerable<HintStatus> hints)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine(header);

        foreach (var item in hints)
        {
            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
        }

        return messageBuilder.ToString();
    }
}
