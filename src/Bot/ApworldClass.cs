using Discord;
using Discord.WebSocket;
using System.IO.Compression;
using System.Text;

public class ApworldClass : Declare
{
    public static async Task<string> SendApworld(SocketSlashCommand command, string message)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".apworld"))
        {
            return "❌ You must send an APWORLD file!";
        }

        var customWorldPath = Path.Combine(BasePath, "extern", "Archipelago", "custom_worlds");

        Directory.CreateDirectory(customWorldPath);

        var filePath = Path.Combine(customWorldPath, attachment.Filename);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using (var response = await HttpClient.GetAsync(attachment.Url))
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await response.Content.CopyToAsync(fs);
        }
        CustomApworldClass.GenerateYamls();
        CustomApworldClass.GenerateItems();
        message = $"File {attachment.Filename} sent.";
        return message;
    }

    public static async Task<string> BackupApworld(SocketSlashCommand command, string message)
    {
        var apworldPath = Path.Combine(BasePath, "extern", "Archipelago", "custom_worlds");
        if (Directory.Exists(apworldPath))
        {
            var backupFolder = Path.Combine(BasePath, "extern", "Archipelago", "backup");
            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
            }

            var zipPath = Path.Combine(backupFolder, $"backup_apworld.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(apworldPath, "*.apworld");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    zipArchive.CreateEntryFromFile(file, fileName);
                }
            }

            await command.FollowupWithFileAsync(zipPath, $"backup_apworld.zip");

            File.Delete(zipPath);
        }
        else
        {
            message += "❌ No APWORLD file found!";
        }

        return message;
    }

    public static async Task<string> ApworldsInfo(SocketSlashCommand command, string? message)
    {
        var infoSelected = command.Data.Options.FirstOrDefault()?.Value as string;

        if (infoSelected == null)
        {
            return "❌ No file selected.";
        }

        message = await ApWorldListCommands.GetItemsByTitleAsync(infoSelected);

        if (string.IsNullOrWhiteSpace(message))
        {
            return "❌ No file selected.";
        }
        return message;
    }

    public static string ListApworld(string message)
    {
        string apworldPath = Path.Combine(BasePath, "extern", "Archipelago", "custom_worlds");

        if (Directory.Exists(apworldPath))
        {
            var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    "scan_items.apworld",
                                    "generate_templates.apworld"
                                };

            var listApworld = Directory
                .EnumerateFiles(apworldPath, "*.apworld")
                .Where(path => !excludedFiles.Contains(Path.GetFileName(path)))
                .OrderBy(path => Path.GetFileName(path));

            if (listApworld.Any())
            {
                var sb = new StringBuilder("List of apworld\n");
                foreach (var apworld in listApworld)
                {
                    sb.AppendLine($"`{Path.GetFileName(apworld)}`");
                }
                message += sb.ToString();
            }
            else
            {
                message += "❌ No APWORLD file found. !";
            }
        }
        else
        {
            message += "❌ custom_worlds folder not found.";
        }

        return message;
    }
}
