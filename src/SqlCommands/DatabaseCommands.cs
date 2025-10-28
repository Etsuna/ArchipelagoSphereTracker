using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;

public static class DatabaseCommands
{
    // ====================
    // 🎯 GET ALL GUILDS (READ)
    // ====================
    public static async Task<List<string>> GetAllGuildsAsync(string table)
    {
        var guilds = new List<string>();
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand($@"SELECT GuildId FROM {table};", connection);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                    guilds.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving guilds: {ex.Message}");
        }
        return guilds;
    }

    // ====================
    // 🎯 PROGRAM IDENTIFIER (READ then WRITE if absent)
    // ====================
    public static async Task<string> ProgramIdentifier(string tableName)
    {
        try
        {
            await using (var connection = await Db.OpenReadAsync())
            {
                using var selectCmd = new SQLiteCommand("SELECT ProgramId FROM ProgramIdTable LIMIT 1;", connection);
                var result = await selectCmd.ExecuteScalarAsync().ConfigureAwait(false) as string;
                if (!string.IsNullOrEmpty(result))
                    return result!;
            }

            var newId = Declare.TelemetryName;

            await Db.WriteAsync(async conn =>
            {
                using var insertCmd = new SQLiteCommand("INSERT INTO ProgramIdTable (ProgramId) VALUES (@ProgramId);", conn);
                insertCmd.Parameters.AddWithValue("@ProgramId", newId);
                await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            });

            return newId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetOrCreateProgramIdAsync: {ex.Message}");
            return Guid.NewGuid().ToString();
        }
    }

    // ====================
    // 🎯 GET ALL CHANNELS (READ)
    // ====================
    public static async Task<List<string>> GetAllChannelsAsync(string guildId, string table)
    {
        var channels = new List<string>();
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand($@"SELECT ChannelId FROM {table} WHERE GuildId = @GuildId;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                    channels.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving channels: {ex.Message}");
        }
        return channels;
    }

    // ==========================
    // 🎯 DISTINCT GUILDS & CHANNELS COUNT (READ)
    // ==========================
    public static async Task<(int GuildCount, int ChannelCount)> GetDistinctGuildsAndChannelsCountAsync(string table)
    {
        int guildCount = 0, channelCount = 0;
        try
        {
            await using var connection = await Db.OpenReadAsync();

            using (var cmdGuild = new SQLiteCommand($@"SELECT COUNT(DISTINCT GuildId) FROM {table};", connection))
            {
                var resultGuild = await cmdGuild.ExecuteScalarAsync().ConfigureAwait(false);
                if (resultGuild != null && int.TryParse(resultGuild.ToString(), out var parsed))
                    guildCount = parsed;
            }

            using (var cmdChannel = new SQLiteCommand($@"SELECT COUNT(DISTINCT ChannelId) FROM {table};", connection))
            {
                var resultChannel = await cmdChannel.ExecuteScalarAsync().ConfigureAwait(false);
                if (resultChannel != null && int.TryParse(resultChannel.ToString(), out var parsed))
                    channelCount = parsed;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving counts: {ex.Message}");
        }
        return (guildCount, channelCount);
    }

    // ==========================================
    // 🎯 GET ID FROM GUILD + CHANNEL + TABLE (READ)
    // ==========================================
    public static async Task<long> GetGuildChannelIdAsync(string guildId, string channelId, string table)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand($@"
                SELECT Id FROM {table}
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            if (result is long l) return l;
            if (result != null && long.TryParse(result.ToString(), out var parsed)) return parsed;

            Console.WriteLine(Resource.NoRecordFoundForGuilChannel);
            return -1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving the channel ID: {ex.Message}");
            return -1;
        }
    }

    // ==========================
    // 🎯 GET IDS BY ALIAS (READ)
    // ==========================
    public static async Task<List<long>> GetIdsAsync(string guildId, string channelId, string alias, string table)
    {
        var ids = new List<long>();
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand($@"
                SELECT Id FROM {table}
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId AND Alias = @Alias;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Alias", alias);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                    ids.Add(reader.GetInt64(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving IDs: {ex.Message}");
        }
        return ids;
    }

    // ==========================
    // 🎯 CHECK IF CHANNEL EXISTS (READ)
    // ==========================
    public static async Task<bool> CheckIfChannelExistsAsync(string guildId, string channelId, string table)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var command = new SQLiteCommand($@"
                SELECT COUNT(*) FROM {table}
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            var count = result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0L;
            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking the existence of the channel: {ex.Message}");
            return false;
        }
    }

    // ==========================
    // 🎯 DELETE BY GUILD + CHANNEL (WRITE)
    // ==========================
    public static async Task DeleteChannelDataAsync(string guildId, string channelId)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();

                command.CommandText = @"
                    DELETE FROM UrlAndChannelPatchTable
                    WHERE ChannelsAndUrlsTableId IN (
                        SELECT Id FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();

                command.CommandText = @"
                    DELETE FROM ChannelsAndUrlsTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM ReceiverAliasesTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id FROM RecapListTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DisplayedItemTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM AliasChoicesTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM GameStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM HintStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DatapackageGameMap
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DatapackageItemGroups
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DatapackageItems
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DatapackageLocationGroups
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DatapackageLocations
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM UpdateAlertsTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            });

            Console.WriteLine(Resource.DeleteChannelDataAsyncDeleteSuccessful);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 DELETE BY GUILD (WRITE)
    // ==========================
    public static async Task DeleteChannelDataByGuildIdAsync(string guildId)
    {
        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();

                command.CommandText = @"
                    DELETE FROM UrlAndChannelPatchTable
                    WHERE ChannelsAndUrlsTableId IN (
                        SELECT Id FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM ChannelsAndUrlsTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM ReceiverAliasesTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id FROM RecapListTable
                        WHERE GuildId = @GuildId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM RecapListTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DisplayedItemTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM AliasChoicesTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM GameStatusTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM HintStatusTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DatapackageGameMap WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DatapackageItemGroups WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DatapackageItems WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DatapackageLocationGroups WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DatapackageLocations WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM UpdateAlertsTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            });

            Console.WriteLine(Resource.DeleteChannelDataByGuildIdAsyncDeletionGuildIdSuccessful);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting by GuildId: {ex.Message}");
        }
    }

    // ==========================
    // 🎯 RECLAIM SPACE (VACUUM) — sérialisé, hors transaction
    // ==========================
    public static async Task ReclaimSpaceAsync(long walTruncateThresholdBytes = 128L * 1024 * 1024)
    {
        try
        {
            var dbPath = Declare.DatabaseFile;
            await Task.Delay(2000);

            var walPath = dbPath + "-wal";
            long walSize = 0;
            if (File.Exists(walPath))
                walSize = new FileInfo(walPath).Length;

            await Db.WriteGate.WaitAsync();
            try
            {
                await using var connection = await Db.OpenWriteAsync();

                using (var cmd = new SQLiteCommand("PRAGMA wal_autocheckpoint=2000;", connection))
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                var checkpointMode = walSize > walTruncateThresholdBytes ? "TRUNCATE" : "PASSIVE";
                using (var chk = new SQLiteCommand($"PRAGMA wal_checkpoint({checkpointMode});", connection))
                    await chk.ExecuteNonQueryAsync().ConfigureAwait(false);
                Console.WriteLine($"Checkpoint {checkpointMode} (wal={walSize / 1024 / 1024} MB) done.");
            }
            finally
            {
                Db.WriteGate.Release();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur ReclaimSpaceAsync: {ex.Message}");
        }
    }
}
