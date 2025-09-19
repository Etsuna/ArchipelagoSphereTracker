using System.Data.SQLite;

public static class GameStatusCommands
{
    public static async Task<List<GameStatus>> GetGameStatusForGuildAndChannelAsync(
        string guildId,
        string channelId
        )
    {
        var gameStatuses = new List<GameStatus>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            using var command = new SQLiteCommand(@"
                SELECT Name, Game, Checks, Total, LastActivity
                FROM GameStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                gameStatuses.Add(new GameStatus
                {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Game = reader["Game"]?.ToString() ?? string.Empty,
                    Checks = reader["Checks"]?.ToString() ?? string.Empty,
                    Total = reader["Total"]?.ToString() ?? string.Empty,
                    LastActivity = reader["LastActivity"]?.ToString() ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving game status information: {ex.Message}");
        }

        return gameStatuses;
    }

    public static async Task UpdateGameStatusBatchAsync(
    string guildId,
    string channelId,
    List<GameStatus> gameStatuses)
    {
        if (gameStatuses is null || gameStatuses.Count == 0) return;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var command = conn.CreateCommand();
                command.CommandText = @"
                INSERT INTO GameStatusTable (GuildId, ChannelId, Name, Game, Total, Checks, LastActivity)
                VALUES (@GuildId, @ChannelId, @Name, @Game, @Total, @Checks, @LastActivity)
                ON CONFLICT(GuildId, ChannelId, Name) DO UPDATE SET
                    Game = excluded.Game,
                    Total = excluded.Total,
                    Checks = excluded.Checks,
                    LastActivity = excluded.LastActivity;";

                command.Parameters.Add(new SQLiteParameter("@GuildId", guildId));
                command.Parameters.Add(new SQLiteParameter("@ChannelId", channelId));
                command.Parameters.Add(new SQLiteParameter("@Name", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Game", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Total", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@Checks", System.Data.DbType.String));
                command.Parameters.Add(new SQLiteParameter("@LastActivity", System.Data.DbType.String));

                command.Prepare();

                foreach (var gs in gameStatuses)
                {
                    command.Parameters["@Name"].Value = (object?)gs.Name ?? DBNull.Value;
                    command.Parameters["@Game"].Value = (object?)gs.Game ?? DBNull.Value;
                    command.Parameters["@Total"].Value = (object?)gs.Total ?? DBNull.Value;
                    command.Parameters["@Checks"].Value = (object?)gs.Checks ?? DBNull.Value;
                    command.Parameters["@LastActivity"].Value = (object?)gs.LastActivity ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while upserting GameStatus: {ex.Message}");
        }
    }
}
