using System.IO;
using System.IO.Compression;
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
}
