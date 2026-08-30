public class Patch
{
    public string GameAlias { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string PatchLink { get; set; } = string.Empty;

}

public class DisplayedItem
{
    public int FinderSlot { get; set; }
    public int ReceiverSlot { get; set; }
    public long ItemId { get; set; }
    public long LocationId { get; set; }
    public string GuildId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Finder { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
}

public class GameStatus
{
    public int Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Checks { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
}

public class HintStatus
{
    public int FinderSlot { get; set; }
    public int ReceiverSlot { get; set; }
    public long ItemId { get; set; }
    public long LocationId { get; set; }
    public string Finder { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Entrance { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public int ItemFlags { get; set; }
    public int Status { get; set; }
}

public class ReceiverUserInfo
{
    public string UserId { get; set; } = string.Empty;
    public required string Flag { get; set; }
}

[Flags]
public enum ReceiverFlag
{
    None = 0,
    Filler = 1 << 0, // 1
    Progression = 1 << 1, // 2
    Useful = 1 << 2, // 4
    Required = 1 << 3, // 8
    Trap = 1 << 4  // 16
}
