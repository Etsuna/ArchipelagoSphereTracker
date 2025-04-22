/*// ==========================
// 🎯 Roles
// ==========================
public class GuildRoles
{
    public Dictionary<string, ChannelRoles> Guild { get; set; } = new();
}

public class ChannelRoles
{
    public Dictionary<string, Roles> Channel { get; set; } = new();
}

public class Roles
{
    public Dictionary<string, AliasUsers> Role { get; set; } = new();
}

public class AliasUsers
{
    public Dictionary<string, List<string>> aliasUsers { get; set; } = new Dictionary<string, List<string>>();
}*/


// ==========================
// 🎯 Channel et URL
// ==========================

public class GuildChannelsAndUrls
{
    public Dictionary<string, ChannelAndUrl> Guild { get; set; } = new Dictionary<string, ChannelAndUrl>();
}

public class ChannelAndUrl
{
    public Dictionary<string, UrlAndChannel> Channel { get; set; } = new Dictionary<string, UrlAndChannel>();
}

public class UrlAndChannel
{
    public string Room { get; set; } = string.Empty;
    public string Tracker { get; set; } = string.Empty;
    public string SphereTracker { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public Dictionary<string, UrlAndChannelPatch> Aliases { get; set; } = new Dictionary<string, UrlAndChannelPatch>();
}

public class UrlAndChannelPatch
{
    public string GameName { get; set; } = string.Empty;
    public string Patch { get; set; } = string.Empty;
}


// ==========================
// 🎯 Recap List
// ==========================
public class GuildRecapList
{
    public Dictionary<string, ChannelRecapList> Guild { get; set; } = new Dictionary<string, ChannelRecapList>();
}

public class ChannelRecapList
{
    public Dictionary<string, UserRecapList> Channel { get; set; } = new Dictionary<string, UserRecapList>();
}

public class UserRecapList
{
    public Dictionary<string, List<RecapList>> Aliases { get; set; } = new Dictionary<string, List<RecapList>>();
}

public class RecapList
{
    public string Alias { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new List<string>();
}


// ==========================
// 🎯 Receiver Aliases
// ==========================
public class GuildReceiverAliases
{
    public Dictionary<string, ChannelReceiverAliases> Guild { get; set; } = new();
}

public class ChannelReceiverAliases
{
    public Dictionary<string, ReceiverAlias> Channel { get; set; } = new();
}

public class ReceiverAlias
{
    public Dictionary<string, Dictionary<string, bool>> receiverAlias = new();
}

// ==========================
// 🎯 Alias Choices
// ==========================
public class GuildAliasChoices
{
    public Dictionary<string, ChannelAliasChoices> Guild { get; set; } = new();
}

public class ChannelAliasChoices
{
    public Dictionary<string, AliasChoice> Channel { get; set; } = new();
}

public class AliasChoice
{
    public Dictionary<string, Dictionary<string, string>> aliasChoices { get; set; } = new Dictionary<string, Dictionary<string, string>>();
}

// ==========================
// 🎯 Displayed Items
// ==========================
public class GuildDisplayedItem
{
    public Dictionary<string, ChannelDisplayedItem> Guild { get; set; } = new Dictionary<string, ChannelDisplayedItem>();
}

public class ChannelDisplayedItem
{
    public Dictionary<string, List<DisplayedItem>> Channel { get; set; } = new Dictionary<string, List<DisplayedItem>>();
}

public class DisplayedItem
{
    public string Sphere { get; set; } = string.Empty;
    public string Finder { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
}

// ==========================
// 🎯 Game Status
// ==========================
public class GuildGameStatus
{
    public Dictionary<string, ChannelGameStatus> Guild { get; set; } = new Dictionary<string, ChannelGameStatus>();
}

public class ChannelGameStatus
{
    public Dictionary<string, List<GameStatus>> Channel { get; set; } = new Dictionary<string, List<GameStatus>>();
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

// ==========================
// 🎯 Hint Status
// ==========================
public class GuildHintStatus
{
    public Dictionary<string, ChannelHintStatus> Guild { get; set; } = new Dictionary<string, ChannelHintStatus>();
}

public class ChannelHintStatus
{
    public Dictionary<string, List<HintStatus>> Channel { get; set; } = new Dictionary<string, List<HintStatus>>();
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