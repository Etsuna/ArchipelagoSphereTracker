using ArchipelagoSphereTracker.src.Resources;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class SlashCommandDefinitionsTests
{
    [Fact]
    public void GetAll_ReturnsBaseCommandsWithExpectedOptions()
    {
        Declare.IsArchipelagoMode = false;
        var commands = SlashCommandDefinitions.GetAll().ToList();

        var names = commands.Select(c => c.Name).ToList();
        var expected = new List<string>
        {
            "get-aliases",
            "add-alias",
            "delete-alias",
            "update-frequency-check",
            "add-url",
            "update-silent-option",
            "delete-url",
            "status-games-list",
            "info",
            "get-patch",
            "recap-all",
            "recap",
            "recap-and-clean",
            "clean",
            "clean-all",
            "hint-from-finder",
            "hint-for-receiver",
            "list-items",
            "apworlds-info",
            "discord",
            "excluded-item",
            "excluded-item-list",
            "delete-excluded-item"
        };

        Assert.Equal(expected.OrderBy(n => n), names.OrderBy(n => n));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(commands, command => Assert.False(string.IsNullOrWhiteSpace(command.Description)));

        AssertCommandOption(commands, "add-alias", "alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "add-alias", "Resource.SCAddAliasSkipMention", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "delete-alias", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "update-frequency-check", "Resource.CheckFrequency", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "url", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "Resource.SCThreadName", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "Resource.SCThreadType", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "auto-add-members", ApplicationCommandOptionType.Boolean, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "Resource.SCSilentOption", ApplicationCommandOptionType.Boolean, required: true, autocomplete: false);
        AssertCommandOption(commands, "add-url", "Resource.CheckFrequency", ApplicationCommandOptionType.String, required: true, autocomplete: false);
        AssertCommandOption(commands, "update-silent-option", "Resource.SCSilentOption", ApplicationCommandOptionType.Boolean, required: true, autocomplete: false);
        AssertCommandOption(commands, "get-patch", "alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "recap", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "recap-and-clean", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "clean", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "hint-from-finder", "alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "hint-for-receiver", "alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "list-items", "alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "excluded-item", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "excluded-item", "items", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "delete-excluded-item", "added-alias", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "delete-excluded-item", "delete-items", ApplicationCommandOptionType.String, required: true, autocomplete: true);
    }

    [Fact]
    public void GetAll_IncludesArchipelagoCommandsWhenEnabled()
    {
        Declare.IsArchipelagoMode = true;
        var commands = SlashCommandDefinitions.GetAll().ToList();

        var names = commands.Select(c => c.Name).ToList();
        var expected = new HashSet<string>
        {
            "list-yamls",
            "list-apworld",
            "backup-yamls",
            "backup-apworld",
            "download-template",
            "delete-yaml",
            "clean-yamls",
            "send-yaml",
            "generate-with-zip",
            "send-apworld",
            "generate",
            "test-generate"
        };

        foreach (var name in expected)
        {
            Assert.Contains(name, names);
        }

        AssertCommandOption(commands, "download-template", "template", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "delete-yaml", "yamlfile", ApplicationCommandOptionType.String, required: true, autocomplete: true);
        AssertCommandOption(commands, "send-yaml", "file", ApplicationCommandOptionType.Attachment, required: true, autocomplete: false);
        AssertCommandOption(commands, "generate-with-zip", "file", ApplicationCommandOptionType.Attachment, required: true, autocomplete: false);
        AssertCommandOption(commands, "send-apworld", "file", ApplicationCommandOptionType.Attachment, required: true, autocomplete: false);
    }

    private static void AssertCommandOption(
        IEnumerable<SlashCommandBuilder> commands,
        string commandName,
        string optionName,
        ApplicationCommandOptionType optionType,
        bool required,
        bool autocomplete)
    {
        var command = commands.Single(c => c.Name == commandName);
        var option = command.Options.Single(o => o.Name == optionName);

        Assert.Equal(optionType, option.Type);
        Assert.Equal(required, option.IsRequired);
        Assert.Equal(autocomplete, option.IsAutocomplete);
        Assert.False(string.IsNullOrWhiteSpace(option.Description));
    }
}
