using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ArchipelagoSphereTracker.Tracking.Scheduling;

public enum WebHostEndpointKind
{
    RoomStatus,
    RuntimeTracker,
    StaticTracker
}

public sealed record WebHostClientOptions
{
    public int GlobalConcurrency { get; init; } = 10;
    public int PerOriginConcurrency { get; init; } = 2;
    public TimeSpan RoomStatusTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan RuntimeTrackerTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan StaticTrackerTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public long MaximumResponseBytes { get; init; } = 32L * 1024 * 1024;
}

public sealed record WebHostFetchResult(string? Json, RoomPollResult PollResult)
{
    public bool Success => PollResult.Success && Json != null;
}

public sealed class ResilientWebHostClient
{
    private readonly HttpClient _httpClient;
    private readonly WebHostClientOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _globalGate;
    private readonly MemoryCache _snapshots = new(new MemoryCacheOptions { SizeLimit = 128L * 1024 * 1024 });
    private readonly ConcurrentDictionary<(Uri, WebHostEndpointKind), Lazy<Task<WebHostFetchResult>>> _inflight = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _originGates =
        new(StringComparer.OrdinalIgnoreCase);

    public ResilientWebHostClient(
        HttpClient httpClient,
        WebHostClientOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new WebHostClientOptions();
        if (_options.GlobalConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(_options.GlobalConcurrency));
        if (_options.PerOriginConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(_options.PerOriginConcurrency));
        if (_options.RoomStatusTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(_options.RoomStatusTimeout));
        if (_options.RuntimeTrackerTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(_options.RuntimeTrackerTimeout));
        if (_options.StaticTrackerTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(_options.StaticTrackerTimeout));
        if (_options.MaximumResponseBytes <= 0) throw new ArgumentOutOfRangeException(nameof(_options.MaximumResponseBytes));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _globalGate = new SemaphoreSlim(_options.GlobalConcurrency, _options.GlobalConcurrency);
    }

    public async Task<WebHostFetchResult> FetchJsonAsync(
        Uri uri,
        WebHostEndpointKind endpointKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Failed(PollFailureKind.Network);

        cancellationToken.ThrowIfCancellationRequested();
        var key = (uri, endpointKind);
        if (_snapshots.TryGetValue(key, out WebHostFetchResult? cached) && cached != null)
            return cached;
        var fetch = _inflight.GetOrAdd(key, _ => new Lazy<Task<WebHostFetchResult>>(
            () => FetchAndCacheAsync(uri, endpointKind), LazyThreadSafetyMode.ExecutionAndPublication));
        // Each consumer may stop waiting without cancelling a snapshot needed by another room.
        return await fetch.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebHostFetchResult> FetchAndCacheAsync(Uri uri, WebHostEndpointKind endpointKind)
    {
        var key = (uri, endpointKind);
        try
        {
            // Recheck after a competing request has published its snapshot.
            if (_snapshots.TryGetValue(key, out WebHostFetchResult? cached) && cached != null)
                return cached;
            var result = await FetchWithGatesAsync(uri, endpointKind, CancellationToken.None).ConfigureAwait(false);
            if (result.Success)
            {
                _snapshots.Set(key, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = endpointKind == WebHostEndpointKind.StaticTracker
                        ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(5),
                    Size = (long)result.Json!.Length * sizeof(char) + 512
                });
            }
            return result;
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }

    private async Task<WebHostFetchResult> FetchWithGatesAsync(
        Uri uri, WebHostEndpointKind endpointKind, CancellationToken cancellationToken)
    {

        var origin = uri.GetLeftPart(UriPartial.Authority);
        var originGate = _originGates.GetOrAdd(
            origin,
            _ => new SemaphoreSlim(_options.PerOriginConcurrency, _options.PerOriginConcurrency));

        await _globalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await originGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await FetchWithinBudgetAsync(uri, endpointKind, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                originGate.Release();
            }
        }
        finally
        {
            _globalGate.Release();
        }
    }

    private async Task<WebHostFetchResult> FetchWithinBudgetAsync(
        Uri uri,
        WebHostEndpointKind endpointKind,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GetTimeout(endpointKind));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.TryParseAdd("AST/1.0 (+https://github.com/Etsuna/ArchipelagoSphereTracker)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Failed(PollFailureKind.NotFound);

            if ((int)response.StatusCode == 429)
                return Failed(
                    PollFailureKind.RateLimited,
                    GetRetryAfter(response, _timeProvider.GetUtcNow()),
                    affectsOriginBreaker: true);

            if ((int)response.StatusCode >= 500)
                return Failed(PollFailureKind.ServerError, affectsOriginBreaker: true);

            if (!response.IsSuccessStatusCode)
                return Failed(PollFailureKind.Network);

            if (response.Content.Headers.ContentLength is { } length && length > _options.MaximumResponseBytes)
                return Failed(PollFailureKind.PartialResponse);

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (body.Length > _options.MaximumResponseBytes)
                return Failed(PollFailureKind.PartialResponse);

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) ||
                body.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                return Failed(PollFailureKind.InvalidContentType);
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return Failed(PollFailureKind.InvalidJson);
                if (!HasMinimumShape(document.RootElement, endpointKind))
                    return Failed(PollFailureKind.PartialResponse);
            }
            catch (JsonException)
            {
                return Failed(PollFailureKind.InvalidJson);
            }

            return new WebHostFetchResult(body, RoomPollResult.Ok());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(PollFailureKind.Timeout, affectsOriginBreaker: true);
        }
        catch (HttpRequestException)
        {
            return Failed(PollFailureKind.Network, affectsOriginBreaker: true);
        }
    }

    private TimeSpan GetTimeout(WebHostEndpointKind endpointKind)
        => endpointKind switch
        {
            WebHostEndpointKind.RoomStatus => _options.RoomStatusTimeout,
            WebHostEndpointKind.StaticTracker => _options.StaticTrackerTimeout,
            _ => _options.RuntimeTrackerTimeout
        };

    private static bool HasMinimumShape(JsonElement root, WebHostEndpointKind endpointKind)
    {
        return endpointKind switch
        {
            WebHostEndpointKind.RuntimeTracker =>
                HasArray(root, "player_items_received") ||
                HasArray(root, "hints") ||
                HasArray(root, "player_checks_done") ||
                HasArray(root, "player_status"),
            WebHostEndpointKind.StaticTracker =>
                HasArray(root, "player_locations_total"),
            WebHostEndpointKind.RoomStatus => root.EnumerateObject().Any(),
            _ => false
        };
    }

    private static bool HasArray(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array;

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta > TimeSpan.Zero ? delta : null;
        if (response.Headers.RetryAfter?.Date is { } date && date > now)
            return date - now;
        return null;
    }

    private static WebHostFetchResult Failed(
        PollFailureKind kind,
        TimeSpan? retryAfter = null,
        bool affectsOriginBreaker = false)
        => new(null, RoomPollResult.Failed(kind, retryAfter, affectsOriginBreaker));
}
