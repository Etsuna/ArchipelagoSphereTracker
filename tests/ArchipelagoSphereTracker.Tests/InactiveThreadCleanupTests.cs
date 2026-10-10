using System;
using System.Threading.Tasks;
using Xunit;

public sealed class InactiveThreadCleanupTests
{
    [Fact]
    public async Task CleanupContinuesWhenDiscordNotificationFails()
    {
        var deleted = false;

        await TrackingDataManager.NotifyThenDeleteInactiveThreadAsync(
            () => throw new InvalidOperationException("archived or locked thread"),
            () =>
            {
                deleted = true;
                return Task.CompletedTask;
            });

        Assert.True(deleted);
    }

    [Fact]
    public async Task CancellationStillStopsCleanup()
    {
        var deleted = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TrackingDataManager.NotifyThenDeleteInactiveThreadAsync(
                () => throw new OperationCanceledException(),
                () =>
                {
                    deleted = true;
                    return Task.CompletedTask;
                }));

        Assert.False(deleted);
    }
}
