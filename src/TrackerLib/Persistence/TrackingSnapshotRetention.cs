namespace ArchipelagoSphereTracker.Tracking.Persistence;

/// <summary>
/// Releases historical snapshot bodies without deleting event identities or their references.
/// All decisions and updates share the same write transaction as polling and delivery claims.
/// </summary>
internal static class TrackingSnapshotRetention
{
    internal const string Indexes = @"
        CREATE INDEX IF NOT EXISTS idx_roomsnapshots_full_payload
            ON RoomSnapshots(Id) WHERE PayloadPruned = 0;
        CREATE INDEX IF NOT EXISTS idx_trackingevents_snapshot
            ON TrackingEvents(SnapshotId);";

    internal static Task InitializeAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        var hasColumn = false;
        using (var inspect = connection.CreateCommand())
        {
            inspect.CommandText = "PRAGMA table_info(RoomSnapshots);";
            using var reader = await inspect.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                hasColumn |= string.Equals(reader["name"].ToString(), "PayloadPruned", StringComparison.OrdinalIgnoreCase);
        }

        using var command = connection.CreateCommand();
        command.CommandText = (hasColumn ? "" : @"
            ALTER TABLE RoomSnapshots ADD COLUMN PayloadPruned INTEGER NOT NULL DEFAULT 0
                CHECK (PayloadPruned IN (0, 1));") + Indexes;
        return await command.ExecuteNonQueryAsync(ct);
    }, ct);

    internal static Task<int> PruneBatchAsync(int batchSize = 8, CancellationToken ct = default)
    {
        if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE RoomSnapshots SET PayloadJson = '', PayloadPruned = 1
                WHERE Id IN (
                    SELECT snapshot.Id FROM RoomSnapshots snapshot
                    WHERE snapshot.PayloadPruned = 0
                      AND snapshot.Id < (
                          SELECT MAX(latest.Id) FROM RoomSnapshots latest
                          WHERE latest.GuildId = snapshot.GuildId
                            AND latest.ChannelId = snapshot.ChannelId)
                      AND NOT EXISTS (
                          SELECT 1 FROM TrackingEvents event
                          JOIN EventDeliveries delivery ON delivery.EventId = event.Id
                          WHERE event.SnapshotId = snapshot.Id AND delivery.Status <> 'Delivered')
                    ORDER BY snapshot.Id LIMIT @BatchSize
                );";
            command.Parameters.AddWithValue("@BatchSize", batchSize);
            return await command.ExecuteNonQueryAsync(ct);
        }, ct);
    }

    internal static async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var total = 0;
                int pruned;
                while ((pruned = await PruneBatchAsync(ct: ct).ConfigureAwait(false)) > 0)
                {
                    total += pruned;
                    // Commit small batches and give polling/deliveries a chance to acquire the writer.
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
                }
                if (total > 0)
                    Console.WriteLine($"[TrackingStorage] Released {total} historical snapshot payloads; events and deliveries retained.");
                await Task.Delay(TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Console.WriteLine($"[TrackingStorage] Cleanup retry ({exception.GetType().Name}).");
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }
}
