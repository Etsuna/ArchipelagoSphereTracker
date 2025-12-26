using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.IO.Compression;
using System.Text;

public class ApworldClass : Declare
{
    public static async Task<string> SendApworld(SocketSlashCommand command)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".apworld"))
        {
            return Resource.ApworldWrongFile;
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
        var message = string.Format(Resource.ApworldFileSent, attachment.Filename);
        return message;
    }

    public static async Task<string> BackupApworld(SocketSlashCommand command)
    {
        var message = string.Empty;
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
            message += Resource.ApworldFileNotFound;
        }

        return message;
    }

    public static async Task<string> ApworldsInfo(SocketSlashCommand command)
    {
        var infoSelected = command.Data.Options.FirstOrDefault()?.Value as string;

        if (infoSelected == null)
        {
            return Resource.NoFileSelected;
        }

        var message = await ApWorldListCommands.GetItemsByTitleAsync(infoSelected) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            return Resource.NoFileSelected;
        }
        return message;
    }

    public static string ListApworld()
    {
        var message = string.Empty;
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
                var sb = new StringBuilder(Resource.ApworldList);
                sb.AppendLine();
                foreach (var apworld in listApworld)
                {
                    sb.AppendLine($"`{Path.GetFileName(apworld)}`");
                }
                message += sb.ToString();
            }
            else
            {
                message += Resource.ApworldNotFound;
            }
        }
        else
        {
            message += Resource.ApworldCustomFolderNotFound;
        }
        return message;
    }
}
