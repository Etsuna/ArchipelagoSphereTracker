using System;
using System.Linq;
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

    [Fact]
    public void Room_selection_updates_target_but_preserves_interaction_scope()
    {
        var store = new AstUiSessionStore();
        var session = store.Start(10, 20, 30, null);

        Assert.True(store.TrySelectRoom(session.Id, 10, 20, 30, 99, out var updated));

        Assert.Equal((ulong)99, updated.RoomChannelId);
        Assert.Equal((ulong)30, updated.SourceChannelId);
        Assert.Equal(AstUiScreen.Home, updated.Screen);
        Assert.True(store.TryGetAuthorized(session.Id, 10, 20, 30, out _));
        Assert.False(store.TryGetAuthorized(session.Id, 10, 20, 99, out _));
    }

    [Fact]
    public void Spoiler_options_are_private_validated_and_can_clear_sphere_limit()
    {
        var store = new AstUiSessionStore();
        var session = store.Start(10, 20, 30, 40);

        Assert.True(store.TrySetSpoilerOptions(
            session.Id, 10, 20, 30, out var configured,
            alias: "Player 1", setAlias: true, sphereLimit: 7, setSphereLimit: true,
            missingMode: "full", hideItems: false));
        Assert.Equal("Player 1", configured.SpoilerAlias);
        Assert.Equal(7, configured.SpoilerSphereLimit);
        Assert.Equal("full", configured.SpoilerMissingMode);
        Assert.False(configured.SpoilerHideItems);

        Assert.False(store.TrySetSpoilerOptions(session.Id, 11, 20, 30, out _, alias: "Other", setAlias: true));
        Assert.False(store.TrySetSpoilerOptions(session.Id, 10, 20, 30, out _, sphereLimit: -1, setSphereLimit: true));
        Assert.False(store.TrySetSpoilerOptions(session.Id, 10, 20, 30, out _, missingMode: "invalid"));
        Assert.True(store.TrySetSpoilerOptions(session.Id, 10, 20, 30, out var cleared, sphereLimit: null, setSphereLimit: true));
        Assert.Null(cleared.SpoilerSphereLimit);
    }

    [Fact]
    public void Generation_option_is_session_scoped()
    {
        var store = new AstUiSessionStore();
        var session = store.Start(10, 20, 30, null);

        Assert.True(store.TrySetGenerationSkipProgBalancing(session.Id, 10, 20, 30, true, out var updated));
        Assert.True(updated.GenerationSkipProgBalancing);
        Assert.False(store.TrySetGenerationSkipProgBalancing(session.Id, 11, 20, 30, false, out _));
    }

    [Fact]
    public void Long_outputs_are_bounded_and_mentions_are_neutralized()
    {
        var output = string.Join('\n', Enumerable.Range(1, 300).Select(index => $"Player @{index}: data"));

        var pages = AstCommandCenter.PaginateOutput(output, 120);

        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.InRange(page.Length, 1, 120));
        Assert.DoesNotContain(pages, page => page.Contains("@1", StringComparison.Ordinal));
        Assert.Contains(pages, page => page.Contains("@\u200b1", StringComparison.Ordinal));
    }

    [Fact]
    public void Output_pagination_is_private_and_bounded()
    {
        var store = new AstUiSessionStore();
        var session = store.Start(10, 20, 30, 40);
        var pages = new[] { "one", "two", "three" };

        Assert.True(store.TrySetOutputPages(session.Id, 10, 20, 30, pages, out var first));
        Assert.Equal(0, first.OutputPageIndex);
        Assert.True(store.TryMoveOutputPage(session.Id, 10, 20, 30, 1, out var second));
        Assert.Equal(1, second.OutputPageIndex);
        Assert.True(store.TryMoveOutputPage(session.Id, 10, 20, 30, 99, out var last));
        Assert.Equal(2, last.OutputPageIndex);
        Assert.False(store.TryMoveOutputPage(session.Id, 11, 20, 30, -1, out _));
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
