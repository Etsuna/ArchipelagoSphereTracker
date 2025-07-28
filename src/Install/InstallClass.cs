using Microsoft.Win32;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

public class InstallClass : Declare
{
    public static async Task Install(string currentVersion, bool isWindows, bool isLinux)
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

        if (isWindows)
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

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        Console.WriteLine($"💾 RAM après nettoyage : {proc.WorkingSet64 / 1024 / 1024} Mo");
    }
}
