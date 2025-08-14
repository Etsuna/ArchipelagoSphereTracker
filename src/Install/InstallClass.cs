using Microsoft.Win32;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

public class InstallClass : Declare
{
    public static async Task Install(string currentVersion, bool isWindows, bool isLinux)
    {
        Console.WriteLine($"New version detected: {Version} (previous: {currentVersion})");
        Console.WriteLine($"{BasePath.ToString()}");

        if (Directory.Exists(ExtractPath))
        {
            Console.WriteLine($"The folder {ExtractPath} already exists, it will be deleted.");
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

        Console.WriteLine("Importing ApWorlds into the database...");
        await ApworldListDatabase.Import();

        await File.WriteAllTextAsync(VersionFile, Version);

        Console.WriteLine("Update completed!");

        static async Task InstallInnoExtractor()
        {
            Console.WriteLine($"Downloading {DownloadInnoExtractor}...");
            HttpResponseMessage responseDownloadInnoExtractor = await Declare.HttpClient.GetAsync(DownloadInnoExtractor);

            if (!responseDownloadInnoExtractor.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: Unable to download Archipelago (code {responseDownloadInnoExtractor.StatusCode}).");
                return;
            }

            byte[] dataDownloadInnoExtractor = await responseDownloadInnoExtractor.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync("innoextract-1.9-windows.zip", dataDownloadInnoExtractor);

            Console.WriteLine("Temporary extraction...");
            ZipFile.ExtractToDirectory($"innoextract-1.9-windows.zip", TempExtractPath);

            string? extractedMainFolder = Directory.GetDirectories(TempExtractPath).FirstOrDefault();
            if (string.IsNullOrEmpty(extractedMainFolder))
            {
                Console.WriteLine("Error: Unable to find the extracted folder!");
                return;
            }
        }

        static async Task DownloadArchipelagoForWindows()
        {
            Console.WriteLine($"Downloading {DownloadWinUrl}...");
            HttpResponseMessage responseDownloadWinUrl = await Declare.HttpClient.GetAsync(DownloadWinUrl);

            if (!responseDownloadWinUrl.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: Unable to download Archipelago (code {responseDownloadWinUrl.StatusCode}).");
                return;
            }

            byte[] dataDownloadWinUrl = await responseDownloadWinUrl.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(Path.Combine(TempExtractPath, $"Setup.Archipelago.{Version}.exe"), dataDownloadWinUrl);
        }

        static async Task ExtractArchipelagoForWindows()
        {
            Console.WriteLine("Extracting Archipelago installer...");
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
                    Console.WriteLine("❌ Unable to start the innoextract process.");
                    return;
                }
                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                Console.WriteLine(output);

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine($"❌ Error during extraction (code {proc.ExitCode}):");
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine("✅ Extraction successful.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception while running innoextract: {ex.Message}");
            }
        }

        static void MoveAppFileToArchipelagoFolder()
        {
            Console.WriteLine("Moving Archipelago installer files...");
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

                Console.WriteLine("✅ Files successfully moved from 'app\\' to the root folder.");
            }
            else
            {
                Console.WriteLine("⚠️ Folder 'app\\' not found after extraction.");
            }
        }

        async Task InstallVcRedist()
        {
            if (!IsVcRedistInstalled())
            {
                Console.WriteLine("Installing vc_redist.x64.exe...");

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
                            Console.WriteLine("✅ VC++ Redistributable installed successfully (silent mode).");
                        }
                        else
                        {
                            Console.WriteLine($"❌ VC++ installation error (code {process.ExitCode}).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Exception during VC++ installation: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ The file vc_redist.x64.exe was not found.");
                }
            }
            else
            {
                Console.WriteLine("✅ VC++ Redistributable already installed, no action needed.");
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
            Console.WriteLine($"Downloading {DownloadLinuxUrl}...");
            HttpResponseMessage responseDownloadWinUrl = await Declare.HttpClient.GetAsync(DownloadLinuxUrl);

            if (!responseDownloadWinUrl.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: Unable to download Archipelago (code {responseDownloadWinUrl.StatusCode}).");
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
        Console.WriteLine($"💾 RAM after cleanup: {proc.WorkingSet64 / 1024 / 1024} MB");
    }
}
