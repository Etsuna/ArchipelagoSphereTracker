using System.Data.SQLite;


public static class ChannelsAndUrlsCommands
{
    private const string DefaultTrackerValue = "Non trouvé";
    private const string DefaultDatabaseFile = "Data Source=AST.db;Version=3;";

    private static SQLiteConnection CreateConnection(string databaseFile = DefaultDatabaseFile)
    {
        return new SQLiteConnection(databaseFile);
    }

    // ==========================
    // 🎯 Channel et URL
    // ==========================
    public static async Task AddOrEditUrlChannelAsync(string guildId, string channelId, string newUrl, string trackerUrl, string sphereTrackerUrl, bool silent)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

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
            Console.WriteLine("URL et autres informations ajoutées ou mises à jour avec succès.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou mise à jour de l'URL: {ex.Message}");
        }
    }

    public static async Task AddOrEditUrlChannelPathAsync(string guildId, string channelId, List<Patch> patch)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

            if (guildChannelId == -1)
            {
                Console.WriteLine("Erreur : Aucun enregistrement Guild/Channel trouvé. Impossible d'ajouter le patch.");
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
            Console.WriteLine($"Erreur lors de l'ajout ou mise à jour de l'alias : {ex.Message}");
        }
    }

    // ==========================
    // 🎯 GET URL AND TRACKER
    // ==========================
    public static async Task<(string trackerUrl, string sphereTrackerUrl, string roomUrl, bool Silent)> GetTrackerUrlsAsync(string guildId, string channelId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

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
            Console.WriteLine($"Erreur lors de la récupération des URLs du tracker : {ex.Message}");
            return (string.Empty, string.Empty, string.Empty, false);
        }
    }

    // ==========================
    // 🎯 GET PATCH AND GAME NAME
    // ==========================
    public static async Task<string> GetPatchAndGameNameForAlias(string guildId, string channelId, string alias)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

        using var command = new SQLiteCommand(connection)
        {
            CommandText = @"SELECT GameName, Patch 
                            FROM UrlAndChannelPatchTable
                            WHERE ChannelsAndUrlsTableId = @ChannelsAndUrlsTableId AND Alias = @Alias"
        };

        command.Parameters.AddWithValue("@ChannelsAndUrlsTableId", guildChannelId);
        command.Parameters.AddWithValue("@Alias", alias);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return $"{reader["GameName"]?.ToString() ?? string.Empty} : {reader["Patch"]?.ToString() ?? string.Empty}";
        }

        return "Aucun enregistrement trouvé.";
    }

    // ==============================
    // 🎯 GET ALL PATCHES FOR CHANNEL
    // ==============================
    public static async Task SendAllPatchesForChannelAsync(string guildId, string channelId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            long guildChannelId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");
            if (guildChannelId == -1)
            {
                Console.WriteLine("Aucun ID de canal trouvé pour ce GuildId et ChannelId.");
                return;
            }

            using var command = new SQLiteCommand(@"
                SELECT Alias, GameName, Patch
                FROM UrlAndChannelPatchTable
                WHERE ChannelsAndUrlsTableId = @ChannelsAndUrlsTableId", connection);

            command.Parameters.AddWithValue("@ChannelsAndUrlsTableId", guildChannelId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string alias = reader["Alias"]?.ToString() ?? "Inconnu";
                string gameName = reader["GameName"]?.ToString() ?? "Non spécifié";
                string patch = reader["Patch"]?.ToString() ?? "Non spécifié";

                await BotCommands.SendMessageAsync($"Patch pour {alias}, {gameName} : {patch}", channelId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi des patchs : {ex.Message}");
        }
    }
}
