using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;
using System.Text;
using System.Text.RegularExpressions;

public static class ChannelsAndUrlsCommands
{
    private const string DefaultTrackerValue = "Non trouvé";

    // ==========================
    // 🎯 Channel et URL (WRITE)
    // ==========================
    public static async Task AddOrEditUrlChannelAsync(
        string guildId,
        string channelId,
        string newUrl,
        string? trackerUrl,
        string? sphereTrackerUrl,
        bool silent,
        CancellationToken ct = default)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO ChannelsAndUrlsTable
                        (GuildId, ChannelId, Room, Tracker, SphereTracker, Silent)
                    VALUES
                        (@GuildId, @ChannelId, @Room, @Tracker, @SphereTracker, @Silent);";

                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                command.Parameters.AddWithValue("@Room", newUrl);
                command.Parameters.AddWithValue("@Tracker", trackerUrl ?? DefaultTrackerValue);
                command.Parameters.AddWithValue("@SphereTracker", sphereTrackerUrl ?? DefaultTrackerValue);
                command.Parameters.AddWithValue("@Silent", silent);

                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }, ct);

            Console.WriteLine(Resource.AddOrEditUrlChannelAsyncSuccessful);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating the URL: {ex.Message}");
        }
    }

    // ==================================================
    // 🎯 Ajout / Edition des patches pour un channel (WRITE)
    // ==================================================
    public static async Task AddOrEditUrlChannelPathAsync(
        string guildId,
        string channelId,
        List<Patch> patch,
        CancellationToken ct = default)
    {
        try
        {
            // On récupère l'ID logique du couple guild/channel (fonction existante)
            long guildChannelId = await DatabaseCommands
                .GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

            if (guildChannelId == -1)
            {
                Console.WriteLine(Resource.AddOrEditUrlChannelPathAsyncError);
                return;
            }

            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO UrlAndChannelPatchTable
                        (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                    VALUES
                        (@ChannelsAndUrlsTableId, @Alias, @GameName, @Patch);";

                command.Parameters.Add(new SQLiteParameter("@ChannelsAndUrlsTableId", guildChannelId));
                var aliasParam = command.Parameters.Add("@Alias", System.Data.DbType.String);
                var gameNameParam = command.Parameters.Add("@GameName", System.Data.DbType.String);
                var patchParam = command.Parameters.Add("@Patch", System.Data.DbType.String);

                command.Prepare();

                foreach (var p in patch)
                {
                    ct.ThrowIfCancellationRequested();
                    aliasParam.Value = p.GameAlias ?? string.Empty;
                    gameNameParam.Value = p.GameName ?? string.Empty;
                    patchParam.Value = p.PatchLink ?? string.Empty;
                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating the alias: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 GET URL AND TRACKER (READ)
    // ==========================
    public static async Task<(string trackerUrl, string sphereTrackerUrl, string roomUrl, bool Silent)>
        GetTrackerUrlsAsync(string guildId, string channelId, CancellationToken ct = default)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

            using var command = new SQLiteCommand(@"
                SELECT Tracker, SphereTracker, Room, Silent
                FROM ChannelsAndUrlsTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return (
                    reader["Tracker"]?.ToString() ?? string.Empty,
                    reader["SphereTracker"]?.ToString() ?? string.Empty,
                    reader["Room"]?.ToString() ?? string.Empty,
                    reader["Silent"] != DBNull.Value && Convert.ToBoolean(reader["Silent"])
                );
            }

            return (string.Empty, string.Empty, string.Empty, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving tracker URLs: {ex.Message}");
            return (string.Empty, string.Empty, string.Empty, false);
        }
    }

    public static async Task<bool> CountChannelByGuildId(string guildId, CancellationToken ct = default)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

            using var countCommand = new SQLiteCommand(@"
            SELECT COUNT(DISTINCT ChannelId)
            FROM ChannelsAndUrlsTable
            WHERE GuildId = @GuildId;", connection);

            countCommand.Parameters.AddWithValue("@GuildId", guildId);

            var result = await countCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var count = Convert.ToInt32(result ?? 0);

            return count <= 2;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while counting channels by guildId: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 GET PATCH AND GAME NAME (READ)
    // ==========================
    public static async Task<string> GetPatchAndGameNameForAlias(
        string guildId,
        string channelId,
        string alias,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

            // Extraire le nom réel si l'alias est de la forme "NomAffiché (NomRéel)"
            var match = Regex.Match(alias, @"\(([^)]+)\)$");
            string realAlias = match.Success ? match.Groups[1].Value.Trim() : alias;

            long guildChannelId = await DatabaseCommands
                .GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

            using var command = new SQLiteCommand(@"
                SELECT GameName, Patch
                FROM UrlAndChannelPatchTable
                WHERE ChannelsAndUrlsTableId = @ChannelsAndUrlsTableId
                  AND Alias = @Alias;", connection);

            command.Parameters.AddWithValue("@ChannelsAndUrlsTableId", guildChannelId);
            command.Parameters.AddWithValue("@Alias", realAlias);

            using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var game = reader["GameName"]?.ToString() ?? string.Empty;
                var patch = reader["Patch"]?.ToString() ?? string.Empty;
                return $"{game} : {patch}";
            }

            return Resource.GetPatchAndGameNameForAliasNoRecordFound;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving patch/game name: {ex.Message}");
            return Resource.GetPatchAndGameNameForAliasNoRecordFound;
        }
    }

    // ==============================
    // 🎯 GET ALL PATCHES FOR CHANNEL (READ)
    // ==============================
    public static async Task SendAllPatchesFileForChannelAsync(
        string guildId,
        string channelId,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync(ct);

            long guildChannelId = await DatabaseCommands
                .GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

            if (guildChannelId == -1)
            {
                Console.WriteLine(Resource.SendAllPatchesForChannelAsyncNoChannelId);
                return;
            }

            using var command = new SQLiteCommand(@"
                SELECT Alias, GameName, Patch
                FROM UrlAndChannelPatchTable
                WHERE ChannelsAndUrlsTableId = @ChannelsAndUrlsTableId;", connection);

            command.Parameters.AddWithValue("@ChannelsAndUrlsTableId", guildChannelId);

            using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var sb = new StringBuilder(capacity: 4096);
            bool any = false;

            sb.AppendLine("**Patches configurés pour ce canal :**");
            sb.AppendLine();

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                any = true;

                string alias = reader["Alias"]?.ToString() ?? "Inconnu";
                string gameName = reader["GameName"]?.ToString() ?? "Non spécifié";
                string patch = reader["Patch"]?.ToString() ?? "Non spécifié";

                string line = "• " + string.Format(
                    Resource.SendAllPatchesForChannelAsyncPathLink, alias, gameName, patch);

                sb.AppendLine(line);
            }

            if (!any)
            {
                await BotCommands.SendMessageAsync("Aucun patch configuré pour ce canal.", channelId);
                return;
            }

            string fileName = $"patches_{channelId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            await File.WriteAllTextAsync(
                tempPath,
                sb.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                ct);

            try
            {
                await using var fs = new FileStream(
                    tempPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    useAsync: true);

                await BotCommands.SendFileAsync(
                    channelId,
                    fs,
                    fileName,
                    "Liste complète des patches pour ce canal.");
            }
            finally
            {
                try { File.Delete(tempPath); } catch { /* no-op */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while sending patches: {ex.Message}");
        }
    }
}
