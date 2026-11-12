using System.Data.SQLite;
using System.Security.Cryptography;
using ArchipelagoSphereTracker.src.Resources;
using Microsoft.AspNetCore.DataProtection;

public sealed partial class PreparationService
{
    private static IDataProtector CreateHostingProtector(string preparationRoot)
    {
        var path = Path.Combine(preparationRoot, ".webhost-keys");
        if (OperatingSystem.IsLinux())
        {
            const UnixFileMode privateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, privateDirectory);
            File.SetUnixFileMode(path, privateDirectory);
        }

        return DataProtectionProvider.Create(new DirectoryInfo(path), builder =>
        {
            builder.SetApplicationName("ArchipelagoSphereTracker.PreparationWebHost");
            if (OperatingSystem.IsWindows()) builder.ProtectKeysWithDpapi();
        }).CreateProtector("WebHost session v1");
    }

    private IDataProtector HostingProtector(PreparationRoom room)
        => _hostingProtector.Value.CreateProtector(room.Id).CreateProtector(room.Hosting!.Origin);

    private async Task SaveHostingCredentialAsync(SQLiteConnection connection, PreparationRoom room, string? input)
    {
        var session = PreparationWebHost.ParseSession(input);
        var key = Convert.ToHexString(SHA256.HashData(session.ToByteArray()));
        using var bind = Query(connection, """
            UPDATE PreparationHosting SET SessionKey=@key
            WHERE PreparationId=@id AND (Attempted=0 OR SessionKey IS NULL OR SessionKey=@key)
            """, ("@id", room.Id), ("@key", key));
        if (await bind.ExecuteNonQueryAsync() != 1)
            throw new PreparationException(Resource.PrepWebHostSessionChanged);

        var protectedSession = HostingProtector(room).Protect(session.ToString("D"));
        await Execute(connection, """
            INSERT INTO PreparationHostingCredentials(PreparationId,ProtectedSession) VALUES(@id,@session)
            ON CONFLICT(PreparationId) DO UPDATE SET ProtectedSession=excluded.ProtectedSession
            """, ("@id", room.Id), ("@session", protectedSession));
    }

    private async Task<Guid?> ReadHostingSessionAsync(SQLiteConnection connection, PreparationRoom room)
    {
        using var query = Query(connection, "SELECT ProtectedSession FROM PreparationHostingCredentials WHERE PreparationId=@id",
            ("@id", room.Id));
        if (await query.ExecuteScalarAsync() is not string protectedSession) return null;
        try
        {
            return Guid.TryParseExact(HostingProtector(room).Unprotect(protectedSession), "D", out var session) ? session : null;
        }
        catch (CryptographicException) { return null; }
    }

    internal async Task<Guid?> GetHostingSessionAsync(PreparationRoom room)
    {
        if (!room.NeedsWebHostSession) return null;
        await using var connection = await Db.OpenReadAsync();
        return await ReadHostingSessionAsync(connection, room);
    }

    internal Task PrepareDeliveryRetryAsync(string guild, string channel, string actor, string? webHostUserId, string? expectedRoom)
        => MutateManagedAsync(guild, channel, actor, async (connection, room) =>
        {
            if (room.State != PreparationState.Generated) throw new PreparationException(Resource.PrepLocked);
            if (room.NeedsWebHostSession && await ReadHostingSessionAsync(connection, room) == null)
                await SaveHostingCredentialAsync(connection, room, webHostUserId);
        }, notify:false, expectedRoom:expectedRoom);

    private static Task ClearHostingCredentialAsync(SQLiteConnection connection, string roomId)
        => Execute(connection, "DELETE FROM PreparationHostingCredentials WHERE PreparationId=@id", ("@id", roomId));
}
