using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using System.Data.SQLite;
using System.Net.Http;
using System.Threading;

public static class DBMigration
{
    public static async Task Migrate_4_to_5Async(CancellationToken ct = default)
    {
        var guildList = await GetAllGuildChannelMappingsAsync();
        await Task.Delay(1000, ct);
        var OldDisplayedItems = await GetAllDisplayedItemsAsync();
        await Task.Delay(1000, ct);

        await RunSchemaUpgradeAsync();
        await Task.Delay(1000, ct);

        foreach (var guild in guildList)
        {
            ct.ThrowIfCancellationRequested();

            Console.WriteLine($"Migrate Guild: {guild.GuildId}, Channel: {guild.ChannelId}, Room: {guild.Room}");

            string guildId = guild.GuildId;
            string channelId = guild.ChannelId;
            string newUrl = guild.Room;
            var silent = guild.Silent;

            string baseUrl = string.Empty;
            string? tracker = string.Empty;
            string? room = string.Empty;
            string port = string.Empty;

            var checkFrequencyStr = "5m";

            var uri = new Uri(newUrl);
            baseUrl = $"{uri.Scheme}://{uri.Authority}";
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            room = segments.Length > 1 ? segments[^1] : "";

            bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

            var roomInfo = await UrlClass.RoomInfo(baseUrl, room);
            if (roomInfo == null)
            {
                Console.WriteLine("Room Not Found");
                var message = string.Format(Resource.RoomNotFound);
                await UrlClass.DeleteChannelAndUrl(guildId, channelId);
                await BotCommands.SendMessageAsync(message, channelId);
                continue;
            }

            tracker = roomInfo.Tracker ?? tracker;
            port = !string.IsNullOrEmpty(roomInfo.LastPort.ToString()) ? roomInfo.LastPort.ToString() : port;

            async Task<bool> CanAddUrlAsync(string gId, string cId)
            {
                var exists = await DatabaseCommands.CheckIfChannelExistsAsync(gId, cId, "ChannelsAndUrlsTable");
                return !exists;
            }

            async Task<(bool isValid, string message)> IsAllUrlIsValidAsync()
            {
                if (!await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId))
                    return (false, Resource.UrlCheckMaxTread);

                var playersCount = roomInfo.Players.Count;
                if (playersCount > Declare.MaxPlayer)
                    return (false, string.Format(Resource.CheckPlayerMinMax, Declare.MaxPlayer));

                return (true, string.Empty);
            }

            if (await CanAddUrlAsync(guildId, channelId))
            {
                if (string.IsNullOrEmpty(newUrl))
                {
                    Console.WriteLine(Resource.URLEmpty);
                }
                else if (!IsValidUrl(newUrl))
                {
                    Console.WriteLine(Resource.URLNotValid);
                }
                else
                {
                    var (isValid, errorMessage) = await IsAllUrlIsValidAsync();
                    if (!isValid)
                    {
                        Console.WriteLine(errorMessage);
                    }
                    else
                    {
                        var patchLinkList = new List<Patch>();
                        var aliasList = new List<(int slot, string alias, string game)>();
                        var aliasSlot = 1;

                        foreach (var player in roomInfo.Players)
                        {
                            ct.ThrowIfCancellationRequested();
                            aliasList.Add((aliasSlot, player.Name, player.Game));
                            aliasSlot++;
                        }

                        foreach (var download in roomInfo.Downloads)
                        {
                            ct.ThrowIfCancellationRequested();
                            aliasList.Where(x => x.slot == download.Slot).ToList().ForEach(slot =>
                            {
                                var patchLink = new Patch
                                {
                                    GameAlias = slot.alias,
                                    GameName = slot.game,
                                    PatchLink = baseUrl + download.Download,
                                };
                                patchLinkList.Add(patchLink);
                                Console.WriteLine(string.Format(Resource.UrlGamePatch, patchLink.GameAlias, patchLink.PatchLink));
                            });
                        }

                        var rootTracker = await TrackerDatapackageFetcher.getRoots(baseUrl, tracker, TrackingDataManager.Http);
                        var checksums = TrackerDatapackageFetcher.GetDatapackageChecksums(rootTracker);
                        await TrackerDatapackageFetcher.SeedDatapackagesFromTrackerAsync(baseUrl, guildId, channelId, rootTracker);

                        if (!string.IsNullOrEmpty(tracker))
                        {
                            Declare.AddedChannelId.Add(channelId);

                            await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, baseUrl, room, tracker, silent, checkFrequencyStr);
                            await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                            await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guildId, channelId, aliasList);

                            await MigrateTableData(guildId, channelId, silent, baseUrl, tracker, OldDisplayedItems, ct);
                            await Telemetry.SendDailyTelemetryAsync(Declare.ProgramID, false);

                            await BotCommands.SendMessageAsync(Resource.BDDUpdated, channelId);

                            Declare.AddedChannelId.Remove(channelId);
                        }

                        Console.WriteLine(string.Format(Resource.URLSet, newUrl));
                    }
                }
            }
            else
            {
                Console.WriteLine(Resource.URLAlreadySet);
            }
        }
    }

    private static readonly HttpClient _http = new HttpClient();

    public static async Task MigrateTableData(string guild, string channel, bool silent, string baseUrl, string tracker, List<DisplayedItem_Old> oldDisplayedItems, CancellationToken ct = default)
    {
        var ctx = await ProcessingContextLoader.LoadOneShotAsync(guild, channel, silent).ConfigureAwait(false);

        var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{tracker}";
        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);

        var newDisplayItems = TrackerStreamParser.ParseItems(ctx, json);

        var cleaned = DisplayedItemCleaner.KeepOnlyCommon(newDisplayItems, oldDisplayedItems);

        await DisplayItemCommands.AddItemsAsync(cleaned, guild, channel);
    }

    public static class DisplayedItemCleaner
    {
        private static string N(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        private static string BuildKey(
            string guildId, string channelId,
            string? finder, string? receiver, string? item,
            string? location, string? game)
        {
            return string.Join("|",
                N(guildId), N(channelId), N(finder), N(receiver), N(item), N(location), N(game));
        }

        /// <summary>
        /// Supprime de la liste DisplayedItem tous les éléments
        /// qui n'ont pas d'équivalent dans DisplayedItem_Old.
        /// </summary>
        public static List<DisplayedItem> KeepOnlyCommon(
            IEnumerable<DisplayedItem> currentItems,
            IEnumerable<DisplayedItem_Old> oldItems)
        {
            var oldKeys = new HashSet<string>(
                oldItems.Select(o => BuildKey(o.GuildId, o.ChannelId, o.Finder, o.Receiver, o.Item, o.Location, o.Game))
            );

            var filtered = currentItems
                .Where(n => oldKeys.Contains(
                    BuildKey(n.GuildId, n.ChannelId, n.Finder, n.Receiver, n.Item, n.Location, n.Game)))
                .ToList();

            return filtered;
        }
    }

    public class GuildChannelMapping
    {
        public string GuildId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public bool Silent { get; set; }

    }

    public class DisplayedItem_Old
    {
        public int Id { get; set; }
        public string GuildId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string? Sphere { get; set; }
        public string? Finder { get; set; }
        public string? Receiver { get; set; }
        public string? Item { get; set; }
        public string? Location { get; set; }
        public string? Game { get; set; }
    }

    public static async Task<string> GetCurrentDbVersionAsync()
    {
        await using var conn = await Db.OpenReadAsync();

        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='BddVersion';";
            var exists = (await checkCmd.ExecuteScalarAsync())?.ToString();
            if (string.IsNullOrEmpty(exists))
                return "-1";
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Version FROM BddVersion WHERE Id = 1;";
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "0";
        }
    }

    public static async Task SetDbVersionAsync(string version)
    {
        await using var conn = await Db.OpenWriteAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS BddVersion (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            Version TEXT NOT NULL
        );

        INSERT INTO BddVersion (Id, Version)
        VALUES (1, @Version)
        ON CONFLICT(Id) DO UPDATE SET Version = @Version;";
        cmd.Parameters.AddWithValue("@Version", version);

        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<GuildChannelMapping>> GetAllGuildChannelMappingsAsync()
    {
        var list = new List<GuildChannelMapping>();
        await using var conn = await Db.OpenReadAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT GuildId, ChannelId, Room, Silent
        FROM ChannelsAndUrlsTable
        ORDER BY GuildId, ChannelId;";
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new GuildChannelMapping
            {
                GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                Room = reader["Room"]?.ToString() ?? string.Empty,
                Silent = reader["Silent"] != DBNull.Value && Convert.ToBoolean(reader["Silent"])
            });
        }
        return list;
    }

    public static async Task<List<DisplayedItem_Old>> GetAllDisplayedItemsAsync()
    {
        var list = new List<DisplayedItem_Old>();
        await using var conn = await Db.OpenReadAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT Id, GuildId, ChannelId, Sphere, Finder, Receiver, Item, Location, Game
        FROM DisplayedItemTable
        ORDER BY GuildId, ChannelId;";
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new DisplayedItem_Old
            {
                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                GuildId = reader["GuildId"]?.ToString() ?? string.Empty,
                ChannelId = reader["ChannelId"]?.ToString() ?? string.Empty,
                Sphere = reader["Sphere"]?.ToString(),
                Finder = reader["Finder"]?.ToString(),
                Receiver = reader["Receiver"]?.ToString(),
                Item = reader["Item"]?.ToString(),
                Location = reader["Location"]?.ToString(),
                Game = reader["Game"]?.ToString()
            });
        }
        return list;
    }

    public static async Task RunSchemaUpgradeAsync()
    {
        await Db.WriteAsync(async conn =>
        {
            using (var pragmaOff = conn.CreateCommand())
            {
                pragmaOff.CommandText = "PRAGMA foreign_keys = OFF;";
                pragmaOff.ExecuteNonQuery();
            }

            try
            {
                await TryRenameAsync(conn, "ChannelsAndUrlsTable", "ChannelsAndUrlsTable_legacy");
                await TryRenameAsync(conn, "UrlAndChannelPatchTable", "UrlAndChannelPatchTable_legacy");
                await TryRenameAsync(conn, "AliasChoicesTable", "AliasChoicesTable_legacy");
                await TryRenameAsync(conn, "DisplayedItemTable", "DisplayedItemTable_legacy");
                await TryRenameAsync(conn, "GameStatusTable", "GameStatusTable_legacy");
                await TryRenameAsync(conn, "HintStatusTable", "HintStatusTable_legacy");

                await Exec(conn, @"
CREATE TABLE IF NOT EXISTS ChannelsAndUrlsTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId        TEXT NOT NULL,
    ChannelId      TEXT NOT NULL,
    BaseUrl        TEXT NOT NULL,
    Room           TEXT NOT NULL,
    Tracker        TEXT NOT NULL,
    CheckFrequency TEXT NOT NULL,
    LastCheck      TEXT,
    Silent         BOOLEAN
);


CREATE TABLE IF NOT EXISTS UrlAndChannelPatchTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChannelsAndUrlsTableId INTEGER NOT NULL,
    Alias     TEXT NOT NULL,
    GameName  TEXT,
    Patch     TEXT,
    FOREIGN KEY (ChannelsAndUrlsTableId) REFERENCES ChannelsAndUrlsTable(Id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS AliasChoicesTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Slot      INTEGER NOT NULL,
    Alias     TEXT NOT NULL,
    Game      TEXT
);

CREATE TABLE IF NOT EXISTS DisplayedItemTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Finder    TEXT,
    Receiver  TEXT,
    Item      TEXT,
    Location  TEXT,
    Game      TEXT,
    Flag      TEXT
);

CREATE TABLE IF NOT EXISTS GameStatusTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId      TEXT NOT NULL,
    ChannelId    TEXT NOT NULL,
    Name         TEXT,
    Game         TEXT,
    Checks       TEXT,
    Total        TEXT,
    LastActivity TEXT
);

CREATE TABLE IF NOT EXISTS HintStatusTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Finder    TEXT,
    Receiver  TEXT,
    Item      TEXT,
    Location  TEXT,
    Game      TEXT,
    Entrance  TEXT,
    Flag     TEXT
);

CREATE TABLE IF NOT EXISTS DatapackageItems(
    GuildId    TEXT NOT NULL,
    ChannelId  TEXT NOT NULL,
    DatasetKey TEXT NOT NULL,
    Id         INTEGER NOT NULL,
    Name       TEXT NOT NULL,
    PRIMARY KEY (GuildId, ChannelId, DatasetKey, Id),
    UNIQUE (GuildId, ChannelId, DatasetKey, Name)
);
CREATE INDEX IF NOT EXISTS IX_DatapackageItems_GCD_Name 
    ON DatapackageItems(GuildId, ChannelId, DatasetKey, Name);

CREATE TABLE IF NOT EXISTS DatapackageItemGroups(
    GuildId    TEXT NOT NULL,
    ChannelId  TEXT NOT NULL,
    DatasetKey TEXT NOT NULL,
    GroupName  TEXT NOT NULL,
    ItemId     INTEGER NOT NULL,
    PRIMARY KEY (GuildId, ChannelId, DatasetKey, GroupName, ItemId),
    FOREIGN KEY (GuildId, ChannelId, DatasetKey, ItemId)
        REFERENCES DatapackageItems(GuildId, ChannelId, DatasetKey, Id)
        ON DELETE CASCADE
        DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE IF NOT EXISTS DatapackageLocations(
    GuildId    TEXT NOT NULL,
    ChannelId  TEXT NOT NULL,
    DatasetKey TEXT NOT NULL,
    Id         INTEGER NOT NULL,
    Name       TEXT NOT NULL,
    PRIMARY KEY (GuildId, ChannelId, DatasetKey, Id),
    UNIQUE (GuildId, ChannelId, DatasetKey, Name)
);
CREATE INDEX IF NOT EXISTS IX_DatapackageLocations_GCD_Name 
    ON DatapackageLocations(GuildId, ChannelId, DatasetKey, Name);

CREATE TABLE IF NOT EXISTS DatapackageLocationGroups(
    GuildId    TEXT NOT NULL,
    ChannelId  TEXT NOT NULL,
    DatasetKey TEXT NOT NULL,
    GroupName  TEXT NOT NULL,
    LocationId INTEGER NOT NULL,
    PRIMARY KEY (GuildId, ChannelId, DatasetKey, GroupName, LocationId),
    FOREIGN KEY (GuildId, ChannelId, DatasetKey, LocationId)
        REFERENCES DatapackageLocations(GuildId, ChannelId, DatasetKey, Id)
        ON DELETE CASCADE
        DEFERRABLE INITIALLY DEFERRED
);

-- Associer un jeu (par salon) au datapackage (checksum/datasetKey)
CREATE TABLE IF NOT EXISTS DatapackageGameMap(
    GuildId    TEXT NOT NULL,
    ChannelId  TEXT NOT NULL,
    GameName   TEXT NOT NULL,
    DatasetKey TEXT NOT NULL,
    ImportedAt TEXT NOT NULL,
    PRIMARY KEY (GuildId, ChannelId, GameName)
);

CREATE INDEX IF NOT EXISTS IX_DatapackageGameMap_GC_Game
  ON DatapackageGameMap(GuildId, ChannelId, GameName);

-- Unicité d’un DisplayedItem (pour INSERT OR IGNORE)
CREATE UNIQUE INDEX IF NOT EXISTS uq_displayeditem_unique
  ON DisplayedItemTable(GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag);
");

                using (var pragmaOn = conn.CreateCommand())
                {
                    pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
                    pragmaOn.ExecuteNonQuery();
                }
            }
            catch
            {
                using (var pragmaOn = conn.CreateCommand())
                {
                    pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
                    pragmaOn.ExecuteNonQuery();
                }
                throw;
            }
        });

        await PostMigrationMaintenanceAsync();
    }

    private static async Task PostMigrationMaintenanceAsync()
    {
        await using var conn = await Db.OpenWriteAsync();

        using (var optimize = conn.CreateCommand())
        {
            optimize.CommandText = "PRAGMA optimize;";
            optimize.ExecuteNonQuery();
        }
        using (var analyze = conn.CreateCommand())
        {
            analyze.CommandText = "ANALYZE;";
            analyze.ExecuteNonQuery();
        }
        using (var vacuum = conn.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();
        }
    }

    private static async Task<bool> TableExistsAsync(SQLiteConnection conn, string name)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@n;";
        check.Parameters.AddWithValue("@n", name);
        return await check.ExecuteScalarAsync().ConfigureAwait(false) != null;
    }
    private static async Task TryRenameAsync(SQLiteConnection conn, string from, string to)
    {
        if (await TableExistsAsync(conn, from).ConfigureAwait(false))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE \"{from}\" RENAME TO \"{to}\";";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
    private static async Task Exec(SQLiteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public static async Task DropLegacyTablesAsync()
    {
        await Db.WriteAsync(async conn =>
        {
            string[] legacyTables =
            {
                "ChannelsAndUrlsTable_legacy",
                "UrlAndChannelPatchTable_legacy",
                "AliasChoicesTable_legacy",
                "DisplayedItemTable_legacy",
                "GameStatusTable_legacy",
                "HintStatusTable_legacy"
            };

            foreach (var table in legacyTables)
            {
                if (await TableExistsAsync(conn, table))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DROP TABLE IF EXISTS \"{table}\";";
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        });
    }
}
