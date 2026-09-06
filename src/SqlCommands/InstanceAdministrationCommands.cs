using System.Data.SQLite;

public sealed record AstInstanceGuildSummary(string GuildId, int RoomCount);

public sealed record AstInstanceRoomSummary(
    string GuildId,
    string ChannelId,
    string BaseUrl,
    string Room,
    string Tracker,
    string CheckFrequency,
    bool Silent);

public static class InstanceAdministrationCommands
{
    public static async Task<IReadOnlyList<AstInstanceGuildSummary>> GetGuildsAsync()
    {
        var guilds = new List<AstInstanceGuildSummary>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            SELECT GuildId, COUNT(*) AS RoomCount
            FROM (
                SELECT GuildId, ChannelId FROM ChannelsAndUrlsTable
                UNION
                SELECT GuildId, ChannelId FROM TrackedRooms
            )
            GROUP BY GuildId
            ORDER BY GuildId;", connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var guildId = reader["GuildId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(guildId))
                guilds.Add(new AstInstanceGuildSummary(guildId, Convert.ToInt32(reader["RoomCount"])));
        }
        return guilds;
    }

    public static async Task<IReadOnlyList<AstInstanceRoomSummary>> GetRoomsAsync(string guildId)
    {
        if (!ulong.TryParse(guildId, out _)) return [];
        var rooms = new List<AstInstanceRoomSummary>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand(@"
            WITH Rooms AS (
                SELECT GuildId, ChannelId FROM ChannelsAndUrlsTable
                UNION
                SELECT GuildId, ChannelId FROM TrackedRooms
            )
            SELECT Rooms.GuildId, Rooms.ChannelId,
                   COALESCE(Config.BaseUrl, '') AS BaseUrl,
                   COALESCE(Config.Room, '') AS Room,
                   COALESCE(Config.Tracker, '') AS Tracker,
                   COALESCE(Config.CheckFrequency, '') AS CheckFrequency,
                   COALESCE(Config.Silent, 0) AS Silent
            FROM Rooms
            LEFT JOIN ChannelsAndUrlsTable AS Config
              ON Config.GuildId = Rooms.GuildId AND Config.ChannelId = Rooms.ChannelId
            WHERE Rooms.GuildId = @GuildId
            ORDER BY Rooms.ChannelId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var channelId = reader["ChannelId"]?.ToString();
            if (string.IsNullOrWhiteSpace(channelId)) continue;
            rooms.Add(new AstInstanceRoomSummary(
                guildId,
                channelId,
                reader["BaseUrl"]?.ToString() ?? string.Empty,
                reader["Room"]?.ToString() ?? string.Empty,
                reader["Tracker"]?.ToString() ?? string.Empty,
                reader["CheckFrequency"]?.ToString() ?? string.Empty,
                reader["Silent"] != DBNull.Value && Convert.ToBoolean(reader["Silent"])));
        }
        return rooms;
    }
}
