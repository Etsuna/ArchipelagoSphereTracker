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

public sealed record CompanionSnapshot(
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CompanionItem> Items);

public sealed record CompanionConnection(
    string BaseUrl,
    string GuildId,
    string ChannelId,
    string Token)
{
    public string SummaryUrl =>
        $"{BaseUrl.TrimEnd('/')}/api/portal/{Uri.EscapeDataString(GuildId)}/{Uri.EscapeDataString(ChannelId)}/{Uri.EscapeDataString(Token)}/summary";
}
