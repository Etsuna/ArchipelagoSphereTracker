using System.Data.SQLite;

public class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync()
    {
        if (!File.Exists(Declare.DatabaseFile))
            SQLiteConnection.CreateFile(Declare.DatabaseFile);

        await using var conn = await Db.OpenWriteAsync();

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA foreign_keys=ON;
                PRAGMA temp_store=MEMORY;
            ";
            pragma.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
-- ==========================
-- 🎯 ChannelsAndUrlsTable
-- ==========================
CREATE TABLE IF NOT EXISTS ChannelsAndUrlsTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId      TEXT NOT NULL,
    ChannelId    TEXT NOT NULL,
    BaseUrl      TEXT NOT NULL,
    Room         TEXT NOT NULL,
    Tracker      TEXT NOT NULL,
    Silent       BOOLEAN
);

CREATE TABLE IF NOT EXISTS UrlAndChannelPatchTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChannelsAndUrlsTableId INTEGER NOT NULL,
    Alias     TEXT NOT NULL,
    GameName  TEXT,
    Patch     TEXT,
    FOREIGN KEY (ChannelsAndUrlsTableId) REFERENCES ChannelsAndUrlsTable(Id) ON DELETE CASCADE
);

-- ==========================
-- 🎯 RecapListTable
-- ==========================
CREATE TABLE IF NOT EXISTS RecapListTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    UserId    TEXT NOT NULL,
    Alias     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RecapListItemsTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RecapListTableId INTEGER NOT NULL,
    Item TEXT,
    FOREIGN KEY (RecapListTableId) REFERENCES RecapListTable(Id) ON DELETE CASCADE
);

-- ==========================
-- 🎯 ReceiverAliasesTable
-- ==========================
CREATE TABLE IF NOT EXISTS ReceiverAliasesTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Receiver  TEXT NOT NULL,
    UserId    TEXT NOT NULL,
    IsEnabled BOOLEAN
);

-- ==========================
-- 🎯 AliasChoicesTable
-- ==========================
CREATE TABLE IF NOT EXISTS AliasChoicesTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId   TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Slot      INTEGER NOT NULL,
    Alias     TEXT NOT NULL,
    Game      TEXT
);

-- ==========================
-- 🎯 DisplayedItemTable
-- ==========================
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

-- ==========================
-- 🎯 GameStatusTable
-- ==========================
CREATE TABLE IF NOT EXISTS GameStatusTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId      TEXT NOT NULL,
    ChannelId    TEXT NOT NULL,
    Hashtag      TEXT,
    Name         TEXT,
    Game         TEXT,
    Status       TEXT,
    Checks       TEXT,
    Percent      TEXT,
    LastActivity TEXT
);

-- ==========================
-- 🎯 HintStatusTable
-- ==========================
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

-- ==========================
-- 🎯 ApWorldList / Items
-- ==========================
CREATE TABLE IF NOT EXISTS ApWorldListTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ApWorldItemTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApWorldListTableId INTEGER,
    Text TEXT NOT NULL,
    Link TEXT,
    FOREIGN KEY (ApWorldListTableId) REFERENCES ApWorldListTable(Id) ON DELETE CASCADE
);

-- ==========================
-- 🎯 ProgramIdTable
-- ==========================
CREATE TABLE IF NOT EXISTS ProgramIdTable (
    ProgramId TEXT PRIMARY KEY
);

-- ==========================
-- 🎯 TelemetryTable
-- ==========================
CREATE TABLE IF NOT EXISTS TelemetryTable (
    Date TEXT PRIMARY KEY
);

-- =====================================================================
-- 🧩 Datapackage store (Items/Locations + groupes)
-- =====================================================================

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

-- ==========================
-- Index & contraintes
-- ==========================

-- Accès fréquents par guilde+channel
CREATE INDEX IF NOT EXISTS idx_channels_guild_channel
  ON ChannelsAndUrlsTable(GuildId, ChannelId);

CREATE INDEX IF NOT EXISTS idx_displayeditem_guild_channel
  ON DisplayedItemTable(GuildId, ChannelId);

CREATE INDEX IF NOT EXISTS idx_displayeditem_receiver
  ON DisplayedItemTable(GuildId, ChannelId, Receiver);

CREATE INDEX IF NOT EXISTS idx_displayeditem_finder
  ON DisplayedItemTable(GuildId, ChannelId, Finder);

CREATE INDEX IF NOT EXISTS idx_displayeditem_game_item
  ON DisplayedItemTable(Game, Item);

CREATE INDEX IF NOT EXISTS idx_receiveraliases_gcr
  ON ReceiverAliasesTable(GuildId, ChannelId, Receiver);

CREATE INDEX IF NOT EXISTS idx_receiveraliases_gcu
  ON ReceiverAliasesTable(GuildId, ChannelId, UserId);

CREATE UNIQUE INDEX IF NOT EXISTS uq_recalias
  ON RecapListTable(GuildId, ChannelId, UserId, Alias);

-- pour REPLACE sur AliasChoices
CREATE UNIQUE INDEX IF NOT EXISTS uq_aliaschoices
  ON AliasChoicesTable(GuildId, ChannelId, Alias);

-- pour REPLACE sur GameStatus
CREATE UNIQUE INDEX IF NOT EXISTS uq_gamestatus_name
  ON GameStatusTable(GuildId, ChannelId, Name);

-- pour REPLACE sur UrlAndChannelPatch
CREATE UNIQUE INDEX IF NOT EXISTS uq_url_patch
  ON UrlAndChannelPatchTable(ChannelsAndUrlsTableId, Alias);

-- Unicité d’un DisplayedItem (pour INSERT OR IGNORE)
CREATE UNIQUE INDEX IF NOT EXISTS uq_displayeditem_unique
  ON DisplayedItemTable(GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag);
";
        cmd.ExecuteNonQuery();

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
}
