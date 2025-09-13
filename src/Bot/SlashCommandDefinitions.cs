using ArchipelagoSphereTracker.src.Resources;
using Discord;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder().WithName("get-aliases").WithDescription(Resource.SCGetAliasesDescription),

            new SlashCommandBuilder()
            .WithName("add-alias")
            .WithDescription(Resource.SCAddAliasDescription)
            .AddOption(AliasOption("alias"))
            .AddOption(BooleanOption(Resource.SCAddAliasSkipMention, Resource.SCAddAliasSkipMentionDescription)),

            new SlashCommandBuilder()
                .WithName("delete-alias")
                .WithDescription(Resource.SCDeleteAliasDescription)
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("update-frequency-check")
                .WithDescription(Resource.CheckFrequency)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(Resource.CheckFrequency)
                    .WithDescription(Resource.CheckFrequencyDesc)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice($"{Resource.Every} 5 minutes", "5m")
                    .AddChoice($"{Resource.Every} 15 minutes", "15m")
                    .AddChoice($"{Resource.Every} 30 minutes", "30m")
                    .AddChoice($"{Resource.Every} {Resource.Hour}", "1h")
                    .AddChoice($"{Resource.Every} 6 {Resource.Hour}", "6h")
                    .AddChoice($"{Resource.Every} 12 {Resource.Hour}", "12h")
                    .AddChoice($"{Resource.Every} 18 {Resource.Hour}", "18h")
                    .AddChoice($"{Resource.EveryDay}", "1d")),

            new SlashCommandBuilder()
                .WithName("add-url")
                .WithDescription(Resource.SCAddUrlDescription)
                .AddOption("url", ApplicationCommandOptionType.String, Resource.SCUrlToTrack, isRequired: true)
                .AddOption(Resource.SCThreadName, ApplicationCommandOptionType.String, Resource.SCThreadNameDescription, isRequired: true)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(Resource.SCThreadType)
                    .WithDescription(Resource.SCThreadTypeDescription)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice(Resource.SCThreadPublic, "Public")
                    .AddChoice(Resource.SCThreadPrivate, "Private"))
                .AddOption(BooleanOption(Resource.SCSilentOption, Resource.SCSilentDescription))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName(Resource.CheckFrequency)
                    .WithDescription(Resource.CheckFrequencyDesc)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice($"{Resource.Every} 5 minutes", "5m")
                    .AddChoice($"{Resource.Every} 15 minutes", "15m")
                    .AddChoice($"{Resource.Every} 30 minutes", "30m")
                    .AddChoice($"{Resource.Every} {Resource.Hour}", "1h")
                    .AddChoice($"{Resource.Every} 6 {Resource.Hour}", "6h")
                    .AddChoice($"{Resource.Every} 12 {Resource.Hour}", "12h")
                    .AddChoice($"{Resource.Every} 18 {Resource.Hour}", "18h")
                    .AddChoice($"{Resource.EveryDay}", "1d")),

            new SlashCommandBuilder().WithName("delete-url").WithDescription(Resource.SCDeleteUrlDescription),
            new SlashCommandBuilder().WithName("status-games-list").WithDescription(Resource.SCStatusGameListDescription),
            new SlashCommandBuilder().WithName("recap-all").WithDescription(Resource.SCRecapAllDescription),
            new SlashCommandBuilder().WithName("info").WithDescription(Resource.SCInfoDescription),

            new SlashCommandBuilder()
                .WithName("get-patch")
                .WithDescription(Resource.SCGetPatchDescription)
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("recap")
                .WithDescription(Resource.SCRecapDescription)
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("recap-and-clean")
                .WithDescription(Resource.RCRecapAndCleanDescription)
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("clean")
                .WithDescription(Resource.SCCleanDescription)
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder().WithName("clean-all").WithDescription(Resource.SCCleanAllDescription),

            new SlashCommandBuilder()
                .WithName("hint-from-finder")
                .WithDescription(Resource.SCGetHintFromFinderDescription)
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("hint-for-receiver")
                .WithDescription(Resource.SCGetHintForReveiverDescription)
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("list-items")
                .WithDescription(Resource.SCListItemDescription)
                .AddOption(AliasOption("alias"))
                .AddOption(BooleanOption("list-by-line", Resource.SCListByLineDescription)),

            new SlashCommandBuilder()
                .WithName("apworlds-info")
                .WithDescription(Resource.SCApworldInfoDescription)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("apworldsinfo")
                    .WithDescription(Resource.SCApworldInfoChoiceDescription)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),
        };

        if (Declare.IsArchipelagoMode)
        {
            commands.AddRange(new[]
            {
                new SlashCommandBuilder().WithName("list-yamls").WithDescription(Resource.SCListYamlsDescription),
                new SlashCommandBuilder().WithName("list-apworld").WithDescription(Resource.SCListApworldDescription),

                new SlashCommandBuilder().WithName("backup-yamls").WithDescription(Resource.SCBackupYamlDescription),
                new SlashCommandBuilder().WithName("backup-apworld").WithDescription(Resource.SCBackupApworldDescription),

                new SlashCommandBuilder()
                    .WithName("download-template")
                    .WithDescription(Resource.SCDownloadYamlTemplateDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("template")
                        .WithDescription(Resource.SCTemplateDescription)
                        .WithType(ApplicationCommandOptionType.String)
                        .WithRequired(true)
                        .WithAutocomplete(true)),

                new SlashCommandBuilder()
                    .WithName("delete-yaml")
                    .WithDescription(Resource.SCDeleteYamlDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("yamlfile")
                        .WithDescription(Resource.SCDeleteYamlChooseDescription)
                        .WithType(ApplicationCommandOptionType.String)
                        .WithRequired(true)
                        .WithAutocomplete(true)),

                new SlashCommandBuilder().WithName("clean-yamls").WithDescription(Resource.SCCleanYamlDescription),

                new SlashCommandBuilder()
                    .WithName("send-yaml")
                    .WithDescription(Resource.SCSendYamlDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("file")
                        .WithDescription(Resource.SCSendYamlChooseDescription)
                        .WithType(ApplicationCommandOptionType.Attachment)
                        .WithRequired(true)),

                new SlashCommandBuilder()
                    .WithName("generate-with-zip")
                    .WithDescription(Resource.SCGenerateWithZipDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("file")
                        .WithDescription(Resource.SCGenerateWithZipChooseDescription)
                        .WithType(ApplicationCommandOptionType.Attachment)
                        .WithRequired(true)),

                new SlashCommandBuilder()
                    .WithName("send-apworld")
                    .WithDescription(Resource.SCSendApworldDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("file")
                        .WithDescription(Resource.SCSendApworldChooseDescription)
                        .WithType(ApplicationCommandOptionType.Attachment)
                        .WithRequired(true)),

                new SlashCommandBuilder().WithName("generate").WithDescription(Resource.SCGenerateDescription),
                new SlashCommandBuilder().WithName("test-generate").WithDescription(Resource.SCTestGenerateDescription)
            });
        }
        return commands;
    }

    #region Helper Methods

    private static SlashCommandOptionBuilder AliasOption(string name)
    {
        return new SlashCommandOptionBuilder()
            .WithName(name)
            .WithDescription(Resource.SCChooseAnAlias)
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .WithAutocomplete(true);
    }

    private static SlashCommandOptionBuilder BooleanOption(string name, string description)
    {
        return new SlashCommandOptionBuilder()
            .WithName(name)
            .WithDescription(description)
            .WithType(ApplicationCommandOptionType.Boolean)
            .WithRequired(true);
    }

    #endregion
}
