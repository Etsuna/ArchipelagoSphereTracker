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
            // 1) Essayer de lire l'ID existant
            await using (var connection = await Db.OpenReadAsync())
            {
                using var selectCmd = new SQLiteCommand("SELECT ProgramId FROM ProgramIdTable LIMIT 1;", connection);
                var result = await selectCmd.ExecuteScalarAsync().ConfigureAwait(false) as string;
                if (!string.IsNullOrEmpty(result))
                    return result!;
            }

            // 2) Générer un nouvel ID
            var newId = Declare.TelemetryName;

            // 3) L'insérer (écriture transactionnelle)
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

            Console.WriteLine("No record found for the specified GuildId and ChannelId.");
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
            var count = (result != null && result != DBNull.Value) ? Convert.ToInt64(result) : 0L;
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

                // 1) UrlAndChannelPatchTable (via sous-requête)
                command.CommandText = @"
                    DELETE FROM UrlAndChannelPatchTable
                    WHERE ChannelsAndUrlsTableId IN (
                        SELECT Id FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId AND ChannelId = @ChannelId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // Réutiliser la même commande, nettoyer/mettre à jour les paramètres si besoin
                command.Parameters.Clear();

                // 2) ChannelsAndUrlsTable
                command.CommandText = @"
                    DELETE FROM ChannelsAndUrlsTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 3) ReceiverAliasesTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM ReceiverAliasesTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 4) RecapListItemsTable (via sous-requête)
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

                // 5) RecapListTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM RecapListTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 6) DisplayedItemTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM DisplayedItemTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 7) AliasChoicesTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM AliasChoicesTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 8) GameStatusTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM GameStatusTable
                    WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 9) HintStatusTable
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM HintStatusTable
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

                // 1) UrlAndChannelPatchTable via sous-requête
                command.CommandText = @"
                    DELETE FROM UrlAndChannelPatchTable
                    WHERE ChannelsAndUrlsTableId IN (
                        SELECT Id FROM ChannelsAndUrlsTable
                        WHERE GuildId = @GuildId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 2) ChannelsAndUrlsTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM ChannelsAndUrlsTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 3) ReceiverAliasesTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM ReceiverAliasesTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 4) RecapListItemsTable via sous-requête
                command.Parameters.Clear();
                command.CommandText = @"
                    DELETE FROM RecapListItemsTable
                    WHERE RecapListTableId IN (
                        SELECT Id FROM RecapListTable
                        WHERE GuildId = @GuildId
                    );";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 5) RecapListTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM RecapListTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 6) DisplayedItemTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM DisplayedItemTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 7) AliasChoicesTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM AliasChoicesTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 8) GameStatusTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM GameStatusTable WHERE GuildId = @GuildId;";
                command.Parameters.AddWithValue("@GuildId", guildId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                // 9) HintStatusTable
                command.Parameters.Clear();
                command.CommandText = @"DELETE FROM HintStatusTable WHERE GuildId = @GuildId;";
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
    public static async Task ReclaimSpaceAsync()
    {
        try
        {
            // éviter Thread.Sleep
            await Task.Delay(3000);

            // On sérialise manuellement via le WriteGate pour exclure toute autre écriture
            await Db.WriteGate.WaitAsync();
            try
            {
                await using var connection = await Db.OpenWriteAsync();
                // wal_checkpoint(TRUNCATE) peut être fait hors transaction
                using (var chk = new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE);", connection))
                    await chk.ExecuteNonQueryAsync().ConfigureAwait(false);

                // VACUUM ne doit pas être exécuté dans une transaction
                using (var vacuum = new SQLiteCommand("VACUUM;", connection))
                    await vacuum.ExecuteNonQueryAsync().ConfigureAwait(false);

                Console.WriteLine("Checkpoint + VACUUM effectués.");
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
