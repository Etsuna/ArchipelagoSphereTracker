using System.Data.SQLite;

namespace ArchipelagoSphereTracker.Tracking.Notifications;

public enum RoomItemNotificationFilter { All, UsefulAndProgression, ProgressionOnly }

public sealed record RoomNotificationPreferences(bool LinkedAliasesOnly, RoomItemNotificationFilter ItemFilter, bool HintsEnabled)
{
    public static RoomNotificationPreferences FromLegacy(bool silent) => new(silent, RoomItemNotificationFilter.All, !silent);

    public bool AllowsItem(int flags) => ItemFilter switch
    {
        RoomItemNotificationFilter.All => true,
        RoomItemNotificationFilter.UsefulAndProgression => (flags & 3) != 0,
        RoomItemNotificationFilter.ProgressionOnly => (flags & 1) != 0,
        _ => false
    };

    public static bool TryParse(string? audience, string? filter, string? hints, out RoomNotificationPreferences preferences)
    {
        preferences = FromLegacy(false);
        if (audience is not ("all" or "linked") || hints is not ("on" or "off") ||
            !Enum.GetNames<RoomItemNotificationFilter>().Contains(filter)) return false;
        preferences = new(audience == "linked", Enum.Parse<RoomItemNotificationFilter>(filter!), hints == "on");
        return true;
    }
}

public static class RoomNotificationPreferencesStore
{
    internal const string Schema = @"
CREATE TABLE IF NOT EXISTS RoomNotificationPreferences (
    RoomId INTEGER PRIMARY KEY,
    ItemFilter TEXT NOT NULL DEFAULT 'All' CHECK(ItemFilter IN ('All','UsefulAndProgression','ProgressionOnly')),
    HintsEnabled INTEGER NOT NULL CHECK(HintsEnabled IN (0,1)),
    FOREIGN KEY(RoomId) REFERENCES ChannelsAndUrlsTable(Id) ON DELETE CASCADE
);";

    internal static Task InitializeAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        return await command.ExecuteNonQueryAsync(ct);
    }, ct);

    public static async Task<RoomNotificationPreferences?> ReadAsync(string guildId, string channelId, CancellationToken ct = default)
    {
        await using var connection = await Db.OpenReadAsync();
        return await ReadAsync(connection, guildId, channelId, ct);
    }

    private static async Task<RoomNotificationPreferences?> ReadAsync(SQLiteConnection connection,
        string guildId, string channelId, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COALESCE(room.Silent,0), preferences.ItemFilter, preferences.HintsEnabled
            FROM ChannelsAndUrlsTable room LEFT JOIN RoomNotificationPreferences preferences
              ON preferences.RoomId=room.Id
            WHERE room.GuildId=@Guild AND room.ChannelId=@Channel;";
        command.Parameters.AddWithValue("@Guild", guildId);
        command.Parameters.AddWithValue("@Channel", channelId);
        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var linkedOnly = Convert.ToBoolean(reader[0]);
        if (reader.IsDBNull(1)) return RoomNotificationPreferences.FromLegacy(linkedOnly);
        return new(linkedOnly, Enum.Parse<RoomItemNotificationFilter>(reader.GetString(1)), Convert.ToBoolean(reader[2]));
    }

    public static Task<bool> SetAsync(string guildId, string channelId, RoomNotificationPreferences preferences,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(preferences.ItemFilter)) return Task.FromResult(false);
        return UpdateAsync(guildId, channelId, _ => preferences, ct);
    }

    public static Task<bool> SetOptionAsync(string guildId, string channelId, string option, string value,
        CancellationToken ct = default)
    {
        var valid = option switch
        {
            "audience" => value is "all" or "linked",
            "items" => Enum.GetNames<RoomItemNotificationFilter>().Contains(value),
            "hints" => value is "on" or "off",
            _ => false
        };
        if (!valid) return Task.FromResult(false);
        return UpdateAsync(guildId, channelId, current => option switch
        {
            "audience" => current with { LinkedAliasesOnly = value == "linked" },
            "items" => current with { ItemFilter = Enum.Parse<RoomItemNotificationFilter>(value) },
            _ => current with { HintsEnabled = value == "on" }
        }, ct);
    }

    private static async Task<bool> UpdateAsync(string guildId, string channelId,
        Func<RoomNotificationPreferences, RoomNotificationPreferences> update, CancellationToken ct)
    {
        var saved = await Db.WriteAsync(async connection =>
        {
            var current = await ReadAsync(connection, guildId, channelId, ct);
            if (current == null) return null;
            var next = update(current);
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE ChannelsAndUrlsTable SET Silent=@Linked WHERE GuildId=@Guild AND ChannelId=@Channel;
                INSERT INTO RoomNotificationPreferences(RoomId,ItemFilter,HintsEnabled)
                SELECT Id,@Filter,@Hints FROM ChannelsAndUrlsTable WHERE GuildId=@Guild AND ChannelId=@Channel
                ON CONFLICT(RoomId) DO UPDATE SET ItemFilter=excluded.ItemFilter,HintsEnabled=excluded.HintsEnabled;";
            command.Parameters.AddWithValue("@Guild", guildId);
            command.Parameters.AddWithValue("@Channel", channelId);
            command.Parameters.AddWithValue("@Linked", next.LinkedAliasesOnly);
            command.Parameters.AddWithValue("@Filter", next.ItemFilter.ToString());
            command.Parameters.AddWithValue("@Hints", next.HintsEnabled);
            await command.ExecuteNonQueryAsync(ct);
            return next;
        }, ct);
        if (saved == null) return false;
        if (ChannelConfigCache.TryGet(guildId, channelId, out var config))
            ChannelConfigCache.Upsert(guildId, channelId, config with { Silent = saved.LinkedAliasesOnly });
        return true;
    }
}
