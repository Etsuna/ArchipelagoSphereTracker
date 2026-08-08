using System.Text.Json;

namespace AST.Companion;

public sealed class CompanionApiClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static bool TryParsePortalUrl(string value, out CompanionConnection? connection)
    {
        connection = null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var portalIndex = Array.FindIndex(segments, x => x.Equals("portal", StringComparison.OrdinalIgnoreCase));
        if (portalIndex < 0 || segments.Length < portalIndex + 4)
            return false;

        var guildId = Uri.UnescapeDataString(segments[portalIndex + 1]);
        var channelId = Uri.UnescapeDataString(segments[portalIndex + 2]);
        var token = Uri.UnescapeDataString(segments[portalIndex + 3]);

        if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(token))
            return false;

        var baseUrl = $"{uri.Scheme}://{uri.Authority}";
        connection = new CompanionConnection(baseUrl, guildId, channelId, token);
        return true;
    }

    public async Task<CompanionSnapshot> GetSnapshotAsync(CompanionConnection connection, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(connection.SummaryUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = document.RootElement;
        var updatedAt = DateTimeOffset.UtcNow;
        if (TryGetProperty(root, "lastUpdated", out var updatedElement) &&
            updatedElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(updatedElement.GetString(), out var parsedUpdatedAt))
        {
            updatedAt = parsedUpdatedAt;
        }

        var items = ParseItems(root);
        var hints = ParseHints(root);
        return new CompanionSnapshot(updatedAt, items, hints);
    }

    private static List<CompanionItem> ParseItems(JsonElement root)
    {
        var items = new List<CompanionItem>();
        if (!TryGetProperty(root, "receivedItems", out var receivedItems) || receivedItems.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var aliasElement in receivedItems.EnumerateArray())
        {
            var alias = GetString(aliasElement, "alias");
            if (!TryGetProperty(aliasElement, "groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var group in groups.EnumerateArray())
            {
                var flag = GetString(group, "flagKey");
                if (!TryGetProperty(group, "items", out var groupItems) || groupItems.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in groupItems.EnumerateArray())
                {
                    items.Add(new CompanionItem(
                        alias,
                        GetString(item, "finder"),
                        GetString(item, "item"),
                        GetString(item, "location"),
                        GetString(item, "game"),
                        flag));
                }
            }
        }

        return items;
    }

    private static List<CompanionHint> ParseHints(JsonElement root)
    {
        var hints = new List<CompanionHint>();
        if (!TryGetProperty(root, "hints", out var hintsRoot) || hintsRoot.ValueKind != JsonValueKind.Array)
            return hints;

        foreach (var aliasElement in hintsRoot.EnumerateArray())
        {
            var alias = GetString(aliasElement, "alias");
            ParseHintDirection(aliasElement, alias, "asReceiver", "receiver", hints);
            ParseHintDirection(aliasElement, alias, "asFinder", "finder", hints);
        }

        return hints;
    }

    private static void ParseHintDirection(
        JsonElement aliasElement,
        string alias,
        string property,
        string direction,
        List<CompanionHint> result)
    {
        if (!TryGetProperty(aliasElement, property, out var values) || values.ValueKind != JsonValueKind.Array)
            return;

        foreach (var hint in values.EnumerateArray())
        {
            result.Add(new CompanionHint(
                alias,
                GetString(hint, "finder"),
                GetString(hint, "receiver"),
                GetString(hint, "item"),
                GetString(hint, "location"),
                GetString(hint, "game"),
                direction));
        }
    }

    private static string GetString(JsonElement element, string propertyName)
        => TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
