using Prometheus;
using System.Data.SQLite;
using System.Diagnostics;
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
            "Configuration non sensible d'un canal suivi par AST.",
            new[]
            {
                "guild_id", "guild_name",
                "channel_id", "channel_name",
                "check_frequency", "silent"
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
           new[] { "guild_id", "guild_name", "channel_id", "channel_name" });

    // Low-cardinality operational overview.
    private static readonly Gauge BuildInfo = Metrics.CreateGauge(
        "ast_info",
        "Static AST build and operating-mode information.",
        new[] { "version", "operating_mode" });

    private static readonly Gauge DiscordConnected = Metrics.CreateGauge(
        "ast_discord_connected",
        "Whether the Discord gateway is currently connected (1 or 0).");

    private static readonly Gauge DiscordGuilds = Metrics.CreateGauge(
        "ast_discord_guilds",
        "Number of Discord guilds currently visible to the bot.");

    private static readonly Gauge TrackedGuilds = Metrics.CreateGauge(
        "ast_tracked_guilds",
        "Number of guilds with at least one tracked room.");

    private static readonly Gauge TrackedRooms = Metrics.CreateGauge(
        "ast_tracked_rooms",
        "Number of rooms currently tracked by AST.");

    private static readonly Gauge TrackedSlots = Metrics.CreateGauge(
        "ast_tracked_slots",
        "Number of slot status rows currently tracked by AST.");

    private static readonly Gauge RoomPollStates = Metrics.CreateGauge(
        "ast_room_poll_states",
        "Rooms by bounded polling state.",
        new[] { "state" });

    private static readonly Gauge EventDeliveries = Metrics.CreateGauge(
        "ast_event_deliveries",
        "Tracking event deliveries by bounded state.",
        new[] { "state" });

    private static readonly Gauge PortalTokens = Metrics.CreateGauge(
        "ast_portal_tokens",
        "Portal tokens by bounded lifecycle state.",
        new[] { "state" });

    private static readonly Gauge ArchipelagoRestrictions = Metrics.CreateGauge(
        "ast_archipelago_access_restrictions",
        "Number of guild-scoped Archipelago access restrictions.");

    private static readonly Gauge DelegatedGuildManagers = Metrics.CreateGauge(
        "ast_delegated_guild_managers",
        "Number of delegated AST guild-manager bindings.");

    private static readonly Gauge SecurityAuditEvents = Metrics.CreateGauge(
        "ast_security_audit_events",
        "Retained security audit entries by bounded outcome.",
        new[] { "outcome" });

    private static readonly Gauge MetricsCollectionSuccess = Metrics.CreateGauge(
        "ast_metrics_collection_success",
        "Whether the latest database metrics collection succeeded (1 or 0).");

    private static readonly Gauge MetricsCollectionLastSuccess = Metrics.CreateGauge(
        "ast_metrics_collection_last_success_timestamp_seconds",
        "Unix timestamp of the latest successful database metrics collection.");

    private static readonly Histogram MetricsCollectionDuration = Metrics.CreateHistogram(
        "ast_metrics_collection_duration_seconds",
        "Database metrics collection duration.",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.01, 2, 14) });

    private static readonly Counter MetricsCollectionFailures = Metrics.CreateCounter(
        "ast_metrics_collection_failures_total",
        "Number of failed database metrics collections.");

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
                var startedAt = Stopwatch.GetTimestamp();
                try
                {
                    await CollectOnce(token);
                    MetricsCollectionSuccess.Set(1);
                    MetricsCollectionLastSuccess.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    Console.WriteLine($"[{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}] Metrics collection completed.");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    MetricsCollectionSuccess.Set(0);
                    MetricsCollectionFailures.Inc();
                    Console.WriteLine($"[Metrics] Collection failed: {exception.GetType().Name}");
                }
                finally
                {
                    MetricsCollectionDuration.Observe(Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
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
        var trackedGuildIds = new HashSet<string>(StringComparer.Ordinal);
        var trackedRoomCount = 0;
        var trackedSlotCount = 0;

        BuildInfo.WithLabels(
            string.IsNullOrWhiteSpace(Declare.BotVersion) ? "unknown" : Declare.BotVersion,
            Declare.IsArchipelagoMode ? "archipelago" : "normal").Set(1);
        DiscordGuilds.Set(Declare.Client?.Guilds.Count ?? 0);

        // ====================
        // ChannelsAndUrlsTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, CheckFrequency, LastCheck, Silent
                FROM ChannelsAndUrlsTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                trackedRoomCount++;
                trackedGuildIds.Add(guild);
                var checkFrequency = rdr.GetString(2);
                var silentStr = rdr.IsDBNull(4) ? "null" : (rdr.GetInt32(4) != 0 ? "true" : "false");

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, checkFrequency, silentStr);
                var ch = ChannelInfo.WithLabels(guild, guildName, channel, channelName, checkFrequency, silentStr);
                ch.Set(1);
                curChannelInfo[key] = ch;

                var chLast = ChannelLastCheckSeconds.WithLabels(guild, guildName, channel, channelName);
                var dto = rdr.IsDBNull(3) ? null : ParseIsoOrUnixMs(rdr.GetValue(3));
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
                trackedSlotCount++;
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

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var dto = ParseIsoOrUnixMs(rdr.GetValue(2));
                if (dto is null)
                    continue;

                var tsSeconds = dto.Value.ToUnixTimeSeconds();

                var ch = LastItemsChecked.WithLabels(guild, guildName, channel, channelName);
                ch.Set(tsSeconds);

                var key = string.Join("|", guild, guildName, channel, channelName);
                curLastItemsChecked[key] = ch;
            }
        }

        TrackedRooms.Set(trackedRoomCount);
        TrackedGuilds.Set(trackedGuildIds.Count);
        TrackedSlots.Set(trackedSlotCount);

        await SetGroupedGaugeAsync(
            conn,
            "SELECT CASE WHEN IsPaused = 1 THEN 'paused' ELSE 'active' END, COUNT(*) FROM RoomPollState GROUP BY IsPaused;",
            RoomPollStates,
            ["active", "paused", "other"],
            ct).ConfigureAwait(false);
        await SetGroupedGaugeAsync(
            conn,
            "SELECT Status, COUNT(*) FROM EventDeliveries GROUP BY Status;",
            EventDeliveries,
            ["pending", "delivering", "delivered", "failed", "other"],
            ct).ConfigureAwait(false);
        await SetGroupedGaugeAsync(
            conn,
            @"SELECT CASE
                    WHEN RevokedAtUtc IS NOT NULL THEN 'revoked'
                    WHEN ExpiresAtUtc <= @Now THEN 'expired'
                    ELSE 'active'
                END, COUNT(*)
                FROM PortalAccessTable
                GROUP BY 1;",
            PortalTokens,
            ["active", "revoked", "expired", "other"],
            ct,
            command => command.Parameters.AddWithValue(
                "@Now", PortalAccessCommands.FormatTimestamp(DateTimeOffset.UtcNow))).ConfigureAwait(false);
        await SetGroupedGaugeAsync(
            conn,
            "SELECT Outcome, COUNT(*) FROM SecurityAuditLogTable GROUP BY Outcome;",
            SecurityAuditEvents,
            ["started", "succeeded", "denied", "failed", "other"],
            ct).ConfigureAwait(false);
        ArchipelagoRestrictions.Set(await ReadCountAsync(
            conn, "SELECT COUNT(*) FROM AstArchipelagoAccessDenyTable;", ct).ConfigureAwait(false));
        DelegatedGuildManagers.Set(await ReadCountAsync(
            conn, "SELECT COUNT(*) FROM AstRoleBindingsTable WHERE Role = 'GuildManager';", ct).ConfigureAwait(false));

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

    public static void SetDiscordConnected(bool connected)
        => DiscordConnected.Set(connected ? 1 : 0);

    private static async Task<long> ReadCountAsync(
        SQLiteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task SetGroupedGaugeAsync(
        SQLiteConnection connection,
        string sql,
        Gauge gauge,
        IReadOnlyCollection<string> knownStates,
        CancellationToken cancellationToken,
        Action<SQLiteCommand>? configure = null)
    {
        var counts = knownStates.ToDictionary(state => state, _ => 0d, StringComparer.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var state = reader.IsDBNull(0) ? "other" : reader.GetString(0).Trim().ToLowerInvariant();
            if (!knownStates.Contains(state))
                state = "other";
            counts[state] += Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        foreach (var (state, count) in counts)
            gauge.WithLabels(state).Set(count);
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
