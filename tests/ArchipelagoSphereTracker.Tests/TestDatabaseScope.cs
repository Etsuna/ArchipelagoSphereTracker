using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

public sealed class TestDatabaseScope : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _databasePath;

    public string BaseDirectory { get; }

    public TestDatabaseScope()
    {
        _originalDirectory = Environment.CurrentDirectory;
        BaseDirectory = Path.Combine(Path.GetTempPath(), $"ast-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(BaseDirectory);
        Environment.CurrentDirectory = BaseDirectory;
        _databasePath = Path.Combine(BaseDirectory, Declare.DatabaseFile);

        ChannelConfigCache.Clear();
        DatabaseInitializer.InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        ChannelConfigCache.Clear();
        Environment.CurrentDirectory = _originalDirectory;

        try
        {
            if (File.Exists(_databasePath))
            {
                var walPath = _databasePath + "-wal";
                var shmPath = _databasePath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);
                File.Delete(_databasePath);
            }

            if (Directory.Exists(BaseDirectory))
            {
                Directory.Delete(BaseDirectory, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures in test scope
        }
    }

    public static async Task<long> CountRowsAsync(string table, string? guildId = null, string? channelId = null)
    {
        await using var conn = await Db.OpenReadAsync();
        using var cmd = conn.CreateCommand();

        var clauses = new List<string>();
        if (guildId is not null) clauses.Add("GuildId = @GuildId");
        if (channelId is not null) clauses.Add("ChannelId = @ChannelId");
        var where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;

        cmd.CommandText = $"SELECT COUNT(*) FROM {table}" + where;
        if (guildId is not null)
            cmd.Parameters.AddWithValue("@GuildId", guildId);
        if (channelId is not null)
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return result is null || result is DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
