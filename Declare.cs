using Discord.WebSocket;
using Discord.Commands;
using System.Data.SQLite;

public static class Declare
{
    public static readonly string DisplayedItemsFile = "displayedItems.json";
    public static readonly string AliasChoicesFile = "aliasChoices.json";
    public static readonly string GameStatusFile = "gameStatus.json";
    public static readonly string HintStatusFile = "hintStatus.json";
    public static readonly string RolesAliasesFile = "rolesAliases.json";
    public static readonly string ItemsTableFile = Program.GenerateItemsTableJson;
    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;

   /* public static ReceiverAliasesTable ReceiverAliases = new ReceiverAliasesTable();*/
    /*public static RecapListTable RecapList = new RecapListTable();*/
    /*public static AliasChoicesTable AliasChoices = new AliasChoicesTable();*/
    /*public static GameStatusTable GameStatus = new GameStatusTable();*/
    /*public static DisplayedItemTable DisplayedItems = new DisplayedItemTable();*/
    /*public static HintStatusTable HintStatuses = new HintStatusTable();*/
   /* public static ChannelsAndUrlsTable ChannelAndUrl = new ChannelsAndUrlsTable();*/
    public static ItemsTable ItemsTable = new ItemsTable();

    public static List<ApWorldJsonList> ApworldsInfo = new List<ApWorldJsonList>();

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static bool ServiceRunning = false;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
    public const string DatabaseFile = "AST.db";
}
