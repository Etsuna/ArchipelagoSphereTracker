using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Discord;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class PreparationTests
{
    private const string Alice = "name: Alice\ngame: Hollow Knight\nHollow Knight:\n  secret: alice-private\n";
    private const string Bob = "name: Bob\ngame: A Link to the Past\nA Link to the Past:\n  secret: bob-private\n";
    private static PreparationService Service(TestDatabaseScope scope, FakeGenerator generator)
        => new(generator, Path.Combine(scope.BaseDirectory,"preparations"));

    private static async Task<PreparationRoom> Create(PreparationService service, int capacity = 20)
        => await service.CreateAsync("guild", "parent", "thread", "owner", "Weekly room", false, capacity);

    private static async Task AddReady(PreparationService service, string actor, string content)
    {
        await service.JoinAsync("guild","thread",actor,actor);
        await service.UploadAsync("guild","thread",actor,"player.yaml",content);
        await service.WaitForJobsAsync();
        await service.ReadyAsync("guild","thread",actor,true);
    }

    [Fact]
    public async Task Archive_delivery_retries_failed_sends_and_survives_restart_without_resending()
    {
        using var scope = new TestDatabaseScope();
        var generator = new FakeGenerator();
        var service = Service(scope, generator);
        await Create(service);
        var sends = 0;
        Task<string> Send(PreparationRoom room, string archive)
        {
            Assert.Equal(PreparationState.Generated, room.State);
            Assert.True(File.Exists(archive));
            sends++;
            return Task.FromResult("discord-message");
        }
        await service.DeliverArchiveAsync("guild", "thread", Send);
        Assert.Equal(0, sends);
        await AddReady(service, "alice", Alice);
        await service.GenerateAsync("guild", "thread", "owner", false);
        await service.WaitForJobsAsync();
        await Assert.ThrowsAsync<IOException>(() => service.DeliverArchiveAsync("guild", "thread", (_, _) => throw new IOException("Discord unavailable")));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.DeliverArchiveAsync("guild", "thread", Send)));
        Assert.Equal(1, sends);
        var restarted = Service(scope, generator);
        await restarted.DeliverArchiveAsync("guild", "thread", Send);
        Assert.Equal(1, sends);
        Assert.Null(await restarted.GetAsync("guild", "thread"));
        Assert.Empty(Directory.GetDirectories(Path.Combine(scope.BaseDirectory, "preparations")));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationRuns"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationFiles"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationParticipants"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationArchiveDeliveries"));
        await Create(restarted);
    }

    [Fact]
    public async Task Cancellation_waits_for_a_running_validation_before_removing_its_working_files()
    {
        using var scope = new TestDatabaseScope();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generator = new FakeGenerator { BeforeRun = async (_, _, _) => { entered.TrySetResult(); await release.Task; } };
        var service = Service(scope, generator); var room = await Create(service);
        await service.JoinAsync("guild", "thread", "alice", "Alice");
        await service.UploadAsync("guild", "thread", "alice", "player.yaml", Alice);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var cancellation = service.SetStateAsync("guild", "thread", "owner", PreparationState.Cancelled);
        try
        {
            Assert.False(cancellation.IsCompleted);
            await Assert.ThrowsAsync<PreparationException>(() => service.JoinAsync("guild", "thread", "other", "Other"));
        }
        finally { release.TrySetResult(); }
        await cancellation.WaitAsync(TimeSpan.FromSeconds(5)); await service.WaitForJobsAsync();
        Assert.Null(await service.GetAsync("guild", "thread"));
        Assert.False(Directory.Exists(Path.Combine(scope.BaseDirectory, "preparations", room.Id)));
    }

    [Fact]
    public async Task Restart_finishes_cleanup_for_a_generation_whose_delivery_was_recorded()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope, new()); await Create(service); await AddReady(service, "alice", Alice);
        await service.GenerateAsync("guild", "thread", "owner", false); await service.WaitForJobsAsync();
        var room = (await service.GetAsync("guild", "thread"))!;
        await Db.WriteAsync(async connection => {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO PreparationArchiveDeliveries(RunId,MessageId,DeliveredAtUtc) VALUES(@run,'sent',@now)";
            command.Parameters.AddWithValue("@run", room.RunId);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
        File.Delete(room.ZipPath!); // Simulates partially completed cleanup after the successful send.
        var restarted = Service(scope, new()); await restarted.RecoverInterruptedAsync();
        Assert.Null(await restarted.GetAsync("guild", "thread"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationArchiveDeliveries"));
        Assert.False(Directory.Exists(Path.Combine(scope.BaseDirectory, "preparations", room.Id)));
    }

    [Fact]
    public async Task Cancellation_removes_files_snapshots_reminders_and_frees_the_channel()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope, new());
        var initial = await Create(service);
        await AddReady(service, "alice", Alice);
        await service.ReadyAsync("guild", "thread", "alice", false);
        await service.RemindAsync("guild", "thread", "owner", _ => Task.CompletedTask);
        await service.GenerateAsync("guild", "thread", "owner", false, testOnly:true);
        await service.WaitForJobsAsync();
        Assert.True(Directory.Exists(Path.Combine(scope.BaseDirectory, "preparations", initial.Id)));
        await service.SetStateAsync("guild", "thread", "owner", PreparationState.Cancelled);
        Assert.Null(await service.GetAsync("guild", "thread"));
        Assert.False(Directory.Exists(Path.Combine(scope.BaseDirectory, "preparations", initial.Id)));
        foreach (var table in new[] { "PreparationParticipants", "PreparationFiles", "PreparationRuns", "PreparationReminders", "PreparationArchiveDeliveries" })
            Assert.Equal(0, await TestDatabaseScope.CountRowsAsync(table));
        Assert.NotEqual(initial.Id, (await Create(service)).Id);
    }

    [Fact]
    public async Task Fallback_archive_survives_preparation_cleanup_but_expires_after_24_hours()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope, new()); await Create(service); await AddReady(service, "alice", Alice);
        await service.GenerateAsync("guild", "thread", "owner", false); await service.WaitForJobsAsync();
        string? token = null;
        await service.DeliverArchiveAsync("guild", "thread", async (_, archive) => {
            token = await service.CreateTemporaryDownloadAsync(archive);
            return "link-message";
        });
        Assert.Null(await service.GetAsync("guild", "thread"));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationRuns"));
        Assert.Equal(64, token!.Length);
        var path = service.GetTemporaryDownload(token);
        Assert.NotNull(path); Assert.Equal("test", await File.ReadAllTextAsync(path));
        Assert.Null(service.GetTemporaryDownload("../" + token));
        var restarted = Service(scope, new());
        Assert.Equal(path, restarted.GetTemporaryDownload(token));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-25));
        restarted.CleanupExpiredDownloads();
        Assert.Null(restarted.GetTemporaryDownload(token)); Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Reminders_only_target_unready_uploaders_and_enforce_owner_and_persistent_cooldown()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope, new()); await Create(service);
        await AddReady(service, "ready", Alice);
        await AddReady(service, "waiting", Bob); await service.ReadyAsync("guild", "thread", "waiting", false);
        await service.JoinAsync("guild", "thread", "empty", "Empty");
        var sent = new List<string>();
        Task Send(IReadOnlyList<string> users) { sent.AddRange(users); return Task.CompletedTask; }
        await Assert.ThrowsAsync<PreparationException>(() => service.RemindAsync("guild", "thread", "waiting", Send));
        await Assert.ThrowsAsync<PreparationException>(() => service.RemindAsync("other", "thread", "owner", Send));
        await service.RemindAsync("guild", "thread", "owner", Send);
        Assert.Equal(new[] { "waiting" }, sent);
        await Assert.ThrowsAsync<PreparationException>(() => service.RemindAsync("guild", "thread", "owner", Send));
        var restarted = Service(scope, new());
        await Assert.ThrowsAsync<PreparationException>(() => restarted.RemindAsync("guild", "thread", "owner", Send));
        await service.ReadyAsync("guild", "thread", "waiting", true);
        await Assert.ThrowsAsync<PreparationException>(() => service.RemindAsync("guild", "thread", "owner", Send));
        Assert.Single(sent);
    }

    [Fact]
    public async Task Registration_is_idempotent_capacity_is_atomic_and_closed_rooms_reject_new_players()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope,new()); await Create(service,2);
        await service.JoinAsync("guild","thread","alice","Alice");
        await service.JoinAsync("guild","thread","alice","Alice");
        var results = await Task.WhenAll(Enumerable.Range(0,10).Select(async index =>
        {
            try { await service.JoinAsync("guild","thread","p"+index,"Player"); return true; }
            catch (PreparationException) { return false; }
        }));
        Assert.Single(results, success => success);
        Assert.Equal(2,(await service.GetAsync("guild","thread"))!.Participants.Count);
        await service.SetStateAsync("guild","thread","owner",PreparationState.Closed);
        await Assert.ThrowsAsync<PreparationException>(() => service.JoinAsync("guild","thread","other","Other"));
        await service.SetStateAsync("guild","thread","owner",PreparationState.Open);
        await service.LeaveAsync("guild","thread","alice");
        await service.JoinAsync("guild","thread","other","Other");
        Assert.Equal(2,(await service.GetAsync("guild","thread"))!.Participants.Count);
    }

    [Fact]
    public async Task Readiness_requires_actual_validation_and_replacing_files_resets_only_their_owner()
    {
        using var scope = new TestDatabaseScope();
        var generator = new FakeGenerator(); var service = Service(scope,generator); await Create(service);
        await service.JoinAsync("guild","thread","alice","Alice");
        await Assert.ThrowsAsync<PreparationException>(() => service.ReadyAsync("guild","thread","alice",true));
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        generator.BeforeRun = async (_,_,_) => { entered.TrySetResult(true); await release.Task; };
        await service.UploadAsync("guild","thread","alice","player.yaml",Alice);
        await entered.Task;
        await Assert.ThrowsAsync<PreparationException>(() => service.ReadyAsync("guild","thread","alice",true));
        release.SetResult(true); await service.WaitForJobsAsync(); generator.BeforeRun = null;
        await service.ReadyAsync("guild","thread","alice",true);
        await AddReady(service,"bob",Bob);
        generator.Fail = true;
        await service.UploadAsync("guild","thread","alice","player.yaml",Alice.Replace("Alice","NewAlice"));
        await service.WaitForJobsAsync();
        var room = (await service.GetAsync("guild","thread"))!;
        Assert.False(room.Participants.Single(player => player.UserId == "alice").Ready);
        Assert.True(room.Participants.Single(player => player.UserId == "bob").Ready);
        Assert.Equal("Invalid",room.Files.Single(file => file.UserId == "alice").Validation);
        await Assert.ThrowsAsync<PreparationException>(() => service.ReadyAsync("guild","thread","alice",true));
        Assert.False(room.CanGenerate);
    }

    [Fact]
    public async Task Files_with_identical_names_are_isolated_and_neither_owner_nor_other_players_can_delete_them()
    {
        using var scope = new TestDatabaseScope();
        var service = Service(scope,new()); await Create(service);
        await AddReady(service,"alice",Alice); await AddReady(service,"bob",Bob);
        var room = (await service.GetAsync("guild","thread"))!;
        Assert.Equal(2,room.Files.Count);
        Assert.Equal(2,room.Files.Select(file => file.Id).Distinct().Count());
        var bobFile = room.Files.Single(file => file.UserId == "bob");
        await Assert.ThrowsAsync<PreparationException>(() => service.RemoveFileAsync("guild","thread","alice",bobFile.Id));
        await Assert.ThrowsAsync<PreparationException>(() => service.RemoveFileAsync("guild","thread","owner",bobFile.Id));
        await Assert.ThrowsAsync<PreparationException>(() => service.RemoveFileAsync("other-guild","thread","bob",bobFile.Id));
        await service.RemoveFileAsync("guild","thread","bob",bobFile.Id);
        room = (await service.GetAsync("guild","thread"))!;
        Assert.Single(room.Files); Assert.False(room.Participants.Single(player => player.UserId=="bob").Ready);
    }

    [Fact]
    public async Task Generation_freezes_a_complete_snapshot_blocks_concurrent_mutations_and_only_runs_once()
    {
        using var scope = new TestDatabaseScope();
        var generator = new FakeGenerator(); var service = Service(scope,generator); await Create(service);
        await AddReady(service,"alice",Alice); await AddReady(service,"bob",Bob);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        generator.BeforeRun = async (input,_,test) =>
        {
            if (test && Directory.GetFiles(input,"*.yaml").Length==2) { entered.TrySetResult(true); await release.Task; }
        };
        await Assert.ThrowsAsync<PreparationException>(() => service.GenerateAsync("guild","thread","alice",false));
        await service.GenerateAsync("guild","thread","owner",true); await entered.Task;
        var frozen = (await service.GetAsync("guild","thread"))!;
        Assert.Equal(PreparationState.Validating,frozen.State);
        await Assert.ThrowsAsync<PreparationException>(() => service.GenerateAsync("guild","thread","owner",false));
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadAsync("guild","thread","alice","player.yaml",Bob));
        await Assert.ThrowsAsync<PreparationException>(() => service.LeaveAsync("guild","thread","bob"));
        await Assert.ThrowsAsync<PreparationException>(() => service.ReadyAsync("guild","thread","bob",false));
        await Assert.ThrowsAsync<PreparationException>(() => service.SetStateAsync("guild","thread","owner",PreparationState.Cancelled));
        await using (var connection=await Db.OpenReadAsync())
        {
            using var command=connection.CreateCommand(); command.CommandText="SELECT SnapshotJson FROM PreparationRuns";
            var snapshot=(string)(await command.ExecuteScalarAsync())!;
            Assert.Contains("alice-private",snapshot); Assert.Contains("bob-private",snapshot);
        }
        release.SetResult(true); await service.WaitForJobsAsync();
        var generated=(await service.GetAsync("guild","thread"))!;
        Assert.Equal(PreparationState.Generated,generated.State);
        Assert.Equal(1,generator.Generations);
        Assert.Equal(new[]{Alice,Bob}.OrderBy(value=>value),generator.GeneratedContents.OrderBy(value=>value));
        Assert.True(generator.SkipBalancing);
        Assert.NotNull(service.GetArchivePath(generated,"owner")); Assert.NotNull(service.GetArchivePath(generated,"alice"));
        Assert.Null(service.GetArchivePath(generated,"outsider"));
        await Assert.ThrowsAsync<PreparationException>(() => service.GenerateAsync("guild","thread","owner",false));
    }

    [Fact]
    public async Task Failed_combined_validation_keeps_the_roster_and_files_and_allows_a_retry()
    {
        using var scope = new TestDatabaseScope();
        var generator = new FakeGenerator(); var service=Service(scope,generator); await Create(service); await AddReady(service,"alice",Alice);
        generator.Fail=true;
        await service.GenerateAsync("guild","thread","owner",false); await service.WaitForJobsAsync();
        var failed=(await service.GetAsync("guild","thread"))!;
        Assert.Equal(PreparationState.Closed,failed.State); Assert.Single(failed.Files); Assert.Single(failed.Participants);
        Assert.Equal(0,generator.Generations); Assert.NotNull(failed.LastError);
        generator.Fail=false;
        await service.GenerateAsync("guild","thread","owner",false); await service.WaitForJobsAsync();
        Assert.Equal(PreparationState.Generated,(await service.GetAsync("guild","thread"))!.State);
    }

    [Fact]
    public async Task A_test_only_run_does_not_generate_and_does_not_require_every_player_to_be_ready()
    {
        using var scope = new TestDatabaseScope();
        var generator=new FakeGenerator(); var service=Service(scope,generator); await Create(service); await AddReady(service,"alice",Alice);
        await service.ReadyAsync("guild","thread","alice",false);
        await service.GenerateAsync("guild","thread","owner",false,testOnly:true); await service.WaitForJobsAsync();
        var room=(await service.GetAsync("guild","thread"))!;
        Assert.Equal(PreparationState.Closed,room.State); Assert.Equal("Validated",room.LastRunStatus);
        Assert.Equal(0,generator.Generations); Assert.False(room.CanGenerate);
        await service.UploadAsync("guild","thread","alice","player.yaml",Alice);
        await service.WaitForJobsAsync();
        Assert.Null((await service.GetAsync("guild","thread"))!.LastRunStatus);
    }

    [Fact]
    public async Task Restart_recovery_preserves_data_and_requires_interrupted_files_to_be_validated_again()
    {
        using var scope = new TestDatabaseScope();
        var service=Service(scope,new()); await Create(service); await AddReady(service,"alice",Alice);
        await Db.WriteAsync(async connection =>
        {
            using var command=connection.CreateCommand();
            command.CommandText="UPDATE RoomPreparations SET State='Generating'; UPDATE PreparationFiles SET Validation='Pending';";
            await command.ExecuteNonQueryAsync();
        });
        var restarted=Service(scope,new()); await restarted.RecoverInterruptedAsync();
        var room=(await restarted.GetAsync("guild","thread"))!;
        Assert.Equal(PreparationState.Closed,room.State); Assert.Single(room.Participants); Assert.Single(room.Files);
        Assert.Equal(Alice,room.Files[0].Content); Assert.Equal("Invalid",room.Files[0].Validation);
        Assert.False(room.Participants[0].Ready); Assert.NotNull(room.LastError);
        await restarted.UploadAsync("guild","thread","alice","player.yaml",Alice); await restarted.WaitForJobsAsync();
        await restarted.ReadyAsync("guild","thread","alice",true);
        Assert.True((await restarted.GetAsync("guild","thread"))!.CanGenerate);
    }

    [Fact]
    public async Task Public_view_never_exposes_YAML_contents_hashes_other_players_files_or_output_paths()
    {
        using var scope = new TestDatabaseScope();
        var service=Service(scope,new()); await Create(service); await AddReady(service,"alice",Alice); await AddReady(service,"bob",Bob);
        var room=(await service.GetAsync("guild","thread"))!;
        var json=JsonSerializer.Serialize(PreparationWeb.View(room,"alice",service));
        Assert.DoesNotContain("alice-private",json); Assert.DoesNotContain("bob-private",json);
        Assert.DoesNotContain("Content",json); Assert.DoesNotContain("Hash",json); Assert.DoesNotContain("ZipPath",json);
        using var view=JsonDocument.Parse(json);
        Assert.Single(view.RootElement.GetProperty("files").EnumerateArray());
        Assert.False(view.RootElement.GetProperty("canManage").GetBoolean());
        Assert.False(view.RootElement.GetProperty("canGenerate").GetBoolean());
    }

    [Fact]
    public async Task Organizer_permissions_and_guild_scope_are_enforced_for_every_state_change()
    {
        using var scope = new TestDatabaseScope(); var service=Service(scope,new()); await Create(service);
        await Assert.ThrowsAsync<PreparationException>(() => service.SetStateAsync("guild","thread","other",PreparationState.Closed));
        await Assert.ThrowsAsync<PreparationException>(() => service.SetStateAsync("other","thread","owner",PreparationState.Cancelled));
        await Assert.ThrowsAsync<PreparationException>(() => service.UploadAsync("guild","thread","outsider","a.yaml",Alice));
        await service.SetStateAsync("guild","thread","owner",PreparationState.Cancelled);
        await Assert.ThrowsAsync<PreparationException>(() => service.JoinAsync("guild","thread","alice","Alice"));
        await Assert.ThrowsAsync<PreparationException>(() => service.SetStateAsync("guild","thread","owner",PreparationState.Open));
        Assert.Null(await service.GetAsync("guild","thread"));
        await Create(service);
    }

    [Fact]
    public async Task Linking_claim_is_atomic_and_cannot_be_claimed_twice()
    {
        using var scope = new TestDatabaseScope(); var service=Service(scope,new()); await Create(service); await AddReady(service,"alice",Alice);
        await service.GenerateAsync("guild","thread","owner",false); await service.WaitForJobsAsync();
        await Assert.ThrowsAsync<PreparationException>(()=>service.BeginAttachAsync("guild","thread","alice"));
        await service.BeginAttachAsync("guild","thread","owner");
        await Assert.ThrowsAsync<PreparationException>(()=>service.BeginAttachAsync("guild","thread","owner"));
        await service.CompleteAttachAsync("guild","thread","owner","tracking");
        var room=(await service.GetAsync("guild","thread"))!; Assert.Equal("tracking",room.TrackingChannelId);
        await Assert.ThrowsAsync<PreparationException>(()=>service.BeginAttachAsync("guild","thread","owner"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("name: Alice")]
    [InlineData("- invalid-root")]
    [InlineData("game: [broken")]
    public void Invalid_YAML_is_rejected(string content) => Assert.Throws<PreparationException>(()=>PreparationService.ParseSlots(content));

    [Fact]
    public async Task YAML_limits_include_size_nesting_and_path_traversal()
    {
        using var scope = new TestDatabaseScope(); var service=Service(scope,new()); await Create(service); await service.JoinAsync("guild","thread","alice","Alice");
        await Assert.ThrowsAsync<PreparationException>(()=>service.UploadAsync("guild","thread","alice","../player.yaml",Alice));
        using var oversized=new MemoryStream(new byte[PreparationService.MaximumYamlBytes+1]);
        await Assert.ThrowsAsync<PreparationException>(()=>PreparationService.ReadYamlAsync(oversized));
        Assert.Throws<PreparationException>(()=>PreparationService.ParseSlots("game: Test\noptions: "+new string('[',80)+"0"+new string(']',80)));
        var slots=PreparationService.ParseSlots(Alice+"---\n"+Bob);
        Assert.Equal(2,slots.Count); Assert.Equal("Alice",slots[0].Name); Assert.Equal("A Link to the Past",slots[1].Game);
    }

    private sealed class FakeGenerator : IPreparationGenerator
    {
        public bool Fail {get;set;}
        public int Generations {get;private set;}
        public bool SkipBalancing {get;private set;}
        public string[] GeneratedContents {get;private set;}=[];
        public Func<string,string,bool,Task>? BeforeRun {get;set;}
        public async Task<PreparationGenerationResult> RunAsync(string input,string output,bool testOnly,bool skipBalancing)
        {
            if(BeforeRun!=null) await BeforeRun(input,output,testOnly);
            if(Fail) return new(false,"test failure");
            if(testOnly) return new(true,"validated");
            Generations++; SkipBalancing=skipBalancing;
            GeneratedContents=Directory.GetFiles(input,"*.yaml").Select(File.ReadAllText).ToArray();
            Directory.CreateDirectory(output); var path=Path.Combine(output,"generated.zip"); await File.WriteAllTextAsync(path,"test");
            return new(true,"generated",path);
        }
    }

    [Fact]
    public async Task Web_routes_enforce_mode_token_scope_file_ownership_and_organizer_generation()
    {
        using var scope = new TestDatabaseScope();
        var previous = Declare.IsArchipelagoMode; Declare.IsArchipelagoMode = true;
        var service=Service(scope,new()); await Create(service); await AddReady(service,"alice",Alice); await AddReady(service,"bob",Bob);
        await service.ReadyAsync("guild","thread","alice",false); await service.ReadyAsync("guild","thread","bob",false);
        var user=DispatchProxy.Create<IGuildUser,PreparationUserProxy>();
        var builder=WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app=builder.Build();
        PreparationWeb.Map(app,(guild,channel,token)=> Task.FromResult(
            guild=="guild" && channel=="thread" && new[]{"owner","alice","bob","outsider","denied"}.Contains(token)
                ? new AstPortalActor(token,user,new AstAuthorizationContext(true,false,false,false,false,false,false,IsArchipelagoAccessDenied:token=="denied"))
                : null),service);
        try
        {
            await app.StartAsync();
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http=new HttpClient {BaseAddress=new Uri(address)};
            string Route(string token)=>$"/api/portal/guild/thread/{token}/preparation";
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(Route("invalid"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(Route("denied"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync("/api/portal/other/thread/alice/preparation")).StatusCode);
            var room=(await service.GetAsync("guild","thread"))!;
            var bobFile=room.Files.Single(file=>file.UserId=="bob");
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(Route("alice")+"/files/"+bobFile.Id)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await http.GetAsync(Route("bob")+"/files/"+bobFile.Id)).StatusCode);
            var json=await http.GetStringAsync(Route("alice")); Assert.DoesNotContain("bob-private",json); Assert.DoesNotContain("alice-private",json);
            using var forged=new FormUrlEncodedContent(new Dictionary<string,string>{{"userId","bob"}});
            Assert.Equal(HttpStatusCode.OK,(await http.PostAsync(Route("alice")+"/ready",forged)).StatusCode);
            room=(await service.GetAsync("guild","thread"))!;
            Assert.True(room.Participants.Single(player=>player.UserId=="alice").Ready);
            Assert.False(room.Participants.Single(player=>player.UserId=="bob").Ready);
            // Discord attachments and web uploads enter the same stream-based pipeline.
            using (var attachment = new MemoryStream(Encoding.UTF8.GetBytes("name: EtsunaZillion1\ngame: Zillion\nZillion:\n")))
                await service.UploadStreamAsync("guild", "thread", "alice", "player.yaml", attachment);
            await service.WaitForJobsAsync();
            room = (await service.GetAsync("guild", "thread"))!;
            Assert.Single(room.Files, file => file.UserId == "alice");
            Assert.Equal("EtsunaZillion1", room.Files.Single(file => file.UserId == "alice").Slots.Single().Name);
            Assert.False(room.Participants.Single(player => player.UserId == "alice").Ready);
            using (var upload = new MultipartFormDataContent())
            {
                upload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(Alice)), "file", "player.yaml");
                Assert.Equal(HttpStatusCode.OK, (await http.PostAsync(Route("alice") + "/upload", upload)).StatusCode);
            }
            await service.WaitForJobsAsync();
            room = (await service.GetAsync("guild", "thread"))!;
            Assert.Single(room.Files, file => file.UserId == "alice");
            Assert.Equal("Alice", room.Files.Single(file => file.UserId == "alice").Slots.Single().Name);
            using var remove=new FormUrlEncodedContent(new Dictionary<string,string>{{"fileId",bobFile.Id}});
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PostAsync(Route("alice")+"/remove",remove)).StatusCode);
            using var generate=new FormUrlEncodedContent(new Dictionary<string,string>());
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PostAsync(Route("alice")+"/generate",generate)).StatusCode);
            var temporaryArchive = Path.Combine(scope.BaseDirectory, "download.zip");
            await File.WriteAllTextAsync(temporaryArchive, "downloadable");
            var downloadToken = await service.CreateTemporaryDownloadAsync(temporaryArchive);
            await service.SetStateAsync("guild", "thread", "owner", PreparationState.Cancelled);
            Assert.Equal(HttpStatusCode.Gone, (await http.GetAsync(Route("alice"))).StatusCode);
            Assert.Equal("downloadable", await http.GetStringAsync("/downloads/preparations/" + downloadToken));
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/downloads/preparations/" + new string('0',64))).StatusCode);
            Declare.IsArchipelagoMode=false;
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/downloads/preparations/" + downloadToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(Route("alice"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync("/portal/guild/thread/alice/prepare.html")).StatusCode);
        }
        finally { await app.StopAsync(); Declare.IsArchipelagoMode=previous; }
    }

    public class PreparationUserProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method,object?[]? args)
            => method?.Name switch { "get_Username"=>"tester", "get_Id"=>1UL, _=>null };
    }
}
