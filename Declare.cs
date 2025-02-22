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
    public static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
    
    public static string urlTracker = string.Empty;
    public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> receiverAliases = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
    public static Dictionary<string, Dictionary<string, Dictionary<string, List<SubElement>>>> recapList = new Dictionary<string, Dictionary<string, Dictionary<string, List<SubElement>>>>();
    public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> aliasChoices = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
    public static Dictionary<string, Dictionary<string, List<gameStatus>>> gameStatus = new Dictionary<string, Dictionary<string, List<gameStatus>>>();
    public static Dictionary<string, Dictionary<string, List<displayedItemsElement>>> displayedItems = new Dictionary<string, Dictionary<string, List<displayedItemsElement>>>();
    public static Dictionary<string, Dictionary<string, List<hintStatus>>> hintStatuses = new Dictionary<string, Dictionary<string, List<hintStatus>>>();
    public static Dictionary<string, Dictionary<string, string>> ChannelAndUrl = new Dictionary<string, Dictionary<string, string>>();

    public static CancellationTokenSource cts;
    public static DiscordSocketClient client;
    public static CommandService commandService;
    public static IServiceProvider services;
    public static bool serviceRunning = false;
}