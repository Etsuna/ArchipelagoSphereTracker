using ArchipelagoSphereTracker.src.Resources;
using Microsoft.Win32;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

public class InstallClass : Declare
{
    public static async Task Install(string currentVersion, bool isWindows, bool isLinux)
    {
        var message = string.IsNullOrEmpty(currentVersion)
            ? string.Format(Resource.InstallNewVersion, Version)
            : string.Format(Resource.InstallNewVersionWithPrevious, Version, currentVersion);

        Console.WriteLine(message);
        Console.WriteLine($"{BasePath.ToString()}");

        if (Directory.Exists(ExtractPath))
        {
            Console.WriteLine(string.Format(Resource.InstallDeleteExtractPath, ExtractPath));
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
            Console.WriteLine(string.Format(Resource.InstallCleanTempExtractPath, TempExtractPath));
            Directory.Delete(TempExtractPath, true);
        }

        Console.WriteLine(Resource.InstallImportApworldsDatabase);
        await ApworldListDatabase.Import();

        await File.WriteAllTextAsync(VersionFile, Version);

        Console.WriteLine(Resource.InstallUpdateComplete);

        static async Task InstallInnoExtractor()
        {
            Console.WriteLine(string.Format(Resource.InstallDLInnoExtractor, DownloadInnoExtractor));
            HttpResponseMessage responseDownloadInnoExtractor = await Declare.HttpClient.GetAsync(DownloadInnoExtractor);

            if (!responseDownloadInnoExtractor.IsSuccessStatusCode)
            {
                Console.WriteLine(string.Format(Resource.InstallDLInnoExtractorError, responseDownloadInnoExtractor.StatusCode));
                return;
            }

            byte[] dataDownloadInnoExtractor = await responseDownloadInnoExtractor.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(InnoExtractorZip, dataDownloadInnoExtractor);

            Console.WriteLine(Resource.InstallDLTempExtraction);
            ZipFile.ExtractToDirectory(InnoExtractorZip, TempExtractPath);

            string? extractedMainFolder = Directory.GetDirectories(TempExtractPath).FirstOrDefault();
            if (string.IsNullOrEmpty(extractedMainFolder))
            {
                Console.WriteLine(Resource.InstallDLTempExtractionError);
                return;
            }
        }

        static async Task DownloadArchipelagoForWindows()
        {
            Console.WriteLine(string.Format(Resource.InstallDLArchipelagoWindows, DownloadWinUrl));
            HttpResponseMessage responseDownloadWinUrl = await Declare.HttpClient.GetAsync(DownloadWinUrl);

            if (!responseDownloadWinUrl.IsSuccessStatusCode)
            {
                Console.WriteLine(string.Format(Resource.InstallDLArchipelagoError, responseDownloadWinUrl.StatusCode));
                return;
            }

            byte[] dataDownloadWinUrl = await responseDownloadWinUrl.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(Path.Combine(TempExtractPath, ArchipelagoWindowsSetup), dataDownloadWinUrl);
        }

        static async Task ExtractArchipelagoForWindows()
        {
            Console.WriteLine(Resource.InstallExtractingArchipelago);
            var innoExtractPath = Path.Combine(TempExtractPath, InnoExtractorExe);
            var archipelagoInstallerPath = Path.Combine(TempExtractPath, ArchipelagoWindowsSetup);

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
                    Console.WriteLine(Resource.InstallUnableStartInnoExtractProcess);
                    return;
                }
                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                Console.WriteLine(output);

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine(string.Format(Resource.InstallInnoExtractError, proc.ExitCode));
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine(Resource.InstallInnoExtractorSuccessful);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(Resource.InstallInnoExtractorException, ex.Message));
            }
        }

        static void MoveAppFileToArchipelagoFolder()
        {
            Console.WriteLine(Resource.InstallMoveArchipelagoFiles);
            
            if (Directory.Exists(AppPath))
            {
                foreach (string file in Directory.GetFiles(AppPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(AppPath, file);
                    string destinationPath = Path.Combine(ExtractPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Move(file, destinationPath, overwrite: true);
                }

                Directory.Delete(AppPath, recursive: true);

                Console.WriteLine(Resource.InstallMoveArchipelagoFilesSuccessful);
            }
            else
            {
                Console.WriteLine(Resource.InstallMoveArchipelagoFilesFailed);
            }
        }

        async Task InstallVcRedist()
        {
            if (!IsVcRedistInstalled())
            {
                Console.WriteLine(Resource.InstallVcRedistX64);

                string vcRedistPath = Path.Combine(TempPath, VcRedistx64Setup);

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
                            Console.WriteLine(Resource.InstallVcRedistX64Successful);
                        }
                        else
                        {
                            Console.WriteLine(string.Format(Resource.InstallVcRedistX64Failed, process.ExitCode));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format(Resource.InstallVcRedistX64Exception, ex.Message));
                    }
                }
                else
                {
                    Console.WriteLine(Resource.InstallVcRedistX64NotFound);
                }
            }
            else
            {
                Console.WriteLine(Resource.InstallVcRedistX64AlreadyInstalled);
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
            Console.WriteLine(string.Format(Resource.InstallDlArchipelagoLinux, DownloadLinuxUrl));
            HttpResponseMessage responseDownloadLinuxUrl = await Declare.HttpClient.GetAsync(DownloadLinuxUrl);

            if (!responseDownloadLinuxUrl.IsSuccessStatusCode)
            {
                Console.WriteLine(string.Format(Resource.InstallDLArchipelagoError, responseDownloadLinuxUrl.StatusCode));
                return;
            }

            byte[] dataDownloadWinUrl = await responseDownloadLinuxUrl.Content.ReadAsByteArrayAsync();

            var sourceTar = Path.Combine(TempExtractPath, ArchipelagoLinuxTarGz);
            await File.WriteAllBytesAsync(Path.Combine(sourceTar), dataDownloadWinUrl);

            using FileStream fs = new(sourceTar, FileMode.Open, FileAccess.Read);
            using GZipStream gz = new(fs, CompressionMode.Decompress, leaveOpen: true);

            TarFile.ExtractToDirectory(gz, ExternalFolder, overwriteFiles: false);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        Console.WriteLine(string.Format(Resource.InstallFreeMemory, proc.WorkingSet64 / 1024 / 1024));
    }
}
