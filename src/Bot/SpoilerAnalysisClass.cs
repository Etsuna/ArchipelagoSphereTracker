using Discord.WebSocket;
using System.Text;
using System.Text.RegularExpressions;

public static class SpoilerAnalysisClass
{
    private static readonly Regex SphereHeader = new(@"^\s*(\d+):\s*\{\s*$", RegexOptions.Compiled);
    private static readonly Regex PlaythroughLine = new(
        @"^\s*(?<location>.*?)\s*\((?<finder>[^)]+)\):\s*(?<item>.*?)\s*\((?<receiver>[^)]+)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PathHeader = new(
        @"^\s*(?<location>.*?)\s*\((?<finder>[^)]+)\)\s*$",
        RegexOptions.Compiled);

    public readonly record struct Check(int Sphere, string Location, string Finder, string Item, string Receiver);

    public static async Task<string> AnalyzeSpoilerLog(SocketSlashCommand command, string channelId, string guildId, string? alias)
    {
        var spoilerPath = SpoilerLogClass.GetLatestSpoilerPath(channelId);
        string? receiver = alias;

        int? sphereLimit = null;
        var sphereRaw = command.Data.Options.FirstOrDefault(o => o.Name == "sphere")?.Value;
        if (sphereRaw != null && int.TryParse(sphereRaw.ToString(), out var parsedSphere))
        {
            sphereLimit = parsedSphere;
        }

        var missingMode = command.Data.Options.FirstOrDefault(o => o.Name == "missing-mode")?.Value?.ToString();
        var showAllMissing = string.Equals(missingMode, "full", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(spoilerPath) || !File.Exists(spoilerPath))
        {
            return "Aucun spoiler log trouvé pour ce thread. Utilise `/send-spoiler-log file:<spoiler.txt>` puis relance l'analyse.";
        }

        var (checks, paths) = ParseSpoiler(spoilerPath);
        var foundItems = await LoadFoundItemsAsync(guildId, channelId);

        return BuildReport(checks, paths, foundItems, receiver, sphereLimit, showAllMissing);
    }

    public static (List<Check> Checks, Dictionary<(string Location, string Finder), List<string>> Paths) ParseSpoiler(string spoilerPath)
    {
        var checks = new List<Check>();
        var paths = new Dictionary<(string, string), List<string>>();
        var lines = File.ReadAllLines(spoilerPath);

        var inPlaythrough = false;
        var inPaths = false;
        int? currentSphere = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.Trim();

            if (trimmedLine == "Playthrough:")
            {
                inPlaythrough = true;
                inPaths = false;
                currentSphere = null;
                continue;
            }

            if (trimmedLine == "Paths:")
            {
                inPlaythrough = false;
                inPaths = true;
                currentSphere = null;
                continue;
            }

            if (inPlaythrough)
            {
                var sphereMatch = SphereHeader.Match(line);
                if (sphereMatch.Success)
                {
                    currentSphere = int.Parse(sphereMatch.Groups[1].Value);
                    continue;
                }

                if (trimmedLine == "}")
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
                        Normalize(itemMatch.Groups["receiver"].Value)
                    ));
                }

                continue;
            }

            if (!inPaths || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var header = PathHeader.Match(line);
            if (!header.Success)
            {
                continue;
            }

            var location = Normalize(header.Groups["location"].Value);
            var finder = Normalize(header.Groups["finder"].Value);
            var steps = new List<string>();

            var j = i + 1;
            while (j < lines.Length)
            {
                var nextLine = lines[j];
                if (string.IsNullOrWhiteSpace(nextLine))
                {
                    break;
                }

                if (PathHeader.IsMatch(nextLine) && !nextLine.TrimStart().StartsWith("=>", StringComparison.Ordinal))
                {
                    break;
                }

                var trimmedNextLine = nextLine.TrimStart();
                if (trimmedNextLine.StartsWith("=>", StringComparison.Ordinal) || nextLine.StartsWith("\t", StringComparison.Ordinal) || nextLine.StartsWith("        ", StringComparison.Ordinal))
                {
                    steps.Add(Normalize(trimmedNextLine.TrimStart('=', '>', ' ')));
                }

                j++;
            }

            if (steps.Count > 0)
            {
                paths[(location, finder)] = steps;
            }

            i = j - 1;
        }

        return (checks, paths);
    }

    private static async Task<HashSet<(string Finder, string Receiver, string Item, string Location)>> LoadFoundItemsAsync(string guildId, string channelId)
    {
        var found = new HashSet<(string, string, string, string)>();

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
            found.Add((
                Normalize(reader["Finder"]?.ToString() ?? string.Empty),
                Normalize(reader["Receiver"]?.ToString() ?? string.Empty),
                Normalize(reader["Item"]?.ToString() ?? string.Empty),
                Normalize(reader["Location"]?.ToString() ?? string.Empty)
            ));
        }

        return found;
    }

    public static string BuildReport(
        List<Check> checks,
        Dictionary<(string Location, string Finder), List<string>> paths,
        HashSet<(string Finder, string Receiver, string Item, string Location)> found,
        string? onlyReceiver,
        int? sphereLimit,
        bool showAllMissing)
    {
        var scopedChecks = checks
            .Where(c => !sphereLimit.HasValue || c.Sphere <= sphereLimit.Value)
            .OrderBy(c => c.Sphere)
            .ToList();

        if (scopedChecks.Count == 0)
        {
            return "Aucune sphère trouvée avec ces filtres.";
        }

        var missingChecks = scopedChecks
            .Where(c => found.Contains((c.Finder, c.Receiver, c.Item, c.Location)) is false)
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

        List<Check> displayed;
        if (showAllMissing)
        {
            displayed = missingChecks
                .OrderBy(c => c.Sphere)
                .ThenBy(c => c.Receiver)
                .ThenBy(c => c.Finder)
                .ThenBy(c => c.Location)
                .ToList();
        }
        else
        {
            var minSphere = missingChecks.Min(c => c.Sphere);
            displayed = missingChecks
                .Where(c => c.Sphere == minSphere)
                .OrderBy(c => c.Receiver)
                .ThenBy(c => c.Finder)
                .ThenBy(c => c.Location)
                .ToList();
        }

        var sb = new StringBuilder();
        var scopeText = showAllMissing
            ? "toutes les sphères"
            : $"sphère la plus basse ({displayed.Min(c => c.Sphere)})";
        var withPathCount = displayed.Count(m => paths.ContainsKey((m.Location, m.Finder)));
        var withoutPathCount = displayed.Count - withPathCount;

        sb.AppendLine($"Items manquants du Playthrough ({scopeText}) : {displayed.Count}");
        sb.AppendLine($"- avec path: {withPathCount}");
        sb.AppendLine($"- sans path: {withoutPathCount}");
        foreach (var missing in displayed)
        {
            var hasPath = paths.TryGetValue((missing.Location, missing.Finder), out var route);
            var pathHint = hasPath && route!.Count > 0 ? $" | path: {route[^1]}" : " | path: (indisponible)";
            sb.AppendLine($"- s{missing.Sphere}: {missing.Finder} -> {missing.Receiver} | {missing.Item} @ {missing.Location}{pathHint}");
        }

        if (withoutPathCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Note: `Paths` du spoiler log ne couvre pas forcément toutes les checks du `Playthrough`.");
            sb.AppendLine("Les entrées `path: (indisponible)` signifient simplement qu'aucun path explicite n'a été fourni pour cette check.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Normalize(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
