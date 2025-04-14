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
    public string Room { get; set; }
    public string Tracker { get; set; }
    public string SphereTracker { get; set; }
    public string Port { get; set; }
    public Dictionary<string, UrlAndChannelPatch> Aliases { get; set; } = new Dictionary<string, UrlAndChannelPatch>();
}

public class UrlAndChannelPatch
{
    public string GameName { get; set; }
    public string Patch { get; set; }
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
    public string Alias { get; set; }
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
    public Dictionary<string, List<string>> receiverAlias = new Dictionary<string, List<string>>();
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
    public Dictionary<string, string> aliasChoices { get; set; } = new Dictionary<string, string>();
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
    public string Sphere { get; set; }
    public string Finder { get; set; }
    public string Receiver { get; set; }
    public string Item { get; set; }
    public string Location { get; set; }
    public string Game { get; set; }
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
    public string Hashtag { get; set; }
    public string Name { get; set; }
    public string Game { get; set; }
    public string Status { get; set; }
    public string Checks { get; set; }
    public string Percent { get; set; }
    public string LastActivity { get; set; }
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
    public string Finder { get; set; }
    public string Receiver { get; set; }
    public string Item { get; set; }
    public string Location { get; set; }
    public string Game { get; set; }
    public string Entrance { get; set; }
    public string Found { get; set; }
}

// ==========================
// 🎯 ApWorldList
// ==========================

public class ApWorldJsonList
{
    public string Title { get; set; }
    public List<ApWorldJsonItem> Items { get; set; }
}

public class ApWorldJsonItem
{
    public string Text { get; set; }
    public string Link { get; set; }
}