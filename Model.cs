public class Patch
{
    public string GameAlias { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string PatchLink { get; set; } = string.Empty;

}

public class DisplayedItem
{
    public string GuildId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Sphere { get; set; } = string.Empty;
    public string Finder { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
}

public class GameStatus
{
    public string Hashtag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Checks { get; set; } = string.Empty;
    public string Percent { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
}

public class HintStatus
{
    public string Finder { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Entrance { get; set; } = string.Empty;
    public string Found { get; set; } = string.Empty;
}

// ==========================
// 🎯 ApWorldList
// ==========================

public class ApWorldJsonList
{
    public string Title { get; set; } = string.Empty;
    public List<ApWorldJsonItem> Items { get; set; } = new List<ApWorldJsonItem>();
}

public class ApWorldJsonItem
{
    public string Text { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
}

// ==========================
// 🎯 Items Table
// ==========================
public class ItemsTableList
{
    public Dictionary<string, ItemsTable> GameData { get; set; } = new Dictionary<string, ItemsTable>();
    public List<string>? progression { get; set; }
    public List<string>? useful { get; set; }
    public List<string>? filler { get; set; }
    public List<string>? trap { get; set; }
    public List<string>? progression_skip_balancing { get; set; }
}

public class ItemsTable : Dictionary<string, ItemsTableList> { }