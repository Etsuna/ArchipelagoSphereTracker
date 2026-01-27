using System.Data.SQLite;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class DatapackageStore
{
    private static readonly HttpClient Http = HttpClientFactory.CreateJsonClient();

    // ---- Modèle du JSON attendu ----
    private sealed class Datapackage
    {
        [JsonPropertyName("item_name_groups")]
        public Dictionary<string, List<string>> ItemNameGroups { get; set; } = new();

        [JsonPropertyName("item_name_to_id")]
        public Dictionary<string, JsonElement> ItemNameToId { get; set; } = new();

        [JsonPropertyName("location_name_groups")]
        public Dictionary<string, List<string>> LocationNameGroups { get; set; } = new();

        [JsonPropertyName("location_name_to_id")]
        public Dictionary<string, JsonElement> LocationNameToId { get; set; } = new();
    }

    /// <summary>
    /// Importe un datapackage depuis une URL http(s) ou un chemin local.
    /// truncate=true : purge d'abord Datapackage* (Items/ItemGroups/Locations/LocationGroups) pour (guildId, channelId, datasetKey).
    /// </summary>
    public static async Task ImportAsync(string pathOrUrl, string guildId, string channelId, string datasetKey, bool truncate = false, string? gameName = null, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(120));

        byte[] payload;
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            payload = await resp.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
        }
        else
        {
            if (!File.Exists(pathOrUrl))
                throw new FileNotFoundException($"JSON introuvable: {pathOrUrl}");

            using var fs = new FileStream(pathOrUrl, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
            if (fs.Length > 64L * 1024 * 1024) 
                throw new InvalidOperationException("Datapackage trop volumineux");

            payload = new byte[fs.Length];
            int off = 0;
            while (off < payload.Length)
            {
                ct.ThrowIfCancellationRequested();
                var read = await fs.ReadAsync(payload.AsMemory(off), cts.Token).ConfigureAwait(false);
                if (read == 0) break;
                off += read;
            }
        }

        var data = JsonSerializer.Deserialize<Datapackage>(payload)
                   ?? throw new InvalidOperationException("JSON invalide (désérialisation nulle)");

        await using var connection = await Db.OpenWriteAsync().ConfigureAwait(false);
        await using var tx = connection.BeginTransaction();

        if (truncate)
        {
            ct.ThrowIfCancellationRequested();
            await ExecAsync(connection, tx,
                @"DELETE FROM DatapackageItemGroups WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;",
                ("@G", guildId), ("@C", channelId), ("@D", datasetKey));
            ct.ThrowIfCancellationRequested();
            await ExecAsync(connection, tx,
                @"DELETE FROM DatapackageLocationGroups WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;",
                ("@G", guildId), ("@C", channelId), ("@D", datasetKey));
            ct.ThrowIfCancellationRequested();
            await ExecAsync(connection, tx,
                @"DELETE FROM DatapackageItems WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;",
                ("@G", guildId), ("@C", channelId), ("@D", datasetKey));
            ct.ThrowIfCancellationRequested();
            await ExecAsync(connection, tx,
                @"DELETE FROM DatapackageLocations WHERE GuildId=@G AND ChannelId=@C AND DatasetKey=@D;",
                ("@G", guildId), ("@C", channelId), ("@D", datasetKey));
        }

        // ----- Items -----
        await using (var insertItem = new SQLiteCommand(
            @"INSERT OR IGNORE INTO DatapackageItems (GuildId, ChannelId, DatasetKey, Id, Name)
              VALUES (@G, @C, @D, @Id, @Name);", connection, tx))
        {
            insertItem.Parameters.Add("@G", System.Data.DbType.String);
            insertItem.Parameters.Add("@C", System.Data.DbType.String);
            insertItem.Parameters.Add("@D", System.Data.DbType.String);
            insertItem.Parameters.Add("@Id", System.Data.DbType.Int64);
            insertItem.Parameters.Add("@Name", System.Data.DbType.String);

            foreach (var kv in data.ItemNameToId)
            {
                ct.ThrowIfCancellationRequested();

                if (!TryGetLong(kv.Value, out var id))
                {
                    Console.WriteLine($"[WARN] Item '{kv.Key}' a un id invalide: {kv.Value.GetRawText()}");
                    continue;
                }

                insertItem.Parameters["@G"].Value = guildId;
                insertItem.Parameters["@C"].Value = channelId;
                insertItem.Parameters["@D"].Value = datasetKey;
                insertItem.Parameters["@Id"].Value = id;
                insertItem.Parameters["@Name"].Value = kv.Key;

                await insertItem.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        // ----- ItemGroups -----
        await using (var insertItemGroup = new SQLiteCommand(
            @"INSERT OR IGNORE INTO DatapackageItemGroups (GuildId, ChannelId, DatasetKey, GroupName, ItemId)
              VALUES (@G, @C, @D, @Group, @ItemId);", connection, tx))
        {
            insertItemGroup.Parameters.Add("@G", System.Data.DbType.String);
            insertItemGroup.Parameters.Add("@C", System.Data.DbType.String);
            insertItemGroup.Parameters.Add("@D", System.Data.DbType.String);
            insertItemGroup.Parameters.Add("@Group", System.Data.DbType.String);
            insertItemGroup.Parameters.Add("@ItemId", System.Data.DbType.Int64);

            foreach (var (groupName, names) in data.ItemNameGroups)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var name in names)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!TryGetId(data.ItemNameToId, name, out var id))
                    {
                        Console.WriteLine($"[WARN] Item group '{groupName}' contient '{name}' sans id valide");
                        continue;
                    }

                    insertItemGroup.Parameters["@G"].Value = guildId;
                    insertItemGroup.Parameters["@C"].Value = channelId;
                    insertItemGroup.Parameters["@D"].Value = datasetKey;
                    insertItemGroup.Parameters["@Group"].Value = groupName;
                    insertItemGroup.Parameters["@ItemId"].Value = id;

                    await insertItemGroup.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        // ----- Locations -----
        await using (var insertLoc = new SQLiteCommand(
            @"INSERT OR IGNORE INTO DatapackageLocations (GuildId, ChannelId, DatasetKey, Id, Name)
              VALUES (@G, @C, @D, @Id, @Name);", connection, tx))
        {
            insertLoc.Parameters.Add("@G", System.Data.DbType.String);
            insertLoc.Parameters.Add("@C", System.Data.DbType.String);
            insertLoc.Parameters.Add("@D", System.Data.DbType.String);
            insertLoc.Parameters.Add("@Id", System.Data.DbType.Int64);
            insertLoc.Parameters.Add("@Name", System.Data.DbType.String);

            foreach (var kv in data.LocationNameToId)
            {
                ct.ThrowIfCancellationRequested();

                if (!TryGetLong(kv.Value, out var id))
                {
                    Console.WriteLine($"[WARN] Location '{kv.Key}' a un id invalide: {kv.Value.GetRawText()}");
                    continue;
                }

                insertLoc.Parameters["@G"].Value = guildId;
                insertLoc.Parameters["@C"].Value = channelId;
                insertLoc.Parameters["@D"].Value = datasetKey;
                insertLoc.Parameters["@Id"].Value = id;
                insertLoc.Parameters["@Name"].Value = kv.Key;

                await insertLoc.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        // ----- LocationGroups -----
        await using (var insertLocGroup = new SQLiteCommand(
            @"INSERT OR IGNORE INTO DatapackageLocationGroups (GuildId, ChannelId, DatasetKey, GroupName, LocationId)
              VALUES (@G, @C, @D, @Group, @LocId);", connection, tx))
        {
            insertLocGroup.Parameters.Add("@G", System.Data.DbType.String);
            insertLocGroup.Parameters.Add("@C", System.Data.DbType.String);
            insertLocGroup.Parameters.Add("@D", System.Data.DbType.String);
            insertLocGroup.Parameters.Add("@Group", System.Data.DbType.String);
            insertLocGroup.Parameters.Add("@LocId", System.Data.DbType.Int64);

            foreach (var (groupName, names) in data.LocationNameGroups)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var name in names)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!TryGetId(data.LocationNameToId, name, out var id))
                    {
                        Console.WriteLine($"[WARN] Location group '{groupName}' contient '{name}' sans id valide");
                        continue;
                    }

                    insertLocGroup.Parameters["@G"].Value = guildId;
                    insertLocGroup.Parameters["@C"].Value = channelId;
                    insertLocGroup.Parameters["@D"].Value = datasetKey;
                    insertLocGroup.Parameters["@Group"].Value = groupName;
                    insertLocGroup.Parameters["@LocId"].Value = id;

                    await insertLocGroup.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        // Mapping jeu -> dataset
        if (!string.IsNullOrWhiteSpace(gameName))
        {
            ct.ThrowIfCancellationRequested();

            await ExecAsync(connection, tx,
                @"INSERT INTO DatapackageGameMap(GuildId, ChannelId, GameName, DatasetKey, ImportedAt)
                  VALUES (@G, @C, @Game, @D, @Now)
                  ON CONFLICT(GuildId, ChannelId, GameName) DO UPDATE SET
                    DatasetKey = excluded.DatasetKey,
                    ImportedAt = excluded.ImportedAt;",
                ("@G", guildId),
                ("@C", channelId),
                ("@Game", gameName!),
                ("@D", datasetKey),
                ("@Now", DateTime.UtcNow.ToString("o")));
        }

        await tx.CommitAsync().ConfigureAwait(false);
    }

    // ----------------- Helpers -----------------

    private static async Task ExecAsync(SQLiteConnection cn, SQLiteTransaction tx, string sql, params (string name, object value)[] p)
    {
        await using var cmd = new SQLiteCommand(sql, cn, tx);
        foreach (var (name, value) in p)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static bool TryGetLong(JsonElement e, out long value)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                return e.TryGetInt64(out value);
            case JsonValueKind.String:
                return long.TryParse(e.GetString(), out value);
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryGetId(Dictionary<string, JsonElement> map, string key, out long id)
    {
        if (map.TryGetValue(key, out var elem))
            return TryGetLong(elem, out id);
        id = 0;
        return false;
    }
}
