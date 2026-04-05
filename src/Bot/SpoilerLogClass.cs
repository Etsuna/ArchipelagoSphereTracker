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

        return Directory.EnumerateFiles(folder)
            .Where(file => file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static async Task<string> SendSpoilerLog(SocketSlashCommand command, string channelId)
    {
        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
        if (attachment == null)
        {
            return "Fichier spoiler manquant.";
        }

        if (!attachment.Filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && !attachment.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "Format invalide. Envoie un spoiler log en .txt ou .json.";
        }

        var folder = GetSpoilerFolder(channelId);
        Directory.CreateDirectory(folder);

        foreach (var existingFile in Directory.EnumerateFiles(folder))
        {
            File.Delete(existingFile);
        }

        var safeName = Path.GetFileName(attachment.Filename);
        var path = Path.Combine(folder, safeName);

        using var response = await Declare.HttpClient.GetAsync(attachment.Url);
        if (!response.IsSuccessStatusCode)
        {
            return "Téléchargement du spoiler log impossible.";
        }

        await using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await response.Content.CopyToAsync(fileStream);
        }

        return $"Spoiler log reçu: {safeName}";
    }
}
