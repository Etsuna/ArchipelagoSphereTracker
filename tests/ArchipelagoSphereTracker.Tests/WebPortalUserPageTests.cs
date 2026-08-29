using System.Globalization;
using Xunit;

public sealed class WebPortalUserPageTests
{
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
