using Discord.WebSocket;
using Discord.Commands;

public static class Declare
{
    public static readonly string displayedItemsFile = "displayedItems.json";
    public static readonly string aliasFile = "aliases.json";
    public static readonly string urlChannelFile = "url_channel.json";
    public static readonly string recapListFile = "recap.json";
    public static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

    public static string url = string.Empty;
    public static ulong channelId = 0;
    public static Dictionary<string, string> receiverAliases = new Dictionary<string, string>();
    public static Dictionary<string, List<SubElement>> recapList = new Dictionary<string, List<SubElement>>();
    public static IDictionary<string, string> aliasChoices = new Dictionary<string, string>();
    public static List<displayedItemsElement> displayedItems = new List<displayedItemsElement>();
    public static CancellationTokenSource cts;
    public static DiscordSocketClient client;
    public static CommandService commandService;
    public static IServiceProvider services;
}