using System.Data.SQLite;
using System.Text.RegularExpressions;

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
                SELECT Hashtag, Name, Game, Status, Checks, Percent, LastActivity
                FROM GameStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);

            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                gameStatuses.Add(new GameStatus
                {
                    Hashtag = reader["Hashtag"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Game = reader["Game"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    Checks = reader["Checks"]?.ToString() ?? string.Empty,
                    Percent = reader["Percent"]?.ToString() ?? string.Empty,
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
