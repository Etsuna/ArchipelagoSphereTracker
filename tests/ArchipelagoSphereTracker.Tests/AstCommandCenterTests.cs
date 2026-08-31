using System;
using Xunit;

public sealed class AstCommandCenterTests
{
    [Fact]
    public void Session_is_bound_to_owner_guild_and_source_channel()
    {
        var store = new AstUiSessionStore(lifetime: TimeSpan.FromMinutes(15));
        var session = store.Start(10, 20, 30, 40);

        Assert.True(store.TryGetAuthorized(session.Id, 10, 20, 30, out var authorized));
        Assert.Equal((ulong)40, authorized.RoomChannelId);
        Assert.False(store.TryGetAuthorized(session.Id, 11, 20, 30, out _));
        Assert.False(store.TryGetAuthorized(session.Id, 10, 21, 30, out _));
        Assert.False(store.TryGetAuthorized(session.Id, 10, 20, 31, out _));
    }

    [Fact]
    public void Starting_again_invalidates_previous_session_in_same_context()
    {
        var store = new AstUiSessionStore();
        var first = store.Start(10, 20, 30, 40);
        var second = store.Start(10, 20, 30, 40);

        Assert.False(store.TryGetAuthorized(first.Id, 10, 20, 30, out _));
        Assert.True(store.TryGetAuthorized(second.Id, 10, 20, 30, out _));
    }

    [Fact]
    public void Expired_session_is_rejected_and_removed()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero));
        var store = new AstUiSessionStore(time, TimeSpan.FromMinutes(15));
        var session = store.Start(10, 20, 30, 40);

        time.Advance(TimeSpan.FromMinutes(16));

        Assert.False(store.TryGetAuthorized(session.Id, 10, 20, 30, out _));
        Assert.Equal(0, store.CleanupExpired());
    }

    [Fact]
    public void Screen_update_refreshes_session_and_preserves_scope()
    {
        var store = new AstUiSessionStore();
        var session = store.Start(10, 20, 30, 40);

        Assert.True(store.TryUpdateScreen(session.Id, 10, 20, 30, AstUiScreen.Personal, out var updated));

        Assert.Equal(AstUiScreen.Personal, updated.Screen);
        Assert.Equal(session.RoomChannelId, updated.RoomChannelId);
    }

    [Theory]
    [InlineData("astui:0123456789abcdef0123456789abcdef:personal", true, "personal")]
    [InlineData("astui:short:personal", false, "")]
    [InlineData("foreign:0123456789abcdef0123456789abcdef:personal", false, "")]
    [InlineData("astui:0123456789abcdef0123456789abcdef", false, "")]
    public void Component_ids_are_strict(string customId, bool expected, string expectedAction)
    {
        Assert.Equal(expected, AstCommandCenter.TryParseCustomId(customId, out _, out var action));
        Assert.Equal(expectedAction, action);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
