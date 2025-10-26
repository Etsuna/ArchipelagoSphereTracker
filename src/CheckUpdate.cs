using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

public static class CheckUpdate
{
    private static readonly HttpClient _http = new();

    public sealed record Release(string tag_name, Asset[] assets);
    public sealed record Asset(string name, string browser_download_url);

    public static async Task CheckAsync(
        string owner = "Etsuna",
        string repo = "ArchipelagoSphereTracker",
        Action<string>? notify = null)
    {
        // opt-out possible via .env : UPDATE_CHECK=false
        var env = Environment.GetEnvironmentVariable("UPDATE_CHECK");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase)) return;

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AST", "1"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        using var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        var rel = JsonSerializer.Deserialize<Release>(json) ?? throw new InvalidOperationException("Invalid JSON");

        var current = GetLocalSemVer();
        var latest = Normalize(rel.tag_name);

        if (IsNewer(latest, current))
        {
            var asset = PickAsset(rel.assets);
            var msg =
                $"Mise à jour disponible : {current} → {latest}" +
                (asset is null ? "" : $"\nTéléchargement suggéré : {asset.browser_download_url}");
            (notify ?? Console.WriteLine)(msg);
        }
    }

    public static async Task<(bool newer, string current, string latest, Asset? asset)> TryGetLatestAsync(
        string owner = "Etsuna", string repo = "ArchipelagoSphereTracker", CancellationToken ct = default)
    {
        var env = Environment.GetEnvironmentVariable("UPDATE_CHECK");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
            return (false, GetLocalSemVer(), GetLocalSemVer(), null);

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AST", "1"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var res = await _http.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", ct);
        res.EnsureSuccessStatusCode();

        var rel = JsonSerializer.Deserialize<Release>(await res.Content.ReadAsStringAsync())
                  ?? throw new InvalidOperationException("Invalid JSON");

        var current = GetLocalSemVer();
        var latest = Normalize(rel.tag_name);
        var asset = PickAsset(rel.assets);

        return (IsNewer(latest, current), current, latest, asset);
    }

    private static string GetLocalSemVer()
    {
        // lit <Version>…</Version> comme InformationalVersion
        var asm = Assembly.GetEntryAssembly()!;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? asm.GetName().Version?.ToString() ?? "0.0.0" : Normalize(info);
    }

    private static string Normalize(string v)
        => v.Trim().TrimStart('v', 'V').Split('+', '-', ' ').FirstOrDefault() ?? "0.0.0";

    private static bool IsNewer(string a, string b) 
    {
        Version.TryParse(a, out var va);
        Version.TryParse(b, out var vb);
        return (va ?? new Version(0, 0)) > (vb ?? new Version(0, 0));
    }

    private static Asset? PickAsset(Asset[] assets)
    {
        if (assets == null || assets.Length == 0) return null;

        string? os = OperatingSystem.IsWindows() ? "win"
                    : OperatingSystem.IsLinux() ? "linux"
                    : null;
        if (os is null) return null;

        foreach (var a in assets)
        {
            var n = a?.name?.ToLowerInvariant();
            if (string.IsNullOrEmpty(n)) continue;

            if ((os == "win" && (n.Contains("win") || n.Contains("windows"))) ||
                (os == "linux" && n.Contains("linux")))
                return a;
        }

        return null;
    }
}
