using System.Data.SQLite;
using System.Globalization;

public static class CheckUpdateCommands
{
    public static async Task<(string? LatestTag, DateTimeOffset? LastSentUtc)?> GetUpdateAlertAsync(string guild, string channel)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();
            using var cmd = new SQLiteCommand(
                "SELECT LatestTag, LastSentUtc FROM UpdateAlertsTable WHERE GuildId=@g AND ChannelId=@c LIMIT 1;",
                connection);

            cmd.Parameters.AddWithValue("@g", guild);
            cmd.Parameters.AddWithValue("@c", channel);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;

            var tag = reader["LatestTag"] as string;
            var lastStr = reader["LastSentUtc"] as string;

            DateTimeOffset? last = null;
            if (!string.IsNullOrWhiteSpace(lastStr))
            {
                if (DateTimeOffset.TryParse(lastStr, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    last = dto;
                else if (DateTime.TryParse(lastStr, null, DateTimeStyles.AdjustToUniversal, out var dt))
                    last = new DateTimeOffset(dt, TimeSpan.Zero);
            }

            return (tag, last);
        }
        catch
        {
            return null;
        }
    }

    public static async Task UpsertUpdateAlertAsync(string guild, string channel, string latestTag, DateTimeOffset lastSentUtc)
    {
        var iso = lastSentUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using (var create = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS UpdateAlertsTable (" +
                    "GuildId TEXT NOT NULL, " +
                    "ChannelId TEXT NOT NULL, " +
                    "LatestTag TEXT NULL, " +
                    "LastSentUtc TEXT NULL, " +
                    "PRIMARY KEY(GuildId, ChannelId)" +
                    ");", conn))
                {
                    await create.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                using (var upsert = new SQLiteCommand(
                    "INSERT INTO UpdateAlertsTable (GuildId, ChannelId, LatestTag, LastSentUtc) " +
                    "VALUES (@g, @c, @t, @d) " +
                    "ON CONFLICT(GuildId, ChannelId) DO UPDATE SET " +
                    "LatestTag = excluded.LatestTag, " +
                    "LastSentUtc = excluded.LastSentUtc;", conn))
                {
                    upsert.Parameters.AddWithValue("@g", guild);
                    upsert.Parameters.AddWithValue("@c", channel);
                    upsert.Parameters.AddWithValue("@t", latestTag);
                    upsert.Parameters.AddWithValue("@d", iso);
                    await upsert.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            });
        }
        catch
        {
            // silencieux
        }
    }
}
