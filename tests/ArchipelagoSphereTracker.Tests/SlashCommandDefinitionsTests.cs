using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class SlashCommandDefinitionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetAll_publishes_only_the_unified_ast_command(bool archipelagoMode)
    {
        Declare.IsArchipelagoMode = archipelagoMode;

        var command = Assert.Single(SlashCommandDefinitions.GetAll());

        Assert.Equal("ast", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
        AssertOption(command, "file", ApplicationCommandOptionType.Attachment);
        AssertOption(command, "skip-prog-balancing", ApplicationCommandOptionType.Boolean);
    }

    [Fact]
    public void Every_removed_slash_command_has_an_ast_destination()
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
