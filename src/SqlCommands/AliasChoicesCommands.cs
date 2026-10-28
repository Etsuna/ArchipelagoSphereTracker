using System.Data.SQLite;

public static class AliasChoicesCommands
{
    public static async Task<string?> GetGameForAliasAsync(
        string guildId,
        string channelId,
        string alias
        )
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
                SELECT Game
                FROM TrackingSlots
                WHERE GuildId   = @GuildId
                  AND ChannelId = @ChannelId
                  AND Alias = @Alias;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Alias", alias);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving the Game for the alias: {ex.Message}");
            return null;
        }
    }

    public static Task AddOrReplaceAliasChoiceAsync(string guildId, string channelId,
        List<(int slot, string alias, string game)> slots)
        => Db.WriteAsync(async connection =>
        {
            await ArchipelagoSphereTracker.Tracking.Persistence.TrackingReadModel.EnsureRoomAsync(
                connection, guildId, channelId, CancellationToken.None);
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO TrackingSlots(GuildId,ChannelId,Slot,Alias,DisplayAlias,Game)
                VALUES(@G,@C,@Slot,@Alias,@Alias,@Game)
                ON CONFLICT(GuildId,ChannelId,Slot) DO UPDATE SET Alias=excluded.Alias,Game=excluded.Game;";
            command.Parameters.AddWithValue("@G", guildId);
            command.Parameters.AddWithValue("@C", channelId);
            command.Parameters.Add("@Slot", System.Data.DbType.Int32);
            command.Parameters.Add("@Alias", System.Data.DbType.String);
            command.Parameters.Add("@Game", System.Data.DbType.String);
            foreach (var slot in slots)
            {
                command.Parameters["@Slot"].Value = slot.slot;
                command.Parameters["@Alias"].Value = slot.alias;
                command.Parameters["@Game"].Value = slot.game;
                await command.ExecuteNonQueryAsync();
            }
        });

    public static async Task<List<string>> GetAliasesForGuildAndChannelAsync(
        string guildId,
        string channelId
        )
    {
        var aliases = new List<string>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
                SELECT Alias
                FROM TrackingSlots
                WHERE GuildId   = @GuildId
                  AND ChannelId = @ChannelId;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alias = reader["Alias"]?.ToString();
                if (!string.IsNullOrEmpty(alias))
                    aliases.Add(alias);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving aliases: {ex.Message}");
        }

        return aliases;
    }
}
