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

    private static string JitterKey(in ChannelConfig cfg)
        => $"{cfg.Room}:{cfg.BaseUrl}:{cfg.Tracker}:{cfg.Port}";

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
    {
        if (_map.TryRemove(Key(guildId, channelId), out var cfg))
        {
            _jitter.TryRemove(JitterKey(cfg), out _);
        }
    }

    public static IEnumerable<string> GetAllGuildIds()
    => _map.Keys
           .Select(k => k.Split(':', 2)[0])
           .Distinct();

    public static IEnumerable<string> GetAllChannelIds()
=> _map.Keys
       .Select(k => k.Split(':', 2)[1])
        .Distinct();

    public static IEnumerable<string> GetChannelIdsForGuild(string guildId)
        => _map.Keys
            .Where(k => k.StartsWith(guildId + ":", StringComparison.Ordinal))
            .Select(k => k.Split(':', 2)[1])
            .Distinct();


    public static void Clear()
    {
        _map.Clear();
        _jitter.Clear();
    }

    /// <summary>
    /// Charge toute la table ChannelsAndUrlsTable en mémoire.
    /// </summary>
    public static async Task LoadAllAsync()
    {
        Clear();

        await using var connection = await Db.OpenReadAsync();
        using var cmd = new SQLiteCommand(@"
                SELECT GuildId, ChannelId, Tracker, BaseUrl, Room, Silent, CheckFrequency, LastCheck, Port
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
            var port = reader["Port"]?.ToString() ?? "0";
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
                    LastCheck: lastCheck,
                    Port : port
                ));
            }
        }
    }

    public static (bool ShouldRun, TimeSpan CheckFrequency) ShouldRunChecks(in ChannelConfig cfg)
    {
        if (cfg.LastCheck is null) return (true, cfg.CheckFrequency);
        var key = JitterKey(cfg);
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
DateTimeOffset? LastCheck,
string Port
);