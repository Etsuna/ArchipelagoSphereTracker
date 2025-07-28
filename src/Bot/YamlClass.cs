using Discord;
using Discord.WebSocket;
using System.IO.Compression;
using System.Text;

public class YamlClass
{
    public static async Task<string> DownloadTemplate(SocketSlashCommand command, string message)
    {
        var yamlFile = command.Data.Options.FirstOrDefault()?.Value as string;

        if (string.IsNullOrEmpty(yamlFile))
        {
            return "❌ Aucun fichier sélectionné.";
        }

        string templatePath = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", "Templates", yamlFile);

        if (File.Exists(templatePath))
        {
            await command.FollowupWithFileAsync(templatePath, yamlFile);
        }
        else
        {
            message = "❌ le fichier n'existe pas !";
        }

        return message;
    }

    public static async Task<string> SendYaml(SocketSlashCommand command, string message, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null || !attachment.Filename.EndsWith(".yaml"))
        {
            return "❌ Vous devez envoyer un fichier YAML !";
        }

        var playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

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
                message = $"Fichier `{attachment.Filename}` envoyé.";
            }
            else
            {
                message = "❌ Échec du téléchargement du fichier.";
            }

        return message;
    }

    public static string CleanYamls(string channelId)
    {
        string message;
        var playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId);
        if (Directory.Exists(playersFolderChannel))
        {
            try
            {
                Directory.Delete(playersFolderChannel, true);
                message = "Tous les fichiers YAML ont été supprimés.";
            }
            catch (IOException ex)
            {
                message = $"Erreur lors de la suppression des fichiers : {ex.Message}";
            }
        }
        else
        {
            message = "Aucun fichier YAML trouvé.";
        }

        return message;
    }

    public static string DeleteYaml(SocketSlashCommand command, string message, string channelId)
    {
        var fileSelected = command.Data.Options.FirstOrDefault()?.Value as string;
        var playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!string.IsNullOrEmpty(fileSelected))
        {
            var deletedfilePath = Path.Combine(playersFolderChannel, fileSelected);

            if (File.Exists(deletedfilePath))
            {
                try
                {
                    File.Delete(deletedfilePath);
                    message += $"Le fichier `{fileSelected}` a été supprimé avec succès. ✅";
                }
                catch (Exception ex)
                {
                    message += $"Erreur lors de la suppression du fichier `{fileSelected}`: {ex.Message} ❌";
                }
            }
            else
            {
                message += $"Le fichier `{fileSelected}` n'existe pas. ❌";
            }
        }
        else
        {
            message += "Aucun fichier sélectionné. ❌";
        }

        return message;
    }

    public static async Task<string> BackupYamls(SocketSlashCommand command, string message, string channelId)
    {
        var playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
        if (Directory.Exists(playersFolderChannel))
        {
            var backupFolder = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "backup");
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
            message += "❌ Aucun fichier YAML trouvé !";
        }

        return message;
    }

    public static string ListYamls(string channelId)
    {
        var playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

        if (!Directory.Exists(playersFolderChannel))
            return "❌ Aucun fichier YAML trouvé !";

        var yamls = Directory.EnumerateFiles(playersFolderChannel, "*.yaml")
                             .OrderBy(Path.GetFileName)
                             .ToList();

        if (!yamls.Any())
            return "❌ Aucun fichier YAML trouvé !";

        var sb = new StringBuilder("Liste de Yamls\n");
        foreach (var yml in yamls)
        {
            sb.AppendLine(Path.GetFileName(yml));
        }

        return sb.ToString();
    }
}
