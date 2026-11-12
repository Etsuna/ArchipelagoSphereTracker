using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class PreparationHostingTests
{
    private const string Seed = "abcdefghijklmnopqrstuv";
    private const string Room = "ABCDEFGHIJKLMNOPQRSTUV";
    private const string Yaml = "name: Player{number}\ngame: Hollow Knight\nHollow Knight: {}\n";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Tracking_preference_is_stored_even_without_automatic_hosting(bool? trackInThread)
    {
        using var database = new TestDatabaseScope();
        var service = new PreparationService(new Generator(), Path.Combine(database.BaseDirectory, "preparations"));
        await service.CreateAsync("g", "channel", "channel", "owner", "Preparation", false, trackInThread:trackInThread);
        await service.RecoverInterruptedAsync();
        var room = Assert.IsType<PreparationRoom>(await service.GetAsync("g", "channel"));
        Assert.Equal(trackInThread, room.TrackInThread);
    }

    [Fact]
    public async Task WebHost_uploads_the_archive_then_reuses_the_room_in_the_same_session()
    {
        using var database = new TestDatabaseScope();
        var zip = Path.Combine(database.BaseDirectory,"generated.zip");
        await File.WriteAllTextAsync(zip,"archive");
        var session = Guid.NewGuid();
        var handler = new WebHostHandler(session);
        using var client = new HttpClient(handler);
        var origin = new Uri("https://example.test");
        string? seed = null;
        Task SaveUpload(string? value) { seed = value; return Task.CompletedTask; }
        Assert.Equal("https://example.test/room/"+Room,await PreparationWebHost.HostAsync(origin,session,zip,new(null,false,SaveUpload),client));
        Assert.Equal("https://example.test/room/"+Room,await PreparationWebHost.HostAsync(origin,session,zip,new(seed,true,SaveUpload),client));
        Assert.Equal(1,handler.Uploads);
        Assert.Equal(1,handler.RoomCreations);
    }

    [Fact]
    public async Task WebHost_rejects_an_upload_redirect_to_another_origin()
    {
        using var database = new TestDatabaseScope();
        var zip = Path.Combine(database.BaseDirectory,"generated.zip");
        await File.WriteAllTextAsync(zip,"archive");
        var session = Guid.NewGuid();
        var handler = new WebHostHandler(session) { ForeignRedirect = true };
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => PreparationWebHost.HostAsync(new Uri("https://example.test"),session,zip,
            new(null,false,_ => Task.CompletedTask),client));
        Assert.Equal(0,handler.RoomCreations);
    }

    [Fact]
    public async Task Shared_session_recovers_only_the_current_seed_after_a_lost_creation_response()
    {
        using var database = new TestDatabaseScope();
        var zip = Path.Combine(database.BaseDirectory,"generated.zip");
        await File.WriteAllTextAsync(zip,"archive");
        var session = Guid.NewGuid();
        var handler = new WebHostHandler(session) { OtherRooms = true, LoseRoomResponse = true };
        using var client = new HttpClient(handler);
        var origin = new Uri("https://example.test");
        string? seed = null;
        Task SaveUpload(string? value) { seed = value; return Task.CompletedTask; }
        await Assert.ThrowsAsync<HttpRequestException>(() => PreparationWebHost.HostAsync(origin,session,zip,new(null,false,SaveUpload),client));
        Assert.Equal(Seed,seed);
        var url = await PreparationWebHost.HostAsync(origin,session,zip,new(seed,true,SaveUpload),client);
        Assert.Equal("https://example.test/room/"+Room,url);
        Assert.Equal(1,handler.Uploads);
        Assert.Equal(1,handler.RoomCreations);
    }

    [Fact]
    public async Task An_uncertain_shared_upload_does_not_upload_again_or_reuse_an_unrelated_room()
    {
        var session = Guid.NewGuid();
        var handler = new WebHostHandler(session) { OtherRooms = true };
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<PreparationException>(() => PreparationWebHost.HostAsync(new Uri("https://example.test"),
            session,"unused.zip",new(null,true,_ => throw new InvalidOperationException()),client));
        Assert.Equal(0,handler.Uploads);
        Assert.Equal(0,handler.RoomCreations);
    }

    [Fact]
    public void Session_link_uses_the_selected_webhost_and_the_id_must_be_a_uuid()
    {
        var id = Guid.NewGuid();
        Assert.Equal("https://example.test:8443/session", PreparationWebHost.SessionPage("https://example.test:8443/"));
        Assert.Equal(id, PreparationWebHost.ParseSession(id.ToString("D")));
        Assert.Throws<PreparationException>(() => PreparationWebHost.ParseSession(null));
        Assert.Throws<PreparationException>(() => PreparationWebHost.ParseSession("https://example.test/session"));
    }

    [Fact]
    public async Task Hosted_timer_uses_the_organizers_encrypted_session_after_restart()
    {
        using var database = new TestDatabaseScope();
        var root = Path.Combine(database.BaseDirectory,"preparations");
        var clock = new TestClock();
        var service = new PreparationService(new Generator(), root, clock);
        var room = await service.CreateAsync("g","parent","thread","owner","Hosted",true,webHost:"https://example.test");
        var session = Guid.NewGuid();
        await AddReadyAsync(service,"100");
        await Assert.ThrowsAsync<PreparationException>(() => service.ScheduleAsync("g","thread","owner",1));
        await Assert.ThrowsAsync<PreparationException>(() => service.ScheduleAsync("g","thread","owner",1,
            webHostUserId:session.ToString("D"), expectedRoom:Guid.NewGuid().ToString("N")));
        await service.ScheduleAsync("g","thread","owner",1,webHostUserId:session.ToString("D"),expectedRoom:room.Id);
        await using (var connection = await Db.OpenReadAsync())
        {
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT ProtectedSession FROM PreparationHostingCredentials";
            var encrypted = Assert.IsType<string>(await query.ExecuteScalarAsync());
            Assert.DoesNotContain(session.ToString("D"), encrypted);
        }

        var restarted = new PreparationService(new Generator(), root, clock);
        await restarted.RecoverInterruptedAsync();
        clock.Now = clock.Now.AddHours(1);
        await restarted.RunSchedulesOnceAsync((_,_,_) => Task.CompletedTask);
        await restarted.WaitForJobsAsync();
        var generated = Assert.IsType<PreparationRoom>(await restarted.GetAsync("g","thread"));
        Assert.Equal(PreparationState.Generated, generated.State);
        Assert.Equal(session, await restarted.GetHostingSessionAsync(generated));
        await restarted.SaveHostedRoomAsync(generated,"https://example.test/room/"+Room);
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("PreparationHostingCredentials"));
    }

    [Fact]
    public async Task Failed_tracking_keeps_the_hosted_room_and_zip_for_retry_without_duplicate_delivery()
    {
        using var database = new TestDatabaseScope();
        var service = new PreparationService(new Generator(),Path.Combine(database.BaseDirectory,"preparations"));
        await service.CreateAsync("g","parent","thread","owner","Hosted",true,webHost:"https://example.test",trackInThread:true);
        await AddReadyAsync(service,"100");
        var session = Guid.NewGuid();
        await Assert.ThrowsAsync<PreparationException>(() => service.GenerateAsync("g","thread","owner",false));
        Assert.Equal(PreparationState.Open, (await service.GetAsync("g","thread"))!.State);
        await service.GenerateAsync("g","thread","owner",false,webHostUserId:session.ToString("D"));
        await service.WaitForJobsAsync();
        var deliveries = 0;
        Task<string> Deliver(PreparationRoom room,string zip)
        {
            Assert.True(File.Exists(zip));
            deliveries++;
            return Task.FromResult("archive-message");
        }
        await service.DeliverArchiveAsync("g","thread",Deliver,async (room,_) =>
        {
            Assert.Equal("100",room.Hosting!.SlotOwners[1]);
            Assert.Equal(session, await service.GetHostingSessionAsync(room));
            await service.SaveHostedRoomAsync(room,"https://example.test/room/"+Room);
            throw new IOException("Tracking temporarily unavailable");
        });
        await service.RecoverInterruptedAsync();
        var retained = Assert.IsType<PreparationRoom>(await service.GetAsync("g","thread"));
        Assert.NotNull(retained.LastError);
        Assert.True(retained.TrackInThread);
        Assert.Equal("https://example.test/room/"+Room,retained.Hosting!.RoomUrl);
        await service.DeliverArchiveAsync("g","thread",Deliver,(_,_) => throw new InvalidOperationException("Explicit retry required"));
        Assert.Equal(1,deliveries);
        await service.DeliverArchiveAsync("g","thread",Deliver,(room,_) =>
        {
            Assert.Equal(retained.Hosting.RoomUrl,room.Hosting!.RoomUrl);
            Assert.True(room.TrackInThread);
            return service.SaveHostedTrackingAsync(room,"tracking-thread",true);
        },retryHosting:true);
        Assert.Equal(1,deliveries);
        Assert.Null(await service.GetAsync("g","thread"));
    }

    [Fact]
    public async Task Ready_and_closing_notices_follow_the_actual_participation_transitions()
    {
        using var database = new TestDatabaseScope();
        var service = new PreparationService(new Generator(),Path.Combine(database.BaseDirectory,"preparations"));
        var readyNotices = new List<PreparationState>();
        var closingNotices = new List<string[]>();
        service.EveryoneReady += room => { readyNotices.Add(room.State); return Task.CompletedTask; };
        service.RegistrationsClosed += room => {
            closingNotices.Add(room.Participants.Where(player => !player.Ready).Select(player => player.UserId).ToArray());
            return Task.CompletedTask;
        };
        await service.CreateAsync("g","parent","thread","owner","Room",true);
        await AddReadyAsync(service,"100");
        await service.ReadyAsync("g","thread","100",true);
        Assert.Equal(new[] { PreparationState.Open },readyNotices);
        await service.JoinAsync("g","thread","200","Bob");
        await service.SetStateAsync("g","thread","owner",PreparationState.Closed);
        await service.SetStateAsync("g","thread","owner",PreparationState.Closed);
        Assert.Equal(new[] { "200" },Assert.Single(closingNotices));
        await AddReadyAsync(service,"200");
        Assert.Equal(new[] { PreparationState.Open,PreparationState.Closed },readyNotices);
    }

    [Fact]
    public async Task Alias_subscription_uses_the_generated_slots_and_can_be_retried()
    {
        using var database = new TestDatabaseScope();
        var slots = new[] { (1,"ResolvedName","Game"),(2,"OtherName","Game") };
        Assert.Equal(0,await UrlClass.RegisterPreparedAliasesAsync("g","c",slots,new Dictionary<int,string> { [1]="100" }));
        var owners = new Dictionary<int,string> { [1]="100",[2]="200" };
        Assert.Equal(2,await UrlClass.RegisterPreparedAliasesAsync("g","c",slots,owners));
        Assert.Equal(2,await UrlClass.RegisterPreparedAliasesAsync("g","c",slots,owners));
        Assert.Equal("100",Assert.Single(await ReceiverAliasesCommands.GetAllUsersIds("g","c","ResolvedName")));
        Assert.Equal("200",Assert.Single(await ReceiverAliasesCommands.GetAllUsersIds("g","c","OtherName")));
        Assert.Equal(2,await TestDatabaseScope.CountRowsAsync("RecapListTable"));
    }

    private static async Task AddReadyAsync(PreparationService service,string user)
    {
        // A registered player can still upload after registrations close.
        var room = (await service.GetAsync("g","thread"))!;
        if (!room.Participants.Any(player => player.UserId == user))
            await service.JoinAsync("g","thread",user,user);
        await service.UploadAsync("g","thread",user,"player.yaml",Yaml);
        await service.WaitForJobsAsync();
        await service.ReadyAsync("g","thread",user,true);
    }

    private sealed class Generator : IPreparationGenerator
    {
        public Task<PreparationGenerationResult> RunAsync(string input,string output,bool testOnly,bool skipBalancing)
        {
            if (testOnly) return Task.FromResult(new PreparationGenerationResult(true,"Valid"));
            Directory.CreateDirectory(output);
            var path = Path.Combine(output,"generated.zip");
            File.WriteAllText(path,"Archive");
            var files = Directory.GetFiles(input,"*.yaml").Select((file,index) => (index,Id:Path.GetFileNameWithoutExtension(file)))
                .ToDictionary(file => file.index+1,file => file.Id);
            return Task.FromResult(new PreparationGenerationResult(true,"Generated",path,files));
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class WebHostHandler(Guid session) : HttpMessageHandler
    {
        public int Uploads;
        public int RoomCreations;
        public bool ForeignRedirect;
        public bool OtherRooms;
        public bool LoseRoomResponse;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("example.test",request.RequestUri!.Host);
            switch (request.RequestUri.AbsolutePath)
            {
                case var path when path == "/session/"+session.ToString("D"):
                    return new(HttpStatusCode.OK);
                case "/api/get_rooms":
                    var rooms = new List<string>();
                    if (OtherRooms) rooms.Add("""{"room_id":"0000000000000000000000","seed_id":"1111111111111111111111"}""");
                    if (RoomCreations != 0) rooms.Add("{\"room_id\":\""+Room+"\",\"seed_id\":\""+Seed+"\"}");
                    return Json("["+string.Join(",",rooms)+"]");
                case "/uploads":
                    Assert.Equal(HttpMethod.Post,request.Method);
                    Assert.Contains("archive",await request.Content!.ReadAsStringAsync(ct));
                    Uploads++;
                    return Redirect((ForeignRedirect ? "https://other.test" : "")+"/seed/"+Seed);
                case var path when path == "/new_room/"+Seed:
                    RoomCreations++;
                    if (LoseRoomResponse) throw new HttpRequestException("Lost response");
                    return Redirect("/room/"+Room);
                default:
                    throw new InvalidOperationException(request.RequestUri.AbsolutePath);
            }
        }

        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
        private static HttpResponseMessage Redirect(string path)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(path,UriKind.RelativeOrAbsolute);
            return response;
        }
    }
}
