using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

public class FileUploadSecurityTests
{
    [Theory]
    [InlineData("player.yaml", ".yaml")]
    [InlineData("world.apworld", ".apworld")]
    [InlineData("players.ZIP", ".zip")]
    public void TryGetSafeFileName_AcceptsExpectedExtension(string name, string extension)
    {
        Assert.True(FileUploadSecurity.TryGetSafeFileName(name, extension, out var safe));
        Assert.Equal(name, safe);
    }

    [Theory]
    [InlineData("../player.yaml", ".yaml")]
    [InlineData("folder/player.yaml", ".yaml")]
    [InlineData("folder\\player.yaml", ".yaml")]
    [InlineData("player.yaml.exe", ".yaml")]
    public void TryGetSafeFileName_RejectsTraversalAndWrongExtension(string name, string extension)
    {
        Assert.False(FileUploadSecurity.TryGetSafeFileName(name, extension, out _));
    }

    [Fact]
    public void IsZipWithinLimits_RejectsNestedAndUnexpectedEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ast-security-{System.Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("nested/player.yaml").Open());
                writer.Write("name: player");
            }

            Assert.False(FileUploadSecurity.IsZipWithinLimits(path, ".yaml"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void IsZipWithinLimits_RejectsCorruptedArchive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ast-security-{System.Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllText(path, "not a zip archive");
            Assert.False(FileUploadSecurity.IsZipWithinLimits(path, ".yaml"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task CopyToFileWithLimitAsync_DoesNotLeavePartialDestination()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ast-security-{System.Guid.NewGuid():N}.bin");
        try
        {
            await using var source = new MemoryStream(new byte[32]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                FileUploadSecurity.CopyToFileWithLimitAsync(source, path, maxBytes: 16));

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Validation_failure_keeps_existing_file_and_clears_quarantine()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ast-quarantine-{System.Guid.NewGuid():N}");
        var quarantine = Path.Combine(root, "quarantine");
        var destination = Path.Combine(root, "active", "player.yaml");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, "existing: true");
            await using var source = new MemoryStream(Encoding.UTF8.GetBytes("invalid"));

            var accepted = await FileUploadSecurity.CopyValidatedToFileWithLimitAsync(
                source,
                destination,
                1024,
                _ => false,
                quarantinePath: quarantine);

            Assert.False(accepted);
            Assert.Equal("existing: true", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(quarantine));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CleanupExpiredQuarantineFiles_Removes_only_expired_opaque_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ast-quarantine-{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var expired = Path.Combine(root, "old.quarantine");
            var current = Path.Combine(root, "current.quarantine");
            var unrelated = Path.Combine(root, "keep.txt");
            File.WriteAllText(expired, "old");
            File.WriteAllText(current, "current");
            File.WriteAllText(unrelated, "keep");
            var now = new System.DateTimeOffset(2026, 8, 31, 12, 0, 0, System.TimeSpan.Zero);
            File.SetLastWriteTimeUtc(expired, now.AddHours(-2).UtcDateTime);
            File.SetLastWriteTimeUtc(current, now.AddMinutes(-10).UtcDateTime);

            var removed = FileUploadSecurity.CleanupExpiredQuarantineFiles(
                root,
                now,
                System.TimeSpan.FromHours(1));

            Assert.Equal(1, removed);
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(current));
            Assert.True(File.Exists(unrelated));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CleanupExpiredSpoilerLogs_Removes_only_expired_supported_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ast-spoilers-{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var expired = Path.Combine(root, "old.txt");
            var current = Path.Combine(root, "current.json");
            var unrelated = Path.Combine(root, "keep.zip");
            File.WriteAllText(expired, "old");
            File.WriteAllText(current, "{}");
            File.WriteAllText(unrelated, "keep");
            var now = new System.DateTimeOffset(2026, 8, 31, 12, 0, 0, System.TimeSpan.Zero);
            File.SetLastWriteTimeUtc(expired, now.AddDays(-31).UtcDateTime);
            File.SetLastWriteTimeUtc(current, now.AddDays(-1).UtcDateTime);

            var removed = SpoilerLogClass.CleanupExpiredSpoilerLogs(
                "unused-in-test",
                now,
                System.TimeSpan.FromDays(30),
                root);

            Assert.Equal(1, removed);
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(current));
            Assert.True(File.Exists(unrelated));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task SpoilerLogStreamUpload_ValidatesAndReplacesTheActiveFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ast-spoiler-upload-{System.Guid.NewGuid():N}");
        var previousBasePath = Declare.BasePath;
        var previousQuarantinePath = Declare.UploadQuarantinePath;
        try
        {
            Declare.BasePath = root;
            Declare.UploadQuarantinePath = Path.Combine(root, "quarantine");
            var spoilerFolder = SpoilerLogClass.GetSpoilerFolder("room");
            Directory.CreateDirectory(spoilerFolder);
            await File.WriteAllTextAsync(Path.Combine(spoilerFolder, "old.txt"), "old spoiler");
            await using var source = new MemoryStream(Encoding.UTF8.GetBytes("0: {\n  Test (Finder): Item (Receiver)\n}\n"));

            var message = await SpoilerLogClass.SendSpoilerLogFromStreamAsync("room", "new.txt", source);

            Assert.Contains("new.txt", message);
            Assert.True(File.Exists(Path.Combine(spoilerFolder, "new.txt")));
            Assert.False(File.Exists(Path.Combine(spoilerFolder, "old.txt")));
        }
        finally
        {
            Declare.BasePath = previousBasePath;
            Declare.UploadQuarantinePath = previousQuarantinePath;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Content_validators_reject_binary_text_invalid_json_and_archive_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ast-validation-{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var binary = Path.Combine(root, "binary.txt");
            var invalidJson = Path.Combine(root, "invalid.json");
            var validJson = Path.Combine(root, "valid.json");
            File.WriteAllBytes(binary, [0x61, 0x00, 0x62]);
            File.WriteAllText(invalidJson, "not-json");
            File.WriteAllText(validJson, "{\"playthrough\": {}}");

            Assert.False(FileUploadSecurity.IsSafeTextFile(binary));
            Assert.False(FileUploadSecurity.IsValidSpoilerFile(invalidJson, requireJson: true));
            Assert.True(FileUploadSecurity.IsValidSpoilerFile(validJson, requireJson: true));

            var safeArchive = Path.Combine(root, "safe.apworld");
            using (var archive = ZipFile.Open(safeArchive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("world/data.py").Open()))
                writer.Write("value = 1");
            Assert.True(FileUploadSecurity.IsArchiveWithinLimits(safeArchive));

            var unsafeArchive = Path.Combine(root, "unsafe.apworld");
            using (var archive = ZipFile.Open(unsafeArchive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("../escape.py").Open()))
                writer.Write("value = 1");
            Assert.False(FileUploadSecurity.IsArchiveWithinLimits(unsafeArchive));

            var rootedArchive = Path.Combine(root, "rooted.apworld");
            using (var archive = ZipFile.Open(rootedArchive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("C:/escape.py").Open()))
                writer.Write("value = 1");
            Assert.False(FileUploadSecurity.IsArchiveWithinLimits(rootedArchive));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
