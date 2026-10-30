using ArchipelagoSphereTracker.Tracking.Notifications;
using Xunit;

public sealed class NotificationDetailsFormattingTests
{
    private const string Arrow = "<:arrow_down_right:1565701680195182622>";

    [Theory]
    [InlineData("- **Jus de Protagoniste**:arrow_down_right: De **Player1** · *Contrôle municipal 030*")]
    [InlineData("- **Jus de Protagoniste**\n  <::arrow_down_right::1565701680195182622> De **Player1** · *Contrôle municipal 030*")]
    [InlineData("- **Jus de Protagoniste**\n  <:arrow_down_right:1565701680195182622> De **Player1** · *Contrôle municipal 030*")]
    [InlineData("- **Jus de Protagoniste**\n  ↳ De **Player1** · *Contrôle municipal 030*")]
    [InlineData("- **Jus de Protagoniste**\n  ➥ De **Player1** · *Contrôle municipal 030*")]
    public void Old_item_details_use_the_chosen_symbol_on_a_separate_line(string entry)
    {
        var notification = TrackingDiscordNotification.Create("📦 **Objets pour Player2** · **(1)** **<@111>**\n>>> " + entry, ["111"]);
        var fixedNotification = NotificationDetailsFormatting.Prepare(notification);
        Assert.Equal("📦 **Objets pour Player2** · **(1)** **<@111>**\n>>> • **Jus de Protagoniste**\n  " + Arrow +
            " De **Player1** · *Contrôle municipal 030*", fixedNotification.Content);
        Assert.Equal(notification.UserIds, fixedNotification.UserIds);
        Assert.DoesNotContain("\n\n", fixedNotification.Content);
        Assert.Same(fixedNotification, NotificationDetailsFormatting.Prepare(fixedNotification));
    }

    [Fact]
    public void Escaped_labels_and_literal_emoji_names_in_item_names_are_preserved()
    {
        var notification = TrackingDiscordNotification.Create(
            "📦 **Items for A\\*lice** · **(1)**\n>>> • **A :arrow_down_right: sword**\n  " + Arrow +
            " From **B\\*ob** · *A \\<:arrow_down_right:999\\> room*", []);
        Assert.Same(notification, NotificationDetailsFormatting.Prepare(notification));
    }

    [Theory]
    [InlineData("De")]
    [InlineData("From")]
    public void Sender_group_headings_use_the_connected_bots_arrow_including_the_first_quote_line(string from)
    {
        var notification = TrackingDiscordNotification.Create(
            "📦 **Items for Alice** · **(3)** **<@111>**\n>>> " + Arrow + " " + from + " **Bob**\n" +
            "• **Sword** · *Castle*\n• **Shield** · *Tower*\n" + Arrow + " " + from + " **Charlie**\n• **Bow** · *Forest*", ["111"]);
        const string selected = "<:arrow_down_right:1565718646989520957>";
        var prepared = NotificationDetailsFormatting.Prepare(notification, selected);
        Assert.Contains(">>> " + selected + " " + from + " **Bob**\n• ", prepared.Content);
        Assert.Contains("\n" + selected + " " + from + " **Charlie**\n• ", prepared.Content);
        Assert.DoesNotContain(Arrow, prepared.Content);
        Assert.Equal(2, prepared.Content.Split(selected).Length - 1);
        Assert.Equal(notification.UserIds, prepared.UserIds);
    }

}
