using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public sealed class PreparationBatchUploadTests
{
    private const string Yaml = "name: Player\ngame: Zillion\nZillion: {}\n";
    private sealed class Generator : IPreparationGenerator
    {
        public Task<PreparationGenerationResult> RunAsync(string input, string output, bool testOnly, bool skipBalancing)
            => Task.FromResult(new PreparationGenerationResult(true, "valid"));
    }

    private static async Task<PreparationService> Create(TestDatabaseScope scope)
    {
        var service = new PreparationService(new Generator(), Path.Combine(scope.BaseDirectory, "preparations"));
        await service.CreateAsync("guild", "channel", "channel", "owner", "Group", false);
        await service.JoinAsync("guild", "channel", "alice", "Alice");
        return service;
    }

    private static MemoryStream Zip(params (string Name, string Content, int? Attributes)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var item in entries)
            {
                var entry = zip.CreateEntry(item.Name);
                if (item.Attributes is { } attributes) entry.ExternalAttributes = attributes;
                using var writer = new StreamWriter(entry.Open()); writer.Write(item.Content);
            }
        stream.Position = 0; return stream;
    }

    [Fact]
    public async Task Batch_replaces_only_own_files_resets_only_own_readiness_and_validates_each_file()
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        await service.JoinAsync("guild", "channel", "bob", "Bob");
        foreach (var user in new[] { "alice", "bob" })
        {
            await service.UploadAsync("guild", "channel", user, "player.yaml", Yaml);
            await service.WaitForJobsAsync(); await service.ReadyAsync("guild", "channel", user, true);
        }
        var bobFile = (await service.GetAsync("guild", "channel"))!.Files.Single(file => file.UserId == "bob");
        await service.UploadBatchAsync("guild", "channel", "alice", [new("player.yaml", Yaml.Replace("Player", "Replacement")), new("extra.yml", Yaml)]);
        await service.WaitForJobsAsync();
        var room = (await service.GetAsync("guild", "channel"))!;
        Assert.Equal(2, room.Files.Count(file => file.UserId == "alice"));
        Assert.Equal(bobFile.Id, room.Files.Single(file => file.UserId == "bob").Id);
        Assert.All(room.Files, file => Assert.Equal("Valid", file.Validation));
        Assert.False(room.Participants.Single(player => player.UserId == "alice").Ready);
        Assert.True(room.Participants.Single(player => player.UserId == "bob").Ready);
    }

    [Fact]
    public async Task Invalid_batch_is_atomic_and_does_not_replace_an_existing_ready_players_file()
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        await service.UploadAsync("guild", "channel", "alice", "player.yaml", Yaml);
        await service.WaitForJobsAsync(); await service.ReadyAsync("guild", "channel", "alice", true);
        var original = (await service.GetAsync("guild", "channel"))!.Files.Single();
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadBatchAsync("guild", "channel", "alice",
            [new("player.yaml", Yaml.Replace("Player", "New")), new("bad.yaml", "name: MissingGame")]));
        var room = (await service.GetAsync("guild", "channel"))!;
        Assert.Equal(original.Id, room.Files.Single().Id); Assert.True(room.Participants.Single().Ready);
    }

    [Fact]
    public async Task Adding_a_batch_over_the_player_limit_does_not_import_its_first_file()
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        await service.UploadBatchAsync("guild", "channel", "alice", Enumerable.Range(0,19).Select(index => new PreparationYamlUpload($"{index}.yaml", Yaml)).ToArray());
        await service.WaitForJobsAsync();
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadBatchAsync("guild", "channel", "alice", [new("19.yaml", Yaml), new("20.yaml", Yaml)]));
        Assert.Equal(19, (await service.GetAsync("guild", "channel"))!.Files.Count);
    }

    [Fact]
    public async Task Zip_upload_flattens_safe_folders_and_imports_multiple_yaml_files()
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        using var zip = Zip(("players/", "", null), ("players/alice.yaml", Yaml, null), ("players/extra.yml", Yaml, null));
        await service.UploadStreamAsync("guild", "channel", "alice", "players.zip", zip);
        await service.WaitForJobsAsync();
        var room = (await service.GetAsync("guild", "channel"))!;
        Assert.Equal(new[] { "alice.yaml", "extra.yml" }, room.Files.Select(file => file.FileName));
        Assert.All(room.Files, file => Assert.Equal("Valid", file.Validation));
    }

    [Theory]
    [InlineData("../escape.yaml")]
    [InlineData("/absolute.yaml")]
    [InlineData("C:/escape.yaml")]
    [InlineData("folder/../escape.yaml")]
    [InlineData("readme.txt")]
    public async Task Unsafe_zip_entries_are_rejected_without_importing_any_file(string name)
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        using var zip = Zip(("valid.yaml", Yaml, null), (name, Yaml, null));
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadStreamAsync("guild", "channel", "alice", "players.zip", zip));
        Assert.Empty((await service.GetAsync("guild", "channel"))!.Files);
    }

    [Fact]
    public async Task Zip_rejects_symlinks_duplicate_basenames_oversized_entries_and_too_many_files()
    {
        using var link = Zip(("link.yaml", Yaml, unchecked((int)0xA1FF0000)));
        using var duplicates = Zip(("one/player.yaml", Yaml, null), ("two/player.yaml", Yaml, null));
        using var large = Zip(("large.yaml", new string('x', PreparationService.MaximumYamlBytes + 1), null));
        using var many = Zip(Enumerable.Range(0,21).Select(index => ($"{index}.yaml", Yaml, (int?)null)).ToArray());
        foreach (var stream in new[] { link, duplicates, large, many })
            await Assert.ThrowsAsync<PreparationException>(() => PreparationUploadReader.ReadAsync("players.zip", stream));
    }

    [Fact]
    public async Task Duplicate_names_in_a_multi_file_form_are_rejected_without_import()
    {
        using var scope = new TestDatabaseScope(); var service = await Create(scope);
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadBatchAsync("guild", "channel", "alice", [new("player.yaml", Yaml), new("PLAYER.yaml", Yaml)]));
        Assert.Empty((await service.GetAsync("guild", "channel"))!.Files);
    }
}
