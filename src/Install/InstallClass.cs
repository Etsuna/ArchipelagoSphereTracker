using ArchipelagoSphereTracker.src.Resources;
using Microsoft.Win32;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

public class InstallClass : Declare
{
    public static string? ArchipelagoPath { get; set; } = string.Empty;

    public static async Task<bool> Install(string currentVersion, bool isWindows, bool isLinux)
    {
        var message = string.IsNullOrEmpty(currentVersion)
            ? string.Format(Resource.InstallNewVersion, ReleaseVersion)
            : string.Format(Resource.InstallNewVersionWithPrevious, ReleaseVersion, currentVersion);

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
#pragma warning disable CA1416 // Valider la compatibilité de la plateforme
            ArchipelagoPath = AppLocator.GetArchipelagoPath();
#pragma warning restore CA1416 // Valider la compatibilité de la plateforme

            if (string.IsNullOrEmpty(ArchipelagoPath))
            {
                Console.WriteLine(string.Format(Resource.DownloadAndInstall, Version));
                Console.WriteLine(DownloadWinUrl);
                return false;
            }
            else
            {
                string getArchipelagoPath = Path.Combine(ArchipelagoPath, "ArchipelagoLauncher.exe");

                if (!File.Exists(getArchipelagoPath))
                {
                    Console.WriteLine(string.Format(Resource.DownloadAndInstall, Version));
                    Console.WriteLine(DownloadWinUrl);
                    return false;
                }
                else
                {
                    var info = FileVersionInfo.GetVersionInfo(getArchipelagoPath);
                    string productVersion = info.ProductVersion ?? string.Empty;

                    Console.WriteLine($"Product Version: {productVersion}");

                    if (!string.IsNullOrEmpty(productVersion) && Version != productVersion)
                    {
                        Console.WriteLine(string.Format(Resource.DownloadAndInstall, Version));
                        return false;
                    }
                    MoveAppFileToArchipelagoFolder();
                }
            }
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

        await File.WriteAllTextAsync(VersionFile, ReleaseVersion);

        Console.WriteLine(Resource.InstallUpdateComplete);

        static void MoveAppFileToArchipelagoFolder()
        {
            Console.WriteLine(Resource.InstallMoveArchipelagoFiles);

            if (Directory.Exists(ArchipelagoPath))
            {
                foreach (string file in Directory.GetFiles(ArchipelagoPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(ArchipelagoPath, file);
                    string destinationPath = Path.Combine(ExtractPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Copy(file, destinationPath, overwrite: true);
                }

                Console.WriteLine(Resource.InstallMoveArchipelagoFilesSuccessful);
            }
            else
            {
                Console.WriteLine(Resource.InstallMoveArchipelagoFilesFailed);
            }
        }

        static async Task DownloadAndExtractArchipelagoForLinux()
        {
            Console.WriteLine(string.Format(Resource.InstallDlArchipelagoLinux, DownloadLinuxUrl));
            HttpResponseMessage responseDownloadLinuxUrl = await HttpClient.GetAsync(DownloadLinuxUrl);

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

        return true;
    }

    public static class AppLocator
    {
        private const string AppIdKey = "{918BA46A-FAB8-460C-9DFF-AE691E1C865B}_is1";
        private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static string? GetArchipelagoPath()
        {
                foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey($@"{UninstallRoot}\{AppIdKey}");
                    var path = key?.GetValue("InstallLocation") as string
                            ?? key?.GetValue("Inno Setup: App Path") as string;
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        return path;
                }

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
                using var cmdKey = baseKey.OpenSubKey(@"archipelago\shell\open\command");
                var cmd = cmdKey?.GetValue(null) as string;
                var exe = ExtractFirstQuotedPath(cmd);
                if (!string.IsNullOrEmpty(exe))
                {
                    var dir = Path.GetDirectoryName(exe);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        return dir;
                }
            }

            return GetFallback();
        }

        private static string? GetFallback()
        {
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Archipelago");
            return Directory.Exists(fallback) ? fallback : null;
        }

        private static string? ExtractFirstQuotedPath(string? cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return null;
            int a = cmd.IndexOf('"');
            if (a < 0) return null;
            int b = cmd.IndexOf('"', a + 1);
            if (b < 0) return null;
            return cmd.Substring(a + 1, b - a - 1);
        }
    }
}
