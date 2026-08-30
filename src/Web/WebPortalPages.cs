using System.Text;

public static class WebPortalPages
{
    private static string GetUserPortalUrl(string guildId, string channelId, string token)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/{token}/";
    }

    public static async Task<string?> EnsureUserPageAsync(string guildId, string channelId, string userId)
    {
        if (!Declare.EnableWebPortal)
            return null;

        var token = await PortalAccessCommands.IssuePortalTokenAsync(guildId, channelId, userId);

        return GetUserPortalUrl(guildId, channelId, token);
    }

    public static async Task<string?> EnsureThreadCommandsPageAsync(string guildId, string channelId, string userId)
    {
        if (!Declare.EnableWebPortal)
            return null;

        await EnsureSharedCommandsPagesAsync();

        var token = await PortalAccessCommands.IssuePortalTokenAsync(guildId, channelId, userId);

        return GetThreadCommandsPortalUrl(guildId, channelId, token);
    }

    public static async Task<string?> EnsureCommandsPageAsync(string guildId, string channelId, string userId)
    {
        if (!Declare.EnableWebPortal)
            return null;

        await EnsureSharedCommandsPagesAsync();

        var token = await PortalAccessCommands.IssuePortalTokenAsync(guildId, channelId, userId);

        return GetCommandsPortalUrl(guildId, channelId, token);
    }

    public static async Task EnsureSharedCommandsPagesAsync()
    {
        if (!Declare.EnableWebPortal)
            return;

        Directory.CreateDirectory(Declare.WebPortalPath);

        var threadCommandsPath = Path.Combine(Declare.WebPortalPath, "thread-commands.html");
        var threadCommandsHtml = WebPortalThreadCommandsPage.Build();
        await File.WriteAllTextAsync(threadCommandsPath, threadCommandsHtml, Encoding.UTF8);

        var commandsPath = Path.Combine(Declare.WebPortalPath, "commands.html");
        var commandsHtml = WebPortalCommandsPage.Build();
        await File.WriteAllTextAsync(commandsPath, commandsHtml, Encoding.UTF8);
    }

    public static Task EnsureMissingUserPagesAsync()
    {
        var deletedCount = DeleteLegacyUserPages(Declare.WebPortalPath);
        if (deletedCount > 0)
            Console.WriteLine($"[Portal] Removed {deletedCount} legacy generated user page directorie(s).");
        return Task.CompletedTask;
    }

    public static int DeleteLegacyUserPages(string portalRoot)
    {
        if (!Directory.Exists(portalRoot))
            return 0;

        var fullRoot = Path.GetFullPath(portalRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPrefix = fullRoot + Path.DirectorySeparatorChar;
        var deletedCount = 0;

        foreach (var guildFolder in Directory.EnumerateDirectories(fullRoot))
        {
            if (!ulong.TryParse(Path.GetFileName(guildFolder), out _))
                continue;

            foreach (var channelFolder in Directory.EnumerateDirectories(guildFolder))
            {
                if (!ulong.TryParse(Path.GetFileName(channelFolder), out _))
                    continue;

                foreach (var tokenFolder in Directory.EnumerateDirectories(channelFolder))
                {
                    var token = Path.GetFileName(tokenFolder);
                    if (!IsLegacyPortalToken(token) ||
                        !File.Exists(Path.Combine(tokenFolder, "index.html")) ||
                        File.GetAttributes(tokenFolder).HasFlag(FileAttributes.ReparsePoint))
                        continue;

                    var fullTokenFolder = Path.GetFullPath(tokenFolder);
                    if (!fullTokenFolder.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.Delete(fullTokenFolder, recursive: true);
                    deletedCount++;
                }
            }
        }

        return deletedCount;
    }

    private static bool IsLegacyPortalToken(string value)
    {
        return value.Length is 32 or 64 && value.All(Uri.IsHexDigit);
    }

    public static void DeleteChannelPages(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
            return;

        var channelFolder = Path.Combine(Declare.WebPortalPath, guildId, channelId);
        if (Directory.Exists(channelFolder))
            Directory.Delete(channelFolder, true);
    }

    public static void DeleteGuildPages(string guildId)
    {
        if (!Declare.EnableWebPortal)
            return;

        var guildFolder = Path.Combine(Declare.WebPortalPath, guildId);
        if (Directory.Exists(guildFolder))
            Directory.Delete(guildFolder, true);
    }

    private static string GetPortalBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Declare.WebPortalBaseUrl))
            return Declare.WebPortalBaseUrl.TrimEnd('/');

        return $"http://localhost:{Declare.WebPortalPort}".TrimEnd('/');
    }

    private static string GetCommandsPortalUrl(string guildId, string channelId, string token)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/{token}/commands.html";
    }

    private static string GetThreadCommandsPortalUrl(string guildId, string channelId, string token)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/{token}/thread-commands.html";
    }

}
