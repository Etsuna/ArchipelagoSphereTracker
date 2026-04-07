using Discord.WebSocket;
using System.Text;
using System.Text.RegularExpressions;

public static class SpoilerAnalysisClass
{
    private static readonly Regex SphereHeader = new(
        @"^\s*(\d+):\s*\{\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PlaythroughLine = new(
        @"^\s*(?<location>.*?)\s*\((?<finder>[^)]+)\):\s*(?<item>.*?)\s*\((?<receiver>[^)]+)\)\s*$",
        RegexOptions.Compiled);

    public readonly record struct Check(
        int Sphere,
        string Location,
        string Finder,
        string Item,
        string Receiver);

    public static async Task<string> AnalyzeSpoilerLog(
        SocketSlashCommand command,
        string channelId,
        string guildId,
        string? alias)
    {
        var spoilerPath = SpoilerLogClass.GetLatestSpoilerPath(channelId);
        string? receiver = alias;

        int? sphereLimit = null;
        var sphereRaw = command.Data.Options.FirstOrDefault(o => o.Name == "sphere")?.Value;
        if (sphereRaw != null && int.TryParse(sphereRaw.ToString(), out var parsedSphere))
        {
            sphereLimit = parsedSphere;
        }

        var missingMode = command.Data.Options.FirstOrDefault(o => o.Name == "missing-mode")?.Value?.ToString() ?? "first";
        var showAllMissing = string.Equals(missingMode, "full", StringComparison.OrdinalIgnoreCase);

        bool hideItems =
            command.Data.Options.FirstOrDefault(o => o.Name == "hide-items")?.Value as bool?
            ?? true;

        if (string.IsNullOrWhiteSpace(spoilerPath) || !File.Exists(spoilerPath))
        {
            return "Aucun spoiler log trouvé pour ce thread. Utilise `/send-spoiler-log file:<spoiler.txt>` puis relance l'analyse.";
        }

        var checks = ParsePlaythrough(spoilerPath);
        var found = await LoadFoundItemsAsync(guildId, channelId);

        return BuildReport(
            checks,
            found,
            receiver,
            sphereLimit,
            showAllMissing,
            hideItems);
    }

    public static List<Check> ParsePlaythrough(string spoilerPath)
    {
        var checks = new List<Check>();
        var lines = File.ReadAllLines(spoilerPath);

        var inPlaythrough = false;
        int? currentSphere = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed == "Playthrough:")
            {
                inPlaythrough = true;
                currentSphere = null;
                continue;
            }

            if (!inPlaythrough)
            {
                continue;
            }

            // Dès qu'on entre dans Paths, on arrête complètement le parse Playthrough
            if (trimmed == "Paths:")
            {
                break;
            }

            var sphereMatch = SphereHeader.Match(line);
            if (sphereMatch.Success)
            {
                currentSphere = int.Parse(sphereMatch.Groups[1].Value);
                continue;
            }

            if (trimmed == "}")
            {
                currentSphere = null;
                continue;
            }

            if (!currentSphere.HasValue)
            {
                continue;
            }

            var itemMatch = PlaythroughLine.Match(line);
            if (itemMatch.Success)
            {
                checks.Add(new Check(
                    currentSphere.Value,
                    Normalize(itemMatch.Groups["location"].Value),
                    Normalize(itemMatch.Groups["finder"].Value),
                    Normalize(itemMatch.Groups["item"].Value),
                    Normalize(itemMatch.Groups["receiver"].Value)));
            }
        }

        return checks;
    }

    private static async Task<HashSet<string>> LoadFoundItemsAsync(string guildId, string channelId)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Finder, Receiver, Item, Location
            FROM DisplayedItemTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(FoundKey(
                reader["Finder"]?.ToString() ?? string.Empty,
                reader["Receiver"]?.ToString() ?? string.Empty,
                reader["Item"]?.ToString() ?? string.Empty,
                reader["Location"]?.ToString() ?? string.Empty));
        }

        return found;
    }

    public static string BuildReport(
        List<Check> checks,
        HashSet<string> found,
        string? onlyReceiver,
        int? sphereLimit,
        bool showAllMissing,
        bool hideItems)
    {
        var scopedChecks = checks
            .Where(c => !sphereLimit.HasValue || c.Sphere <= sphereLimit.Value)
            .OrderBy(c => c.Sphere)
            .ThenBy(c => c.Receiver)
            .ThenBy(c => c.Finder)
            .ThenBy(c => c.Location)
            .ToList();

        if (scopedChecks.Count == 0)
        {
            return "Aucune sphère trouvée avec ces filtres.";
        }

        var missingChecks = scopedChecks
            .Where(c => !found.Contains(FoundKey(c)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(onlyReceiver))
        {
            missingChecks = missingChecks
                .Where(c => string.Equals(c.Receiver, onlyReceiver, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (missingChecks.Count == 0)
        {
            return "Aucun item manquant dans le Playthrough avec les paramètres actuels.";
        }

        var earliestIncompleteSphere = missingChecks.Min(c => c.Sphere);

        var actionableNow = missingChecks
            .Where(c => c.Sphere == earliestIncompleteSphere)
            .OrderBy(c => c.Receiver)
            .ThenBy(c => c.Finder)
            .ThenBy(c => c.Location)
            .ToList();

        var laterMissing = missingChecks
            .Where(c => c.Sphere > earliestIncompleteSphere)
            .OrderBy(c => c.Sphere)
            .ThenBy(c => c.Receiver)
            .ThenBy(c => c.Finder)
            .ThenBy(c => c.Location)
            .ToList();

        var displayedTotal = showAllMissing
            ? missingChecks.Count
            : actionableNow.Count;

        var sb = new StringBuilder();

        sb.AppendLine($"Sphère actuellement bloquante : {earliestIncompleteSphere}");
        sb.AppendLine($"Checks manquantes affichées : {displayedTotal}");
        sb.AppendLine($"- actionnables maintenant : {actionableNow.Count}");

        if (showAllMissing)
        {
            sb.AppendLine($"- dans les sphères suivantes : {laterMissing.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("Checks à faire maintenant :");

        foreach (var check in actionableNow)
        {
            sb.AppendLine($"- {FormatCheck(check, hideItems)}");
        }

        if (showAllMissing && laterMissing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Checks dans les sphères suivantes :");

            foreach (var group in laterMissing.GroupBy(c => c.Sphere).OrderBy(g => g.Key))
            {
                sb.AppendLine($"Sphère {group.Key} :");
                foreach (var check in group)
                {
                    sb.AppendLine($"- {FormatCheck(check, hideItems)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("Règle utilisée :");
        sb.AppendLine("- Le Playthrough définit l'ordre des sphères.");
        sb.AppendLine("- La plus petite sphère contenant au moins une check manquante est la sphère bloquante actuelle.");
        sb.AppendLine("- Toutes les checks manquantes de cette sphère sont considérées comme à faire maintenant.");
        sb.AppendLine("- Les checks manquantes des sphères suivantes sont listées séparément, sans utiliser la section Paths.");

        return sb.ToString().TrimEnd();
    }

    private static string FormatCheck(Check check, bool hideItems)
    {
        var itemPart = hideItems ? string.Empty : $" | {check.Item}";
        return $"[S{check.Sphere}] {check.Finder} -> {check.Receiver}{itemPart} @ {check.Location}";
    }

    private static string FoundKey(Check check)
        => FoundKey(check.Finder, check.Receiver, check.Item, check.Location);

    private static string FoundKey(string finder, string receiver, string item, string location)
        => string.Join("||", new[]
        {
            Normalize(finder).ToUpperInvariant(),
            Normalize(receiver).ToUpperInvariant(),
            Normalize(item).ToUpperInvariant(),
            Normalize(location).ToUpperInvariant()
        });

    private static string Normalize(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}