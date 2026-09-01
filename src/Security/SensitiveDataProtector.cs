using System.Security.Cryptography;
using System.Text;

public static class SensitiveDataPurposes
{
    public const string Room = "ChannelsAndUrlsTable.Room";
    public const string Tracker = "ChannelsAndUrlsTable.Tracker";
    public const string Patch = "UrlAndChannelPatchTable.Patch";
    public const string KeyCheck = "DataProtectionMetadata.KeyCheck";
}

public sealed class AesGcmDataProtector
{
    public const string EnvelopePrefix = "astenc:v1:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmDataProtector(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException("The data-protection key must contain exactly 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public bool IsProtected(string? value)
        => value?.StartsWith(EnvelopePrefix, StringComparison.Ordinal) == true;

    public string Protect(string? value, string purpose)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (IsProtected(value))
        {
            _ = Unprotect(value, purpose);
            return value;
        }

        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(purpose));
        }

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);
        return EnvelopePrefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string? value, string purpose)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value)) return value ?? string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        try
        {
            var payload = Convert.FromBase64String(value[EnvelopePrefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
                throw new CryptographicException("Invalid protected-data envelope.");

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var ciphertext = payload.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(purpose));
            }
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Invalid protected-data envelope.", exception);
        }
    }
}

public static class SensitiveDataProtector
{
    private const string KeyEnvironmentVariable = "AST_DATA_PROTECTION_KEY";
    private const string LocalKeyFileName = "AST.data-protection.key";
    private static readonly Lazy<AesGcmDataProtector> Shared = new(CreateProtector, true);

    public static bool IsProtected(string? value) => Shared.Value.IsProtected(value);
    public static string Protect(string? value, string purpose) => Shared.Value.Protect(value, purpose);
    public static string Unprotect(string? value, string purpose) => Shared.Value.Unprotect(value, purpose);

    private static AesGcmDataProtector CreateProtector()
    {
        var configured = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return new AesGcmDataProtector(ParseKey(configured, KeyEnvironmentVariable));

        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(Declare.DatabaseFile))
                                ?? Environment.CurrentDirectory;
        var keyPath = Path.Combine(databaseDirectory, LocalKeyFileName);
        if (File.Exists(keyPath))
        {
            RestrictLocalKeyPermissions(keyPath);
            return new AesGcmDataProtector(ParseKey(File.ReadAllText(keyPath), LocalKeyFileName));
        }

        var generated = RandomNumberGenerator.GetBytes(32);
        var encoded = Encoding.ASCII.GetBytes(Convert.ToBase64String(generated));
        try
        {
            using var stream = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(encoded);
            stream.Flush(flushToDisk: true);
            RestrictLocalKeyPermissions(keyPath);
            return new AesGcmDataProtector(generated);
        }
        catch (IOException) when (File.Exists(keyPath))
        {
            return new AesGcmDataProtector(ParseKey(File.ReadAllText(keyPath), LocalKeyFileName));
        }
    }

    private static byte[] ParseKey(string value, string source)
    {
        try
        {
            var key = Convert.FromBase64String(value.Trim());
            if (key.Length != 32)
                throw new InvalidOperationException($"{source} must decode to exactly 32 bytes.");
            return key;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{source} must be a Base64-encoded 32-byte key.", exception);
        }
    }

    private static void RestrictLocalKeyPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
