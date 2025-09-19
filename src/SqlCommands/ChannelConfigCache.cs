using ArchipelagoSphereTracker.src.Resources;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Globalization;
using static TrackingDataManager;

public static class ChannelConfigCache
{
    private static readonly ConcurrentDictionary<string, ChannelConfig> _map = new();
    private static readonly ConcurrentDictionary<string, TimeSpan> _jitter = new();
    private static readonly TimeSpan _maxJitter = TimeSpan.FromSeconds(60);

    private static TimeSpan GetJitter(string key)
        => _jitter.GetOrAdd(
            key,
            _ => TimeSpan.FromMilliseconds(
                Random.Shared.NextInt64(0, (long)_maxJitter.TotalMilliseconds)
            )
        );

    public static string Key(string guildId, string channelId) => $"{guildId}:{channelId}";

    public static bool TryGet(string guildId, string channelId, out ChannelConfig cfg)
        => _map.TryGetValue(Key(guildId, channelId), out cfg);

    public static void Upsert(string guildId, string channelId, ChannelConfig cfg)
        => _map[Key(guildId, channelId)] = cfg;

    public static void Remove(string guildId, string channelId)
        => _map.TryRemove(Key(guildId, channelId), out _);

    public static void Clear() => _map.Clear();

    /// <summary>
    /// Charge toute la table ChannelsAndUrlsTable en mémoire.
    /// </summary>
    public static async Task LoadAllAsync()
    {
        Clear();

        await using var connection = await Db.OpenReadAsync();
        using var cmd = new SQLiteCommand(@"
                SELECT GuildId, ChannelId, Tracker, BaseUrl, Room, Silent, CheckFrequency, LastCheck
                FROM ChannelsAndUrlsTable;", connection);

        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

        var minCheckFrequency = TimeSpan.FromMinutes(5);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var guildId = reader["GuildId"]?.ToString() ?? "";
            var channelId = reader["ChannelId"]?.ToString() ?? "";
            var tracker = reader["Tracker"]?.ToString() ?? Resource.NotFound;
            var baseUrl = reader["BaseUrl"]?.ToString() ?? "";
            var room = reader["Room"]?.ToString() ?? "";
            var silent = reader["Silent"] != DBNull.Value && Convert.ToBoolean(reader["Silent"]);
            var checkFrequencyS = reader["CheckFrequency"]?.ToString() ?? "5m";
            var checkFrequency = CheckFrequencyParser.ParseOrDefault(checkFrequencyS, minCheckFrequency, minCheckFrequency, null);

            DateTimeOffset? lastCheck = null;
            var lastCheckS = reader["LastCheck"] as string;
            if (!string.IsNullOrWhiteSpace(lastCheckS) &&
                DateTimeOffset.TryParse(lastCheckS, CultureInfo.InvariantCulture,
                                        DateTimeStyles.AssumeUniversal, out var dt))
            {
                lastCheck = dt;
            }

            if (!string.IsNullOrEmpty(guildId) && !string.IsNullOrEmpty(channelId))
            {
                Upsert(guildId, channelId, new ChannelConfig(
                    Tracker: tracker,
                    BaseUrl: baseUrl,
                    Room: room,
                    Silent: silent,
                    CheckFrequency: checkFrequency,
                    LastCheck: lastCheck
                ));
            }
        }
    }

    public static (bool ShouldRun, TimeSpan CheckFrequency) ShouldRunChecks(in ChannelConfig cfg)
    {
        if (cfg.LastCheck is null) return (true, cfg.CheckFrequency);
        var key = $"{cfg.Room}:{cfg.BaseUrl}:{cfg.Tracker}";
        var should = DateTimeOffset.UtcNow - cfg.LastCheck.Value + GetJitter(key) >= cfg.CheckFrequency;
        return (should, cfg.CheckFrequency);
    }
}

public readonly record struct ChannelConfig(
string Tracker,
string BaseUrl,
string Room,
bool Silent,
TimeSpan CheckFrequency,
DateTimeOffset? LastCheck
);
