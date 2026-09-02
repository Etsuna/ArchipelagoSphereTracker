public static class RoomFileStorage
{
    public static void DeleteChannelData(string guildId, string channelId)
    {
        if (!ulong.TryParse(guildId, out _) || !ulong.TryParse(channelId, out _))
            return;

        DeleteDirectory(Path.Combine(Declare.PlayersPath, channelId));
        DeleteDirectory(Path.Combine(Declare.OutputPath, channelId));
        DeleteDirectory(Path.Combine(Declare.WebPortalDownloadPath, guildId, channelId));
    }

    public static void DeleteGuildData(string guildId, IEnumerable<string> channelIds)
    {
        if (!ulong.TryParse(guildId, out _))
            return;

        foreach (var channelId in channelIds.Distinct(StringComparer.Ordinal))
            DeleteChannelData(guildId, channelId);

        DeleteDirectory(Path.Combine(Declare.WebPortalDownloadPath, guildId));
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Room cleanup] Unable to delete '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[Room cleanup] Unable to delete '{path}': {ex.Message}");
        }
    }
}
