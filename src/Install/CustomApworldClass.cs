using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

public class CustomApworldClass : Declare
{
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
}
