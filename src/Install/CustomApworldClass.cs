using ArchipelagoSphereTracker.src.Resources;
using System.Diagnostics;
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
                Arguments = "\"Generate Template Options\" -- --skip_open_folder",
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
}
