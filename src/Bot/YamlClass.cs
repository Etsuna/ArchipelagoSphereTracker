using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.IO.Compression;
using System.Text;

public class YamlClass : Declare
{
    public static async Task<string> DownloadTemplate(SocketSlashCommand command)
    {
        var yamlFile = command.Data.Options.FirstOrDefault()?.Value as string;
        var message = string.Empty;

        if (!FileUploadSecurity.TryGetSafeFileName(yamlFile, ".yaml", out var safeFileName))
        {
            return Resource.NoFileSelected;
        }

        string templatePath = Path.Combine(BasePath, "extern", "Archipelago", "Players", "Templates", safeFileName);

        if (File.Exists(templatePath))
        {
            await command.FollowupWithFileAsync(templatePath, safeFileName);
        }
        else
        {
            message = Resource.YamlFileNotExists;
        }

        return message;
    }

    public static async Task<string> SendYaml(SocketSlashCommand command, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        var message = string.Empty;
        if (attachment == null ||
            attachment.Size <= 0 ||
            attachment.Size > Declare.WebPortalMaxUploadBytes ||
            !FileUploadSecurity.TryGetSafeFileName(attachment.Filename, ".yaml", out var safeFileName))
        {
            return Resource.YamlWrongFile;
        }

        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!Directory.Exists(playersFolderChannel))
        {
            Directory.CreateDirectory(playersFolderChannel);
        }

        string filePath = Path.Combine(playersFolderChannel, safeFileName);

        using (var response = await HttpClient.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead))
            if (response.IsSuccessStatusCode)
            {
                var accepted = await FileUploadSecurity.CopyValidatedToFileWithLimitAsync(
                    await response.Content.ReadAsStreamAsync(),
                    filePath,
                    Declare.WebPortalMaxUploadBytes,
                    FileUploadSecurity.IsSafeTextFile);
                if (!accepted)
                    return Resource.YamlWrongFile;
                message = string.Format(Resource.YamlFileSent, safeFileName);
            }
            else
            {
                message = Resource.YamlFileDownloadFailed;
            }

        return message;
    }

    public static async Task<string> SendYamlFromStreamAsync(string channelId, string fileName, Stream content)
    {
        if (!FileUploadSecurity.TryGetSafeFileName(fileName, ".yaml", out var safeFileName))
        {
            return Resource.YamlWrongFile;
        }

        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        Directory.CreateDirectory(playersFolderChannel);

        string filePath = Path.Combine(playersFolderChannel, safeFileName);
        var accepted = await FileUploadSecurity.CopyValidatedToFileWithLimitAsync(
            content,
            filePath,
            Declare.WebPortalMaxUploadBytes,
            FileUploadSecurity.IsSafeTextFile);
        if (!accepted)
            return Resource.YamlWrongFile;

        return string.Format(Resource.YamlFileSent, safeFileName);
    }

    public static string CleanYamls(string channelId)
    {
        string message;
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId);
        if (Directory.Exists(playersFolderChannel))
        {
            try
            {
                Directory.Delete(playersFolderChannel, true);
                message = Resource.YamlDeleteAllFiles;
            }
            catch (IOException ex)
            {
                message = string.Format(Resource.YamlDeleteAllFilesError, ex.Message);
            }
        }
        else
        {
            message = Resource.YamlNotFound;
        }

        return message;
    }

    public static string DeleteYaml(SocketSlashCommand command, string channelId)
    {
        var fileSelected = command.Data.Options.FirstOrDefault()?.Value as string;
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        var message = string.Empty;

        if (FileUploadSecurity.TryGetSafeFileName(fileSelected, ".yaml", out var safeFileName))
        {
            var deletedfilePath = Path.Combine(playersFolderChannel, safeFileName);

            if (File.Exists(deletedfilePath))
            {
                try
                {
                    File.Delete(deletedfilePath);
                    message += string.Format(Resource.YamlFileDeleted, safeFileName);
                }
                catch (Exception ex)
                {
                    message += string.Format(Resource.YamlFileDeletedError, safeFileName, ex.Message);
                }
            }
            else
            {
                message += string.Format(Resource.YamlDeleteFileNotExists, safeFileName);
            }
        }
        else
        {
            message += Resource.NoFileSelected;
        }

        return message;
    }

    public static string DeleteYamlByName(string channelId, string? fileSelected)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        var message = string.Empty;

        if (FileUploadSecurity.TryGetSafeFileName(fileSelected, ".yaml", out var safeFileName))
        {
            var deletedfilePath = Path.Combine(playersFolderChannel, safeFileName);

            if (File.Exists(deletedfilePath))
            {
                try
                {
                    File.Delete(deletedfilePath);
                    message += string.Format(Resource.YamlFileDeleted, safeFileName);
                }
                catch (Exception ex)
                {
                    message += string.Format(Resource.YamlFileDeletedError, safeFileName, ex.Message);
                }
            }
            else
            {
                message += string.Format(Resource.YamlDeleteFileNotExists, safeFileName);
            }
        }
        else
        {
            message += Resource.NoFileSelected;
        }

        return message;
    }

    public static async Task<string> BackupYamls(SocketSlashCommand command, string channelId)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        var message = string.Empty;
        if (Directory.Exists(playersFolderChannel))
        {
            var backupFolder = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "backup");
            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
            }

            var zipPath = Path.Combine(backupFolder, $"backup_yaml_{channelId}.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(playersFolderChannel, "*.yaml");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    zipArchive.CreateEntryFromFile(file, fileName);
                }
            }

            await command.FollowupWithFileAsync(zipPath, $"backup_yaml_{channelId}.zip");

            File.Delete(zipPath);
        }
        else
        {
            message += Resource.YamlNoYaml;
        }

        return message;
    }

    public static async Task<string> BackupYamlsToFileAsync(string channelId, string zipPath)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        var message = string.Empty;
        if (Directory.Exists(playersFolderChannel))
        {
            var zipDirectory = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(zipDirectory))
            {
                Directory.CreateDirectory(zipDirectory);
            }

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(playersFolderChannel, "*.yaml");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    zipArchive.CreateEntryFromFile(file, fileName);
                }
            }
        }
        else
        {
            message += Resource.YamlNoYaml;
        }

        return message;
    }

    public static string DownloadTemplateToFile(string templateName, string destinationPath)
    {
        if (!FileUploadSecurity.TryGetSafeFileName(templateName, ".yaml", out var safeFileName))
        {
            return Resource.NoFileSelected;
        }

        string templatePath = Path.Combine(BasePath, "extern", "Archipelago", "Players", "Templates", safeFileName);

        if (File.Exists(templatePath))
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(templatePath, destinationPath, overwrite: true);
            return string.Empty;
        }

        return Resource.YamlFileNotExists;
    }

    public static string ListYamls(string channelId)
    {
        var yamls = GetYamlFileNames(channelId).ToList();

        if (!yamls.Any())
            return Resource.YamlNoYaml;

        var sb = new StringBuilder(Resource.YamlList);
        sb.AppendLine();
        foreach (var yml in yamls)
        {
            sb.AppendLine(yml);
        }

        return sb.ToString();
    }

    public static IReadOnlyList<string> GetYamlFileNames(string channelId)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!Directory.Exists(playersFolderChannel))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(playersFolderChannel, "*.yaml")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }
}
