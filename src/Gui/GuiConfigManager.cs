using System.Text;

public static class GuiConfigManager
{
    public static readonly string EnvPath = Path.Combine(AppContext.BaseDirectory, ".env");

    private static readonly Dictionary<string, string> DefaultValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DISCORD_TOKEN"] = "",
        ["LANGUAGE"] = "en",
        ["ENABLE_WEB_PORTAL"] = "true",
        ["WEB_PORT"] = "5199",
        ["WEB_BASE_URL"] = "",
        ["EXPORT_METRICS"] = "false",
        ["METRICS_PORT"] = "",
        ["ALLOW_DISCORD"] = "",
        ["USER_ID_FOR_BIG_ASYNC"] = ""
    };

    public static bool EnsureEnvFileExists()
    {
        if (File.Exists(EnvPath))
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("# ArchipelagoSphereTracker configuration");
        sb.AppendLine("# Generated automatically by --gui mode");
        foreach (var kv in DefaultValues)
            sb.AppendLine($"{kv.Key}={kv.Value}");

        File.WriteAllText(EnvPath, sb.ToString(), Encoding.UTF8);
        return true;
    }

    public static Dictionary<string, string> ReadEnv()
    {
        EnsureEnvFileExists();

        var result = new Dictionary<string, string>(DefaultValues, StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(EnvPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }

    public static void SaveEnv(Dictionary<string, string> values)
    {
        var merged = ReadEnv();
        foreach (var kv in values)
            merged[kv.Key] = kv.Value ?? string.Empty;

        var keys = merged.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# ArchipelagoSphereTracker configuration");
        sb.AppendLine("# Updated from GUI");
        foreach (var key in keys)
            sb.AppendLine($"{key}={merged[key]}");

        File.WriteAllText(EnvPath, sb.ToString(), Encoding.UTF8);
    }
}
