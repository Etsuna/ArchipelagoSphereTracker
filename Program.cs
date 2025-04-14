using Discord;
using Discord.WebSocket;
using Discord.Commands;
using DotNetEnv;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

class Program
{
    public static string version = "0.6.1";
    public static string basePath = Path.Combine(AppContext.BaseDirectory);
    public static string externalFolder = Path.Combine(basePath, "extern");
    public static string versionFile = Path.Combine(externalFolder, "versionFile.txt");
    public static string extractPath = Path.Combine(externalFolder, "Archipelago");
    public static string downloadUrl = $"https://github.com/ArchipelagoMW/Archipelago/archive/refs/tags/{version}.zip";
    public static string archivePath = Path.Combine(basePath, "archive");
    public static string tempExtractPath = Path.Combine(basePath, "tempExtract");
    public static string generateTemplatesPath = Path.Combine(extractPath, "generateTemplates.py");

    static async Task Main(string[] args)
    {
        string currentVersion = File.Exists(versionFile) ? await File.ReadAllTextAsync(versionFile) : "";
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        Console.WriteLine($"Starting bot... Archipelago Version: Archipelago_{currentVersion}");

        if (currentVersion.Trim() == version)
        {
            Console.WriteLine($"Archipelago {version} est déjà installé.");
        }
        else
        {
            Console.WriteLine($"Nouvelle version détectée : {version} (ancienne : {currentVersion})");
            Directory.CreateDirectory(externalFolder);

            var venvPath = Path.Combine(extractPath, "venv");
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
                process.WaitForExit();
                Console.WriteLine("✅ Processus Python arrêtés !");
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

            using (HttpClient client = new HttpClient())
            {
                Console.WriteLine($"Téléchargement de {downloadUrl}...");
                HttpResponseMessage response = await client.GetAsync(downloadUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erreur : Impossible de télécharger Archipelago (code {response.StatusCode}).");
                    return;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(archivePath, data);
            }

            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);

            Console.WriteLine("Extraction temporaire...");
            ZipFile.ExtractToDirectory(archivePath, tempExtractPath);

            string extractedMainFolder = Directory.GetDirectories(tempExtractPath).FirstOrDefault();
            if (string.IsNullOrEmpty(extractedMainFolder))
            {
                Console.WriteLine("Erreur : Impossible de trouver le dossier extrait !");
                return;
            }

            Console.WriteLine("Déplacement des fichiers...");
            MoveFilesRecursively(extractedMainFolder, extractPath);

            Directory.Delete(tempExtractPath, true);
            File.Delete(archivePath);

            if (isLinux)
            {
                InstallLinuxBuildTools();
            }

            Console.WriteLine("Création du virtualenv...");
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
                process.WaitForExit();
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

            var allRequirements = Directory.GetFiles(extractPath, "requirements.txt", SearchOption.AllDirectories);

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
            }

            GenerateYamls();

            await File.WriteAllTextAsync(versionFile, version);

            Console.WriteLine("Mise à jour terminée !");
        }

        Env.Load();

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

        await BotCommands.InstallCommandsAsync();

        DataManager.LoadReceiverAliases();
        DataManager.LoadAliasChoices();
        DataManager.LoadGameStatus();
        DataManager.LoadUrlAndChannel();
        DataManager.LoadRecapList();
        DataManager.LoadHintStatus();
        DataManager.LoadDisplayedItems();
        DataManager.LoadApWorldJsonList();

        await Declare.Client.LoginAsync(TokenType.Bot, Declare.DiscordToken);
        await Declare.Client.StartAsync();

        await Task.Delay(-1);
    }

    public static void GenerateYamls()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var venvPath = Path.Combine(extractPath, "venv");
        var pythonExecutable = isWindows
                ? Path.Combine(venvPath, "Scripts", "python.exe")
                : Path.Combine(venvPath, "bin", "python3");

        Console.WriteLine("Génération des templates YAML...");
        if (File.Exists(generateTemplatesPath))
        {
            File.Delete(generateTemplatesPath);
        }

        File.WriteAllText(generateTemplatesPath, GenerateTemplates.pythonCode);

        Console.WriteLine($"Fichier Python créé à l'emplacement : {generateTemplatesPath}");

        var generateYamlCommand = isWindows
   ? $"cmd /c echo yes | \"{pythonExecutable}\" \"{generateTemplatesPath}\""
   : $"bash -c 'yes | \"{pythonExecutable}\" \"{generateTemplatesPath}\"'";

        ProcessStartInfo generateYamlProcess = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            Arguments = isWindows ? $"/c {generateYamlCommand}" : $"-c \"{generateYamlCommand}\"",
            WorkingDirectory = extractPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(generateYamlProcess))
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
    }

    static async Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
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
    }
}