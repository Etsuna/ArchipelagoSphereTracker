using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using System.Runtime.InteropServices;

class Program
{
    static async Task Main(string[] args)
    {
        Env.Load();

        string currentVersion = File.Exists(Declare.VersionFile) ? await File.ReadAllTextAsync(Declare.VersionFile) : "";
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        DatabaseInitializer.InitializeDatabase();

        if (args.Length > 0 && args[0].ToLower() == "install")
        {
            Console.WriteLine("Installation Mode Only");
            await BackupRestoreClass.Backup();
            await InstallClass.Install(currentVersion, isWindows, isLinux);
            await BackupRestoreClass.RestoreBackup();

            CustomApworldClass.GenerateYamls();
            CustomApworldClass.GenerateItems();

            return;
        }

        if (currentVersion.Trim() == Declare.Version)
        {
            Console.WriteLine($"Archipelago {Declare.Version} is already installed.");
        }
        else
        {
            await BackupRestoreClass.Backup();
            await InstallClass.Install(currentVersion, isWindows, isLinux);
            await BackupRestoreClass.RestoreBackup();
        }

        CustomApworldClass.GenerateYamls();
        CustomApworldClass.GenerateItems();

        string version = $"AST v{Declare.BotVersion} - Archipelago v{Declare.Version}";

        Console.WriteLine($"Starting bot... {version}");

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            UseInteractionSnowflakeDate = false,
            ResponseInternalTimeCheck = false
        };

        Declare.Client = new DiscordSocketClient(config);
        Declare.CommandService = new CommandService();

        Declare.Client.Log += LogAsync;
        Declare.Client.Ready += ReadyAsync;
        Declare.Client.MessageReceived += BotCommands.MessageReceivedAsync;
        Declare.Client.JoinedGuild += OnGuildJoined;

        await Declare.Client.SetCustomStatusAsync(version);

        await BotCommands.InstallCommandsAsync();

        await Declare.Client.LoginAsync(TokenType.Bot, Declare.DiscordToken);
        await Declare.Client.StartAsync();

        await Task.Delay(-1);
    }

    private static async Task OnGuildJoined(SocketGuild guild)
    {
        await BotCommands.RegisterCommandsAsync();
    }

    static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
        return Task.CompletedTask;
    }

    static async Task ReadyAsync()
    {
        await BotCommands.RegisterCommandsAsync();
        Console.WriteLine("Bot is connected!");
        TrackingDataManager.StartTracking();
    }
}