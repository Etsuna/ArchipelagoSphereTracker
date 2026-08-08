namespace AST.Companion;

public sealed record CompanionItem(
    string Alias,
    string Finder,
    string Item,
    string Location,
    string Game,
    string Flag)
{
    public string Identity => string.Join("|", Alias, Finder, Item, Location, Game, Flag);
}

public sealed record CompanionHint(
    string Alias,
    string Finder,
    string Receiver,
    string Item,
    string Location,
    string Game,
    string Direction)
{
    public string Identity => string.Join("|", Alias, Finder, Receiver, Item, Location, Game, Direction);
}

public sealed record CompanionSnapshot(
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CompanionItem> Items,
    IReadOnlyList<CompanionHint> Hints);

public sealed record CompanionConnection(
    string BaseUrl,
    string GuildId,
    string ChannelId,
    string Token)
{
    public string SummaryUrl =>
        $"{BaseUrl.TrimEnd('/')}/api/portal/{Uri.EscapeDataString(GuildId)}/{Uri.EscapeDataString(ChannelId)}/{Uri.EscapeDataString(Token)}/summary";
}
