using Discord;
using Discord.WebSocket;

public static class SpoilerLogClass
{
    public static string GetSpoilerFolder(string channelId)
        => Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", channelId, "spoiler");

    public static string? GetLatestSpoilerPath(string channelId)
    {
        var folder = GetSpoilerFolder(channelId);
        if (!Directory.Exists(folder)) return null;

        CleanupExpiredSpoilerLogs(channelId);

        return Directory.EnumerateFiles(folder)
            .Where(file => file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static async Task<string> SendSpoilerLog(SocketSlashCommand command, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null ||
            attachment.Size <= 0 ||
            attachment.Size > Declare.WebPortalMaxUploadBytes ||
            !FileUploadSecurity.TryGetSafeFileName(
                attachment.Filename,
                [".txt", ".json"],
                out var safeName))
        {
            return "Fichier spoiler invalide ou trop volumineux. Envoie un fichier .txt ou .json.";
        }

        var folder = GetSpoilerFolder(channelId);
        var path = Path.Combine(folder, safeName);

        using var response = await Declare.HttpClient.GetAsync(
            attachment.Url,
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return "Téléchargement du spoiler log impossible.";
        }
        if (response.Content.Headers.ContentLength is { } contentLength &&
            contentLength > Declare.WebPortalMaxUploadBytes)
        {
            return "Fichier spoiler trop volumineux.";
        }

        var isJson = safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        var accepted = await FileUploadSecurity.CopyValidatedToFileWithLimitAsync(
            await response.Content.ReadAsStreamAsync(),
            path,
            Declare.WebPortalMaxUploadBytes,
            quarantinePath => FileUploadSecurity.IsValidSpoilerFile(quarantinePath, isJson));
        if (!accepted)
        {
            return "Contenu du spoiler log invalide.";
        }

        foreach (var existingFile in Directory.EnumerateFiles(folder)
                     .Where(existingFile => !string.Equals(
                         Path.GetFullPath(existingFile),
                         Path.GetFullPath(path),
                         StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(existingFile);
        }

        return $"Spoiler log reçu: {safeName}";
    }

    public static int CleanupExpiredSpoilerLogs(
        string channelId,
        DateTimeOffset? now = null,
        TimeSpan? retention = null,
        string? folderPath = null)
    {
        var folder = folderPath ?? GetSpoilerFolder(channelId);
        if (!Directory.Exists(folder)) return 0;

        var cutoff = (now ?? DateTimeOffset.UtcNow) -
                     (retention ?? TimeSpan.FromDays(Declare.SpoilerLogRetentionDays));
        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in files)
        {
            if (!file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime)
                    continue;
                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return removed;
    }
}
