using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

public class CustomApworldClass : Declare
{
    public static void GenerateYamls()
    {
        Console.WriteLine("📦 Generating YAML templates...");

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
                Console.WriteLine("❌ Unable to find the embedded resource:" + GenerateTemplatesPath);
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine("❌ The file 'generate_templates.apworld' was not written correctly.");
                return;
            }

            Console.WriteLine($"✅ File copied to: {destinationPath}");

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine($"❌ Launcher not found: {launcherPath}");
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
                Console.WriteLine("❌ ERROR: Failed to start the build process.");
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
                Console.WriteLine("✅ YAML generated successfully!");
            }
            else
            {
                Console.WriteLine($"❌ ERROR: YAML generation failed (code {process.ExitCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception : {ex.GetType().Name} - {ex.Message}");
        }
    }


    public static void GenerateItems()
    {
        Console.WriteLine("📦 Generating Items Category JSON templates...");

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
                Console.WriteLine("❌ Unable to find the embedded resource:" + ScanItemsPath);
                return;
            }

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
                fileStream.Flush(true);
            }

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine("❌ The file was not written correctly to disk.");
                return;
            }

            Console.WriteLine($"✅ File copied to: {destinationPath}");

            string launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ArchipelagoLauncher.exe"
                : "ArchipelagoLauncher";

            string launcherPath = Path.Combine(ExtractPath, launcher);

            if (!File.Exists(launcherPath))
            {
                Console.WriteLine($"❌ Launcher not found: {launcherPath}");
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
                Console.WriteLine("❌ ERROR: Unable to start the process.");
                return;
            }

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("⚠️ " + e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✅ YAML generated successfully!");
            }
            else
            {
                Console.WriteLine($"❌ ERROR: YAML generation failed (code {process.ExitCode})");
            }

            var jsonFile = Directory.GetFiles(ItemCategoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(jsonFile) && File.Exists(jsonFile))
            {
                ItemsCommands.SyncItemsFromJsonAsync(jsonFile).GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine("⚠️ No JSON file found for synchronization.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception : {ex.GetType().Name} - {ex.Message}");
        }
    }
}
