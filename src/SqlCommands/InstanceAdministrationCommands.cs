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
    private const string StoredRoomsQuery = @"
                SELECT GuildId, ChannelId FROM ChannelsAndUrlsTable
                UNION SELECT GuildId, ChannelId FROM RecapListTable
                UNION SELECT GuildId, ChannelId FROM PortalAccessTable
                UNION SELECT GuildId, ChannelId FROM TrackedRooms
                UNION SELECT GuildId, ChannelId FROM RoomSnapshots
                UNION SELECT GuildId, ChannelId FROM TrackingEvents
                UNION SELECT GuildId, ChannelId FROM RoomPollState
                UNION SELECT GuildId, ChannelId FROM ReceiverAliasesTable
                UNION SELECT GuildId, ChannelId FROM AliasChoicesTable
                UNION SELECT GuildId, ChannelId FROM DisplayedItemTable
                UNION SELECT GuildId, ChannelId FROM GameStatusTable
                UNION SELECT GuildId, ChannelId FROM HintStatusTable
                UNION SELECT GuildId, ChannelId FROM DatapackageItems
                UNION SELECT GuildId, ChannelId FROM DatapackageItemGroups
                UNION SELECT GuildId, ChannelId FROM DatapackageLocations
                UNION SELECT GuildId, ChannelId FROM DatapackageLocationGroups
                UNION SELECT GuildId, ChannelId FROM DatapackageGameMap
                UNION SELECT GuildId, ChannelId FROM UpdateAlertsTable
                UNION SELECT GuildId, ChannelId FROM SpoilerSphereValidationTable
                UNION SELECT GuildId, ChannelId FROM LastItemsCheckTable
                UNION SELECT GuildId, ChannelId FROM ExcludedItemTable";

    public static async Task<IReadOnlyList<AstInstanceGuildSummary>> GetGuildsAsync()
    {
        var guilds = new List<AstInstanceGuildSummary>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = new SQLiteCommand($@"
            SELECT GuildId, COUNT(*) AS RoomCount
            FROM (
                {StoredRoomsQuery}
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
        using var command = new SQLiteCommand($@"
            WITH Rooms AS (
                {StoredRoomsQuery}
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
