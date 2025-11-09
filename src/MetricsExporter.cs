using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Prometheus;

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
                "base_url", "room", "tracker", "check_frequency", "silent"
            });

    private static readonly Gauge ChannelLastCheckSeconds =
        Metrics.CreateGauge(
            "ast_channel_last_check_seconds",
            "Âge de LastCheck en secondes (NaN si nul ou invalide).",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name" });

    // ==============
    // DISPLAYED ITEMS
    // ==============
    private static readonly Gauge DisplayedItem =
        Metrics.CreateGauge(
            "ast_displayed_item",
            "Ligne de DisplayedItemTable.",
            new[]
            {
                "guild_id", "guild_name",
                "channel_id", "channel_name",
                "finder", "receiver", "item", "location", "game", "flag"
            });

    private static readonly Gauge DisplayedItemByFinder =
        Metrics.CreateGauge(
            "ast_displayed_items_by_finder",
            "Nombre d’items trouvés par finder.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "finder" });

    private static readonly Gauge DisplayedItemByReceiver =
        Metrics.CreateGauge(
            "ast_displayed_items_by_receiver",
            "Nombre d’items reçus par receiver.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "receiver" });

    private static readonly Gauge DisplayedItemTotal =
        Metrics.CreateGauge(
            "ast_displayed_items_total",
            "Nombre d’items affichés dans le channel.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name" });

    // ==============
    // HINTS
    // ==============
    private static readonly Gauge HintItem =
        Metrics.CreateGauge(
            "ast_hint_item",
            "Ligne de HintStatusTable.",
            new[]
            {
                "guild_id", "guild_name",
                "channel_id", "channel_name",
                "finder", "receiver", "item", "location", "game", "entrance", "flag"
            });

    private static readonly Gauge HintByFinder =
        Metrics.CreateGauge(
            "ast_hints_by_finder",
            "Nombre de hints faits par finder.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "finder" });

    private static readonly Gauge HintByReceiver =
        Metrics.CreateGauge(
            "ast_hints_by_receiver",
            "Nombre de hints reçus par receiver.",
            new[] { "guild_id", "guild_name", "channel_id", "channel_name", "receiver" });

    private static readonly Gauge HintTotal =
        Metrics.CreateGauge(
            "ast_hints_total",
            "Nombre de hints dans le channel.",
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
    // état interne pour dépublier
    // ==============
    private static readonly object _stateLock = new();

    private static Dictionary<string, Gauge.Child> _channelInfo = new();
    private static Dictionary<string, Gauge.Child> _channelLastCheck = new();

    private static Dictionary<string, Gauge.Child> _displayedItem = new();
    private static Dictionary<string, Gauge.Child> _displayedItemByFinder = new();
    private static Dictionary<string, Gauge.Child> _displayedItemByReceiver = new();
    private static Dictionary<string, Gauge.Child> _displayedItemTotal = new();

    private static Dictionary<string, Gauge.Child> _hintItem = new();
    private static Dictionary<string, Gauge.Child> _hintByFinder = new();
    private static Dictionary<string, Gauge.Child> _hintByReceiver = new();
    private static Dictionary<string, Gauge.Child> _hintTotal = new();

    private static Dictionary<string, Gauge.Child> _gameStatusChecks = new();
    private static Dictionary<string, Gauge.Child> _gameStatusTotal = new();
    private static Dictionary<string, Gauge.Child> _gameStatusLastActivity = new();

    private static Dictionary<string, Gauge.Child> _aliasChoice = new();

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

        var curDisplayedItem = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curDisplayedItemByFinder = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curDisplayedItemByReceiver = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curDisplayedItemTotal = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curHintItem = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curHintByFinder = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curHintByReceiver = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curHintTotal = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curGameStatusChecks = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curGameStatusTotal = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);
        var curGameStatusLastActivity = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        var curAliasChoice = new Dictionary<string, Gauge.Child>(StringComparer.Ordinal);

        // ====================
        // ChannelsAndUrlsTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, BaseUrl, Room, Tracker, CheckFrequency, LastCheck, Silent
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

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, baseUrl, room, tracker, checkFrequency, silentStr);
                var ch = ChannelInfo.WithLabels(guild, guildName, channel, channelName, baseUrl, room, tracker, checkFrequency, silentStr);
                ch.Set(1);
                curChannelInfo[key] = ch;

                var chLast = ChannelLastCheckSeconds.WithLabels(guild, guildName, channel, channelName);
                if (rdr.IsDBNull(6))
                {
                    chLast.Set(double.NaN);
                }
                else
                {
                    var lastStr = rdr.GetString(6);
                    if (DateTime.TryParse(lastStr, out var dt))
                    {
                        var ageSec = (DateTime.UtcNow - dt.ToUniversalTime()).TotalSeconds;
                        if (ageSec < 0) ageSec = 0;
                        chLast.Set(ageSec);
                    }
                    else
                    {
                        chLast.Set(double.NaN);
                    }
                }
                curChannelLastCheck[guild + "|" + guildName + "|" + channel + "|" + channelName] = chLast;
            }
        }

        // ====================
        // DisplayedItemTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Finder,''), IFNULL(Receiver,''), IFNULL(Item,''), IFNULL(Location,''), IFNULL(Game,''), IFNULL(Flag,'')
                FROM DisplayedItemTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var finder = rdr.GetString(2);
                var receiver = rdr.GetString(3);
                var item = rdr.GetString(4);
                var location = rdr.GetString(5);
                var game = rdr.GetString(6);
                var flag = rdr.GetString(7);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, finder, receiver, item, location, game, flag);
                var ch = DisplayedItem.WithLabels(guild, guildName, channel, channelName, finder, receiver, item, location, game, flag);
                ch.Set(1);
                curDisplayedItem[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Finder,''), COUNT(*) 
                FROM DisplayedItemTable
                GROUP BY GuildId, ChannelId, IFNULL(Finder,'');";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var finder = rdr.GetString(2);
                var cnt = rdr.GetInt64(3);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, finder);
                var ch = DisplayedItemByFinder.WithLabels(guild, guildName, channel, channelName, finder);
                ch.Set(cnt);
                curDisplayedItemByFinder[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Receiver,''), COUNT(*) 
                FROM DisplayedItemTable
                GROUP BY GuildId, ChannelId, IFNULL(Receiver,'');";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var receiver = rdr.GetString(2);
                var cnt = rdr.GetInt64(3);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, receiver);
                var ch = DisplayedItemByReceiver.WithLabels(guild, guildName, channel, channelName, receiver);
                ch.Set(cnt);
                curDisplayedItemByReceiver[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, COUNT(*)
                FROM DisplayedItemTable
                GROUP BY GuildId, ChannelId;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var cnt = rdr.GetInt64(2);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName);
                var ch = DisplayedItemTotal.WithLabels(guild, guildName, channel, channelName);
                ch.Set(cnt);
                curDisplayedItemTotal[key] = ch;
            }
        }

        // ====================
        // HintStatusTable
        // ====================
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Finder,''), IFNULL(Receiver,''), IFNULL(Item,''), IFNULL(Location,''), IFNULL(Game,''), IFNULL(Entrance,''), IFNULL(Flag,'')
                FROM HintStatusTable;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var finder = rdr.GetString(2);
                var receiver = rdr.GetString(3);
                var item = rdr.GetString(4);
                var location = rdr.GetString(5);
                var game = rdr.GetString(6);
                var entrance = rdr.GetString(7);
                var flag = rdr.GetString(8);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, finder, receiver, item, location, game, entrance, flag);
                var ch = HintItem.WithLabels(guild, guildName, channel, channelName, finder, receiver, item, location, game, entrance, flag);
                ch.Set(1);
                curHintItem[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Finder,''), COUNT(*)
                FROM HintStatusTable
                GROUP BY GuildId, ChannelId, IFNULL(Finder,'');";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var finder = rdr.GetString(2);
                var cnt = rdr.GetInt64(3);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, finder);
                var ch = HintByFinder.WithLabels(guild, guildName, channel, channelName, finder);
                ch.Set(cnt);
                curHintByFinder[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, IFNULL(Receiver,''), COUNT(*)
                FROM HintStatusTable
                GROUP BY GuildId, ChannelId, IFNULL(Receiver,'');";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var receiver = rdr.GetString(2);
                var cnt = rdr.GetInt64(3);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName, receiver);
                var ch = HintByReceiver.WithLabels(guild, guildName, channel, channelName, receiver);
                ch.Set(cnt);
                curHintByReceiver[key] = ch;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT GuildId, ChannelId, COUNT(*)
                FROM HintStatusTable
                GROUP BY GuildId, ChannelId;";
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var guild = rdr.GetString(0);
                var channel = rdr.GetString(1);
                var cnt = rdr.GetInt64(2);

                var guildName = ResolveGuildName(guild) ?? "unknown";
                var channelName = ResolveChannelName(guild, channel) ?? "unknown";

                var key = string.Join("|", guild, guildName, channel, channelName);
                var ch = HintTotal.WithLabels(guild, guildName, channel, channelName);
                ch.Set(cnt);
                curHintTotal[key] = ch;
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
                if (!rdr.IsDBNull(6))
                {
                    var last = rdr.GetString(6);
                    if (DateTime.TryParse(last, out var dt))
                    {
                        var age = (DateTime.UtcNow - dt.ToUniversalTime()).TotalSeconds;
                        if (age < 0) age = 0;
                        gLast.Set(age);
                    }
                    else
                    {
                        gLast.Set(double.NaN);
                    }
                }
                else
                {
                    gLast.Set(double.NaN);
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
        // nettoyage
        // ====================
        lock (_stateLock)
        {
            UnpublishMissing(_channelInfo, curChannelInfo);
            UnpublishMissing(_channelLastCheck, curChannelLastCheck);

            UnpublishMissing(_displayedItem, curDisplayedItem);
            UnpublishMissing(_displayedItemByFinder, curDisplayedItemByFinder);
            UnpublishMissing(_displayedItemByReceiver, curDisplayedItemByReceiver);
            UnpublishMissing(_displayedItemTotal, curDisplayedItemTotal);

            UnpublishMissing(_hintItem, curHintItem);
            UnpublishMissing(_hintByFinder, curHintByFinder);
            UnpublishMissing(_hintByReceiver, curHintByReceiver);
            UnpublishMissing(_hintTotal, curHintTotal);

            UnpublishMissing(_gameStatusChecks, curGameStatusChecks);
            UnpublishMissing(_gameStatusTotal, curGameStatusTotal);
            UnpublishMissing(_gameStatusLastActivity, curGameStatusLastActivity);

            UnpublishMissing(_aliasChoice, curAliasChoice);

            _channelInfo = curChannelInfo;
            _channelLastCheck = curChannelLastCheck;
            _displayedItem = curDisplayedItem;
            _displayedItemByFinder = curDisplayedItemByFinder;
            _displayedItemByReceiver = curDisplayedItemByReceiver;
            _displayedItemTotal = curDisplayedItemTotal;
            _hintItem = curHintItem;
            _hintByFinder = curHintByFinder;
            _hintByReceiver = curHintByReceiver;
            _hintTotal = curHintTotal;
            _gameStatusChecks = curGameStatusChecks;
            _gameStatusTotal = curGameStatusTotal;
            _gameStatusLastActivity = curGameStatusLastActivity;
            _aliasChoice = curAliasChoice;
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
}
