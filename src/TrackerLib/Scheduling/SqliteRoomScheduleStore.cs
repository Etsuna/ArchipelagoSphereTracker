using System.Data.Common;
using System.Globalization;

namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public sealed class SqliteRoomScheduleStore : IRoomScheduleStore
{
    private readonly TimeProvider _timeProvider;

    public SqliteRoomScheduleStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ScheduledRoomRegistration>> LoadAsync(
        CancellationToken cancellationToken)
    {
        var rooms = new List<ScheduledRoomRegistration>();
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                channel.GuildId,
                channel.ChannelId,
                channel.Tracker,
                channel.BaseUrl,
                channel.Room,
                channel.Silent,
                channel.Port,
                channel.CheckFrequency,
                channel.PollingMode,
                channel.MaximumCheckFrequency,
                channel.LastCheck,
                state.NextPollAtUtc,
                state.LastAttemptAtUtc,
                state.LastSuccessAtUtc,
                state.ConsecutiveFailures,
                state.LastFailureKind,
                state.BreakerOpenUntilUtc,
                state.LastLatencyMilliseconds,
                state.IsPaused,
                state.PausedAtUtc,
                state.LastForcedSyncAtUtc,
                state.LastContentHash,
                state.UnchangedSuccessCount,
                state.EffectiveIntervalSeconds,
                state.LastChangeAtUtc
            FROM ChannelsAndUrlsTable channel
            LEFT JOIN RoomPollState state
              ON state.GuildId = channel.GuildId
             AND state.ChannelId = channel.ChannelId
            ORDER BY channel.GuildId, channel.ChannelId;";

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var guildId = Text(reader, "GuildId");
            var channelId = Text(reader, "ChannelId");
            var baseUrl = Text(reader, "BaseUrl");
            if (string.IsNullOrWhiteSpace(guildId) ||
                string.IsNullOrWhiteSpace(channelId) ||
                !TryGetOrigin(baseUrl, out var origin))
            {
                continue;
            }

            var interval = TrackingDataManager.CheckFrequencyParser.ParseOrDefault(
                Text(reader, "CheckFrequency"),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5),
                null);
            var lastCheck = Timestamp(reader, "LastCheck");
            var initialNext = lastCheck?.Add(interval) ?? _timeProvider.GetUtcNow();
            if (!Enum.TryParse(Text(reader, "PollingMode", "Automatic"), true, out RoomPollingMode pollingMode))
                pollingMode = RoomPollingMode.Automatic;
            var maximumInterval = TrackingDataManager.CheckFrequencyParser.ParseOrDefault(
                Text(reader, "MaximumCheckFrequency", "1h"),
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromDays(1));
            if (maximumInterval < interval)
                maximumInterval = interval;
            var definition = new ScheduledRoomDefinition(
                guildId,
                channelId,
                origin,
                SensitiveDataProtector.Unprotect(
                    Text(reader, "Tracker"),
                    SensitiveDataPurposes.Tracker),
                baseUrl,
                SensitiveDataProtector.Unprotect(
                    Text(reader, "Room"),
                    SensitiveDataPurposes.Room),
                reader["Silent"] is not DBNull && Convert.ToBoolean(reader["Silent"], CultureInfo.InvariantCulture),
                Text(reader, "Port", "0"),
                interval,
                initialNext,
                pollingMode,
                maximumInterval);

            RoomScheduleState? state = null;
            if (Timestamp(reader, "NextPollAtUtc") is { } nextPollAt)
            {
                Enum.TryParse(Text(reader, "LastFailureKind"), ignoreCase: true, out PollFailureKind failureKind);
                state = new RoomScheduleState(
                    guildId,
                    channelId,
                    nextPollAt,
                    Timestamp(reader, "LastAttemptAtUtc"),
                    Timestamp(reader, "LastSuccessAtUtc"),
                    Integer(reader, "ConsecutiveFailures"),
                    failureKind,
                    Timestamp(reader, "BreakerOpenUntilUtc"),
                    Double(reader, "LastLatencyMilliseconds"),
                    Integer(reader, "IsPaused") == 1,
                    Timestamp(reader, "PausedAtUtc"),
                    Timestamp(reader, "LastForcedSyncAtUtc"),
                    NullText(reader, "LastContentHash"),
                    Integer(reader, "UnchangedSuccessCount"),
                    Double(reader, "EffectiveIntervalSeconds"),
                    Timestamp(reader, "LastChangeAtUtc"));
            }

            rooms.Add(new ScheduledRoomRegistration(definition, state));
        }

        return rooms;
    }

    public async Task SaveStateAsync(RoomScheduleState state, CancellationToken cancellationToken)
    {
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO RoomPollState
                    (GuildId, ChannelId, NextPollAtUtc, LastAttemptAtUtc, LastSuccessAtUtc,
                     ConsecutiveFailures, LastFailureKind, BreakerOpenUntilUtc,
                     LastLatencyMilliseconds, IsPaused, PausedAtUtc, LastForcedSyncAtUtc,
                     LastContentHash, UnchangedSuccessCount, EffectiveIntervalSeconds,
                     LastChangeAtUtc, UpdatedAtUtc)
                VALUES
                    (@GuildId, @ChannelId, @NextPollAtUtc, @LastAttemptAtUtc, @LastSuccessAtUtc,
                     @ConsecutiveFailures, @LastFailureKind, @BreakerOpenUntilUtc,
                     @LastLatencyMilliseconds, @IsPaused, @PausedAtUtc, @LastForcedSyncAtUtc,
                     @LastContentHash, @UnchangedSuccessCount, @EffectiveIntervalSeconds,
                     @LastChangeAtUtc, @UpdatedAtUtc)
                ON CONFLICT(GuildId, ChannelId) DO UPDATE SET
                    NextPollAtUtc = excluded.NextPollAtUtc,
                    LastAttemptAtUtc = excluded.LastAttemptAtUtc,
                    LastSuccessAtUtc = excluded.LastSuccessAtUtc,
                    ConsecutiveFailures = excluded.ConsecutiveFailures,
                    LastFailureKind = excluded.LastFailureKind,
                    BreakerOpenUntilUtc = excluded.BreakerOpenUntilUtc,
                    LastLatencyMilliseconds = excluded.LastLatencyMilliseconds,
                    IsPaused = excluded.IsPaused,
                    PausedAtUtc = excluded.PausedAtUtc,
                    LastForcedSyncAtUtc = excluded.LastForcedSyncAtUtc,
                    LastContentHash = excluded.LastContentHash,
                    UnchangedSuccessCount = excluded.UnchangedSuccessCount,
                    EffectiveIntervalSeconds = excluded.EffectiveIntervalSeconds,
                    LastChangeAtUtc = excluded.LastChangeAtUtc,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;";
            command.Parameters.AddWithValue("@GuildId", state.GuildId);
            command.Parameters.AddWithValue("@ChannelId", state.ChannelId);
            command.Parameters.AddWithValue("@NextPollAtUtc", Format(state.NextPollAtUtc));
            command.Parameters.AddWithValue("@LastAttemptAtUtc", DbValue(state.LastAttemptAtUtc));
            command.Parameters.AddWithValue("@LastSuccessAtUtc", DbValue(state.LastSuccessAtUtc));
            command.Parameters.AddWithValue("@ConsecutiveFailures", state.ConsecutiveFailures);
            command.Parameters.AddWithValue("@LastFailureKind", state.LastFailureKind.ToString());
            command.Parameters.AddWithValue("@BreakerOpenUntilUtc", DbValue(state.BreakerOpenUntilUtc));
            command.Parameters.AddWithValue("@LastLatencyMilliseconds", state.LastLatencyMilliseconds);
            command.Parameters.AddWithValue("@IsPaused", state.IsPaused ? 1 : 0);
            command.Parameters.AddWithValue("@PausedAtUtc", DbValue(state.PausedAtUtc));
            command.Parameters.AddWithValue("@LastForcedSyncAtUtc", DbValue(state.LastForcedSyncAtUtc));
            command.Parameters.AddWithValue(
                "@LastContentHash",
                string.IsNullOrWhiteSpace(state.LastContentHash) ? DBNull.Value : state.LastContentHash);
            command.Parameters.AddWithValue("@UnchangedSuccessCount", state.UnchangedSuccessCount);
            command.Parameters.AddWithValue("@EffectiveIntervalSeconds", state.EffectiveIntervalSeconds);
            command.Parameters.AddWithValue("@LastChangeAtUtc", DbValue(state.LastChangeAtUtc));
            command.Parameters.AddWithValue("@UpdatedAtUtc", Format(_timeProvider.GetUtcNow()));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetOrigin(string baseUrl, out string origin)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            origin = uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            return true;
        }

        origin = string.Empty;
        return false;
    }

    private static string Text(DbDataReader reader, string name, string fallback = "")
        => reader[name] is DBNull ? fallback : reader[name]?.ToString() ?? fallback;

    private static string? NullText(DbDataReader reader, string name)
        => reader[name] is DBNull ? null : reader[name]?.ToString();

    private static int Integer(DbDataReader reader, string name)
        => reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);

    private static double Double(DbDataReader reader, string name)
        => reader[name] is DBNull ? 0 : Convert.ToDouble(reader[name], CultureInfo.InvariantCulture);

    private static DateTimeOffset? Timestamp(DbDataReader reader, string name)
    {
        if (reader[name] is DBNull) return null;
        return DateTimeOffset.TryParse(
            reader[name]?.ToString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value)
            ? value
            : null;
    }

    private static string Format(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static object DbValue(DateTimeOffset? value)
        => value is { } timestamp ? Format(timestamp) : DBNull.Value;
}
