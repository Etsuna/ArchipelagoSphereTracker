using Prometheus;
using System.Data.SQLite;
using System.Globalization;

public static class MetricsExporter
{
    public static Func<string, string?> ResolveGuildName = gid => null;
    public static Func<string, string, string?> ResolveChannelName = (gid, cid) => null;

    // ==============
    // CHANNELS
    // ==============
    private static readonly Gauge ChannelInfo =
        Metrics.CreateGauge(
            "ast_channel_info",
            "Ligne brute de ChannelsAndUrlsTable.",
            new[]
            {
                "guild_id", "guild_name",
                "channel_id", "channel_name",
                "base_url", "room", "tracker", "check_frequency", "silent", "port"
            });

    private static readonly Gauge ChannelLastCheckSeconds =
        Metrics.CreateGauge(
            "ast_channel_last_check_seconds",
            "Âge de LastCheck en secondes (NaN si nul ou invalide).",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name" });

    // ==============
    // GAME STATUS
    // ==============
    private static readonly Gauge GameStatusChecks =
        Metrics.CreateGauge(
            "ast_game_status_checks",
            "Checks du joueur dans GameStatusTable.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "name", "game" });

    private static readonly Gauge GameStatusTotal =
        Metrics.CreateGauge(
            "ast_game_status_total",
            "Total possible dans GameStatusTable.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "name", "game" });

    private static readonly Gauge GameStatusLastActivitySeconds =
        Metrics.CreateGauge(
            "ast_game_status_last_activity_seconds",
            "Âge de la dernière activité (en secondes).",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "name", "game" });

    // ==============
    // ALIAS CHOICES
    // ==============
    private static readonly Gauge AliasChoice =
        Metrics.CreateGauge(
            "ast_alias_choice",
            "Ligne de AliasChoicesTable.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "slot", "alias", "game" });

    // ==============
    // OBJETS VÉRIFIÉS
    // ==============
    private static readonly Gauge LastItemsChecked =
       Metrics.CreateGauge(
           "ast_last_items_checked_timestamp",
           "Horodatage Unix UTC (secondes) du dernier contrôle des objets.",
           new[] { "guild_id", "channel_id" });

    // ==============
    // état interne pour dépublier
    // ==============
    private static readonly object _stateLock = new();

    private static Dictionary<string, Gauge.Child> _channelInfo = new();
    private static Dictionary<string, Gauge.Child> _channelLastCheck = new();

    private static Dictionary<string, Gauge.Child> _gameStatusChecks = new();
    private static Dictionary<string, Gauge.Child> _gameStatusTotal = new();
    private static Dictionary<string, Gauge.Child> _gameStatusLastActivity = new();

    private static Dictionary<string, Gauge.Child> _aliasChoice = new();

    private static Dictionary<string, Gauge.Child> _lastItemsChecked = new();

    public static Task StartAsync(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await CollectOnce(token);
                Console.WriteLine($"[{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}] Metrics collection completed.");
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            }
        }, token);
    }

    public static async Task CollectOnce(CancellationToken ct = default)
    {
        Console.WriteLine($"[{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}] Starting metrics collection...");
        var cs =
            $"Data Source={Declare.DatabaseFile};Version=3;Pooling=True;Journal Mode=WAL;Synchronous=NORMAL;BusyTimeout=5000;";
        using var conn = new SQLiteConnection(cs);
        await conn.OpenAsync(ct);

        var curChannelInfo = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curChannelLastCheck = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curGameStatusChecks = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curGameStatusTotal = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curGameStatusLastActivity = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curAliasChoice = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curLastItemsChecked = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        // ====================
        // ChannelsAndUrlsTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, LastCheck, Silent, Port
                FROM ChannelsAndUrlsTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var baseUrl = rdr.GetString(2);
                var room = rdr.GetString(3);
                var tracker = rdr.GetString(4);
                var checkFrequency = rdr.GetString(5);
                var silentStr = rdr.IsDBNull(7) ? "null" : (rdr.GetInt32(7) != 0 ? "true" : "false");
                var portStr = rdr.GetString(8);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, baseUrl, room, tracker, checkFrequency, silentStr, portStr);
                var ch = ChannelInfo.WithLabels(guild, guildName, channel, channelName, baseUrl, room, tracker, checkFrequency, silentStr, portStr);
                ch.Set(1);
                curChannelInfo[key] = ch;

                var chLast = ChannelLastCheckSeconds.WithLabels(guild, guildName, channel, channelName);
                var dto = rdr.IsDBNull(6) ? null : ParseIsoOrUnixMs(rdr.GetValue(6));
                if (dto is null)
                {
                    chLast.Set(double.NaN);
                }
                else
                {
                    var ageSec = (DateTimeOffset.UtcNow - dto.Value).TotalSeconds;
                    if (ageSec < 0) ageSec = 0;
                    chLast.Set(ageSec);
                }

                curChannelLastCheck[guild + "|" + guildName + "|" + channel + "|" + channelName] = chLast;
            }
        }

        // ====================
        // GameStatusTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Name,''), IFNULL(Game,''), IFNULL(Checks,'0'), IFNULL(Total,'0'), LastActivity
                FROM GameStatusTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var name = rdr.GetString(2);
                var game = rdr.GetString(3);
                var checksStr = rdr.GetString(4);
                var totalStr = rdr.GetString(5);
                _ = int.TryParse(checksStr, out var checks);
                _ = int.TryParse(totalStr, out var total);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, name, game);

                var gChecks = GameStatusChecks.WithLabels(guild, guildName, channel, channelName, name, game);
                gChecks.Set(checks);
                curGameStatusChecks[key] = gChecks;

                var gTotal = GameStatusTotal.WithLabels(guild, guildName, channel, channelName, name, game);
                gTotal.Set(total);
                curGameStatusTotal[key] = gTotal;

                var gLast = GameStatusLastActivitySeconds.WithLabels(guild, guildName, channel, channelName, name, game);
                if (rdr.IsDBNull(6))
                {
                    gLast.Set(double.NaN);
                }
                else
                {
                    var lastStr = rdr.GetString(6);

                    DateTimeOffset? dto = null;
                    if (DateTimeOffset.TryParseExact(
                            lastStr,
                            "r",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var rfcDto))
                    {
                        dto = rfcDto;
                    }
                    else if (DateTimeOffset.TryParse(
                            lastStr,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var anyDto))
                    {
                        dto = anyDto;
                    }

                    if (dto is null)
                    {
                        gLast.Set(double.NaN);
                    }
                    else
                    {
                        var age = (DateTimeOffset.UtcNow - dto.Value).TotalSeconds;
                        if (age < 0) age = 0;
                        gLast.Set(age);
                    }
                }

                curGameStatusLastActivity[key] = gLast;
            }
        }

        // ====================
        // AliasChoicesTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, Slot, Alias, IFNULL(Game,'')
                FROM AliasChoicesTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var slot = rdr.GetInt64(2);
                var alias = rdr.GetString(3);
                var game = rdr.GetString(4);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var slotStr = slot.ToString(CultureInfo.InvariantCulture);
                var key = string.Join("|", guild, guildName, channel, channelName, slotStr, alias, game);
                var ch = AliasChoice.WithLabels(guild, guildName, channel, channelName, slotStr, alias, game);
                ch.Set(1);
                curAliasChoice[key] = ch;
            }
        }

        // ====================
        // LastItemsCheckTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
            SELECT GuildId, ChannelId, LastItemCheck
            FROM LastItemsCheckTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);

            while (await rdr.ReadAsync(ct))
            {
                if (rdr.IsDBNull(2))
                    continue;

                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);

                var dto = ParseIsoOrUnixMs(rdr.GetValue(2));
                if (dto is null)
                    continue;

                var tsSeconds = dto.Value.ToUnixTimeSeconds();

                var ch = LastItemsChecked.WithLabels(guild, channel);
                ch.Set(tsSeconds);

                var key = guild + "|" + channel;
                curLastItemsChecked[key] = ch;
            }
        }

        // ====================
        // nettoyage
        // ====================
        lock (_stateLock)
        {
            UnpublishMissing(_channelInfo, curChannelInfo);
            UnpublishMissing(_channelLastCheck, curChannelLastCheck);

            UnpublishMissing(_gameStatusChecks, curGameStatusChecks);
            UnpublishMissing(_gameStatusTotal, curGameStatusTotal);

            UnpublishMissing(_gameStatusLastActivity, curGameStatusLastActivity);

            UnpublishMissing(_aliasChoice, curAliasChoice);

            UnpublishMissing(_lastItemsChecked, curLastItemsChecked);

            _channelInfo = curChannelInfo;
            _channelLastCheck = curChannelLastCheck;
            _gameStatusChecks = curGameStatusChecks;
            _gameStatusTotal = curGameStatusTotal;
            _gameStatusLastActivity = curGameStatusLastActivity;
            _aliasChoice = curAliasChoice;
            _lastItemsChecked = curLastItemsChecked;
        }
    }

    private static void UnpublishMissing(Dictionary<string, Gauge.Child> oldMap, Dictionary<string, Gauge.Child> newMap)
    {
        foreach (var kv in oldMap)
        {
            if (!newMap.ContainsKey(kv.Key))
                kv.Value.Unpublish();
        }
    }

    // ==========================================================
    // FIX: parser robuste ISO 8601 ("o") + legacy unix ms (TEXT/INT)
    // ==========================================================
    private static DateTimeOffset? ParseIsoOrUnixMs(object? value)
    {
        if (value is null || value is DBNull) return null;

        if (value is long l) return DateTimeOffset.FromUnixTimeMilliseconds(l);
        if (value is int i) return DateTimeOffset.FromUnixTimeMilliseconds(i);
        if (value is double d) return DateTimeOffset.FromUnixTimeMilliseconds((long)d);
        if (value is decimal m) return DateTimeOffset.FromUnixTimeMilliseconds((long)m);

        if (value is DateTime dt)
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        if (value is string s)
        {
            s = s.Trim();

            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
                return DateTimeOffset.FromUnixTimeMilliseconds(ms);

            if (DateTimeOffset.TryParse(
                    s,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
            {
                return dto;
            }
        }

        return null;
    }
}
