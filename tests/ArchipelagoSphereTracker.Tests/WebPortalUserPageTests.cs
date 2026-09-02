using System.Globalization;
using Xunit;

public sealed class WebPortalUserPageTests
{
    [Theory]
    [InlineData("en", "Quick actions", "Your aliases", "Current recap", "Active Hints", "Received items")]
    [InlineData("fr", "Actions rapides", "Vos alias", "Recap en cours", "Hints actifs", "Items reçus")]
    public void UserPage_UsesLocalizedResourceValues(
        string cultureName,
        string quickActions,
        string aliases,
        string recap,
        string hints,
        string receivedItems)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var html = WebPortalUserPage.Build("guild", "channel", "token");

            Assert.Contains(quickActions, html);
            Assert.Contains(aliases, html);
            Assert.Contains(recap, html);
            Assert.Contains(hints, html);
            Assert.Contains(receivedItems, html);
            Assert.DoesNotContain("WebQuickActions", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void UserPage_ContainsCompanionDeepLinkAndAliasGate()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var html = WebPortalUserPage.Build("guild", "channel", "token");

            Assert.Contains("id=\"open-companion-button\" disabled", html);
            Assert.Contains("ast-companion://connect?portal=", html);
            Assert.Contains("encodeURIComponent(companionPortalName)", html);
            Assert.Contains("setCompanionAvailability(userAliases)", html);
            Assert.Contains("navigator.clipboard.writeText(currentPortalUrl())", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
