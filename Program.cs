﻿using Discord;
using Discord.WebSocket;
using Discord.Commands;
using DotNetEnv;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

class Program
{
    public static string Version = "0.6.1";
    public static string BotVersion = "2.3.0";
    public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");
    public static string ExternalFolder = Path.Combine(BasePath, "extern");
    public static string VersionFile = Path.Combine(ExternalFolder, "versionFile.txt");
    public static string ExtractPath = Path.Combine(ExternalFolder, "Archipelago");
    public static string DownloadUrl = $"https://github.com/ArchipelagoMW/Archipelago/archive/refs/tags/{Version}.zip";
    public static string ArchivePath = Path.Combine(BasePath, "archive");
    public static string TempExtractPath = Path.Combine(BasePath, "tempExtract");
    public static string GenerateTemplatesPath = Path.Combine(ExtractPath, "generateTemplates.py");
    public static string GenerateItemsTablePath = Path.Combine(ExtractPath, "generateItemsTable.py");
    public static string BddPath = Path.Combine(BasePath, "AST.db");
    public static string PlayersPath = Path.Combine(ExtractPath, "Players");
    public static string CustomPath = Path.Combine(ExtractPath, "custom_worlds");
    public static string WorldsPath = Path.Combine(ExtractPath, "worlds");
    public static string BackupPath = Path.Combine(ExternalFolder,$"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
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

        Console.WriteLine($"Starting bot... Archipelago Version: Archipelago_{currentVersion}");


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

        await Declare.Client.SetCustomStatusAsync($"Version {BotVersion}");

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
            string extension = Path.GetExtension(fichier);
            if (extensionsRoms.Contains(extension))
            {
                string nomFichier = Path.GetFileName(fichier);
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

            GenerateYamls();
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

        if(Directory.Exists(ExtractPath))
        {
            Console.WriteLine($"Le dossier {ExtractPath} existe déjà, on va le supprimer.");
            Directory.Delete(ExtractPath, true);
        }

        if (!Directory.Exists(ExternalFolder))
        {
            Directory.CreateDirectory(ExternalFolder);
        }

        var venvPath = Path.Combine(ExtractPath, "venv");
        var pythonExecutable = isWindows
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python3");

        Console.WriteLine("Arrêt des processus Python en cours...");

        var killPythonProcess = new ProcessStartInfo
        {
            FileName = isWindows ? "powershell" : "bash",
            Arguments = isWindows
                ? $"-Command \"Get-WmiObject Win32_Process | Where-Object {{$_.ExecutablePath -like '*{venvPath}*'}} | ForEach-Object {{$_.Terminate()}}\""
                : $"-c \"pkill -f '{venvPath}'\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(killPythonProcess))
        {
            if (process != null)
            {
                process.WaitForExit();
                Console.WriteLine("✅ Processus Python arrêtés !");
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible d'arrêter les processus Python.");
            }
        }

        if (Directory.Exists(venvPath))
        {
            Console.WriteLine("Suppression de l'ancien venv...");
            Directory.Delete(venvPath, true);
        }

        var pyxbldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pyxbld");

        if (Directory.Exists(pyxbldPath))
        {
            Directory.Delete(pyxbldPath, true);
            Console.WriteLine($"🧹 Dossier {pyxbldPath} supprimé.");
        }
        else
        {
            Console.WriteLine("Aucun dossier .pyxbld à supprimer.");
        }

        Console.WriteLine($"Téléchargement de {DownloadUrl}...");
        HttpResponseMessage response = await Declare.HttpClient.GetAsync(DownloadUrl);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Erreur : Impossible de télécharger Archipelago (code {response.StatusCode}).");
            return;
        }

        byte[] data = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(ArchivePath, data);

        if (Directory.Exists(TempExtractPath))
            Directory.Delete(TempExtractPath, true);

        Console.WriteLine("Extraction temporaire...");
        ZipFile.ExtractToDirectory(ArchivePath, TempExtractPath);

        string? extractedMainFolder = Directory.GetDirectories(TempExtractPath).FirstOrDefault();
        if (string.IsNullOrEmpty(extractedMainFolder))
        {
            Console.WriteLine("Erreur : Impossible de trouver le dossier extrait !");
            return;
        }

        Console.WriteLine("Déplacement des fichiers...");
        MoveFilesRecursively(extractedMainFolder, ExtractPath);

        Directory.Delete(TempExtractPath, true);
        File.Delete(ArchivePath);

        if (isLinux)
        {
            InstallLinuxBuildTools();
        }

        var venvCreateProcess = new ProcessStartInfo
        {
            FileName = isWindows ? "python" : "python3",
            Arguments = $"-m venv \"{venvPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(venvCreateProcess))
        {
            if (process != null)
            {
                Console.WriteLine("Création du virtualenv...");
                process.WaitForExit();
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible de créer le virtualenv.");
                return;
            }
        }

        if (!File.Exists(pythonExecutable))
        {
            Console.WriteLine($"❌ ERREUR : Python introuvable dans le venv : {pythonExecutable}");
            return;
        }
        else
        {
            Console.WriteLine($"✅ Virtualenv créé avec succès : {pythonExecutable}");
        }

        Console.WriteLine("Mise à jour de pip...");
        var pipUpdateProcess = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "-m pip install --upgrade pip",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(pipUpdateProcess))
        {
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ ERREUR : Échec de la mise à jour de pip (code {process.ExitCode})");
                }
                else
                {
                    Console.WriteLine("✅ Pip mis à jour avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible de mettre à jour pip.");
                return;
            }
        }

        Console.WriteLine("Mise à jour de setuptools...");
        var setuptoolsUpdateProcess = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "-m pip install --upgrade setuptools",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(setuptoolsUpdateProcess))
        {
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ ERREUR : Échec de la mise à jour de setuptools (code {process.ExitCode})");
                }
                else
                {
                    Console.WriteLine("✅ Setuptools mis à jour avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible de mettre à jour setuptools.");
                return;
            }
        }

        Console.WriteLine("Installation de Cython...");
        var CythonProcess = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "-m pip install cython",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(CythonProcess))
        {
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ ERREUR : Échec de l'instalation de Cython (code {process.ExitCode})");
                }
                else
                {
                    Console.WriteLine("✅ Cython installé avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible d'installer Cython.");
                return;
            }

        }

        var allRequirements = Directory.GetFiles(ExtractPath, "requirements.txt", SearchOption.AllDirectories);

        foreach (var requirement in allRequirements)
        {
            Console.WriteLine($"Installation des dépendances : {requirement}...");

            ProcessStartInfo pipInstallProcess;

            pipInstallProcess = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-m pip install --quiet --no-input -r \"{requirement}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(pipInstallProcess))
            {

                if (process != null)
                {
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"❌ ERREUR : pip install a échoué pour {requirement} (code {process.ExitCode})");
                    }
                    else
                    {
                        Console.WriteLine("✅ Dépendances installées avec succès !");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ ERREUR : Impossible d'installer les dépendances pour {requirement}.");
                    return;
                }
            }
        }

        GenerateYamls();
        GenerateItemsTableClass();

        Console.WriteLine("Importation des ApWorlds dans la BDD...");
        await ApworldListDatabase.Import();

        await File.WriteAllTextAsync(VersionFile, Version);

        Console.WriteLine("Mise à jour terminée !");
    }

    private static async Task OnGuildJoined(SocketGuild guild)
    {
        await BotCommands.RegisterCommandsAsync();
    }

    public static void GenerateYamls()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var venvPath = Path.Combine(ExtractPath, "venv");
        var pythonExecutable = isWindows
                ? Path.Combine(venvPath, "Scripts", "python.exe")
                : Path.Combine(venvPath, "bin", "python3");

        Console.WriteLine("Génération des templates YAML...");
        if (File.Exists(GenerateTemplatesPath))
        {
            File.Delete(GenerateTemplatesPath);
        }

        File.WriteAllText(GenerateTemplatesPath, GenerateTemplates.pythonCode);

        Console.WriteLine($"Fichier Python créé à l'emplacement : {GenerateTemplatesPath}");

        List<string> dossiersExtraits = new List<string>();
        if (Directory.Exists(CustomPath))
        {
            var fichiersApworld = Directory.GetFiles(CustomPath, "*.apworld");

            if (fichiersApworld.Length > 0)
            {
                HashSet<string> dossiersExistants = new HashSet<string>(Directory.GetDirectories(WorldsPath));

                foreach (var fichier in Directory.GetFiles(CustomPath, "*.apworld"))
                {
                    using (ZipArchive archive = ZipFile.OpenRead(fichier))
                    {
                        string? dossierRacine = null;
                        foreach (var entry in archive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.FullName) && entry.FullName.EndsWith("/"))
                            {
                                dossierRacine = entry.FullName.Split('/')[0];
                                break;
                            }
                        }

                        if (dossierRacine == null)
                        {
                            Console.WriteLine($"Pas de dossier racine trouvé dans {fichier}");
                            continue;
                        }

                        string cheminExtraction = Path.Combine(WorldsPath, dossierRacine);

                        if (!Directory.Exists(cheminExtraction))
                        {
                            Console.WriteLine($"Extraction de {fichier} dans {cheminExtraction}...");
                            ZipFile.ExtractToDirectory(fichier, WorldsPath);
                            dossiersExtraits.Add(cheminExtraction);
                        }
                        else
                        {
                            Console.WriteLine($"Le dossier {cheminExtraction} existe déjà, on ignore.");
                        }
                    }
                }
            }
        }

        var generateYamlCommand = isWindows
       ? $"cmd /c echo yes | \"{pythonExecutable}\" \"{GenerateTemplatesPath}\""
       : $"bash -c 'yes | \"{pythonExecutable}\" \"{GenerateTemplatesPath}\"'";

        ProcessStartInfo generateYamlProcess = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            Arguments = isWindows ? $"/c {generateYamlCommand}" : $"-c \"{generateYamlCommand}\"",
            WorkingDirectory = ExtractPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(generateYamlProcess))
        {
            if (process != null)
            {
                process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                process.ErrorDataReceived += (sender, args) => Console.WriteLine("Warning : " + args.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ ERREUR : Échec de la génération des YAML (code {process.ExitCode})");
                }
                else
                {
                    Console.WriteLine("✅ YAML générés avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible de générer les YAML.");
                return;
            }
        }

        if (Directory.Exists(CustomPath))
        {
            var fichiersApworld = Directory.GetFiles(CustomPath, "*.apworld");
            if (fichiersApworld.Length > 0)
            {
                foreach (var dossier in dossiersExtraits)
                {
                    try
                    {
                        Directory.Delete(dossier, recursive: true);
                        Console.WriteLine($"Supprimé : {dossier}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur suppression {dossier} : {ex.Message}");
                    }
                }
            }
        }
    }

    public static void GenerateItemsTableClass()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var venvPath = Path.Combine(ExtractPath, "venv");
        var pythonExecutable = isWindows
                ? Path.Combine(venvPath, "Scripts", "python.exe")
                : Path.Combine(venvPath, "bin", "python3");

        Console.WriteLine("Génération des templates Tables D'items...");
        if (File.Exists(GenerateItemsTablePath))
        {
            File.Delete(GenerateItemsTablePath);
        }

        File.WriteAllText(GenerateItemsTablePath, GenerateItemsTable.pythonCode.Replace("{WORLDS_PATH}", ExtractPath).Replace("{BDD_PATH}", BddPath));

        Console.WriteLine($"Fichier Python créé à l'emplacement : {GenerateItemsTablePath}");

        var generateItemsTableCommand = isWindows
    ? $"cmd /c echo yes | \"{pythonExecutable}\" \"{GenerateItemsTablePath}\""
    : $"bash -c 'yes | \"{pythonExecutable}\" \"{GenerateItemsTablePath}\"'";

        ProcessStartInfo generateItemsTableProcess = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            Arguments = isWindows ? $"/c {generateItemsTableCommand}" : $"-c \"{generateItemsTableCommand}\"",
            WorkingDirectory = ExtractPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(generateItemsTableProcess))
        {
            if (process != null)
            {
                process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                process.ErrorDataReceived += (sender, args) => Console.WriteLine("Warning : " + args.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"❌ ERREUR : Échec de la génération des Tables D'items (code {process.ExitCode})");
                }
                else
                {
                    Console.WriteLine("✅ Tables D'items générés avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible de générer les Tables D'items.");
                return;
            }
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

    static void MoveFilesRecursively(string sourcePath, string targetPath)
    {
        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourcePath, targetPath));
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            File.Move(file, file.Replace(sourcePath, targetPath), true);
        }
    }

    private static void InstallLinuxBuildTools()
    {
        Console.WriteLine("⚠️ Les outils nécessaires ne sont pas installés. Installation de build-essential et python3-dev...");

        var installProcess = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = "-c \"sudo apt-get install -y build-essential python3-dev python3-pip\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(installProcess))
        {
            if (process != null)
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine("❌ ERREUR : L'installation des outils de compilation a échoué.");
                }
                else
                {
                    Console.WriteLine("✅ Outils de compilation installés avec succès !");
                }
            }
            else
            {
                Console.WriteLine("❌ ERREUR : Impossible d'installer les outils de compilation.");
            }

        }
    }
}