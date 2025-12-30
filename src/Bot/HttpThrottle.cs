using System.Collections.Concurrent;
using System.Net.Http.Headers;

internal static class HttpThrottle
{
    private sealed class Gate
    {
        public readonly SemaphoreSlim Mutex = new(1, 1);
        public DateTimeOffset NextAllowedUtc = DateTimeOffset.MinValue;
    }

    private static readonly ConcurrentDictionary<string, Gate> _byHost = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string?> GetStringThrottledAsync(
        HttpClient http,
        string url,
        TimeSpan minSpacingPerHost,
        CancellationToken ct,
        int maxAttempts = 3,
        Action<string>? log = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var gate = _byHost.GetOrAdd(uri.Host, _ => new Gate());

        await gate.Mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (gate.NextAllowedUtc > now)
                await Task.Delay(gate.NextAllowedUtc - now, ct).ConfigureAwait(false);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.UserAgent.TryParseAdd("AST/1.0 (+https://github.com/Etsuna/ArchipelagoSphereTracker)");
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage? res = null;
                try
                {
                    res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                    if ((int)res.StatusCode == 429)
                    {
                        var delay = GetRetryDelay(res) ?? Backoff(attempt);
                        log?.Invoke($"[HTTP] 429 Too Many Requests from {uri.Host}, retry in {Math.Ceiling(delay.TotalSeconds)}s (attempt {attempt}/{maxAttempts}).");

                        gate.NextAllowedUtc = DateTimeOffset.UtcNow + delay + minSpacingPerHost;

                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            continue;
                        }

                        return null;
                    }

                    if ((int)res.StatusCode >= 500)
                    {
                        var delay = Backoff(attempt);
                        log?.Invoke($"[HTTP] {(int)res.StatusCode} {res.ReasonPhrase} from {uri.Host}, retry in {Math.Ceiling(delay.TotalSeconds)}s (attempt {attempt}/{maxAttempts}).");

                        gate.NextAllowedUtc = DateTimeOffset.UtcNow + delay + minSpacingPerHost;

                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                            continue;
                        }

                        return null;
                    }

                    if (!res.IsSuccessStatusCode)
                    {
                        log?.Invoke($"[HTTP] {(int)res.StatusCode} {res.ReasonPhrase} for {url}");
                        gate.NextAllowedUtc = DateTimeOffset.UtcNow + minSpacingPerHost;
                        return null;
                    }

                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                    gate.NextAllowedUtc = DateTimeOffset.UtcNow + minSpacingPerHost;
                    return body;
                }
                catch (OperationCanceledException)
                {
                    gate.NextAllowedUtc = DateTimeOffset.UtcNow + minSpacingPerHost;
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    var delay = Backoff(attempt);
                    log?.Invoke($"[HTTP] Network error: {ex.Message}, retry in {Math.Ceiling(delay.TotalSeconds)}s (attempt {attempt}/{maxAttempts}).");

                    gate.NextAllowedUtc = DateTimeOffset.UtcNow + delay + minSpacingPerHost;

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }

                    return null;
                }
                finally
                {
                    res?.Dispose();
                }
            }

            return null;
        }
        finally
        {
            gate.Mutex.Release();
        }
    }

    private static TimeSpan? GetRetryDelay(HttpResponseMessage res)
    {
        if (res.Headers.RetryAfter?.Delta is not null)
            return res.Headers.RetryAfter.Delta;

        if (res.Headers.RetryAfter?.Date is not null)
        {
            var d = res.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
            if (d > TimeSpan.Zero) return d;
        }

        return null;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(1 << (attempt - 1), 15);
        return TimeSpan.FromSeconds(seconds);
    }
}
