using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using System.Globalization;
using System.Runtime.InteropServices;

class Program
{
    static async Task Main(string[] args)
    {
        Env.Load();
#if DEBUG
        //args = new string[] { "--normalmode" };
        //args = new string[] { "--install" };
        args = new string[] { "--archipelagoMode" };
#endif

        string currentVersion = File.Exists(Declare.VersionFile) ? await File.ReadAllTextAsync(Declare.VersionFile) : "";
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);


        if (!isWindows && !isLinux)
        {
            Console.WriteLine(Resource.ProgramOSNotSupported);
            return;
        }

        Thread.CurrentThread.CurrentUICulture = new CultureInfo(Declare.Language);

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        if (args[0].ToLower() == "--archipelagomode")
        {
            Console.WriteLine(Resource.ArchipelagoModeStarted);
            Declare.IsArchipelagoMode = true;
        }

        if (args[0].ToLower() == "--normalmode")
        {
            Console.WriteLine(Resource.NormalModeStarted);
            Declare.IsArchipelagoMode = false;
        }

        if(args[0].ToLower() == "")
        {
            ShowHelp();
            return;
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine($"  --install           {Resource.ProgramInstall}");
            Console.WriteLine($"  --ArchipelagoMode   {Resource.ProgramArchipelagoMode}");
            Console.WriteLine($"  --NormalMode        {Resource.ProgramNormalMode}");
            Console.WriteLine();
            Console.WriteLine(Resource.ProgramHelp);
        }

        await DatabaseInitializer.InitializeDatabaseAsync();

        if (args[0].ToLower() == "--install")
        {
            Console.WriteLine(Resource.ProgramInstallationMode);
            await BackupRestoreClass.Backup();
            await InstallClass.Install(currentVersion, isWindows, isLinux);
            await BackupRestoreClass.RestoreBackup();

            CustomApworldClass.GenerateYamls();

            return;
        }

        if (Declare.IsArchipelagoMode)
        {
            if (currentVersion.Trim() == Declare.Version)
            {
                Console.WriteLine(string.Format(Resource.ProgramArchipelagoAlreadyInstalled, Declare.Version));
            }
            else
            {
                await BackupRestoreClass.Backup();
                await InstallClass.Install(currentVersion, isWindows, isLinux);
                await BackupRestoreClass.RestoreBackup();
            }

            CustomApworldClass.GenerateYamls();
        }

        string version = Declare.IsArchipelagoMode ? $"AST v{Declare.BotVersion} - Archipelago v{Declare.Version}" : $"AST v{Declare.BotVersion}";

        Console.WriteLine(string.Format(Resource.ProgramStartingBot, version));

        var config = new DiscordSocketConfig
        {
            GatewayIntents =
            GatewayIntents.Guilds |
            GatewayIntents.GuildMessages | 
            GatewayIntents.MessageContent, 
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
        Console.WriteLine(Resource.ProgramBotIsConnected);
        TrackingDataManager.StartTracking();
    }
}