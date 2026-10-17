using System.IO.Compression;
using System.Text;
using ArchipelagoSphereTracker.src.Resources;

public static class PreparationUploadReader
{
    public const int MaximumZipBytes = 16 * 1024 * 1024;

    public static async Task<IReadOnlyList<PreparationYamlUpload>> ReadAsync(string fileName, Stream stream)
    {
        if (!FileUploadSecurity.TryGetSafeFileName(fileName, ".zip", out _))
        {
            if (!FileUploadSecurity.TryGetSafeFileName(fileName, [".yaml", ".yml"], out _))
                throw new PreparationException(Resource.PrepInvalidYaml);
            return [new(fileName, await PreparationService.ReadYamlAsync(stream))];
        }
        using var compressed = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (compressed.Length + read > MaximumZipBytes) throw InvalidZip();
            await compressed.WriteAsync(buffer.AsMemory(0, read));
        }
        compressed.Position = 0;
        try
        {
            using var archive = new ZipArchive(compressed, ZipArchiveMode.Read, leaveOpen:true);
            if (archive.Entries.Count is < 1 or > 100) throw InvalidZip();
            var files = new List<PreparationYamlUpload>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var type = (entry.ExternalAttributes >> 16) & 0xF000;
                if (path.StartsWith('/') || parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':') || part.Contains('\0')) ||
                    type is not (0 or 0x8000 or 0x4000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw InvalidZip();
                if (path.EndsWith('/')) continue;
                var name = parts[^1];
                if (!FileUploadSecurity.TryGetSafeFileName(name, [".yaml", ".yml"], out _) || !names.Add(name) ||
                    files.Count >= 20 || entry.Length is <= 0 or > PreparationService.MaximumYamlBytes)
                    throw InvalidZip();
                totalBytes += entry.Length;
                if (totalBytes > MaximumZipBytes) throw InvalidZip();
                await using var content = entry.Open();
                try { files.Add(new(name, await PreparationService.ReadYamlAsync(content))); }
                catch (PreparationException error) { throw new PreparationException(name + " : " + error.Message); }
            }
            if (files.Count == 0 || files.Sum(file => (long)Encoding.UTF8.GetByteCount(file.Content)) > MaximumZipBytes) throw InvalidZip();
            return files;
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException)
        {
            throw InvalidZip();
        }
    }

    private static PreparationException InvalidZip() => new(Resource.ResourceManager.GetString("PrepInvalidYamlZip")!);
}
