using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

public class GenerationClass : Declare
{
    public record GenerationResult(string Message, string? ZipPath);

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

    private static async Task<string> RunGenerationProcessAsync(ProcessStartInfo startInfo, SocketSlashCommand command, string? outputFolder = null, string? playersFolder = null)
    {
        bool errorDetected = false;
        StringBuilder errorMessage = new();
        var timeout = TimeSpan.FromMinutes(30);

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(string.Format(Resource.GenerationLog, e.Data));
                    if (e.Data.Contains("Opening file input dialog"))
                    {
                        errorMessage.AppendLine(string.Format(Resource.GenerationError, e.Data));
                        errorDetected = true;
                        if (!process.HasExited) process.Kill();
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorMessage.AppendLine(string.Format(Resource.GenerationError, e.Data));
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

            if (!process.WaitForExit((int)timeout.TotalMilliseconds) && !errorDetected)
            {
                if (!process.HasExited) process.Kill();
                errorMessage.AppendLine(Resource.GenerationTimeout);
                errorDetected = true;
            }
        }

        if (errorDetected)
        {
            return errorMessage.ToString();
        }

        if (!string.IsNullOrEmpty(outputFolder))
        {
            if (!Directory.Exists(outputFolder))
            {
                return string.Format(Resource.GenerationOutputFolderNotExists, outputFolder);
            }

            var zipFile = Directory.GetFiles(outputFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (zipFile != null)
            {
                var zipFileName = Path.GetFileName(zipFile);
                var zipFilePath = Path.GetFullPath(zipFile);

                await command.FollowupWithFileAsync(zipFile, zipFileName);
            }
            else
            {
                return Resource.GenerationZipNotFound;
            }
        }
        else
        {
            return Resource.GenerationTestSuccessful;
        }

        return string.Empty;
    }

    private static async Task<GenerationResult> RunGenerationProcessForWebAsync(ProcessStartInfo startInfo, string? outputFolder = null)
    {
        bool errorDetected = false;
        StringBuilder errorMessage = new();
        var timeout = TimeSpan.FromMinutes(30);

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(string.Format(Resource.GenerationLog, e.Data));
                    if (e.Data.Contains("Opening file input dialog"))
                    {
                        errorMessage.AppendLine(string.Format(Resource.GenerationError, e.Data));
                        errorDetected = true;
                        if (!process.HasExited) process.Kill();
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorMessage.AppendLine(string.Format(Resource.GenerationError, e.Data));
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

            if (!process.WaitForExit((int)timeout.TotalMilliseconds) && !errorDetected)
            {
                if (!process.HasExited) process.Kill();
                errorMessage.AppendLine(Resource.GenerationTimeout);
                errorDetected = true;
            }
        }

        if (errorDetected)
        {
            return new GenerationResult(errorMessage.ToString(), null);
        }

        if (!string.IsNullOrEmpty(outputFolder))
        {
            if (!Directory.Exists(outputFolder))
            {
                return new GenerationResult(string.Format(Resource.GenerationOutputFolderNotExists, outputFolder), null);
            }

            var zipFile = Directory.GetFiles(outputFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (zipFile != null)
            {
                return new GenerationResult(string.Empty, zipFile);
            }

            return new GenerationResult(Resource.GenerationZipNotFound, null);
        }

        return new GenerationResult(Resource.GenerationTestSuccessful, null);
    }

    public static async Task<string> GenerateWithZip(SocketSlashCommand command, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".zip"))
            return Resource.GenerationWrongZipFormat;

        var playersFolder = Path.Combine(PlayersPath, channelId, "zip");
        var outputFolder = Path.Combine(OutputPath, channelId);
        var filePath = Path.Combine(playersFolder, attachment.Filename);

        if (Directory.Exists(playersFolder)) Directory.Delete(playersFolder, true);
        if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);

        Directory.CreateDirectory(playersFolder);

        using (var response = await HttpClient.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs);
            }
        }

        ZipFile.ExtractToDirectory(filePath, playersFolder, true);

        File.Delete(filePath);

        foreach (var file in Directory.GetFiles(playersFolder))
        {
            if (!file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(file);
                await command.FollowupAsync(string.Format(Resource.GenerationNotAYamlFileIntoZip, fileName) + "\n");
                File.Delete(file);
            }
        }

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
        {
            return Resource.GenerationNoYamlIntoZip;
        }

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        var message = await RunGenerationProcessAsync(startInfo, command, outputFolder, playersFolder);

        return message;
    }

    public static async Task<GenerationResult> GenerateWithZipFromStreamAsync(string channelId, string fileName, Stream content)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new GenerationResult(Resource.GenerationWrongZipFormat, null);
        }

        var playersFolder = Path.Combine(PlayersPath, channelId, "zip");
        var outputFolder = Path.Combine(OutputPath, channelId);
        var filePath = Path.Combine(playersFolder, Path.GetFileName(fileName));

        if (Directory.Exists(playersFolder)) Directory.Delete(playersFolder, true);
        if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);

        Directory.CreateDirectory(playersFolder);

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs);
        }

        ZipFile.ExtractToDirectory(filePath, playersFolder, true);

        File.Delete(filePath);

        foreach (var file in Directory.GetFiles(playersFolder))
        {
            if (!file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                var fileNameEntry = Path.GetFileName(file);
                File.Delete(file);
                return new GenerationResult(string.Format(Resource.GenerationNotAYamlFileIntoZip, fileNameEntry), null);
            }
        }

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
        {
            return new GenerationResult(Resource.GenerationNoYamlIntoZip, null);
        }

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        return await RunGenerationProcessForWebAsync(startInfo, outputFolder);
    }

    public static async Task<string> TestGenerateAsync(SocketSlashCommand command, string channelId)
    {
        var playersFolder = Path.Combine(PlayersPath, channelId, "yaml");

        Directory.CreateDirectory(playersFolder);

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
            return Resource.GenerationNoYaml;

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --skip_output";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        var message = await RunGenerationProcessAsync(startInfo, command);

        return message;
    }

    public static async Task<string> TestGenerateAsyncForWeb(string channelId)
    {
        var playersFolder = Path.Combine(PlayersPath, channelId, "yaml");

        Directory.CreateDirectory(playersFolder);

        if (!Directory.GetFiles(playersFolder, "*.yaml").Any())
            return Resource.GenerationNoYaml;

        var launcherPath = GetLauncherPath();
        var arguments = $"--player_files_path \"{playersFolder}\" --skip_output";
        var startInfo = CreateProcessStartInfo(launcherPath, arguments);

        var result = await RunGenerationProcessForWebAsync(startInfo);
        return result.Message;
    }

    public static async Task<string> GenerateAsync(SocketSlashCommand command, string channelId)
    {
        var playersFolder = Path.Combine(PlayersPath, channelId, "yaml");
        var outputFolder = Path.Combine(OutputPath, channelId);

        try
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);

            Directory.CreateDirectory(playersFolder);
            Directory.CreateDirectory(outputFolder);

            var hasYaml = Directory.EnumerateFiles(playersFolder, "*.yaml").Any()
                       || Directory.EnumerateFiles(playersFolder, "*.yml").Any();

            if (!hasYaml)
                return Resource.GenerationNoYaml;

            var launcherPath = GetLauncherPath();
            if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
                return string.Format(Resource.CALauncherNotFound, launcherPath);

            var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
            var startInfo = CreateProcessStartInfo(launcherPath, arguments);

            var message = await RunGenerationProcessAsync(startInfo, command, outputFolder, playersFolder);
            return message;
        }
        catch (Exception ex)
        {
            return string.Format(Resource.GenerationError, ex.Message);
        }
    }
    public static async Task<GenerationResult> GenerateAsyncForWeb(string channelId)
    {
        var playersFolder = Path.Combine(PlayersPath, channelId, "yaml");
        var outputFolder = Path.Combine(OutputPath, channelId);

        try
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);

            Directory.CreateDirectory(playersFolder);
            Directory.CreateDirectory(outputFolder);

            var hasYaml = Directory.EnumerateFiles(playersFolder, "*.yaml").Any()
                       || Directory.EnumerateFiles(playersFolder, "*.yml").Any();

            if (!hasYaml)
                return new GenerationResult(Resource.GenerationNoYaml, null);

            var launcherPath = GetLauncherPath();
            if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
                return new GenerationResult(string.Format(Resource.CALauncherNotFound, launcherPath), null);

            var arguments = $"--player_files_path \"{playersFolder}\" --outputpath \"{outputFolder}\"";
            var startInfo = CreateProcessStartInfo(launcherPath, arguments);

            return await RunGenerationProcessForWebAsync(startInfo, outputFolder);
        }
        catch (Exception ex)
        {
            return new GenerationResult(string.Format(Resource.GenerationError, ex.Message), null);
        }
    }
}