using ArchipelagoSphereTracker.src.Resources;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

public class CustomApworldClass : Declare
{
    public static void GenerateYamls()
    {
        Console.WriteLine(Resource.CAGeneratingYamlTemplates);

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
                Console.WriteLine(string.Format(Resource.CAEmbededError, GenerateTemplatesPath));
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine(Resource.CAGenerateTemplateApworldError);
                return;
            }

            Console.WriteLine(string.Format(Resource.CAFileCopied, destinationPath));

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine(string.Format(Resource.CALauncherNotFound, launcherPath));
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
                Console.WriteLine(Resource.CAErrorProcess);
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
                Console.WriteLine(Resource.CAYamlGeneratedSuccessfully);
            }
            else
            {
                Console.WriteLine(string.Format(Resource.CAYamlGeneratedError, process.ExitCode));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(Resource.CAException, ex.GetType().Name, ex.Message));
        }
    }


    public static void GenerateItems()
    {
        Console.WriteLine(Resource.CAGeneratedItemJson);

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
                Console.WriteLine(string.Format(Resource.CAEmbededError, ScanItemsPath));
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine(Resource.CAScanItemsApworldError);
                return;
            }

            Console.WriteLine(string.Format(Resource.CAFileCopied, destinationPath));

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine(string.Format(Resource.CALauncherNotFound, launcherPath));
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
                Console.WriteLine(Resource.CAErrorProcess);
                return;
            }

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("⚠️ " + e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine(Resource.CAYamlGeneratedSuccessfully);
            }
            else
            {
                Console.WriteLine(string.Format(Resource.CAYamlGeneratedError, process.ExitCode));
            }

            var jsonFile = Directory.GetFiles(ItemCategoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(jsonFile) && File.Exists(jsonFile))
            {
                ItemsCommands.SyncItemsFromJsonAsync(jsonFile).GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine(Resource.CAJsonNotfound);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(Resource.CAException, ex.GetType().Name, ex.Message));
        }
    }
}
