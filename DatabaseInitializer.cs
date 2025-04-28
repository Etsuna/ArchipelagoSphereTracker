using System.Data.SQLite;

public class DatabaseInitializer
{
    private const string DatabaseFile = "AST.db";

    public static void InitializeDatabase()
    {
        if (!File.Exists(DatabaseFile))
        {
            SQLiteConnection.CreateFile(DatabaseFile);
        }

        using var connection = new SQLiteConnection($"Data Source={DatabaseFile};Version=3;");
        connection.Open();

        using var command = new SQLiteCommand(connection);

        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        command.CommandText = @"
-- ==========================
-- 🎯 ChannelsAndUrlsTable
-- ==========================
CREATE TABLE IF NOT EXISTS ChannelsAndUrlsTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Room TEXT,
    Tracker TEXT,
    SphereTracker TEXT,
    Silent BOOLEAN
);

CREATE TABLE IF NOT EXISTS UrlAndChannelPatchTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChannelsAndUrlsTableId INTEGER NOT NULL,
    Alias TEXT NOT NULL,
    GameName TEXT,
    Patch TEXT,
    FOREIGN KEY (ChannelsAndUrlsTableId) REFERENCES ChannelsAndUrlsTable(Id)
);

-- ==========================
-- 🎯 RecapListTable
-- ==========================
CREATE TABLE IF NOT EXISTS RecapListTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    Alias TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RecapListItemsTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RecapListTableId INTEGER NOT NULL,
    Item TEXT,
    FOREIGN KEY (RecapListTableId) REFERENCES RecapListTable(Id)
);

-- ==========================
-- 🎯 ReceiverAliasesTable
-- ==========================
CREATE TABLE IF NOT EXISTS ReceiverAliasesTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Receiver TEXT NOT NULL,
    UserId TEXT NOT NULL,
    IsEnabled BOOLEAN
);

-- ==========================
-- 🎯 AliasChoicesTable
-- ==========================
CREATE TABLE IF NOT EXISTS AliasChoicesTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Alias TEXT NOT NULL,
    Game TEXT
);

-- ==========================
-- 🎯 DisplayedItemTable
-- ==========================
CREATE TABLE IF NOT EXISTS DisplayedItemTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Sphere TEXT,
    Finder TEXT,
    Receiver TEXT,
    Item TEXT,
    Location TEXT,
    Game TEXT
);

-- ==========================
-- 🎯 GameStatusTable
-- ==========================
CREATE TABLE IF NOT EXISTS GameStatusTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Hashtag TEXT,
    Name TEXT,
    Game TEXT,
    Status TEXT,
    Checks TEXT,
    Percent TEXT,
    LastActivity TEXT
);

-- ==========================
-- 🎯 HintStatusTable
-- ==========================
CREATE TABLE IF NOT EXISTS HintStatusTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GuildId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    Finder TEXT,
    Receiver TEXT,
    Item TEXT,
    Location TEXT,
    Game TEXT,
    Entrance TEXT,
    Found TEXT
);

-- ==========================
-- 🎯 ApWorldList
-- ==========================
CREATE TABLE IF NOT EXISTS ApWorldListTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    Item TEXT NOT NULL 
);

CREATE TABLE IF NOT EXISTS ApWorldItemTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApWorldListTableId INTEGER,
    Text TEXT NOT NULL,
    Link TEXT NOT NULL,
    FOREIGN KEY (ApWorldListTableId) REFERENCES ApWorldListTable(Id)
);

-- ==========================
-- 🎯 ItemsTable
-- ==========================
CREATE TABLE IF NOT EXISTS ItemsTable (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameName TEXT NOT NULL,
    Category TEXT NOT NULL,
    ItemName TEXT NOT NULL,
    UNIQUE(GameName, Category, ItemName)
);
";
        command.ExecuteNonQuery();
    }
}
