using Discord.WebSocket;
using System.Security.Cryptography;
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

        int? sphereToValidate = null;
        var validateRaw = command.Data.Options.FirstOrDefault(o => o.Name == "validate-sphere")?.Value;
        if (validateRaw != null && int.TryParse(validateRaw.ToString(), out var parsedValidationSphere))
        {
            sphereToValidate = parsedValidationSphere;
        }

        var resetValidation =
            command.Data.Options.FirstOrDefault(o => o.Name == "reset-validation")?.Value as bool?
            ?? false;

        if (string.IsNullOrWhiteSpace(spoilerPath) || !File.Exists(spoilerPath))
        {
            return "Aucun spoiler log trouvé pour ce thread. Utilise `/send-spoiler-log file:<spoiler.txt>` puis relance l'analyse.";
        }

        var checks = ParsePlaythrough(spoilerPath);
        var spoilerFingerprint = ComputeSpoilerFingerprint(spoilerPath);

        if (!string.IsNullOrWhiteSpace(receiver))
        {
            if (resetValidation)
            {
                await ResetValidatedSphereAsync(guildId, channelId, spoilerFingerprint, receiver);
            }

            if (sphereToValidate.HasValue)
            {
                await SaveValidatedSphereAsync(
                    guildId, channelId, spoilerFingerprint, receiver, sphereToValidate.Value);
            }
        }

        var manuallyValidatedSphere = string.IsNullOrWhiteSpace(receiver)
            ? null
            : await LoadValidatedSphereAsync(guildId, channelId, spoilerFingerprint, receiver);
        var found = await LoadFoundItemsAsync(guildId, channelId);
        var autoCompleted = await LoadAutoCompletedChecksAsync(guildId, channelId);

        return BuildReport(
            checks,
            found,
            receiver,
            sphereLimit,
            showAllMissing,
            hideItems,
            autoCompleted,
            manuallyValidatedSphere);
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

    private static async Task<HashSet<string>> LoadAutoCompletedChecksAsync(string guildId, string channelId)
    {
        var autoCompleted = new HashSet<string>(StringComparer.Ordinal);

        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT a.Alias AS PlayerAlias, l.Name AS EntryName,
                   CASE WHEN l.Id < 0 THEN 'location' ELSE 'known-location' END AS EntryType
            FROM AliasChoicesTable a
            JOIN DatapackageGameMap gm
              ON gm.GuildId = a.GuildId
             AND gm.ChannelId = a.ChannelId
             AND gm.GameName = a.Game
            JOIN DatapackageLocations l
              ON l.GuildId = gm.GuildId
             AND l.ChannelId = gm.ChannelId
             AND l.DatasetKey = gm.DatasetKey
            WHERE a.GuildId = @GuildId
              AND a.ChannelId = @ChannelId

            UNION ALL

            SELECT a.Alias AS PlayerAlias, '' AS EntryName, 'location-catalog' AS EntryType
            FROM AliasChoicesTable a
            JOIN DatapackageGameMap gm
              ON gm.GuildId = a.GuildId
             AND gm.ChannelId = a.ChannelId
             AND gm.GameName = a.Game
            WHERE a.GuildId = @GuildId
              AND a.ChannelId = @ChannelId
              AND EXISTS (
                  SELECT 1
                  FROM DatapackageLocations l
                  WHERE l.GuildId = gm.GuildId
                    AND l.ChannelId = gm.ChannelId
                    AND l.DatasetKey = gm.DatasetKey
              )

            UNION ALL

            SELECT a.Alias AS PlayerAlias, i.Name AS EntryName,
                   CASE WHEN i.Id < 0 THEN 'item' ELSE 'known-item' END AS EntryType
            FROM AliasChoicesTable a
            JOIN DatapackageGameMap gm
              ON gm.GuildId = a.GuildId
             AND gm.ChannelId = a.ChannelId
             AND gm.GameName = a.Game
            JOIN DatapackageItems i
              ON i.GuildId = gm.GuildId
             AND i.ChannelId = gm.ChannelId
             AND i.DatasetKey = gm.DatasetKey
            WHERE a.GuildId = @GuildId
              AND a.ChannelId = @ChannelId
            
            UNION ALL

            SELECT a.Alias AS PlayerAlias, '' AS EntryName, 'item-catalog' AS EntryType
            FROM AliasChoicesTable a
            JOIN DatapackageGameMap gm
              ON gm.GuildId = a.GuildId
             AND gm.ChannelId = a.ChannelId
             AND gm.GameName = a.Game
            WHERE a.GuildId = @GuildId
              AND a.ChannelId = @ChannelId
              AND EXISTS (
                  SELECT 1
                  FROM DatapackageItems i
                  WHERE i.GuildId = gm.GuildId
                    AND i.ChannelId = gm.ChannelId
                    AND i.DatasetKey = gm.DatasetKey
              );";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            autoCompleted.Add(AutoCompletedKey(
                reader["EntryType"]?.ToString() ?? string.Empty,
                reader["PlayerAlias"]?.ToString() ?? string.Empty,
                reader["EntryName"]?.ToString() ?? string.Empty));
        }

        return autoCompleted;
    }

    public static string BuildReport(
        List<Check> checks,
        HashSet<string> found,
        string? onlyReceiver,
        int? sphereLimit,
        bool showAllMissing,
        bool hideItems,
        HashSet<string>? autoCompleted = null,
        int? manuallyValidatedSphere = null)
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

        var allMissingChecks = scopedChecks
            .Where(c => !found.Contains(FoundKey(c))
                && !IsAutoCompleted(c, autoCompleted)
                && !IsManuallyValidated(c, onlyReceiver, manuallyValidatedSphere))
            .ToList();

        var missingChecks = allMissingChecks;
        if (!string.IsNullOrWhiteSpace(onlyReceiver))
        {
            missingChecks = missingChecks
                .Where(c => string.Equals(c.Receiver, onlyReceiver, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var blockingOthersNow = string.IsNullOrWhiteSpace(onlyReceiver)
            ? new List<Check>()
            : allMissingChecks
                .GroupBy(c => c.Receiver, StringComparer.OrdinalIgnoreCase)
                .SelectMany(group =>
                {
                    var receiverCurrentSphere = group.Min(c => c.Sphere);
                    return group.Where(c =>
                        c.Sphere == receiverCurrentSphere
                        && string.Equals(c.Finder, onlyReceiver, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(c.Receiver, onlyReceiver, StringComparison.OrdinalIgnoreCase));
                })
                .OrderBy(c => c.Receiver)
                .ThenBy(c => c.Sphere)
                .ThenBy(c => c.Location)
                .ToList();

        if (missingChecks.Count == 0 && blockingOthersNow.Count == 0)
        {
            if (manuallyValidatedSphere.HasValue && !string.IsNullOrWhiteSpace(onlyReceiver))
            {
                return $"Sphères locales validées manuellement pour {onlyReceiver} : jusqu’à S{manuallyValidatedSphere}\n\n"
                    + "Aucun item manquant dans le Playthrough avec les paramètres actuels.";
            }

            return "Aucun item manquant dans le Playthrough avec les paramètres actuels.";
        }

        int? earliestIncompleteSphere = missingChecks.Count > 0
            ? missingChecks.Min(c => c.Sphere)
            : null;

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

        if (manuallyValidatedSphere.HasValue && !string.IsNullOrWhiteSpace(onlyReceiver))
        {
            sb.AppendLine($"Sphères locales validées manuellement pour {onlyReceiver} : jusqu’à S{manuallyValidatedSphere}");
            sb.AppendLine();
        }

        if (earliestIncompleteSphere.HasValue)
        {
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
        }
        else
        {
            sb.AppendLine($"Aucune check ne bloque actuellement {onlyReceiver}.");
        }

        if (!string.IsNullOrWhiteSpace(onlyReceiver))
        {
            sb.AppendLine();
            sb.AppendLine($"Checks avec lesquelles {onlyReceiver} bloque actuellement d'autres joueurs : {blockingOthersNow.Count}");
            foreach (var check in blockingOthersNow)
            {
                sb.AppendLine($"- {FormatCheck(check, hideItems)}");
            }
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
        if (!string.IsNullOrWhiteSpace(onlyReceiver))
        {
            sb.AppendLine("- Pour chaque autre joueur, une check détenue par l'alias sélectionné est bloquante seulement si elle appartient à sa sphère actuelle.");
        }
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

    private static bool IsAutoCompleted(Check check, HashSet<string>? autoCompleted)
        => autoCompleted != null
           && (autoCompleted.Contains(AutoCompletedKey("location", check.Finder, check.Location))
               || (autoCompleted.Contains(AutoCompletedKey("location-catalog", check.Finder, string.Empty))
                   && !autoCompleted.Contains(AutoCompletedKey("known-location", check.Finder, check.Location)))
               || autoCompleted.Contains(AutoCompletedKey("item", check.Receiver, check.Item))
               || (autoCompleted.Contains(AutoCompletedKey("item-catalog", check.Receiver, string.Empty))
                   && !autoCompleted.Contains(AutoCompletedKey("known-item", check.Receiver, check.Item))));

    private static bool IsManuallyValidated(Check check, string? alias, int? validatedSphere)
        => validatedSphere.HasValue
           && !string.IsNullOrWhiteSpace(alias)
           && check.Sphere <= validatedSphere.Value
           && string.Equals(check.Finder, alias, StringComparison.OrdinalIgnoreCase)
           && string.Equals(check.Receiver, alias, StringComparison.OrdinalIgnoreCase);

    private static string ComputeSpoilerFingerprint(string spoilerPath)
    {
        using var stream = File.OpenRead(spoilerPath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static async Task<int?> LoadValidatedSphereAsync(
        string guildId, string channelId, string fingerprint, string alias)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ValidatedSphere
            FROM SpoilerSphereValidationTable
            WHERE GuildId=@GuildId AND ChannelId=@ChannelId
              AND SpoilerFingerprint=@Fingerprint AND Alias=@Alias;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Fingerprint", fingerprint);
        command.Parameters.AddWithValue("@Alias", alias);
        var value = await command.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private static async Task SaveValidatedSphereAsync(
        string guildId, string channelId, string fingerprint, string alias, int sphere)
    {
        await using var connection = await Db.OpenWriteAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SpoilerSphereValidationTable
                (GuildId, ChannelId, SpoilerFingerprint, Alias, ValidatedSphere)
            VALUES (@GuildId, @ChannelId, @Fingerprint, @Alias, @Sphere)
            ON CONFLICT(GuildId, ChannelId, SpoilerFingerprint, Alias)
            DO UPDATE SET ValidatedSphere = MAX(ValidatedSphere, excluded.ValidatedSphere);";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Fingerprint", fingerprint);
        command.Parameters.AddWithValue("@Alias", alias);
        command.Parameters.AddWithValue("@Sphere", sphere);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ResetValidatedSphereAsync(
        string guildId, string channelId, string fingerprint, string alias)
    {
        await using var connection = await Db.OpenWriteAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM SpoilerSphereValidationTable
            WHERE GuildId=@GuildId AND ChannelId=@ChannelId
              AND SpoilerFingerprint=@Fingerprint AND Alias=@Alias;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        command.Parameters.AddWithValue("@Fingerprint", fingerprint);
        command.Parameters.AddWithValue("@Alias", alias);
        await command.ExecuteNonQueryAsync();
    }

    private static string AutoCompletedKey(string entryType, string playerAlias, string entryName)
        => string.Join("||", new[]
        {
            Normalize(entryType).ToUpperInvariant(),
            Normalize(playerAlias).ToUpperInvariant(),
            Normalize(entryName).ToUpperInvariant()
        });

    private static string Normalize(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
