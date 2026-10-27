using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Xunit;

public sealed class RoomDestinationTests
{
    [Theory]
    [InlineData(null, RoomDestinationMode.PrivateThread)]
    [InlineData("", RoomDestinationMode.PrivateThread)]
    [InlineData("Private", RoomDestinationMode.PrivateThread)]
    [InlineData("Public", RoomDestinationMode.PublicThread)]
    [InlineData("Channel", RoomDestinationMode.Channel)]
    public void DestinationValuesPreserveBothThreadModesAndThePrivateDefault(string? value, RoomDestinationMode expected)
    {
        Assert.True(UrlClass.TryResolveDestination(value, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("unknown")]
    public void UnknownDestinationValuesAreRejected(string value)
        => Assert.False(UrlClass.TryResolveDestination(value, out _));

    [Fact]
    public async Task DirectChannelCreationUsesTheOriginalChannelWithoutCreatingOrJoiningAThread()
    {
        var calls = new List<string>();
        var source = Channel(calls);
        var destination = await UrlClass.CreateDestinationAsync(RoomDestinationMode.Channel, source, "", null);
        Assert.Same(source, destination);
        Assert.Equal((ulong)10, destination.Id);
        Assert.DoesNotContain("CreateThreadAsync", calls);
        Assert.DoesNotContain("AddUserAsync", calls);
        Assert.DoesNotContain("SendMessageAsync", calls);
    }

    [Theory]
    [InlineData(RoomDestinationMode.PrivateThread, ThreadType.PrivateThread)]
    [InlineData(RoomDestinationMode.PublicThread, ThreadType.PublicThread)]
    public async Task ThreadCreationPreservesTheChosenVisibilityAndAddsOnlyItsCreator(
        RoomDestinationMode mode, ThreadType expectedType)
    {
        var calls = new List<string>();
        ThreadType? requestedType = null;
        var creator = Proxy<IGuildUser>((method, _) => method.Name == "get_Id" ? (ulong)123 : throw new InvalidOperationException(method.Name));
        var thread = Proxy<IThreadChannel>((method, arguments) =>
        {
            calls.Add(method.Name);
            switch (method.Name)
            {
                case "get_Name": return "My game";
                case "get_Id": return (ulong)20;
                case "SendMessageAsync": return Task.FromResult<IUserMessage>(null!);
                case "AddUserAsync":
                    Assert.Same(creator, arguments![0]);
                    return Task.CompletedTask;
                default: throw new InvalidOperationException(method.Name);
            }
        });
        var source = Proxy<ITextChannel>((method, arguments) =>
        {
            calls.Add(method.Name);
            Assert.Equal("CreateThreadAsync", method.Name);
            var parameters = method.GetParameters();
            requestedType = (ThreadType)arguments![Array.FindIndex(parameters, parameter => parameter.Name == "type")]!;
            Assert.Equal("My game", arguments[0]);
            return Task.FromResult(thread);
        });
        var destination = await UrlClass.CreateDestinationAsync(mode, source, "My game", creator);
        Assert.Same(thread, destination);
        Assert.Equal(expectedType, requestedType);
        Assert.Equal(1, calls.Count(value => value == "CreateThreadAsync"));
        Assert.Equal(1, calls.Count(value => value == "AddUserAsync"));
        Assert.DoesNotContain("GetUsersAsync", calls);
    }

    [Theory]
    [InlineData("recap", false, true, true)]
    [InlineData("info", false, true, true)]
    [InlineData("delete-url", false, true, true)]
    [InlineData("recap", true, true, true)]
    [InlineData("recap", false, false, false)]
    [InlineData("add-url", false, true, false)]
    [InlineData("ast-portal", false, true, false)]
    [InlineData("generate", false, true, false)]
    public void TrackedChannelCommandsUseRoomDataWithoutHijackingServerCommands(
        string command, bool isThread, bool isTrackedRoom, bool expected)
        => Assert.Equal(expected, BotCommands.UsesRoomCommandRoute(command, isThread, isTrackedRoom));

    [Fact]
    public async Task OccupiedDirectChannelIsRejectedWithoutDiscordOrWebHostSideEffects()
    {
        using var scope = new TestDatabaseScope();
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO ChannelsAndUrlsTable
                (GuildId,ChannelId,BaseUrl,Room,Tracker,CheckFrequency,Silent)
                VALUES ('1','10','https://archipelago.example','original-room','original-tracker','5m',0);";
            await command.ExecuteNonQueryAsync();
        });
        var calls = new List<string>();
        var wasBigAsync = Declare.IsBigAsync;
        try
        {
            Declare.IsBigAsync = false;
            var options = new UrlClass.UrlAddOptions("https://archipelago.example/room/replacement", "", "Channel", false, "5m", null);
            var result = await UrlClass.AddUrlFromSetupAsync(options, "10", "1", Channel(calls));
            Assert.False(result.Success);
            Assert.Equal(Resource.URLAlreadySet, result.Message);
            var configuration = await ChannelsAndUrlsCommands.GetChannelConfigAsync("1", "10");
            Assert.Equal("original-room", configuration.room);
            Assert.Equal("original-tracker", configuration.tracker);
            Assert.All(calls, call => Assert.StartsWith("get_", call));
        }
        finally { Declare.IsBigAsync = wasBigAsync; }
    }

    private static ITextChannel Channel(List<string> calls)
        => Proxy<ITextChannel>((method, _) =>
        {
            calls.Add(method.Name);
            return method.Name switch
            {
                "get_Id" => (ulong)10,
                "get_GuildId" => (ulong)1,
                "get_Name" => "existing-channel",
                _ => throw new InvalidOperationException("Unexpected Discord operation: " + method.Name)
            };
        });

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, RoomDestinationDiscordProxy>();
        ((RoomDestinationDiscordProxy)(object)proxy).Handler = invoke;
        return proxy;
    }
}

public class RoomDestinationDiscordProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}
