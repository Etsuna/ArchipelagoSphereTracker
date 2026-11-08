using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Discord;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class PreparationScheduleTests
{
    private const string Yaml = "name: Alice\ngame: Hollow Knight\nHollow Knight: {}\n";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026,11,8,12,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Generator : IPreparationGenerator
    {
        public bool Fail;
        public int Runs;
        public string[] Contents = [];
        public Task<PreparationGenerationResult> RunAsync(string input,string output,bool testOnly,bool skip)
        {
            if (Fail) return Task.FromResult(new PreparationGenerationResult(false,"generation rejected"));
            if (testOnly) return Task.FromResult(new PreparationGenerationResult(true,"valid"));
            Runs++;
            Contents = Directory.GetFiles(input,"*.yaml").Select(File.ReadAllText).ToArray();
            Directory.CreateDirectory(output);
            var archive=Path.Combine(output,"archive.zip"); File.WriteAllText(archive,"zip");
            return Task.FromResult(new PreparationGenerationResult(true,"generated",archive));
        }
    }
    private static PreparationService Service(TestDatabaseScope scope,Generator generator,Clock clock,
        Func<string,string,string,Task<bool>>? manager=null)
        => new(generator,Path.Combine(scope.BaseDirectory,"preparations"),clock,manager);
    private static Task<PreparationRoom> Create(PreparationService service,int? hours=72)
        => service.CreateAsync("g","parent","c","owner","Timed room",false,timerHours:hours);
    private static async Task Ready(PreparationService service,string user="ready")
    {
        await service.JoinAsync("g","c",user,user);
        await service.UploadAsync("g","c",user,"player.yaml",Yaml);
        await service.WaitForJobsAsync();
        await service.ReadyAsync("g","c",user,true);
    }
    private static Task NoNotice(PreparationRoom room,IReadOnlyList<PreparationReminder> users,bool due) => Task.CompletedTask;

    [Fact]
    public async Task Deadline_survives_restart_and_concurrent_ticks_generate_once_with_only_complete_ready_participants()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); await Create(service); await Ready(service);
        await service.JoinAsync("g","c","missing","missing");
        await service.JoinAsync("g","c","not-ready","not-ready");
        await service.UploadAsync("g","c","not-ready","valid.yaml",Yaml); await service.WaitForJobsAsync();
        await service.JoinAsync("g","c","invalid","invalid");
        await service.UploadAsync("g","c","invalid","bad.yaml",Yaml); await service.WaitForJobsAsync();
        await Db.WriteAsync(async connection => {
            using var cmd=connection.CreateCommand();
            cmd.CommandText="UPDATE PreparationFiles SET Validation='Invalid' WHERE UserId='invalid';"; await cmd.ExecuteNonQueryAsync();
        });
        clock.Now += TimeSpan.FromHours(72);
        var restarted=Service(scope,generator,clock);
        var notices=new List<(string[] Users,bool Due)>();
        Task Notice(PreparationRoom room,IReadOnlyList<PreparationReminder> users,bool due)
        { notices.Add((users.Select(user=>user.UserId).ToArray(),due)); return Task.CompletedTask; }
        await Task.WhenAll(service.RunSchedulesOnceAsync(Notice),restarted.RunSchedulesOnceAsync(Notice));
        await service.WaitForJobsAsync(); await restarted.WaitForJobsAsync();
        Assert.Equal(1,generator.Runs); Assert.Single(generator.Contents);
        var sent=Assert.Single(notices); Assert.True(sent.Due);
        Assert.Equal(new[]{"invalid","missing","not-ready"},sent.Users.OrderBy(user=>user));
        var room=(await restarted.GetAsync("g","c"))!;
        Assert.Equal(PreparationState.Generated,room.State);
        Assert.Equal(4,room.Participants.Count); Assert.Equal(3,room.Files.Count); // excluded inputs retained until delivery
        await restarted.RunSchedulesOnceAsync(Notice); Assert.Equal(1,generator.Runs);
    }

    [Fact]
    public async Task Reminders_are_targeted_persistent_and_do_not_replay_all_missed_thresholds()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); await Create(service); await Ready(service);
        await service.JoinAsync("g","c","late","late");
        var reminders=new List<string[]>();
        Task Send(PreparationRoom room,IReadOnlyList<PreparationReminder> users,bool due)
        { Assert.False(due); reminders.Add(users.Select(user=>user.UserId).ToArray()); return Task.CompletedTask; }
        await service.RunSchedulesOnceAsync(Send); Assert.Empty(reminders);
        clock.Now += TimeSpan.FromHours(48); await service.RunSchedulesOnceAsync(Send);
        await Service(scope,generator,clock).RunSchedulesOnceAsync(Send); Assert.Single(reminders);
        clock.Now += TimeSpan.FromHours(23); await service.RunSchedulesOnceAsync(Send);
        Assert.Equal(2,reminders.Count); Assert.All(reminders,users=>Assert.Equal(new[]{"late"},users));
        Assert.Equal(3,(await service.GetAsync("g","c"))!.Schedule!.ReminderStage);
    }

    [Fact]
    public async Task No_eligible_participants_closes_without_generating_and_can_be_rescheduled()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); await Create(service,1); await service.JoinAsync("g","c","late","late");
        clock.Now += TimeSpan.FromHours(1);
        await Assert.ThrowsAsync<PreparationException>(()=>service.UploadAsync("g","c","late","late.yaml",Yaml));
        await service.RunSchedulesOnceAsync(NoNotice); await service.WaitForJobsAsync();
        var room=(await service.GetAsync("g","c"))!;
        Assert.Equal(PreparationState.Closed,room.State); Assert.Equal("Failed",room.Schedule!.State);
        Assert.NotNull(room.LastError); Assert.Equal(0,generator.Runs);
        await service.RunSchedulesOnceAsync(NoNotice); Assert.Equal(0,generator.Runs);
        await service.ScheduleAsync("g","c","owner",2); await Ready(service,"late");
        clock.Now += TimeSpan.FromHours(2); await service.RunSchedulesOnceAsync(NoNotice); await service.WaitForJobsAsync();
        Assert.Equal(1,generator.Runs);
    }

    [Fact]
    public async Task Manual_generation_consumes_timer_and_failed_generation_is_not_automatically_retried()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); await Create(service,1); await Ready(service);
        generator.Fail=true;
        await service.GenerateAsync("g","c","owner",false); await service.WaitForJobsAsync();
        Assert.Equal("Consumed",(await service.GetAsync("g","c"))!.Schedule!.State);
        generator.Fail=false; clock.Now += TimeSpan.FromHours(5);
        await service.RunSchedulesOnceAsync(NoNotice); Assert.Equal(0,generator.Runs);
        await service.ScheduleAsync("g","c","owner",1); generator.Fail=true; clock.Now+=TimeSpan.FromHours(1);
        await service.RunSchedulesOnceAsync(NoNotice); await service.WaitForJobsAsync();
        Assert.Equal("Failed",(await service.GetAsync("g","c"))!.Schedule!.State);
        generator.Fail=false; await service.RunSchedulesOnceAsync(NoNotice); Assert.Equal(0,generator.Runs);
    }

    [Fact]
    public async Task Failed_discord_notice_cannot_strand_the_claimed_generation()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); await Create(service,1); await Ready(service); clock.Now+=TimeSpan.FromHours(1);
        await service.RunSchedulesOnceAsync((_,_,_)=>throw new IOException("Discord unavailable"));
        await service.WaitForJobsAsync(); Assert.Equal(1,generator.Runs);
    }

    [Fact]
    public async Task Timer_changes_cancellation_and_live_management_permissions_are_enforced()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator(); var granted=true;
        var service=Service(scope,generator,clock,(g,c,u)=>Task.FromResult(g=="g" && c=="c" && u=="manager" && granted));
        await Create(service); Assert.True(await service.CanManageAsync("g","c","manager"));
        await service.ScheduleAsync("g","c","manager",24);
        Assert.Equal(clock.Now.AddHours(24),(await service.GetAsync("g","c"))!.Schedule!.Deadline);
        await service.SetStateAsync("g","c","manager",PreparationState.Closed);
        await service.SetStateAsync("g","c","manager",PreparationState.Open);
        await Assert.ThrowsAsync<PreparationException>(()=>service.ScheduleAsync("g","c","stranger",1));
        granted=false; await Assert.ThrowsAsync<PreparationException>(()=>service.ScheduleAsync("g","c","manager",1));
        await service.ScheduleAsync("g","c","owner",0); Assert.Null((await service.GetAsync("g","c"))!.Schedule);
        clock.Now+=TimeSpan.FromDays(10); await service.RunSchedulesOnceAsync(NoNotice); Assert.Equal(0,generator.Runs);
        await service.ScheduleAsync("g","c","owner",1);
        await service.SetStateAsync("g","c","owner",PreparationState.Cancelled);
        Assert.Equal(0,await TestDatabaseScope.CountRowsAsync("PreparationSchedules"));
    }

    [Fact]
    public async Task Timers_observe_manual_reminder_cooldown_and_manual_management_still_works()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock,(_,_,user)=>Task.FromResult(user=="manager"));
        await Create(service,25); await service.JoinAsync("g","c","late","late");
        clock.Now+=TimeSpan.FromMinutes(59);
        await service.RemindAsync("g","c","manager",_=>Task.CompletedTask);
        clock.Now+=TimeSpan.FromMinutes(1);
        var notices=0;
        await service.RunSchedulesOnceAsync((_,_,_)=>{notices++;return Task.CompletedTask;});
        Assert.Equal(0,notices);
        await service.ScheduleAsync("g","c","manager",0);
        await Ready(service,"late");
        await service.GenerateAsync("g","c","manager",false); await service.WaitForJobsAsync();
        Assert.Equal(1,generator.Runs);
    }

    [Fact]
    public async Task Restart_preserves_future_deadline_and_stops_an_interrupted_generation_from_restarting()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock); var room=await Create(service,24);
        await service.RecoverInterruptedAsync(); Assert.Equal("Scheduled",(await service.GetAsync("g","c"))!.Schedule!.State);
        await Db.WriteAsync(async connection=>{
            using var cmd=connection.CreateCommand();
            cmd.CommandText="UPDATE PreparationSchedules SET State='Started'; UPDATE RoomPreparations SET State='Validating';";
            await cmd.ExecuteNonQueryAsync();
        });
        await Service(scope,generator,clock).RecoverInterruptedAsync();
        Assert.Equal("Interrupted",(await service.GetAsync("g","c"))!.Schedule!.State);
        clock.Now+=TimeSpan.FromDays(5); await service.RunSchedulesOnceAsync(NoNotice); Assert.Equal(0,generator.Runs);
    }

    [Fact]
    public async Task Portal_managers_can_manage_the_preparation_without_access_to_other_players_yaml()
    {
        using var scope=new TestDatabaseScope(); var clock=new Clock(); var generator=new Generator();
        var service=Service(scope,generator,clock,(_,_,user)=>Task.FromResult(user=="manager"));
        await Create(service); await Ready(service);
        var previous=Declare.IsArchipelagoMode; Declare.IsArchipelagoMode=true;
        var user=DispatchProxy.Create<IGuildUser,PreparationTests.PreparationUserProxy>();
        var builder=WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app=builder.Build();
        PreparationWeb.Map(app,(g,c,token)=>Task.FromResult<AstPortalActor?>(g=="g" && c=="c" ?
            new(token,user,new AstAuthorizationContext(true,false,false,false,false,false,false,IsDelegatedGuildManager:token=="manager")) : null),service);
        try
        {
            await app.StartAsync();
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http=new HttpClient {BaseAddress=new Uri(address)};
            string Route(string actor)=>$"/api/portal/g/c/{actor}/preparation";
            using var view=JsonDocument.Parse(await http.GetStringAsync(Route("manager")));
            Assert.True(view.RootElement.GetProperty("canManage").GetBoolean());
            Assert.Empty(view.RootElement.GetProperty("files").EnumerateArray());
            var file=(await service.GetAsync("g","c"))!.Files.Single();
            Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(Route("manager")+"/files/"+file.Id)).StatusCode);
            using var schedule=new FormUrlEncodedContent(new Dictionary<string,string>{{"hours","48"}});
            Assert.Equal(HttpStatusCode.OK,(await http.PostAsync(Route("manager")+"/schedule",schedule)).StatusCode);
            using var forged=new FormUrlEncodedContent(new Dictionary<string,string>{{"hours","1"},{"userId","manager"}});
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PostAsync(Route("outsider")+"/schedule",forged)).StatusCode);
            Assert.Equal(clock.Now.AddHours(48),(await service.GetAsync("g","c"))!.Schedule!.Deadline);
        }
        finally { await app.StopAsync(); Declare.IsArchipelagoMode=previous; }
    }

    [Theory]
    [InlineData("-1")][InlineData("721")][InlineData("1.5")][InlineData("1e2")][InlineData("NaN")]
    public void Timer_rejects_invalid_hours(string input) => Assert.Throws<PreparationException>(()=>PreparationService.ParseTimerHours(input));
}
