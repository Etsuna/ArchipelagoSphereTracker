using System.IO.Compression;

public static class FileUploadSecurity
{
    public const int MaxArchiveEntries = 500;
    public const long MaxArchiveUncompressedBytes = 256L * 1024 * 1024;

    public static bool TryGetSafeFileName(string? submittedName, string requiredExtension, out string safeFileName)
    {
        safeFileName = Path.GetFileName(submittedName ?? string.Empty);
        return !string.IsNullOrWhiteSpace(safeFileName) &&
               string.Equals(submittedName, safeFileName, StringComparison.Ordinal) &&
               safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               safeFileName.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task CopyToFileWithLimitAsync(
        Stream source,
        string destinationPath,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = destinationPath + ".upload-" + Guid.NewGuid().ToString("N");
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
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static bool IsZipWithinLimits(
        string zipPath,
        string requiredEntryExtension,
        int maxEntries = MaxArchiveEntries,
        long maxUncompressedBytes = MaxArchiveUncompressedBytes)
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
}
