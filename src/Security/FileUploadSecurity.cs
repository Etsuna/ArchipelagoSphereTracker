using System.IO.Compression;
using System.Text;
using System.Text.Json;

public static class FileUploadSecurity
{
    public const int MaxArchiveEntries = 500;
    public const long MaxArchiveUncompressedBytes = 256L * 1024 * 1024;

    public static bool TryGetSafeFileName(string? submittedName, string requiredExtension, out string safeFileName)
        => TryGetSafeFileName(submittedName, [requiredExtension], out safeFileName);

    public static bool TryGetSafeFileName(
        string? submittedName,
        IReadOnlyCollection<string> requiredExtensions,
        out string safeFileName)
    {
        var candidate = Path.GetFileName(submittedName ?? string.Empty);
        safeFileName = candidate;
        return !string.IsNullOrWhiteSpace(candidate) &&
               string.Equals(submittedName, candidate, StringComparison.Ordinal) &&
               !candidate.Contains('/') &&
               !candidate.Contains('\\') &&
               candidate.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               requiredExtensions.Any(extension =>
                   candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task CopyToFileWithLimitAsync(
        Stream source,
        string destinationPath,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        _ = await CopyThroughQuarantineAsync(
            source,
            destinationPath,
            maxBytes,
            validator: null,
            cancellationToken,
            quarantinePath: null).ConfigureAwait(false);
    }

    public static Task<bool> CopyValidatedToFileWithLimitAsync(
        Stream source,
        string destinationPath,
        long maxBytes,
        Func<string, bool> validator,
        CancellationToken cancellationToken = default,
        string? quarantinePath = null)
    {
        ArgumentNullException.ThrowIfNull(validator);
        return CopyThroughQuarantineAsync(
            source,
            destinationPath,
            maxBytes,
            validator,
            cancellationToken,
            quarantinePath);
    }

    private static async Task<bool> CopyThroughQuarantineAsync(
        Stream source,
        string destinationPath,
        long maxBytes,
        Func<string, bool>? validator,
        CancellationToken cancellationToken,
        string? quarantinePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var effectiveQuarantinePath = quarantinePath ?? Declare.UploadQuarantinePath;
        CleanupExpiredQuarantineFiles(effectiveQuarantinePath);
        Directory.CreateDirectory(effectiveQuarantinePath);
        var temporaryPath = Path.Combine(
            effectiveQuarantinePath,
            Guid.NewGuid().ToString("N") + ".quarantine");
        try
        {
            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                total += read;
                if (total > maxBytes)
                    throw new InvalidDataException($"Upload exceeds the {maxBytes}-byte limit.");

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            destination.Close();

            if (validator != null && !validator(temporaryPath))
                return false;

            File.Move(temporaryPath, destinationPath, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static int CleanupExpiredQuarantineFiles(
        string? quarantinePath = null,
        DateTimeOffset? now = null,
        TimeSpan? retention = null)
    {
        var path = quarantinePath ?? Declare.UploadQuarantinePath;
        if (!Directory.Exists(path)) return 0;

        var cutoff = (now ?? DateTimeOffset.UtcNow) -
                     (retention ?? TimeSpan.FromMinutes(Declare.UploadQuarantineRetentionMinutes));
        string[] files;
        try
        {
            files = Directory.GetFiles(path, "*.quarantine", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime)
                    continue;
                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return removed;
    }

    public static bool IsSafeTextFile(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Contains((byte)0)) return false;
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsValidSpoilerFile(string path, bool requireJson = false)
    {
        if (!IsSafeTextFile(path)) return false;
        if (!requireJson) return true;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsArchiveWithinLimits(
        string zipPath,
        int maxEntries = MaxArchiveEntries,
        long maxUncompressedBytes = MaxArchiveUncompressedBytes)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > maxEntries)
                return false;

            long totalLength = 0;
            foreach (var entry in archive.Entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                if (IsUnsafeArchivePath(normalized) ||
                    normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part is "." or ".."))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                totalLength += entry.Length;
                if (totalLength > maxUncompressedBytes)
                    return false;
            }
            return totalLength > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsUnsafeArchivePath(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath) || normalizedPath.StartsWith('/'))
            return true;

        var firstSeparator = normalizedPath.IndexOf('/');
        var firstComponent = firstSeparator >= 0
            ? normalizedPath[..firstSeparator]
            : normalizedPath;
        return firstComponent.EndsWith(':');
    }

    public static bool IsZipWithinLimits(
        string zipPath,
        string requiredEntryExtension,
        int maxEntries = MaxArchiveEntries,
        long maxUncompressedBytes = MaxArchiveUncompressedBytes)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > maxEntries)
                return false;

            long totalLength = 0;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                if (!TryGetSafeFileName(entry.FullName, requiredEntryExtension, out _))
                    return false;

                totalLength += entry.Length;
                if (totalLength > maxUncompressedBytes)
                    return false;
            }

            return totalLength > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
