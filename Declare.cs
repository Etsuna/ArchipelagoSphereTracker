using Discord.WebSocket;
using Discord.Commands;

public static class Declare
{
    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;
    public static readonly bool IsDev = (Environment.GetEnvironmentVariable("IS_DEV") ?? "false").ToLower() == "true";

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
    public const string DatabaseFile = "AST.db";
    public static HttpClient HttpClient = new HttpClient();
    public static string ProgramID { get; set; } = "";
}
