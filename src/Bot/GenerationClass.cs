using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;


public class GenerationClass : Declare
{
    private static string GetLauncherPath()
    {
        var launcher = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "ArchipelagoGenerate.exe"
            : "ArchipelagoGenerate";

        return Path.Combine(ExtractPath, launcher);
    }

    private static ProcessStartInfo CreateProcessStartInfo(string launcherPath, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = launcherPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task RunGenerationProcessAsync(ProcessStartInfo startInfo, SocketSlashCommand command, string? outputFolder = null, string? playersFolder = null)
    {
        bool errorDetected = false;
        StringBuilder errorMessage = new();

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"🟢 **Log** : {e.Data}");
                    if (e.Data.Contains("Opening file input dialog"))
                    {
                        errorMessage.AppendLine($"❌ **Error** : {e.Data}");
                        errorDetected = true;
                        if (!process.HasExited) process.Kill();
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorMessage.AppendLine($"❌ **Error** : {e.Data}");
                    if (e.Data.Contains("ValueError") || e.Data.Contains("Exception") || e.Data.Contains("FileNotFoundError"))
                    {
                        errorDetected = true;
                        if (!process.HasExited) process.Kill();
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(600000) && !errorDetected)
            {
                if (!process.HasExited) process.Kill();
                errorMessage.AppendLine("⏳ Timeout: Process stopped after 10 minutes.");
                errorDetected = true;
            }
        }

        if (errorDetected)
        {
            await command.FollowupAsync(errorMessage.ToString());
            return;
        }

        if (!string.IsNullOrEmpty(outputFolder))
        {
            if (!Directory.Exists(outputFolder))
            {
                await command.FollowupAsync($"❌ The output folder {outputFolder} does not exist.");
                return;
            }

            var zipFile = Directory.GetFiles(outputFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (zipFile != null)
            {
                var zipFileName = Path.GetFileName(zipFile);
                var zipFilePath = Path.GetFullPath(zipFile);

                ZipFile.ExtractToDirectory(zipFilePath, outputFolder);
                await command.FollowupWithFileAsync(zipFile, zipFileName);
            }
            else
            {
                await command.FollowupAsync("❌ No ZIP file found.");
            }

            if (playersFolder != null) Directory.Delete(playersFolder, true);
            Directory.Delete(outputFolder, true);
        }
        else
        {
            await command.FollowupAsync("✅ Test generation successful!");
        }
    }

    public static async Task<string> GenerateWithZip(SocketSlashCommand command, string message, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".zip"))
            return "❌ You must send a ZIP file containing the YAML files!";

        var basePath = Path.Combine(BasePath, "extern", "Archipelago");
        var playersFolder = Path.Combine(basePath, "Players", channelId, "zip");
        var outputFolder = Path.Combine(basePath, "output", channelId, "zip");
        var filePath = Path.Combine(playersFolder, attachment.Filename);

        if (Directory.Exists(playersFolder)) Directory.Delete(playersFolder, true);
        if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);

        Directory.CreateDirectory(playersFolder);

        using var response = await Declare.HttpClient.GetAsync(attachment.Url);
        using var fs = new FileStream(filePath, FileMode.Create);
        await response.Content.CopyToAsync(fs);

        ZipFile.ExtractToDirectory(filePath, playersFolder);

        foreach (var file in Directory.GetFiles(playersFolder))
        {
            if (!file.EndsWith(".yaml"))
            {
                var fileName = Path.GetFileName(file);
                await command.FollowupAsync($"ℹ️ Info: {fileName} is not a YAML file. It was deleted before generation.");
                File.Delete(file);
            }
        }

        File.Delete(filePath);

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
        {
            await command.FollowupAsync("❌ No YAML file found in the archive!");
        }

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        _ = RunGenerationProcessAsync(startInfo, command, outputFolder, playersFolder);

        return message;
    }

    public static string TestGenerate(SocketSlashCommand command, string message, string channelId)
    {
        var basePath = Path.Combine(BasePath, "extern", "Archipelago");
        var playersFolder = Path.Combine(basePath, "Players", channelId, "yaml");

        Directory.CreateDirectory(playersFolder);

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
            return "❌ No YAML file found!";

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --skip_output";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        _ = RunGenerationProcessAsync(startInfo, command);

        return message;
    }

    public static string Generate(SocketSlashCommand command, string message, string channelId)
    {
        var basePath = Path.Combine(BasePath, "extern", "Archipelago");
        var playersFolder = Path.Combine(basePath, "Players", channelId, "yaml");
        var outputFolder = Path.Combine(basePath, "output", channelId, "yaml");

        if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);

        Directory.CreateDirectory(playersFolder);

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
            return "❌ No YAML file found!";

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        _ = RunGenerationProcessAsync(startInfo, command, outputFolder, playersFolder);

        return message;
    }
}
