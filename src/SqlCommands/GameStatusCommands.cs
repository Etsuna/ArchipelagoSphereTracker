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
                SELECT Slot, Name, Game, Checks, Total, LastActivity
                FROM TrackingPlayerStates
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                gameStatuses.Add(new GameStatus
                {
                    Slot = Convert.ToInt32(reader["Slot"]),
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

}
