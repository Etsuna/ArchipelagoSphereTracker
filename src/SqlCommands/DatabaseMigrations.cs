using ArchipelagoSphereTracker.src.Resources;

internal sealed record DatabaseMigrationStep(Version Version, Func<CancellationToken, Task> Apply);

internal static class DatabaseMigrations
{
    // Register each migration once, in numeric version order. The V1 conversion
    // remains a separate first step for unversioned and pre-V2 databases.
    private static readonly DatabaseMigrationStep[] Steps =
    [
        new(new Version(5, 0, 0), DBMigration.Migrate_4_to_5Async),
        new(new Version(5, 0, 1), DBMigration_5.Migrate_5_0_1),
        new(new Version(5, 0, 2), DBMigration_5.Migrate_5_0_2),
        new(new Version(5, 0, 3), DBMigration_5.Migrate_5_0_3),
        new(new Version(5, 0, 4), DBMigration_5.Migrate_5_0_4),
        new(new Version(5, 0, 5), DBMigration_5.Migrate_5_0_5),
        new(new Version(5, 0, 6), DBMigration_5.Migrate_5_0_6),
        new(new Version(5, 0, 7), DBMigration_5.Migrate_5_0_7),
        new(new Version(5, 0, 8), DBMigration_5.Migrate_5_0_8),
        new(new Version(5, 0, 9), DBMigration_5.Migrate_5_0_9),
        new(new Version(5, 0, 10), DBMigration_5.Migrate_5_0_10),
        new(new Version(5, 0, 11), DBMigration_5.Migrate_5_0_11),
        new(new Version(5, 0, 12), DBMigration_5.Migrate_5_0_12),
        new(new Version(5, 0, 13), DBMigration_5.Migrate_5_0_13),
        new(new Version(5, 0, 14), DBMigration_5.Migrate_5_0_14),
        new(new Version(5, 0, 15), DBMigration_5.Migrate_5_0_15),
        new(new Version(5, 0, 16), DBMigration_5.Migrate_5_0_16),
        new(new Version(5, 0, 17), DBMigration_5.Migrate_5_0_17),
        new(new Version(5, 0, 18), DBMigration_5.Migrate_5_0_18),
        new(new Version(5, 0, 19), DBMigration_5.Migrate_5_0_19),
        new(new Version(5, 0, 20), DBMigration_5.Migrate_5_0_20),
        new(new Version(5, 0, 21), DBMigration_5.Migrate_5_0_21)
    ];

    internal static IReadOnlyList<DatabaseMigrationStep> GetPending(string currentVersion, string targetVersion)
    {
        InvalidOperationException Unsupported() => new(string.Format(
            Resource.DatabaseMigrationUnsupportedVersion, currentVersion, targetVersion));

        if (!Version.TryParse(targetVersion, out var target) || !Steps.Any(step => step.Version == target))
            throw Unsupported();

        // GetCurrentDbVersionAsync uses -1 for a missing version table and 0 for
        // a table without a recorded version. Both used the legacy path before.
        var legacy = currentVersion is "-1" or "0";
        Version? current = null;
        if (!legacy)
        {
            if (!Version.TryParse(currentVersion, out current)) throw Unsupported();
            legacy = current.Major < 5;
            if (!legacy && (current > target || !Steps.Any(step => step.Version == current)))
                throw Unsupported();
        }

        return Steps.Where(step => step.Version <= target && (legacy || step.Version > current!)).ToArray();
    }
}
