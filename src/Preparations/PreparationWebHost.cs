using System.Net;
using System.Text.Json;
using ArchipelagoSphereTracker.src.Resources;

internal static class PreparationWebHost
{
    internal sealed record SharedSession(string? SeedId, bool UploadStarted, Func<string?,Task> SaveUploadAsync);

    internal static string SessionPage(string origin) => new Uri(new Uri(origin), "/session").AbsoluteUri;

    internal static Guid ParseSession(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new PreparationException(Resource.PrepWebHostSessionRequired);
        if (!Guid.TryParseExact(value.Trim(), "D", out var session))
            throw new PreparationException(Resource.PrepWebHostSessionInvalid);
        var id = session.ToString("D");
        if (id[14] != '4' || id[19] is not ('8' or '9' or 'a' or 'b'))
            throw new PreparationException(Resource.PrepWebHostSessionInvalid);
        return session;
    }

    internal static Uri? ParseOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
                !(uri.Scheme == Uri.UriSchemeHttp && Declare.AllowedArchipelagoHosts.Contains(uri.DnsSafeHost))) ||
            uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new PreparationException(Resource.PrepWebHostInvalid);
        return uri;
    }

    internal static async Task<string> HostAsync(Uri origin, Guid session, string archive, SharedSession shared,
        HttpClient? transport = null, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        ct = deadline.Token;
        using var client = transport == null
            ? HttpClientFactory.CreateJsonClient(TimeSpan.FromMinutes(2), allowAutoRedirect: false)
            : null;
        transport ??= client!;
        using var sessionResponse = await transport.GetAsync(new Uri(origin, "/session/" + session.ToString("D")),
            HttpCompletionOption.ResponseHeadersRead, ct);
        sessionResponse.EnsureSuccessStatusCode();
        // The organizer's session can already contain unrelated rooms and seeds.
        var seed = shared.SeedId;
        if (seed == null && shared.UploadStarted)
            throw new PreparationException(Resource.PrepWebHostUploadUncertain);
        if (seed != null)
        {
            seed = ValidateId(seed);
            var room = await ReadRoomIdAsync(transport, origin, seed, ct);
            if (room != null) return new Uri(origin,"/room/"+room).AbsoluteUri;
        }
        if (seed == null)
        {
            await using var stream = File.OpenRead(archive);
            await shared.SaveUploadAsync(null);
            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "file", "Archipelago.zip");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/uploads")) { Content = form };
            using var uploaded = await transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            seed = ReadRedirectId(uploaded, origin, "/seed/");
            await shared.SaveUploadAsync(seed);
        }
        using var hosted = await transport.GetAsync(new Uri(origin, "/new_room/" + seed),
            HttpCompletionOption.ResponseHeadersRead, ct);
        return new Uri(origin, "/room/" + ReadRedirectId(hosted, origin, "/room/")).AbsoluteUri;
    }

    private static async Task<string?> ReadRoomIdAsync(HttpClient client, Uri origin, string seed, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri(origin, "/api/get_rooms"), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(1024 * 1024).WaitAsync(ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unexpected WebHost session response.");
        var matches = json.RootElement.EnumerateArray()
            .Where(entry => entry.GetProperty("seed_id").GetString() == seed).Take(2).ToArray();
        if (matches.Length > 1)
            throw new InvalidDataException("Ambiguous WebHost session.");
        return matches.Length == 0 ? null : ValidateId(matches[0].GetProperty("room_id").GetString());
    }

    private static string ReadRedirectId(HttpResponseMessage response, Uri origin, string prefix)
    {
        if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.SeeOther) ||
            response.Headers.Location is not { } location ||
            !Uri.TryCreate(origin, location, out var target) ||
            !SameOrigin(origin,target) ||
            !target.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) || target.Query.Length != 0 || target.Fragment.Length != 0)
            throw new InvalidDataException("Unexpected WebHost response.");
        return ValidateId(target.AbsolutePath[prefix.Length..]);
    }

    private static bool SameOrigin(Uri first, Uri second)
        => string.Equals(first.GetLeftPart(UriPartial.Authority),second.GetLeftPart(UriPartial.Authority),StringComparison.OrdinalIgnoreCase);

    private static string ValidateId(string? id)
        => id is { Length: 22 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? id : throw new InvalidDataException("Invalid WebHost identifier.");
}
