using System;
using System.Linq;
using Discord;
using Xunit;

public class AstSetupWizardTests
{
    [Fact]
    public void SessionStore_EnforcesOwnerScopeAndExpiration()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new AstSetupSessionStore(time, TimeSpan.FromMinutes(15));
        var draft = store.Start(ownerUserId: 1, guildId: 2, sourceChannelId: 3);

        Assert.True(store.TryGetAuthorized(draft.SessionId, 1, 2, 3, out _));
        Assert.False(store.TryGetAuthorized(draft.SessionId, 99, 2, 3, out _));
        Assert.False(store.TryGetAuthorized(draft.SessionId, 1, 99, 3, out _));

        Assert.True(store.TryUpdate(
            draft.SessionId,
            1,
            2,
            3,
            current => current with { ThreadTitle = "Multiworld" },
            out var updated));
        Assert.Equal("Multiworld", updated.ThreadTitle);

        time.Advance(TimeSpan.FromMinutes(16));
        Assert.False(store.TryGetAuthorized(draft.SessionId, 1, 2, 3, out _));
        Assert.Equal(0, store.CleanupExpired());
    }

    [Fact]
    public void StartingSecondSession_InvalidatesPreviousSessionForSameOwnerAndChannel()
    {
        var store = new AstSetupSessionStore();
        var first = store.Start(1, 2, 3);
        var second = store.Start(1, 2, 3);

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.False(store.TryGetAuthorized(first.SessionId, 1, 2, 3, out _));
        Assert.True(store.TryTake(second.SessionId, 1, 2, 3, out _));
        Assert.False(store.TryTake(second.SessionId, 1, 2, 3, out _));
    }

    [Fact]
    public void SummaryAndComponents_NeverExposeRoomIdentifier()
    {
        var draft = new AstSetupDraft(
            Guid.NewGuid().ToString("N"),
            1,
            2,
            3,
            3,
            DateTimeOffset.UtcNow.AddMinutes(15),
            "https://archipelago.example/room/private-room-token",
            "Weekly Multiworld",
            "Private",
            false,
            false,
            "15m");

        var summary = AstSetupWizard.BuildSummary(draft);
        var components = AstSetupWizard.BuildComponents(draft);
        var customIds = components.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .Select(component => component switch
            {
                ButtonComponent button => button.CustomId,
                SelectMenuComponent menu => menu.CustomId,
                _ => null
            })
            .Where(value => value != null)
            .ToList();

        Assert.True(AstSetupWizard.IsReady(draft));
        Assert.Contains("archipelago.example", summary, StringComparison.Ordinal);
        Assert.Contains("<#3>", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-room-token", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private-room-token", draft.ToString(), StringComparison.Ordinal);
        Assert.Equal(8, customIds.Count);
        Assert.All(customIds, customId => Assert.StartsWith(
            $"{AstSetupWizard.CustomIdPrefix}:{draft.SessionId}:",
            customId!,
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("astsetup:not-a-guid:preview")]
    [InlineData("astsetup:00000000000000000000000000000000")]
    public void CustomIdParser_RejectsForeignOrMalformedIds(string customId)
    {
        Assert.False(AstSetupWizard.TryParseCustomId(customId, out _, out _));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
