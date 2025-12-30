using ArchipelagoSphereTracker.src.Resources;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

public static class CheckUpdate
{
    private static readonly HttpClient _http = CreateHttpClient();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record Release(string tag_name, Asset[] assets);
    public sealed record Asset(string name, string browser_download_url);

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public static async Task CheckAsync(
        string owner = "Etsuna",
        string repo = "ArchipelagoSphereTracker",
        Action<string>? notify = null)
    {
        var env = Environment.GetEnvironmentVariable("UPDATE_CHECK");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
            return;

        notify ??= Console.WriteLine;

        try
        {
            var (rel, error) = await GetLatestReleaseAsync(owner, repo, CancellationToken.None);
            if (rel is null)
            {
                if (!string.IsNullOrWhiteSpace(error))
                    notify($"[UpdateCheck] {error}");
                return;
            }

            var current = GetLocalSemVer();
            var latest = Normalize(rel.tag_name);

            if (IsNewer(latest, current))
            {
                var asset = PickAsset(rel.assets);
                var msg =
                    $"{Resource.UpdateAvailable} {current} → {latest}" +
                    (asset is null ? "" : $"\n{Resource.DownloadUpdate} {asset.browser_download_url}");
                notify(msg);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            notify($"[UpdateCheck] Exception ignorée: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static async Task<(bool newer, string current, string latest, Asset? asset)> TryGetLatestAsync(
        string owner = "Etsuna",
        string repo = "ArchipelagoSphereTracker",
        CancellationToken ct = default)
    {
        var current = GetLocalSemVer();

        var env = Environment.GetEnvironmentVariable("UPDATE_CHECK");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
            return (false, current, current, null);

        try
        {
            var (rel, _) = await GetLatestReleaseAsync(owner, repo, ct);
            if (rel is null)
                return (false, current, current, null);

            var latest = Normalize(rel.tag_name);
            var asset = PickAsset(rel.assets);

            return (IsNewer(latest, current), current, latest, asset);
        }
        catch (OperationCanceledException)
        {
            return (false, current, current, null);
        }
        catch
        {
            return (false, current, current, null);
        }
    }

    private static async Task<(Release? release, string? error)> GetLatestReleaseAsync(
        string owner, string repo, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var req = BuildGitHubRequest(url);

            HttpResponseMessage? res = null;
            try
            {
                res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                if (IsRateLimited(res, out var retryIn, out var resetAt))
                {
                    var msg = retryIn is not null
                        ? $"GitHub rate limit: réessayer dans {Math.Ceiling(retryIn.Value.TotalSeconds)}s."
                        : resetAt is not null
                            ? $"GitHub rate limit: réessayer après {resetAt:O}."
                            : "GitHub rate limit: réessayer plus tard.";

                    return (null, msg);
                }

                if ((int)res.StatusCode >= 500 || (int)res.StatusCode == 429)
                {
                    var delay = Backoff(attempt, res);
                    await Task.Delay(delay, ct);
                    continue;
                }

                if (!res.IsSuccessStatusCode)
                {
                    var body = await SafeReadBody(res, ct);
                    return (null, $"Update check HTTP {(int)res.StatusCode} {res.ReasonPhrase}{(string.IsNullOrWhiteSpace(body) ? "" : $": {body}")}");
                }

                var json = await res.Content.ReadAsStringAsync(ct);
                var rel = JsonSerializer.Deserialize<Release>(json, _jsonOptions);
                if (rel is null)
                    return (null, "Invalid JSON from GitHub.");

                return (rel, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                var delay = Backoff(attempt, res);
                if (attempt < 2)
                {
                    await Task.Delay(delay, ct);
                    continue;
                }

                return (null, $"Network error: {ex.Message}");
            }
            finally
            {
                res?.Dispose();
            }
        }

        return (null, "Update check failed after retries.");
    }

    private static HttpRequestMessage BuildGitHubRequest(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.UserAgent.ParseAdd("AST/1");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        return req;
    }

    private static bool IsRateLimited(HttpResponseMessage res, out TimeSpan? retryIn, out DateTimeOffset? resetAt)
    {
        retryIn = null;
        resetAt = null;

        if ((int)res.StatusCode == 429 && res.Headers.RetryAfter is not null)
        {
            if (res.Headers.RetryAfter.Delta is not null)
                retryIn = res.Headers.RetryAfter.Delta;

            if (res.Headers.RetryAfter.Date is not null)
                resetAt = res.Headers.RetryAfter.Date;

            return true;
        }

        if (res.StatusCode == HttpStatusCode.Forbidden &&
            res.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) &&
            remaining.FirstOrDefault() == "0")
        {
            if (res.Headers.TryGetValues("X-RateLimit-Reset", out var reset) &&
                long.TryParse(reset.FirstOrDefault(), out var unix))
            {
                resetAt = DateTimeOffset.FromUnixTimeSeconds(unix);
                var delta = resetAt.Value - DateTimeOffset.UtcNow;
                if (delta > TimeSpan.Zero)
                    retryIn = delta;
            }

            return true;
        }

        return false;
    }

    private static TimeSpan Backoff(int attempt, HttpResponseMessage? res)
    {
        if (res?.Headers.RetryAfter?.Delta is not null)
            return res.Headers.RetryAfter.Delta.Value;

        var seconds = 1 << attempt;
        return TimeSpan.FromSeconds(Math.Min(seconds, 10));
    }

    private static async Task<string?> SafeReadBody(HttpResponseMessage res, CancellationToken ct)
    {
        try
        {
            var s = await res.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= 300 ? s : s[..300] + "…";
        }
        catch
        {
            return null;
        }
    }

    public static string GetLocalSemVer()
    {
        var asm = Assembly.GetEntryAssembly()!;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info)
            ? asm.GetName().Version?.ToString() ?? "0.0.0"
            : Normalize(info);
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
