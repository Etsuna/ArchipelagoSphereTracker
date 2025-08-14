using Discord;
using Discord.WebSocket;
using System.IO.Compression;
using System.Text;

public class YamlClass : Declare
{
    public static async Task<string> DownloadTemplate(SocketSlashCommand command, string message)
    {
        var yamlFile = command.Data.Options.FirstOrDefault()?.Value as string;

        if (string.IsNullOrEmpty(yamlFile))
        {
            return "❌ No file selected.";
        }

        string templatePath = Path.Combine(BasePath, "extern", "Archipelago", "Players", "Templates", yamlFile);

        if (File.Exists(templatePath))
        {
            await command.FollowupWithFileAsync(templatePath, yamlFile);
        }
        else
        {
            message = "❌ The file does not exist!";
        }

        return message;
    }

    public static async Task<string> SendYaml(SocketSlashCommand command, string message, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".yaml"))
        {
            return "❌ You must send a YAML file!";
        }

        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!Directory.Exists(playersFolderChannel))
        {
            Directory.CreateDirectory(playersFolderChannel);
        }

        string filePath = Path.Combine(playersFolderChannel, attachment.Filename);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using (var response = await Declare.HttpClient.GetAsync(attachment.Url))
            if (response.IsSuccessStatusCode)
            {
                await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await response.Content.CopyToAsync(fs);
                }
                message = $"File {attachment.Filename} sent.";
            }
            else
            {
                message = "❌ File download failed.";
            }

        return message;
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
                message = "All YAML files have been deleted.";
            }
            catch (IOException ex)
            {
                message = $"Error while deleting the files: {ex.Message}";
            }
        }
        else
        {
            message = "No YAML file found.";
        }

        return message;
    }

    public static string DeleteYaml(SocketSlashCommand command, string message, string channelId)
    {
        var fileSelected = command.Data.Options.FirstOrDefault()?.Value as string;
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!string.IsNullOrEmpty(fileSelected))
        {
            var deletedfilePath = Path.Combine(playersFolderChannel, fileSelected);

            if (File.Exists(deletedfilePath))
            {
                try
                {
                    File.Delete(deletedfilePath);
                    message += $"The file {fileSelected} has been successfully deleted. ✅";
                }
                catch (Exception ex)
                {
                    message += $"Error while deleting the file {fileSelected}: {ex.Message} ❌";
                }
            }
            else
            {
                message += $"The file {fileSelected} does not exist. ❌";
            }
        }
        else
        {
            message += "No file selected. ❌";
        }

        return message;
    }

    public static async Task<string> BackupYamls(SocketSlashCommand command, string message, string channelId)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
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
            message += "❌ No YAML file found!";
        }

        return message;
    }

    public static string ListYamls(string channelId)
    {
        var playersFolderChannel = Path.Combine(BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!Directory.Exists(playersFolderChannel))
            return "❌ No YAML file found!";

        var yamls = Directory.EnumerateFiles(playersFolderChannel, "*.yaml")
                             .OrderBy(Path.GetFileName)
                             .ToList();

        if (!yamls.Any())
            return "❌ No YAML file found!";

        var sb = new StringBuilder("List of Yamls\n");
        foreach (var yml in yamls)
        {
            sb.AppendLine(Path.GetFileName(yml));
        }

        return sb.ToString();
    }
}
