using Discord;
using Discord.WebSocket;
using ArchipelagoSphereTracker.src.Resources;

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
        if (attachment == null ||
            attachment.Size <= 0 ||
            attachment.Size > Declare.WebPortalMaxUploadBytes ||
            !FileUploadSecurity.TryGetSafeFileName(
                attachment.Filename,
                [".txt", ".json"],
                out var safeName))
        {
            return Resource.SpoilerLogInvalidFile;
        }

        using var response = await Declare.HttpClient.GetAsync(
            attachment.Url,
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return Resource.SpoilerLogDownloadFailed;
        }
        if (response.Content.Headers.ContentLength is { } contentLength &&
            contentLength > Declare.WebPortalMaxUploadBytes)
        {
            return Resource.SpoilerLogTooLarge;
        }

        return await SendSpoilerLogFromStreamAsync(
            channelId,
            safeName,
            await response.Content.ReadAsStreamAsync()).ConfigureAwait(false);
    }

    public static async Task<string> SendSpoilerLogFromStreamAsync(
        string channelId,
        string fileName,
        Stream content)
    {
        if (!FileUploadSecurity.TryGetSafeFileName(
                fileName,
                [".txt", ".json"],
                out var safeName))
        {
            return Resource.SpoilerLogInvalidFile;
        }

        var folder = GetSpoilerFolder(channelId);
        var path = Path.Combine(folder, safeName);

        var isJson = safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        var accepted = await FileUploadSecurity.CopyValidatedToFileWithLimitAsync(
            content,
            path,
            Declare.WebPortalMaxUploadBytes,
            quarantinePath => FileUploadSecurity.IsValidSpoilerFile(quarantinePath, isJson));
        if (!accepted)
        {
            return Resource.SpoilerLogInvalidContent;
        }

        foreach (var existingFile in Directory.EnumerateFiles(folder)
                     .Where(existingFile => !string.Equals(
                         Path.GetFullPath(existingFile),
                         Path.GetFullPath(path),
                         StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(existingFile);
        }

        return string.Format(Resource.SpoilerLogReceived, safeName);
    }

}
