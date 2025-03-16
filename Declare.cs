using Discord.WebSocket;
using Discord.Commands;

public static class Declare
{
    public static readonly string displayedItemsFile = "displayedItems.json";
    public static readonly string aliasFile = "aliases.json";
    public static readonly string aliasChoicesFile = "aliasChoices.json";
    public static readonly string gameStatusFile = "gameStatus.json";
    public static readonly string urlChannelFile = "url_channel.json";
    public static readonly string recapListFile = "recap.json";
    public static readonly string hintStatusFile = "hintStatus.json";
    public static readonly string rolesAliasesFile = "rolesAliases.json";
    public static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
    
    public static GuildReceiverAliases receiverAliases = new GuildReceiverAliases();
    public static GuildRecapList recapList = new GuildRecapList();
    public static GuildAliasChoices aliasChoices = new GuildAliasChoices();
    public static GuildGameStatus gameStatus = new GuildGameStatus();
    public static GuildDisplayedItem displayedItems = new GuildDisplayedItem();
    public static GuildHintStatus hintStatuses = new GuildHintStatus();
    public static GuildChannelsAndUrls ChannelAndUrl = new GuildChannelsAndUrls();

    public static CancellationTokenSource cts;
    public static DiscordSocketClient client;
    public static CommandService commandService;
    public static IServiceProvider services;
    public static bool serviceRunning = false;
    public static HashSet<string> warnedThreads = new HashSet<string>();
}