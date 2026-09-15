using System.Threading.Tasks;
using Xunit;

public sealed class CommandTelemetryTests
{
    [Fact]
    public async Task MetricsCollection_SupportsAFreshDatabase()
    {
        using var scope = new TestDatabaseScope();
        await MetricsExporter.CollectOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryRegisteredSlashCommand_HasABoundedTelemetryLabel(bool archipelagoMode)
    {
        var previousMode = Declare.IsArchipelagoMode;
        try
        {
            Declare.IsArchipelagoMode = archipelagoMode;
            Assert.All(
                SlashCommandDefinitions.GetAll(),
                command => Assert.NotEqual("unknown", CommandTelemetry.NormalizeSlashCommand(command.Name)));
        }
        finally
        {
            Declare.IsArchipelagoMode = previousMode;
        }
    }

    [Theory]
    [InlineData("ast", "ast")]
    [InlineData("send-apworld", "send-apworld")]
    [InlineData("not-a-command", "unknown")]
    [InlineData(null, "unknown")]
    public void SlashCommandNames_AreBounded(string? command, string expected)
        => Assert.Equal(expected, CommandTelemetry.NormalizeSlashCommand(command));

    [Theory]
    [InlineData("archipelago-tools", "archipelago-tools")]
    [InlineData("generation-run", "generation-run")]
    [InlineData("attacker-controlled-value", "unknown")]
    public void CommandCenterActions_AreBounded(string action, string expected)
        => Assert.Equal(expected, CommandTelemetry.NormalizeCommandCenterAction(action));

    [Fact]
    public void FreeTextOptionValues_AreNeverExported()
    {
        Assert.Equal("provided", CommandTelemetry.ClassifyOptionSelection("alias", "A private slot name"));
        Assert.Equal("provided", CommandTelemetry.ClassifyOptionSelection("url", "https://private.example/room/secret"));
        Assert.Equal("provided", CommandTelemetry.ClassifyOptionSelection("thread-name", "Private async"));
    }

    [Theory]
    [InlineData("thread-type", "Public", "public")]
    [InlineData("check-frequency", "15m", "15m")]
    [InlineData("missing-mode", "full", "full")]
    [InlineData("silent", "true", "true")]
    [InlineData("skip-prog-balancing", "false", "false")]
    public void BoundedChoices_AreExported(string option, string value, string expected)
        => Assert.Equal(expected, CommandTelemetry.ClassifyOptionSelection(option, value));

    [Theory]
    [InlineData("players.yaml", "yaml")]
    [InlineData("world.apworld", "apworld")]
    [InlineData("spoiler.json", "json")]
    [InlineData("private-name.exe", "other_file")]
    public void Attachments_ExposeOnlyAnAllowlistedExtension(string fileName, string expected)
        => Assert.Equal(expected, CommandTelemetry.ClassifyAttachmentExtension(fileName));

    [Fact]
    public void DynamicCommandCenterSelections_AreNeverExported()
    {
        Assert.Equal("provided", CommandTelemetry.ClassifyCommandCenterSelection("select-room", "123456789"));
        Assert.Equal("provided", CommandTelemetry.ClassifyCommandCenterSelection("alias-add", "Private slot"));
        Assert.Equal("automatic|30m", CommandTelemetry.ClassifyCommandCenterSelection("poll-policy", "automatic|30m"));
    }
}
