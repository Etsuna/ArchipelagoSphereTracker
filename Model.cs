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
