using ArchipelagoSphereTracker.src.Resources;
using System.Text;

public class HintClass
{
    public static async Task<string> HintHandler(
        string? realAlias,
        string channelId,
        string guildId,
        Func<string, string, string, Task<List<HintStatus>>> fetchHintsFunc,
        Func<HintStatus, string?, bool> filterFunc,
        string headerTemplate,
        string noHintMessage
        )
    {
        if (string.IsNullOrWhiteSpace(realAlias))
            return Resource.HintNoAlias;

        var hints = await fetchHintsFunc(guildId, channelId, realAlias).ConfigureAwait(false);
        var message = string.Empty;

        if (hints.Any())
        {
            var filteredHints = hints.Where(h => filterFunc(h, realAlias)).ToList();

            message = filteredHints.Count > 0
                ? BuildHintMessage(string.Format(headerTemplate, realAlias), filteredHints)
                : noHintMessage;
        }
        else
        {
            message = Resource.HintNoUrl;
        }

        return message;
    }

    public static Task<string> HintForReceiver(
        string? realAlias, string channelId, string guildId) =>
        HintHandler(
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForReceiver,
            (hint, alias) => hint.Receiver == alias,
            Resource.HintItemFor,
            Resource.HintNoHintFoundForReceiver
            );

    public static Task<string> HintForFinder(
        string? realAlias, string channelId, string guildId) =>
        HintHandler(
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForFinder,
            (hint, alias) => hint.Finder == alias,
            Resource.HintItemFrom,
            Resource.HintNoHintFoundFromFinder
            );

    public static string BuildHintMessage(string header, IEnumerable<HintStatus> hints)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);

        foreach (var item in hints)
            sb.AppendLine(string.Format(Resource.HintItem, item.Receiver, item.Item, item.Location, item.Finder));

        return sb.ToString();
    }
}
