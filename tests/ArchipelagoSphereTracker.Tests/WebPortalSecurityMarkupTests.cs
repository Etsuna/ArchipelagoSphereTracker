using System.Globalization;
using Xunit;

public class WebPortalSecurityMarkupTests
{
    [Fact]
    public void GuildCommandsPage_UsesTokenBoundApisAndNoClientSuppliedUserId()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var html = WebPortalCommandsPage.Build();

            Assert.Contains("const token = m ? m[3] : '';", html);
            Assert.Contains("token +", html);
            Assert.Contains("'/commands/execute'", html);
            Assert.Contains("'/commands/yamls'", html);
            Assert.DoesNotContain("name=\"userId\"", html);
            Assert.DoesNotContain("data.set('userId'", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void ThreadCommandsPage_UsesOneTokenBoundApiBase()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var html = WebPortalThreadCommandsPage.Build();

            Assert.Contains("const token = m ? m[3] : '';", html);
            Assert.Contains("const securedApiBase", html);
            Assert.Contains("securedApiBase + '/thread-commands/execute'", html);
            Assert.Contains("securedApiBase + '/thread-commands/patches'", html);
            Assert.Contains("securedApiBase + '/info'", html);
            Assert.Contains("data-command=\"ast-room-health\"", html);
            Assert.Contains("data-command=\"ast-sync-now\"", html);
            Assert.Contains("data-command=\"ast-pause\"", html);
            Assert.Contains("data-command=\"ast-resume\"", html);
            Assert.Contains("data-command=\"ast-polling\"", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
