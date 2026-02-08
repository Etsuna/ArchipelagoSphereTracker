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

        var token = await PortalAccessCommands.EnsurePortalTokenAsync(guildId, channelId, userId);
        var userFolder = GetUserFolder(guildId, channelId, token);

        Directory.CreateDirectory(userFolder);

        var htmlPath = Path.Combine(userFolder, "index.html");
        var html = WebPortalUserPage.Build(guildId, channelId, token);
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);

        return GetUserPortalUrl(guildId, channelId, token);
    }

    public static async Task<string?> EnsureThreadCommandsPageAsync(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
            return null;

        Directory.CreateDirectory(Declare.WebPortalPath);

        var htmlPath = Path.Combine(Declare.WebPortalPath, "thread-commands.html");
        var html = WebPortalThreadCommandsPage.Build();
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);

        return GetThreadCommandsPortalUrl(guildId, channelId);
    }

    public static async Task<string?> EnsureCommandsPageAsync(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
            return null;

        Directory.CreateDirectory(Declare.WebPortalPath);

        var htmlPath = Path.Combine(Declare.WebPortalPath, "commands.html");
        var html = WebPortalCommandsPage.Build();
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);

        return GetCommandsPortalUrl(guildId, channelId);
    }

    public static async Task EnsureMissingUserPagesAsync()
    {
        if (!Declare.EnableWebPortal)
            return;

        var users = await RecapListCommands.GetPortalUsersAsync();
        foreach (var (guildId, channelId, userId) in users)
            await EnsureUserPageIfMissingAsync(guildId, channelId, userId);
    }

    private static async Task EnsureUserPageIfMissingAsync(string guildId, string channelId, string userId)
    {
        var token = await PortalAccessCommands.EnsurePortalTokenAsync(guildId, channelId, userId);
        var userFolder = GetUserFolder(guildId, channelId, token);
        Directory.CreateDirectory(userFolder);

        var htmlPath = Path.Combine(userFolder, "index.html");
        if (File.Exists(htmlPath))
            return;

        var html = WebPortalUserPage.Build(guildId, channelId, token);
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);
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

    private static string GetCommandsPortalUrl(string guildId, string channelId)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/commands.html";
    }

    private static string GetThreadCommandsPortalUrl(string guildId, string channelId)
    {
        var baseUrl = GetPortalBaseUrl();
        return $"{baseUrl}/portal/{guildId}/{channelId}/thread-commands.html";
    }

    private static string GetUserFolder(string guildId, string channelId, string token)
    {
        return Path.Combine(Declare.WebPortalPath, guildId, channelId, token);
    }
}
