using Discord;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

public class SlashCommandDefinitionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetAll_publishes_ast_and_the_v5_6_7_commands(bool archipelagoMode)
    {
        Declare.IsArchipelagoMode = archipelagoMode;

        var commands = SlashCommandDefinitions.GetAll().ToList();
        var commandNames = commands.Select(command => command.Name).ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "ast", "get-aliases", "add-alias", "delete-alias", "update-frequency-check", "add-url",
            "update-silent-option", "delete-url", "status-games-list", "info", "get-patch", "recap-all",
            "recap", "recap-and-clean", "clean", "clean-all", "hint-from-finder", "hint-for-receiver",
            "list-items", "analyze-spoiler-log", "send-spoiler-log", "apworlds-info", "discord",
            "excluded-item", "excluded-item-list", "delete-excluded-item", "ast-user-portal",
            "ast-room-portal", "ast-portal"
        };

        if (archipelagoMode)
        {
            expected.UnionWith(new[]
            {
                "list-yamls", "list-apworld", "backup-yamls", "backup-apworld", "download-template",
                "delete-yaml", "clean-yamls", "send-yaml", "generate-with-zip", "send-apworld",
                "generate", "test-generate"
            });
        }

        Assert.Equal(expected.Count, commands.Count);
        Assert.True(expected.SetEquals(commandNames));
        Assert.All(commands, candidate => Assert.NotNull(candidate.Build()));

        var command = commands.Single(candidate => candidate.Name == "ast");
        Assert.Equal("ast", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
        AssertOption(command, "file", ApplicationCommandOptionType.Attachment);
        if (archipelagoMode)
            AssertOption(command, "skip-prog-balancing", ApplicationCommandOptionType.Boolean);
        else
            Assert.DoesNotContain(command.Options, option => option.Name == "skip-prog-balancing");
    }

    [Fact]
    public void Every_legacy_slash_command_has_an_ast_destination()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "get-aliases", "add-alias", "delete-alias", "update-frequency-check", "add-url", "ast-setup",
            "update-silent-option", "delete-url", "status-games-list", "ast-health", "ast-room-health",
            "ast-sync-now", "ast-pause", "ast-resume", "ast-polling", "info", "get-patch", "recap-all",
            "recap", "recap-and-clean", "clean", "clean-all", "hint-from-finder", "hint-for-receiver",
            "list-items", "analyze-spoiler-log", "send-spoiler-log", "apworlds-info", "ast-user-portal",
            "ast-room-portal", "ast-portal", "discord", "excluded-item", "excluded-item-list",
            "delete-excluded-item", "list-yamls", "list-apworld", "backup-yamls", "backup-apworld",
            "download-template", "delete-yaml", "clean-yamls", "send-yaml", "generate-with-zip",
            "send-apworld", "generate", "test-generate"
        };

        Assert.Equal(expected.Count, AstCommandCenter.LegacyCommandCoverage.Count);
        Assert.True(expected.SetEquals(AstCommandCenter.LegacyCommandCoverage.Keys));
        Assert.All(AstCommandCenter.LegacyCommandCoverage.Values, destination =>
            Assert.False(string.IsNullOrWhiteSpace(destination)));
    }

    [Theory]
    [InlineData("en", "Analyze blocking spheres and dependencies in the spoiler log", "first blocking sphere only")]
    [InlineData("fr", "Analyse les sphères bloquantes et les dépendances du spoiler log", "première sphère bloquante uniquement")]
    public void Spoiler_slash_commands_use_the_configured_culture(
        string cultureName,
        string expectedDescription,
        string expectedFirstChoice)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var command = SlashCommandDefinitions.GetAll()
                .Single(candidate => candidate.Name == "analyze-spoiler-log");
            var mode = command.Options.Single(option => option.Name == "missing-mode");

            Assert.Equal(expectedDescription, command.Description);
            Assert.Equal(expectedFirstChoice, mode.Choices.First().Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private static void AssertOption(
        SlashCommandBuilder command,
        string optionName,
        ApplicationCommandOptionType optionType)
    {
        var option = command.Options.Single(candidate => candidate.Name == optionName);
        Assert.Equal(optionType, option.Type);
        Assert.False(option.IsRequired);
        Assert.False(option.IsAutocomplete);
        Assert.False(string.IsNullOrWhiteSpace(option.Description));
    }
}
