using Discord.WebSocket;
using Discord.Commands;
using System.Net.NetworkInformation;

public static class Declare
{
    public static readonly string displayedItemsFile = "displayedItems.json";
    public static readonly string aliasFile = "aliases.json";
    public static readonly string aliasChoicesFile = "aliasChoices.json";
    public static readonly string gameStatusFile = "gameStatus.json";
    public static readonly string urlChannelFile = "url_channel.json";
    public static readonly string recapListFile = "recap.json";
    public static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

    public static string urlSphereTracker = string.Empty;
    public static string urlTracker = string.Empty;
    public static ulong channelId = 0;
    public static Dictionary<string, string> receiverAliases = new Dictionary<string, string>();
    public static Dictionary<string, List<SubElement>> recapList = new Dictionary<string, List<SubElement>>();
    public static IDictionary<string, string> aliasChoices = new Dictionary<string, string>();
    public static List<trackerElement> gameStatus = new List<trackerElement>();
    public static List<displayedItemsElement> displayedItems = new List<displayedItemsElement>();
    public static CancellationTokenSource cts;
    public static DiscordSocketClient client;
    public static CommandService commandService;
    public static IServiceProvider services;
}