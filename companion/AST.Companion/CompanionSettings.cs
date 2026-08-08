using System.Text.Json;

namespace AST.Companion;

public sealed class CompanionSettings
{
    public string PortalUrl { get; set; } = string.Empty;
    public bool AlwaysOnTop { get; set; } = true;
    public int PollSeconds { get; set; } = 5;

    private static string SettingsPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directory = Path.Combine(root, "AST.Companion");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "settings.json");
        }
    }

    public static async Task<CompanionSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
            return new CompanionSettings();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            return JsonSerializer.Deserialize<CompanionSettings>(json) ?? new CompanionSettings();
        }
        catch
        {
            return new CompanionSettings();
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}
