using Discord.WebSocket;
using Discord.Commands;

public static class Declare
{
    public static readonly string DisplayedItemsFile = "displayedItems.json";
    public static readonly string AliasFile = "aliases.json";
    public static readonly string AliasChoicesFile = "aliasChoices.json";
    public static readonly string GameStatusFile = "gameStatus.json";
    public static readonly string UrlChannelFile = "url_channel.json";
    public static readonly string RecapListFile = "recap.json";
    public static readonly string HintStatusFile = "hintStatus.json";
    public static readonly string RolesAliasesFile = "rolesAliases.json";
    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;

    public static GuildReceiverAliases ReceiverAliases = new GuildReceiverAliases();
    public static GuildRecapList RecapList = new GuildRecapList();
    public static GuildAliasChoices AliasChoices = new GuildAliasChoices();
    public static GuildGameStatus GameStatus = new GuildGameStatus();
    public static GuildDisplayedItem DisplayedItems = new GuildDisplayedItem();
    public static GuildHintStatus HintStatuses = new GuildHintStatus();
    public static GuildChannelsAndUrls ChannelAndUrl = new GuildChannelsAndUrls();

    public static List<ApWorldJsonList> ApworldsInfo = new List<ApWorldJsonList>();

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static bool ServiceRunning = false;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
}
