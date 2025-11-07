using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using System.Globalization;
using System.Runtime.InteropServices;

class Program
{
    public static bool SetBddVersion = false;
    static void Notify(string msg) => Console.WriteLine(msg);
    static async Task Main(string[] args)
    {
        Env.Load();
#if ARCHIPELAGOMODE
        args = ["--archipelagoMode"];
#elif RC
        args = ["--archipelagoMode"];
#elif NORMALMODE
        args = ["--normalmode"];
#elif DEBUG
        args = ["--normalmode"];
#endif

        string currentVersion = File.Exists(Declare.VersionFile) ? await File.ReadAllTextAsync(Declare.VersionFile) : "";
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        await CheckUpdate.CheckAsync(
            owner: "Etsuna",
            repo: "ArchipelagoSphereTracker",
            notify: Notify
        );

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

        if (args[0].ToLower() == "")
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

        if (!File.Exists(Declare.DatabaseFile))
        {
            Console.WriteLine(Resource.SkipBDDMigration);
            SetBddVersion = true;
        }
        else
        {
            await CheckBdd();
        }

        await DatabaseInitializer.InitializeDatabaseAsync();

        if (SetBddVersion)
        {
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }

        Declare.ProgramID = await DatabaseCommands.ProgramIdentifier("ProgramIdTable");

        if (args[0].ToLower() == "--install")
        {
            Console.WriteLine(Resource.ProgramInstallationMode);
            await BackupRestoreClass.Backup();
            var installStatus = await InstallClass.Install(currentVersion, isWindows, isLinux);
            if (!installStatus)
            {
                return;
            }
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
                var installStatus = await InstallClass.Install(currentVersion, isWindows, isLinux);
                if (!installStatus)
                {
                    return;
                }
                await BackupRestoreClass.RestoreBackup();
            }

            CustomApworldClass.GenerateYamls();
        }

        string version = Declare.IsArchipelagoMode ? $"AST v{Declare.BotVersion} - Archipelago v{Declare.ReleaseVersion}" : $"AST v{Declare.BotVersion}";

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
        Declare.Client.Connected += OnConnected;
        Declare.Client.Disconnected += OnDisconnected;

        await Declare.Client.SetCustomStatusAsync(version);

        await BotCommands.InstallCommandsAsync();

        await Declare.Client.LoginAsync(TokenType.Bot, Declare.DiscordToken);
        await Declare.Client.StartAsync();

        await Task.Delay(-1);

        static Task OnDisconnected(Exception _)
        {
            Declare.Cts?.Cancel();
            return Task.CompletedTask;
        }

        static Task OnConnected()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                if (Declare.Cts == null || Declare.Cts.IsCancellationRequested)
                    TrackingDataManager.StartTracking();
            });
            return Task.CompletedTask;
        }
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

    static Task ReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            await BotCommands.RegisterCommandsAsync();
            Console.WriteLine(Resource.ProgramBotIsConnected);

            TrackingDataManager.StartTracking();
            UpdateReminder.Start();

        });
        return Task.CompletedTask;
    }

    private static async Task CheckBdd()
    {
        Console.WriteLine(Resource.CheckingBDDVersion);
        string bddVersion = await DBMigration.GetCurrentDbVersionAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        if (bddVersion == "-1")
        {
            Console.WriteLine(Resource.NoBddVersionTable);
            await DBMigration.Migrate_4_to_5Async(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
            await DBMigration.DropLegacyTablesAsync();
        }
        else if (bddVersion == Declare.BddVersion)
        {
            Console.WriteLine(Resource.BDDUpToDate);
        }
        else
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration.Migrate_4_to_5Async(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
            await DBMigration.DropLegacyTablesAsync();
        }
    }
}
