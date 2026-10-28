// Historical migration schema only. Fresh V2 databases never create these tables.
internal static class LegacyTrackingSchema
{
    internal static readonly string[] TableNames =
        ["HintStatusTable", "DisplayedItemTable", "GameStatusTable", "AliasChoicesTable"];

    internal const string Tables = @"CREATE TABLE IF NOT EXISTS AliasChoicesTable (
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
);";

    internal const string Indexes = @"CREATE UNIQUE INDEX IF NOT EXISTS uq_aliaschoices
  ON AliasChoicesTable(GuildId, ChannelId, Alias);

CREATE UNIQUE INDEX IF NOT EXISTS uq_displayeditem_unique
  ON DisplayedItemTable(GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Flag);

CREATE INDEX IF NOT EXISTS idx_displayeditem_gci_flag
  ON DisplayedItemTable(GuildId, ChannelId, Item, Flag DESC);

CREATE INDEX IF NOT EXISTS idx_displayeditem_rcv_item
  ON DisplayedItemTable(GuildId, ChannelId, Receiver, Item);

CREATE INDEX IF NOT EXISTS idx_displayeditem_finder_item
  ON DisplayedItemTable(GuildId, ChannelId, Finder, Item);

CREATE UNIQUE INDEX IF NOT EXISTS uq_gamestatus_name
  ON GameStatusTable(GuildId, ChannelId, Name);

CREATE UNIQUE INDEX IF NOT EXISTS uq_hintstatus_unique
  ON HintStatusTable(
      GuildId,
      ChannelId,
      IFNULL(Finder, ''),
      IFNULL(Receiver, ''),
      IFNULL(Item, ''),
      IFNULL(Location, ''),
      IFNULL(Game, ''),
      IFNULL(Entrance, '')
  );

CREATE INDEX IF NOT EXISTS idx_hintstatus_gcr_item
  ON HintStatusTable(GuildId, ChannelId, Receiver, Item);

CREATE INDEX IF NOT EXISTS idx_hintstatus_gcf_item
  ON HintStatusTable(GuildId, ChannelId, Finder, Item);";
}
