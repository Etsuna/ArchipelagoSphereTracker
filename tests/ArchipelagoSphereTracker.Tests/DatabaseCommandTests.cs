using System.Globalization;
using System.Data.SQLite;
using System;
using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DatabaseCommandTests
{
    [Fact]
    public async Task AddOrEditUrlChannelAsync_PersistsConfigAndCache()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-1";
        var channelId = "channel-1";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/alpha",
            "alpha",
            "tracker-1",
            silent: true,
            checkFrequency: "15m",
            port: "1234");

        var config = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guildId, channelId);

        Assert.Equal("tracker-1", config.tracker);
        Assert.Equal("https://example.com", config.baseUrl);
        Assert.Equal("alpha", config.room);
        Assert.True(config.silent);
        Assert.Equal("15m", config.checkFrequency);
        Assert.Equal("1234", config.port);

        Assert.True(ChannelConfigCache.TryGet(guildId, channelId, out var cached));
        Assert.Equal("tracker-1", cached.Tracker);
        Assert.Equal("https://example.com", cached.BaseUrl);
        Assert.Equal("alpha", cached.Room);
        Assert.True(cached.Silent);

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand(@"
            SELECT Room, Tracker
            FROM ChannelsAndUrlsTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection);
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("alpha", reader["Room"]?.ToString());
        Assert.Equal("tracker-1", reader["Tracker"]?.ToString());
    }

    [Fact]
    public async Task AddOrEditUrlChannelPathAsync_StoresPatchAndLookup()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-2";
        var channelId = "channel-2";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/beta",
            "beta",
            "tracker-2",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        var patches = new List<Patch>
        {
            new Patch { GameAlias = "PlayerOne", GameName = "GameA", PatchLink = "https://example.com/patch" }
        };

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patches);

        var result = await ChannelsAndUrlsCommands.GetPatchAndGameNameForAlias(
            guildId,
            channelId,
            "Alias (PlayerOne)");

        Assert.Equal("GameA : https://example.com/patch", result);

        await using var connection = await Db.OpenReadAsync();
        using var command = new SQLiteCommand("SELECT Patch FROM UrlAndChannelPatchTable LIMIT 1;", connection);
        Assert.Equal("https://example.com/patch", (await command.ExecuteScalarAsync())?.ToString());
    }

    [Fact]
    public async Task GetChannelIdForRoomAsync_ReturnsChannelId()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-3";
        var channelId = "channel-3";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/gamma",
            "gamma",
            "tracker-3",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        var found = await ChannelsAndUrlsCommands.GetChannelIdForRoomAsync(
            guildId,
            "https://example.com/room/gamma",
            "gamma");

        Assert.Equal(channelId, found);
    }

    [Fact]
    public async Task UpdateLastCheckAsync_PersistsTimestamp()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-4";
        var channelId = "channel-4";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/delta",
            "delta",
            "tracker-4",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        await ChannelsAndUrlsCommands.UpdateLastCheckAsync(guildId, channelId);

        var config = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guildId, channelId);
        Assert.False(string.IsNullOrWhiteSpace(config.lastCheck));
    }

    [Fact]
    public async Task UpdateChannelPortAsync_UpdatesPortValue()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-5";
        var channelId = "channel-5";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/epsilon",
            "epsilon",
            "tracker-5",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        var updated = await ChannelsAndUrlsCommands.UpdateChannelPortAsync(guildId, channelId, "9999");
        Assert.True(updated);

        var config = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guildId, channelId);
        Assert.Equal("9999", config.port);
    }

    [Fact]
    public async Task CountChannelByGuildId_EnforcesLimit()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-6";

        Assert.True(Declare.MaxThreadByGuild > 0);

        for (var i = 0; i < Declare.MaxThreadByGuild - 1; i++)
        {
            await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
                guildId,
                $"channel-{i}",
                "https://example.com/room/zeta",
                $"room-{i}",
                "tracker",
                silent: false,
                checkFrequency: "5m",
                port: "0");
        }

        Assert.True(await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId));

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            $"channel-{Declare.MaxThreadByGuild - 1}",
            "https://example.com/room/zeta",
            $"room-{Declare.MaxThreadByGuild - 1}",
            "tracker",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        Assert.False(await ChannelsAndUrlsCommands.CountChannelByGuildId(guildId));
    }

    [Fact]
    public async Task DeleteChannelDataAsync_RemovesRelatedRows()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-7";
        var channelId = "channel-7";

        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/eta",
            "eta",
            "tracker",
            silent: false,
            checkFrequency: "5m",
            port: "0");

        var channelTableId = await DatabaseCommands.GetGuildChannelIdAsync(guildId, channelId, "ChannelsAndUrlsTable");

        await using (var conn = await Db.OpenWriteAsync())
        {
            using var cmd = new SQLiteCommand(@"
                INSERT INTO UrlAndChannelPatchTable (ChannelsAndUrlsTableId, Alias, GameName, Patch)
                VALUES (@Id, @Alias, @Game, @Patch);", conn);
            cmd.Parameters.AddWithValue("@Id", channelTableId);
            cmd.Parameters.AddWithValue("@Alias", "Alias");
            cmd.Parameters.AddWithValue("@Game", "Game");
            cmd.Parameters.AddWithValue("@Patch", "Patch");
            await cmd.ExecuteNonQueryAsync();
        }

        await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);

        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("ChannelsAndUrlsTable", guildId, channelId));
        Assert.Equal(0, await TestDatabaseScope.CountRowsAsync("UrlAndChannelPatchTable"));
    }

    [Fact]
    public async Task UpdateLastItemCheckAsync_PersistsAndReadsTimestamp()
    {
        using var scope = new TestDatabaseScope();
        var guildId = "guild-8";
        var channelId = "channel-8";

        await ChannelsAndUrlsCommands.UpdateLastItemCheckAsync(guildId, channelId);

        var result = await ChannelsAndUrlsCommands.GetLastItemCheckAsync(guildId, channelId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdatePollingPolicyFromWeb_ValidatesBoundsAndPersistsPolicy()
    {
        using var scope = new TestDatabaseScope();
        const string guildId = "guild-policy";
        const string channelId = "channel-policy";
        await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(
            guildId,
            channelId,
            "https://example.com/room/policy",
            "policy",
            "tracker",
            silent: false,
            checkFrequency: "30m",
            port: "0");

        var rejected = await ChannelsAndUrlsCommands.UpdatePollingPolicyFromWeb(
            "automatic",
            "15m",
            channelId,
            guildId);
        Assert.NotEmpty(rejected);

        await ChannelsAndUrlsCommands.UpdatePollingPolicyFromWeb(
            "fixed",
            "1h",
            channelId,
            guildId);

        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT PollingMode || ':' || MaximumCheckFrequency
            FROM ChannelsAndUrlsTable
            WHERE GuildId = @GuildId AND ChannelId = @ChannelId;";
        command.Parameters.AddWithValue("@GuildId", guildId);
        command.Parameters.AddWithValue("@ChannelId", channelId);
        Assert.Equal("Fixed:1h", await command.ExecuteScalarAsync());
    }
}
