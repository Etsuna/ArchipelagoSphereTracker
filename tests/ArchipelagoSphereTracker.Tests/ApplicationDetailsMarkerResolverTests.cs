using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.Tracking.Notifications;
using Discord;
using Xunit;

public sealed class ApplicationDetailsMarkerResolverTests
{
    [Theory]
    [InlineData(1565718646989520957UL)] // Test application.
    [InlineData(1565718735447269406UL)] // Randophina.
    [InlineData(1565701680195182622UL)] // AST.
    public async Task Each_application_selects_the_exact_markdown_for_its_own_emoji(ulong arrowId)
    {
        var resolver = new ApplicationDetailsMarkerResolver(_ => Task.FromResult<IReadOnlyCollection<Emote>>(
            [new(123, "unrelated"), new(arrowId, "arrow_down_right")]));
        Assert.Equal($"<:arrow_down_right:{arrowId}>", await resolver.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_application_without_the_arrow_never_uses_another_bots_ID()
    {
        var resolver = new ApplicationDetailsMarkerResolver(_ => Task.FromResult<IReadOnlyCollection<Emote>>(
            [new(123, "unrelated"), new(456, "arrow_down_right")]));
        Assert.Equal("➥", await resolver.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_notifications_share_one_application_lookup()
    {
        var calls = 0;
        var resolver = new ApplicationDetailsMarkerResolver(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult<IReadOnlyCollection<Emote>>([new(1565718646989520957, "arrow_down_right")]);
        });
        var markers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => resolver.GetAsync(CancellationToken.None)));
        Assert.All(markers, marker => Assert.Equal("<:arrow_down_right:1565718646989520957>", marker));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Missing_emoji_is_rechecked_after_one_minute()
    {
        var time = new TestTimeProvider();
        IReadOnlyCollection<Emote> emojis = [];
        var resolver = new ApplicationDetailsMarkerResolver(_ => Task.FromResult(emojis), time);
        Assert.Equal("➥", await resolver.GetAsync(CancellationToken.None));
        emojis = [new(1565718735447269406, "arrow_down_right")];
        Assert.Equal("➥", await resolver.GetAsync(CancellationToken.None));
        time.Now = time.Now.AddMinutes(1);
        Assert.Equal("<:arrow_down_right:1565718735447269406>", await resolver.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_temporary_Discord_failure_keeps_notifications_available()
    {
        var resolver = new ApplicationDetailsMarkerResolver(_ => throw new System.Net.Http.HttpRequestException("unavailable"));
        Assert.Equal("➥", await resolver.GetAsync(CancellationToken.None));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.GetAsync(cts.Token));
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-30T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
