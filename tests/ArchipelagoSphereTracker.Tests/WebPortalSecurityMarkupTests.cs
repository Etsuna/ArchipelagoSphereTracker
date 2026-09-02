using System.Globalization;
using Xunit;

public class WebPortalSecurityMarkupTests
{
    [Fact]
    public void GuildCommandsPage_UsesTokenBoundApisAndNoClientSuppliedUserId()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        var previousMode = Declare.IsArchipelagoMode;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Declare.IsArchipelagoMode = true;
            var html = WebPortalCommandsPage.Build();

            Assert.Contains("const token = m ? m[3] : '';", html);
            Assert.Contains("token +", html);
            Assert.Contains("'/commands/execute'", html);
            Assert.Contains("'/commands/yamls'", html);
            Assert.Contains("data-command=\"ast-health\"", html);
            Assert.Contains("name=\"skipProgBalancing\"", html);
            Assert.Contains("'/audit'", html);
            Assert.Contains("'/revoke'", html);
            Assert.DoesNotContain("name=\"userId\"", html);
            Assert.DoesNotContain("data.set('userId'", html);
        }
        finally
        {
            Declare.IsArchipelagoMode = previousMode;
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
            Assert.Contains("data-command=\"get-aliases\"", html);
            Assert.Contains("data-command=\"send-spoiler-log\"", html);
            Assert.Contains("data-command=\"analyze-spoiler-log\"", html);
            Assert.Contains("securedApiBase + '/revoke'", html);
            Assert.DoesNotContain("data-command=\"ast-health\"", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void UserPage_ExposesOnlyTokenBoundPersonalActions()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var html = WebPortalUserPage.Build("guild", "channel", "token");

            Assert.Contains("apiBase + '/patches'", html);
            Assert.Contains("apiBase + '/exclusions'", html);
            Assert.Contains("apiBase + '/exclusion/' + action", html);
            Assert.Contains("apiBase + '/recap/delete-all'", html);
            Assert.Contains("apiBase + '/revoke'", html);
            Assert.DoesNotContain("data.append('userId'", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
