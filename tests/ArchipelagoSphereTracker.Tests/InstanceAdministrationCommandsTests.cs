using System;
using System.Threading.Tasks;
using Xunit;

public sealed class InstanceAdministrationCommandsTests
{
    [Fact]
    public async Task Inventory_IncludesConfiguredAndV2OnlyRooms()
    {
        using var scope = new TestDatabaseScope();
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            "100", "200", "https://example.com/room/test", "room", "tracker",
            silent: false, checkFrequency: "5m", port: "0");
        await Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO TrackedRooms
                    (GuildId, ChannelId, CreatedAtUtc, UpdatedAtUtc, IsBaselineInitialized)
                VALUES ('100', '201', @Now, @Now, 0);";
            command.Parameters.AddWithValue("@Now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });

        var guild = Assert.Single(await InstanceAdministrationCommands.GetGuildsAsync());
        Assert.Equal(2, guild.RoomCount);
        var rooms = await InstanceAdministrationCommands.GetRoomsAsync("100");
        Assert.Collection(rooms,
            room => Assert.Equal("200", room.ChannelId),
            room =>
            {
                Assert.Equal("201", room.ChannelId);
                Assert.Equal(string.Empty, room.BaseUrl);
            });
    }
}
