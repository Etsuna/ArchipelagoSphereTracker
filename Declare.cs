using Discord.WebSocket;
using Discord.Commands;
using System.Data.SQLite;

public static class Declare
{
    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static bool ServiceRunning = false;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
    public const string DatabaseFile = "AST.db";
    public static HttpClient HttpClient = new HttpClient();
}
