using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using Prometheus;
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
#elif UPDATEBDD
        args = ["--updatebdd"];
#elif BIGASYNC
        args = ["--bigasync"];
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

        if (args[0].ToLower() == "--updatebdd")
        {
            Console.WriteLine("UpdateBdd");
            Declare.UpdateBdd = true;
        }

        if(args[0].ToLower() == "--bigasync")
        {
            Console.WriteLine("BigAsync mode enabled");
            Declare.IsBigAsync = true;
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
            Console.WriteLine($"  --UpdateBDD         {Resource.ProgramUpdateBDD}");
            Console.WriteLine($"  --BigAsync          {Resource.ProgramBigAsyncMode}");
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
            if(Declare.UpdateBdd)
            {
                return;
            }
        }

        await DatabaseInitializer.InitializeDatabaseAsync();

        if (SetBddVersion)
        {
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }


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
            if (currentVersion.Trim() == Declare.ReleaseVersion)
            {
                Console.WriteLine(string.Format(Resource.ProgramArchipelagoAlreadyInstalled, Declare.ReleaseVersion));
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

        var shutdownSignal = new CancellationTokenSource();
        void RequestShutdown()
        {
            try
            {
                if (!shutdownSignal.IsCancellationRequested)
                    shutdownSignal.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestShutdown();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RequestShutdown();
        PosixSignalRegistration? sigTermRegistration = null;
        PosixSignalRegistration? sigIntRegistration = null;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => RequestShutdown());
            sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => RequestShutdown());
        }

        await Declare.Client.SetCustomStatusAsync(version);

        await BotCommands.InstallCommandsAsync();

        await Declare.Client.LoginAsync(TokenType.Bot, Declare.DiscordToken);
        await Declare.Client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, shutdownSignal.Token);
        }
        catch (TaskCanceledException)
        {
        }

        await ShutdownAsync();
        sigTermRegistration?.Dispose();
        sigIntRegistration?.Dispose();
        shutdownSignal.Dispose();

        static Task OnDisconnected(Exception _)
        {
            Declare.Cts?.Cancel();
            return Task.CompletedTask;
        }

        static async Task ShutdownAsync()
        {
            Declare.Cts?.Cancel();
            await WebPortalServer.StopAsync();

            if (Declare.Client != null)
            {
                await Declare.Client.StopAsync();
                await Declare.Client.LogoutAsync();
                Declare.Client.Dispose();
            }
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
            WebPortalServer.Start();
            await WebPortalPages.EnsureSharedCommandsPagesAsync();
            await WebPortalPages.EnsureMissingUserPagesAsync();

            if (Declare.ExportMetrics && !string.IsNullOrEmpty(Declare.MetricsPort))
            {
                var port = int.Parse(Declare.MetricsPort);

                var metricsServer = new MetricServer(port: port);
                metricsServer.Start();

                var cts = new CancellationTokenSource();

                _ = MetricsExporter.StartAsync(cts.Token);
            }

            MetricsExporter.ResolveGuildName = id =>
            {
                var g = Declare.Client.GetGuild(ulong.Parse(id));
                return g?.Name;
            };

            MetricsExporter.ResolveChannelName = (gid, cid) =>
            {
                var g = Declare.Client.GetGuild(ulong.Parse(gid));
                var ch = g?.GetChannel(ulong.Parse(cid));
                return ch?.Name;
            };

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
            await DBMigration_5.Migrate_5_0_1(cts.Token);
            await DBMigration_5.Migrate_5_0_2(cts.Token);
            await DBMigration_5.Migrate_5_0_3(cts.Token);
            await DBMigration_5.Migrate_5_0_4(cts.Token);
            await DBMigration_5.Migrate_5_0_5(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
            await DBMigration.DropLegacyTablesAsync();
        }
        else if (bddVersion == Declare.BddVersion)
        {
            Console.WriteLine(Resource.BDDUpToDate);
        }
        else if (bddVersion == "5.0.0")
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration_5.Migrate_5_0_1(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }
        else if (bddVersion == "5.0.1")
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration_5.Migrate_5_0_2(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }
        else if (bddVersion == "5.0.2")
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration_5.Migrate_5_0_3(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }
        else if (bddVersion == "5.0.3")
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration_5.Migrate_5_0_4(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }
        else if (bddVersion == "5.0.4")
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration_5.Migrate_5_0_5(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
        }
        else
        {
            Console.WriteLine(string.Format(Resource.BDDForceUpdate, bddVersion, Declare.BddVersion));
            await DBMigration.Migrate_4_to_5Async(cts.Token);
            await DBMigration_5.Migrate_5_0_1(cts.Token);
            await DBMigration_5.Migrate_5_0_2(cts.Token);
            await DBMigration_5.Migrate_5_0_3(cts.Token);
            await DBMigration_5.Migrate_5_0_4(cts.Token);
            await DBMigration_5.Migrate_5_0_5(cts.Token);
            await DBMigration.SetDbVersionAsync(Declare.BddVersion);
            await DBMigration.DropLegacyTablesAsync();
        }
    }
}
