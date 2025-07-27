using Discord;
using Discord.WebSocket;
using Discord.Commands;
using DotNetEnv;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Formats.Tar;
using System.Reflection;

class Program
{
    public static string Version = "0.6.2";
    public static string BotVersion = "3.1.1";

    public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");

    public static string DownloadWinUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{Version}/Setup.Archipelago.{Version}.exe";
    public static string DownloadLinuxUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{Version}/Archipelago_{Version}_linux-x86_64.tar.gz";
    public static string DownloadInnoExtractor = $"https://constexpr.org/innoextract/files/innoextract-1.9-windows.zip";

    public static string ArchivePath = Path.Combine(BasePath, "archive");
    public static string TempExtractPath = Path.Combine(BasePath, "tempExtract");
    public static string BddPath = Path.Combine(BasePath, "AST.db");
    public static string ExternalFolder = Path.Combine(BasePath, "extern");
    public static string ScanItemsPath = "ArchipelagoSphereTracker.apworld.scan_items.apworld";
    public static string GenerateTemplatesPath = "ArchipelagoSphereTracker.apworld.generate_templates.apworld";

    public static string VersionFile = Path.Combine(ExternalFolder, "versionFile.txt");
    public static string ExtractPath = Path.Combine(ExternalFolder, "Archipelago");
    public static string BackupPath = Path.Combine(ExternalFolder, $"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

    public static string ItemCategoryPath = Path.Combine(ExtractPath, "ItemCategory");
    public static string PlayersPath = Path.Combine(ExtractPath, "Players");
    public static string CustomPath = Path.Combine(ExtractPath, "custom_worlds");

    public static string RomBackupPath = Path.Combine(BackupPath, "rom_backup");
    public static string ApworldsBackupPath = Path.Combine(BackupPath, "apworlds_backup");
    public static string PlayersBackup = Path.Combine(BackupPath, "players_backup");

    static async Task Main(string[] args)
    {
        Env.Load();

        string currentVersion = File.Exists(VersionFile) ? await File.ReadAllTextAsync(VersionFile) : "";
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        DatabaseInitializer.InitializeDatabase();

        if (args.Length > 0 && args[0].ToLower() == "install")
        {
            Console.WriteLine("Installation Mode Only");
            await Backup();
            await Install(currentVersion, isWindows, isLinux);
            await RestoreBackup();

            GenerateYamls();
            GenerateItems();

            return;
        }

        if (currentVersion.Trim() == Version)
        {
            Console.WriteLine($"Archipelago {Version} est déjà installé.");
        }
        else
        {
            await Backup();
            await Install(currentVersion, isWindows, isLinux);
            await RestoreBackup();
        }

        GenerateYamls();
        GenerateItems();

        string version = $"AST v{BotVersion} - Archipelago v{Version}";

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

    public static Task Backup()
    {
        if (!Directory.Exists(ExtractPath))
        {
            Console.WriteLine("Le dossier externe n'existe pas, impossible de faire une sauvegarde.");
            return Task.CompletedTask;
        }

        if (!Directory.Exists(BackupPath))
        {
            Directory.CreateDirectory(BackupPath);
        }

        if (Directory.Exists(RomBackupPath))
        {
            Directory.Delete(RomBackupPath, true);
        }

        Directory.CreateDirectory(RomBackupPath);

        HashSet<string> extensionsRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".apworld", ".zip", ".7z",
            ".nes", ".sfc", ".smc", ".gba", ".gb", ".gbc",
            ".z64", ".n64", ".v64", ".nds", ".gcm", ".iso", ".cue", ".bin",
            ".gen", ".md", ".img", ".msu", ".pcm"
        };

        foreach (var fichier in Directory.GetFiles(ExtractPath, "*.*", SearchOption.TopDirectoryOnly))
        {
            string nomFichier = Path.GetFileName(fichier);

            if (nomFichier.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                continue;

            string extension = Path.GetExtension(fichier);
            if (extensionsRoms.Contains(extension))
            {
                string cheminDestination = Path.Combine(RomBackupPath, nomFichier);

                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine($"Déplacé : {nomFichier} → {cheminDestination}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur sur {fichier} : {ex.Message}");
                }
            }
        }

        Console.WriteLine("Rom Backup terminé.");

        if (Directory.Exists(CustomPath))
        {
            if (Directory.Exists(ApworldsBackupPath))
            {
                Directory.Delete(ApworldsBackupPath, true);
            }

            Directory.CreateDirectory(ApworldsBackupPath);

            foreach (var fichier in Directory.GetFiles(CustomPath, "*.apworld", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(ApworldsBackupPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine($"Déplacé : {nomFichier} → {cheminDestination}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur sur {fichier} : {ex.Message}");
                }
            }

            Console.WriteLine("Custom_Worlds Backup terminé.");
        }


        if (Directory.Exists(PlayersPath))
        {
            if (Directory.Exists(PlayersBackup))
            {
                Directory.Delete(PlayersBackup, true);
            }

            Directory.CreateDirectory(PlayersBackup);

            foreach (var fichier in Directory.GetFiles(PlayersPath, "*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string destination = Path.Combine(PlayersBackup, nomFichier);
                File.Move(fichier, destination, overwrite: true);
                Console.WriteLine($"Fichier déplacé : {nomFichier}");
            }

            foreach (var dossier in Directory.GetDirectories(PlayersPath, "*", SearchOption.TopDirectoryOnly))
            {
                string nomDossier = Path.GetFileName(dossier);
                if (string.Equals(nomDossier, "templates", StringComparison.OrdinalIgnoreCase))
                    continue;

                string destination = Path.Combine(PlayersBackup, nomDossier);

                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);

                Directory.Move(dossier, destination);
                Console.WriteLine($"Dossier déplacé : {nomDossier}");
            }

            Console.WriteLine("Déplacement du dossier Players terminé.");
        }

        return Task.CompletedTask;
    }

    public static Task RestoreBackup()
    {
        if (!Directory.Exists(BackupPath))
        {
            Console.WriteLine("Le dossier de sauvegarde n'existe pas, impossible de restaurer.");
            return Task.CompletedTask;
        }

        if (Directory.Exists(RomBackupPath))
        {
            foreach (var fichier in Directory.GetFiles(RomBackupPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(ExtractPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine($"Restauré : {nomFichier} → {cheminDestination}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur sur {fichier} : {ex.Message}");
                }
            }
        }

        if (Directory.Exists(ApworldsBackupPath))
        {
            if(!Directory.Exists(CustomPath))
            {
                Directory.CreateDirectory(CustomPath);
            }

            foreach (var fichier in Directory.GetFiles(ApworldsBackupPath, "*.apworld", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(CustomPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine($"Restauré : {nomFichier} → {cheminDestination}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur sur {fichier} : {ex.Message}");
                }
            }
        }

        if (Directory.Exists(PlayersBackup))
        {
            foreach (var fichier in Directory.GetFiles(PlayersBackup, "*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string destination = Path.Combine(PlayersPath, nomFichier);
                File.Move(fichier, destination, overwrite: true);
                Console.WriteLine($"Fichier restauré : {nomFichier}");
            }
            foreach (var dossier in Directory.GetDirectories(PlayersBackup, "*", SearchOption.TopDirectoryOnly))
            {
                string nomDossier = Path.GetFileName(dossier);
                string destination = Path.Combine(PlayersPath, nomDossier);
                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);
                Directory.Move(dossier, destination);
                Console.WriteLine($"Dossier restauré : {nomDossier}");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task Install(string currentVersion, bool isWindows, bool isLinux)
    {
        Console.WriteLine($"Nouvelle version détectée : {Version} (ancienne : {currentVersion})");
        Console.WriteLine($"{BasePath.ToString()}");

        if (Directory.Exists(ExtractPath))
        {
            Console.WriteLine($"Le dossier {ExtractPath} existe déjà, on va le supprimer.");
            Directory.Delete(ExtractPath, true);
        }

        if (!Directory.Exists(ExternalFolder))
        {
            Directory.CreateDirectory(ExternalFolder);
        }

        if(isWindows)
        {
            if (Directory.Exists(TempExtractPath))
                Directory.Delete(TempExtractPath, true);

            await InstallInnoExtractor();
            await DownloadArchipelagoForWindows();
            await ExtractArchipelagoForWindows();
            MoveAppFileToArchipelagoFolder();
            await InstallVcRedist();
        }

        if (isLinux)
        {
            if (Directory.Exists(TempExtractPath))
                Directory.Delete(TempExtractPath, true);

            if (!Directory.Exists(TempExtractPath))
            {
                Directory.CreateDirectory(TempExtractPath);
            }

            await DownloadAndExtractArchipelagoForLinux();
        }

        if (Path.Exists(TempExtractPath))
        {
            Console.WriteLine($"Clean {TempExtractPath}");
            Directory.Delete(TempExtractPath, true);
        }

        Console.WriteLine("Importation des ApWorlds dans la BDD...");
        await ApworldListDatabase.Import();

        await File.WriteAllTextAsync(VersionFile, Version);

        Console.WriteLine("Mise à jour terminée !");

        static async Task InstallInnoExtractor()
        {
            Console.WriteLine($"Téléchargement de {DownloadInnoExtractor}...");
            HttpResponseMessage responseDownloadInnoExtractor = await Declare.HttpClient.GetAsync(DownloadInnoExtractor);

            if (!responseDownloadInnoExtractor.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erreur : Impossible de télécharger Archipelago (code {responseDownloadInnoExtractor.StatusCode}).");
                return;
            }

            byte[] dataDownloadInnoExtractor = await responseDownloadInnoExtractor.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync("innoextract-1.9-windows.zip", dataDownloadInnoExtractor);

            Console.WriteLine("Extraction temporaire...");
            ZipFile.ExtractToDirectory($"innoextract-1.9-windows.zip", TempExtractPath);

            string? extractedMainFolder = Directory.GetDirectories(TempExtractPath).FirstOrDefault();
            if (string.IsNullOrEmpty(extractedMainFolder))
            {
                Console.WriteLine("Erreur : Impossible de trouver le dossier extrait !");
                return;
            }
        }

        static async Task DownloadArchipelagoForWindows()
        {
            Console.WriteLine($"Téléchargement de {DownloadWinUrl}...");
            HttpResponseMessage responseDownloadWinUrl = await Declare.HttpClient.GetAsync(DownloadWinUrl);

            if (!responseDownloadWinUrl.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erreur : Impossible de télécharger Archipelago (code {responseDownloadWinUrl.StatusCode}).");
                return;
            }

            byte[] dataDownloadWinUrl = await responseDownloadWinUrl.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(Path.Combine(TempExtractPath, $"Setup.Archipelago.{Version}.exe"), dataDownloadWinUrl);
        }

        static async Task ExtractArchipelagoForWindows()
        {
            Console.WriteLine("Extraction de l'installeur Archipelago...");
            var innoExtractPath = Path.Combine(TempExtractPath, "innoextract.exe");
            var archipelagoInstallerPath = Path.Combine(TempExtractPath, $"Setup.Archipelago.{Version}.exe");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = innoExtractPath,
                Arguments = $"--extract --output-dir \"{ExtractPath}\" \"{archipelagoInstallerPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using Process? proc = Process.Start(psi);
                if (proc == null)
                {
                    Console.WriteLine("❌ Impossible de démarrer le processus innoextract.");
                    return;
                }
                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                Console.WriteLine(output);

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine($"❌ Erreur lors de l'extraction (code {proc.ExitCode}) :");
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine("✅ Extraction réussie.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception lors de l'exécution de innoextract : {ex.Message}");
            }
        }

        static void MoveAppFileToArchipelagoFolder()
        {
            Console.WriteLine("Déplacement des fichiers de l'installeur Archipelago...");
            string appFolder = Path.Combine(ExtractPath, "app");

            if (Directory.Exists(appFolder))
            {
                foreach (string file in Directory.GetFiles(appFolder, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(appFolder, file);
                    string destinationPath = Path.Combine(ExtractPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Move(file, destinationPath, overwrite: true);
                }

                Directory.Delete(appFolder, recursive: true);

                Console.WriteLine("✅ Fichiers déplacés avec succès depuis 'app\\' vers le dossier racine.");
            }
            else
            {
                Console.WriteLine("⚠️ Dossier 'app\\' non trouvé après extraction.");
            }
        }

        async Task InstallVcRedist()
        {
            if (!IsVcRedistInstalled())
            {
                Console.WriteLine("Installation de vc_redist.x64.exe...");

                string vcRedistPath = Path.Combine(ExtractPath, "tmp", "vc_redist.x64.exe");

                if (File.Exists(vcRedistPath))
                {
                    var psivc_redist = new ProcessStartInfo
                    {
                        FileName = vcRedistPath,
                        Arguments = "/install /quiet /norestart",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    try
                    {
                        using Process process = Process.Start(psivc_redist)!;
                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0)
                        {
                            Console.WriteLine("✅ VC++ Redistributable installé avec succès (mode silencieux).");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Erreur d'installation VC++ (code {process.ExitCode}).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Exception lors de l'installation VC++ : {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ Le fichier vc_redist.x64.exe est introuvable.");
                }
            }
            else
            {
                Console.WriteLine("✅ VC++ Redistributable déjà installé, aucune action nécessaire.");
            }
        }

        bool IsVcRedistInstalled()
        {
            const string keyPath = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null) return false;

            object? value = key.GetValue("Installed");
            return value is int installed && installed == 1;
        }

        static async Task DownloadAndExtractArchipelagoForLinux()
        {
            Console.WriteLine($"Téléchargement de {DownloadLinuxUrl}...");
            HttpResponseMessage responseDownloadWinUrl = await Declare.HttpClient.GetAsync(DownloadLinuxUrl);

            if (!responseDownloadWinUrl.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erreur : Impossible de télécharger Archipelago (code {responseDownloadWinUrl.StatusCode}).");
                return;
            }

            byte[] dataDownloadWinUrl = await responseDownloadWinUrl.Content.ReadAsByteArrayAsync();

            var sourceTar = Path.Combine(TempExtractPath, $"Archipelago_{Version}_linux-x86_64.tar.gz");
            await File.WriteAllBytesAsync(Path.Combine(sourceTar), dataDownloadWinUrl);

            using FileStream fs = new(sourceTar, FileMode.Open, FileAccess.Read);
            using GZipStream gz = new(fs, CompressionMode.Decompress, leaveOpen: true);

            TarFile.ExtractToDirectory(gz, ExternalFolder, overwriteFiles: false);
        }
    }

    private static async Task OnGuildJoined(SocketGuild guild)
    {
        await BotCommands.RegisterCommandsAsync();
    }

    public static void GenerateYamls()
    {
        Console.WriteLine("📦 Génération des templates YAML...");

        try
        {
            if (!Directory.Exists(CustomPath))
            {
                Directory.CreateDirectory(CustomPath);
            }

            string destinationPath = Path.Combine(CustomPath, "generate_templates.apworld");

            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GenerateTemplatesPath);
            if (stream == null)
            {
                Console.WriteLine("❌ Impossible de trouver la ressource embarquée : " + GenerateTemplatesPath);
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine("❌ Le fichier 'generate_templates.apworld' n’a pas été écrit correctement.");
                return;
            }

            Console.WriteLine($"✅ Fichier copié vers : {destinationPath}");

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine($"❌ Launcher introuvable : {launcherPath}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = launcherPath,
                Arguments = "\"Generate Templates\"",
                WorkingDirectory = ExtractPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("❌ ERREUR : Impossible de démarrer le processus de génération.");
                return;
            }

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("⚠️ " + e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✅ YAML générés avec succès !");
            }
            else
            {
                Console.WriteLine($"❌ ERREUR : Échec de la génération des YAML (code {process.ExitCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception : {ex.GetType().Name} - {ex.Message}");
        }
    }


    public static void GenerateItems()
    {
        Console.WriteLine("📦 Génération des templates Items Category Json...");

        try
        {
            if (!Directory.Exists(CustomPath))
            {
                Directory.CreateDirectory(CustomPath);
            }

            string destinationPath = Path.Combine(CustomPath, "scan_items.apworld");

            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScanItemsPath);
            if (stream == null)
            {
                Console.WriteLine("❌ Impossible de trouver la ressource embarquée : " + ScanItemsPath);
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine("❌ Le fichier n’a pas été écrit correctement sur le disque.");
                return;
            }

            Console.WriteLine($"✅ Fichier copié vers : {destinationPath}");

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine($"❌ Launcher introuvable : {launcherPath}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = launcherPath,
                Arguments = "\"Scan Items\"",
                WorkingDirectory = ExtractPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("❌ ERREUR : Impossible de démarrer le processus.");
                return;
            }

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("⚠️ " + e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✅ YAML générés avec succès !");
            }
            else
            {
                Console.WriteLine($"❌ ERREUR : Échec de la génération des YAML (code {process.ExitCode})");
            }

            var jsonFile = Directory.GetFiles(ItemCategoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(jsonFile) && File.Exists(jsonFile))
            {
                ItemsCommands.SyncItemsFromJsonAsync(jsonFile).GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine("⚠️ Aucun fichier JSON trouvé pour la synchronisation.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception : {ex.GetType().Name} - {ex.Message}");
        }
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