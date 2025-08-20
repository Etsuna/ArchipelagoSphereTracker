using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;
using System.Text;
using System.Text.RegularExpressions;

public static class ChannelsAndUrlsCommands
{
    private const string DefaultTrackerValue = "Non trouvé";

    // ==========================
    // 🎯 Channel et URL
    // ==========================
    public static async Task AddOrEditUrlChannelAsync(string guildId, string channelId, string newUrl, string trackerUrl, string sphereTrackerUrl, bool silent)
    {
        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            using var command = new SQLiteCommand(connection)
            {
                CommandText = @"INSERT OR REPLACE INTO ChannelsAndUrlsTable
                                (GuildId, ChannelId, Room, Tracker, SphereTracker, Silent)
                                VALUES (@GuildId, @ChannelId, @Room, @Tracker, @SphereTracker, @Silent)"
            };

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Room", newUrl);
            command.Parameters.AddWithValue("@Tracker", trackerUrl ?? DefaultTrackerValue);
            command.Parameters.AddWithValue("@SphereTracker", sphereTrackerUrl ?? DefaultTrackerValue);
            command.Parameters.AddWithValue("@Silent", silent);

            await command.ExecuteNonQueryAsync();
            Console.WriteLine(Resource.AddOrEditUrlChannelAsyncSuccessful);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating the URL: {ex.Message}");
        }
    }

    public static async Task AddOrEditUrlChannelPathAsync(string guildId, string channelId, List<Patch> patch)
    {
        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

            if (guildChannelId == -1)
            {
                Console.WriteLine(Resource.AddOrEditUrlChannelPathAsyncError);
                return;
            }

            using var transaction = await connection.BeginTransactionAsync();
            using var command = new SQLiteCommand(connection)
            {
                CommandText = @"INSERT OR REPLACE INTO UrlAndChannelPatchTable
                                (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                                VALUES (@ChannelsAndUrlsTableId, @Alias, @GameName, @Patch)"
            };

            command.Parameters.Add(new SQLiteParameter("@ChannelsAndUrlsTableId", guildChannelId));
            var aliasParam = new SQLiteParameter("@Alias", string.Empty);
            var gameNameParam = new SQLiteParameter("@GameName", string.Empty);
            var patchParam = new SQLiteParameter("@Patch", string.Empty);

            command.Parameters.Add(aliasParam);
            command.Parameters.Add(gameNameParam);
            command.Parameters.Add(patchParam);

            foreach (var patchItem in patch)
            {
                aliasParam.Value = patchItem.GameAlias;
                gameNameParam.Value = patchItem.GameName;
                patchParam.Value = patchItem.PatchLink;
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding or updating the alias: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 GET URL AND TRACKER
    // ==========================
    public static async Task<(string trackerUrl, string sphereTrackerUrl, string roomUrl, bool Silent)> GetTrackerUrlsAsync(string guildId, string channelId)
    {
        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            using var command = new SQLiteCommand(connection)
            {
                CommandText = @"SELECT Tracker, SphereTracker, Room, Silent 
                                FROM ChannelsAndUrlsTable
                                WHERE GuildId = @GuildId AND ChannelId = @ChannelId"
            };

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
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

    // ==========================
    // 🎯 GET PATCH AND GAME NAME
    // ==========================
    public static async Task<string> GetPatchAndGameNameForAlias(string guildId, string channelId, string alias)
    {
        using var connection = await Db.OpenAsync(Declare.CT);

        // Extraire le nom réel si l'alias est de la forme "NomAffiché (NomRéel)"
        var match = Regex.Match(alias, @"\(([^)]+)\)$");
        string realAlias = match.Success ? match.Groups[1].Value.Trim() : alias;

        long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

        using var command = new SQLiteCommand(connection)
        {
            CommandText = @"
            SELECT GameName, Patch 
            FROM UrlAndChannelPatchTable
            WHERE ChannelsAndUrlsTableId = @ChannelsAndUrlsTableId
              AND Alias = @Alias"
        };

        command.Parameters.AddWithValue("@ChannelsAndUrlsTableId", guildChannelId);
        command.Parameters.AddWithValue("@Alias", realAlias);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return $"{reader["GameName"]?.ToString() ?? string.Empty} : {reader["Patch"]?.ToString() ?? string.Empty}";
        }

        return Resource.GetPatchAndGameNameForAliasNoRecordFound;
    }


    // ==============================
    // 🎯 GET ALL PATCHES FOR CHANNEL
    // ==============================
    public static async Task SendAllPatchesFileForChannelAsync(string guildId, string channelId)
    {
        try
        {
            using var connection = await Db.OpenAsync(Declare.CT);

            long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(
                guildId, channelId, "ChannelsAndUrlsTable");

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

            using var reader = await command.ExecuteReaderAsync();

            var sb = new StringBuilder(capacity: 4096);
            bool any = false;

            sb.AppendLine("**Patches configurés pour ce canal :**");
            sb.AppendLine();

            while (await reader.ReadAsync())
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

            await File.WriteAllTextAsync(tempPath, sb.ToString(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            try
            {
                await using var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await BotCommands.SendFileAsync(channelId, fs, fileName, "Liste complète des patches pour ce canal.");
            }
            finally
            {
                try { File.Delete(tempPath); } catch {  }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while sending patches: {ex.Message}");
        }
    }
}
