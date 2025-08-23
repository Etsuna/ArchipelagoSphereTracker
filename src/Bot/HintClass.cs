using ArchipelagoSphereTracker.src.Resources;
using System.Text;

public class HintClass
{
    public static async Task<string> HintHandler(
        string message,
        string? realAlias,
        string channelId,
        string guildId,
        Func<string, string, string, CancellationToken, Task<List<HintStatus>>> fetchHintsFunc,
        Func<HintStatus, string?, bool> filterFunc,
        string headerTemplate,
        string noHintMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(realAlias))
            return Resource.HintNoAlias;

        var hints = await fetchHintsFunc(guildId, channelId, realAlias, ct).ConfigureAwait(false);

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
        string message, string? realAlias, string channelId, string guildId, CancellationToken ct = default) =>
        HintHandler(
            message,
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForReceiver, // (guildId, channelId, receiver, ct)
            (hint, alias) => hint.Receiver == alias,
            Resource.HintItemFor,
            Resource.HintNoHintFoundForReceiver,
            ct);

    public static Task<string> HintForFinder(
        string message, string? realAlias, string channelId, string guildId, CancellationToken ct = default) =>
        HintHandler(
            message,
            realAlias,
            channelId,
            guildId,
            HintStatusCommands.GetHintStatusForFinder, // (guildId, channelId, finder, ct)
            (hint, alias) => hint.Finder == alias,
            Resource.HintItemFrom,
            Resource.HintNoHintFoundFromFinder,
            ct);

    public static string BuildHintMessage(string header, IEnumerable<HintStatus> hints)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);

        foreach (var item in hints)
            sb.AppendLine(string.Format(Resource.HintItem, item.Receiver, item.Item, item.Location, item.Finder));

        return sb.ToString();
    }
}
