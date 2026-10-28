using System.Data.SQLite;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

internal static class TrackingLegacyRetirement
{
    internal static Task MigrateAsync(CancellationToken ct = default) => Db.WriteAsync(async connection =>
    {
        // Prevent DROP TABLE from cascading into an unexpected custom schema.
        await CheckExternalReferencesAsync(connection, ct);
        // Refresh the import even on a 5.0.16/17 database. A V1 process may have
        // written additional rows since the earlier copy. Active V2 snapshots
        // remain authoritative; provisional V1 rooms receive their latest data.
        await TrackingReadModel.MigrateAsync(connection, ct, refreshLegacy: true);
        foreach (var table in new[] { "TrackingSlots", "TrackingItems", "TrackingHints", "TrackingPlayerStates" })
        {
            using var check = connection.CreateCommand();
            check.CommandText = $"PRAGMA foreign_key_check({table});";
            using var reader = await check.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
                throw new InvalidOperationException($"Native tracking import has invalid references in {table}.");
        }
        foreach (var table in LegacyTrackingSchema.TableNames)
        {
            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP TABLE IF EXISTS {table};";
            await drop.ExecuteNonQueryAsync(ct);
        }
        using var marker = connection.CreateCommand();
        marker.CommandText = "INSERT OR IGNORE INTO TrackingReadModelMigrations(Version) VALUES(18);";
        await marker.ExecuteNonQueryAsync(ct);
        return 0;
    }, ct);

    private static async Task CheckExternalReferencesAsync(SQLiteConnection connection, CancellationToken ct)
    {
        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
        }
        foreach (var table in tables.Except(LegacyTrackingSchema.TableNames, StringComparer.OrdinalIgnoreCase))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"table\" FROM pragma_foreign_key_list(@Table);";
            command.Parameters.AddWithValue("@Table", table);
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                if (LegacyTrackingSchema.TableNames.Contains(reader.GetString(0), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Cannot retire V1 tracking: {table} still references {reader.GetString(0)}.");
        }
    }
}
