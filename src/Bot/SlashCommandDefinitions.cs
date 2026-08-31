using ArchipelagoSphereTracker.src.Resources;
using Discord;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder()
                .WithName("ast")
                .WithDescription(Declare.Language == "fr"
                    ? "Ouvrir votre centre de commandes AST privé"
                    : "Open your private AST command center")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("file")
                    .WithDescription(Declare.Language == "fr"
                        ? "Importer un YAML, ZIP, APWorld ou spoiler log"
                        : "Import a YAML, ZIP, APWorld or spoiler log")
                    .WithType(ApplicationCommandOptionType.Attachment)
                    .WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("skip-prog-balancing")
                    .WithDescription(Declare.Language == "fr"
                        ? "Ignorer l’équilibrage lors d’une génération"
                        : "Skip progression balancing during generation")
                    .WithType(ApplicationCommandOptionType.Boolean)
                    .WithRequired(false)),

            new SlashCommandBuilder().WithName("get-aliases").WithDescription(Resource.SCGetAliasesDescription),

            new SlashCommandBuilder()
            .WithName("add-alias")
            .WithDescription(Resource.SCAddAliasDescription)
            .AddOption(AliasOption("alias"))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(Resource.SCAddAliasSkipMention)
                    .WithDescription(Resource.SCAddAliasSkipMentionDescription)
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice($"{Resource.None}", "0")
                    .AddChoice($"{Resource.Filler}", "1")
                    .AddChoice($"{Resource.Trap}", "16")
                    .AddChoice($"{Resource.Filler} + {Resource.Trap}", "17")
                    .AddChoice($"{Resource.Filler} + {Resource.Trap} + {Resource.Useful}", "21")
                    .AddChoice($"{Resource.Filler} + {Resource.Trap} + {Resource.Useful} + {Resource.Required}", "27")
                    .AddChoice($"{Resource.Filler} + {Resource.Trap} + {Resource.Useful} + {Resource.Required} + {Resource.Progression}", "31")),

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
                .AddOption(BooleanOption("auto-add-members", "Auto-add all channel members to the thread (public only)"))
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

            new SlashCommandBuilder()
                .WithName("ast-setup")
                .WithDescription(Declare.Language == "fr"
                    ? "Configurer une room Archipelago avec un assistant interactif"
                    : "Configure an Archipelago room with an interactive assistant"),

            new SlashCommandBuilder()
                .WithName("update-silent-option")
                .WithDescription(Resource.SCUpdateSilentOptionDescription)
                .AddOption(BooleanOption(Resource.SCSilentOption, Resource.SCSilentDescription)),

            new SlashCommandBuilder().WithName("delete-url").WithDescription(Resource.SCDeleteUrlDescription),
            new SlashCommandBuilder().WithName("status-games-list").WithDescription(Resource.SCStatusGameListDescription),

            new SlashCommandBuilder()
                .WithName("ast-health")
                .WithDescription(Declare.Language == "fr" ? "Afficher la santé globale du suivi AST" : "Show overall AST tracking health"),
            new SlashCommandBuilder()
                .WithName("ast-room-health")
                .WithDescription(Declare.Language == "fr" ? "Afficher la santé du suivi de cette room" : "Show tracking health for this room"),
            new SlashCommandBuilder()
                .WithName("ast-sync-now")
                .WithDescription(Declare.Language == "fr" ? "Prioriser une synchronisation de cette room" : "Prioritize a sync for this room"),
            new SlashCommandBuilder()
                .WithName("ast-pause")
                .WithDescription(Declare.Language == "fr" ? "Suspendre le suivi de cette room" : "Pause tracking for this room"),
            new SlashCommandBuilder()
                .WithName("ast-resume")
                .WithDescription(Declare.Language == "fr" ? "Reprendre le suivi de cette room" : "Resume tracking for this room"),
            new SlashCommandBuilder()
                .WithName("ast-polling")
                .WithDescription(Declare.Language == "fr" ? "Configurer le polling automatique de cette room" : "Configure adaptive polling for this room")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("mode")
                    .WithDescription(Declare.Language == "fr" ? "Mode automatique ou fréquence fixe" : "Automatic or fixed-frequency mode")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice(Declare.Language == "fr" ? "Automatique" : "Automatic", "automatic")
                    .AddChoice(Declare.Language == "fr" ? "Fixe" : "Fixed", "fixed"))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("maximum-frequency")
                    .WithDescription(Declare.Language == "fr" ? "Intervalle maximal en mode automatique" : "Maximum interval in automatic mode")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("15 minutes", "15m")
                    .AddChoice("30 minutes", "30m")
                    .AddChoice("1 hour", "1h")
                    .AddChoice("6 hours", "6h")
                    .AddChoice("12 hours", "12h")
                    .AddChoice("18 hours", "18h")
                    .AddChoice("1 day", "1d")),

            new SlashCommandBuilder().WithName("info").WithDescription(Resource.SCInfoDescription),

            new SlashCommandBuilder()
                .WithName("get-patch")
                .WithDescription(Resource.SCGetPatchDescription)
                .AddOption(AliasOption("alias")),

             new SlashCommandBuilder()
                .WithName("recap-all").WithDescription(Resource.SCRecapAllDescription),

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
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("analyze-spoiler-log")
                .WithDescription("Analyse les sphères bloquantes et les dépendances du spoiler log")
                .AddOption(AliasOption("alias"))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("sphere")
                    .WithDescription("Sphère maximale à analyser (optionnel)")
                    .WithType(ApplicationCommandOptionType.Integer)
                    .WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("missing-mode")
                    .WithDescription("first = première sphère bloquante, full = toutes les checks manquantes")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(false)
                    .AddChoice("lowest-sphere-only", "first")
                    .AddChoice("full", "full"))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("hide-items")
                    .WithDescription("Masquer le nom des items dans le rapport")
                    .WithType(ApplicationCommandOptionType.Boolean)
                    .WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("validate-sphere")
                    .WithDescription("Valide les checks locales ambiguës jusqu’à cette sphère")
                    .WithType(ApplicationCommandOptionType.Integer)
                    .WithMinValue(0)
                    .WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("reset-validation")
                    .WithDescription("Efface la validation manuelle des sphères pour cet alias")
                    .WithType(ApplicationCommandOptionType.Boolean)
                    .WithRequired(false)),

            new SlashCommandBuilder()
                .WithName("send-spoiler-log")
                .WithDescription("Upload le spoiler log pour l'analyse")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("file")
                    .WithDescription("Fichier spoiler log (.txt/.json)")
                    .WithType(ApplicationCommandOptionType.Attachment)
                    .WithRequired(true)),

            new SlashCommandBuilder()
                .WithName("apworlds-info")
                .WithDescription(Resource.SCApworldInfoDescription),

            new SlashCommandBuilder()
                .WithName("discord")
                .WithDescription(Resource.DiscordDesc),

            new SlashCommandBuilder()
                .WithName("excluded-item")
                .WithDescription(Resource.SCExcludedItemDesc)
                .AddOption(AliasOption("added-alias"))
                .AddOption(ItemsOption("items")),

             new SlashCommandBuilder()
                .WithName("excluded-item-list")
                .WithDescription(Resource.SCExcludedItemListDesc),

            new SlashCommandBuilder()
                .WithName("delete-excluded-item")
                .WithDescription(Resource.SCDeleteExcludedItemDesc)
                .AddOption(AliasOption("added-alias"))
                .AddOption(ItemsOption("delete-items")),

            new SlashCommandBuilder()
                .WithName("ast-user-portal")
                .WithDescription(Resource.SCPortalLinkDescription)
                .AddOption(PortalRevokeOption()),

            new SlashCommandBuilder()
                .WithName("ast-room-portal")
                .WithDescription("Afficher la page web des commandes du thread")
                .AddOption(PortalRevokeOption()),

            new SlashCommandBuilder()
                .WithName("ast-portal")
                .WithDescription(Resource.SCPortalUrlDescription)
                .AddOption(PortalRevokeOption()),

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
                        .WithRequired(true))
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("skip-prog-balancing")
                        .WithDescription("skip-prog-balancing")
                        .WithType(ApplicationCommandOptionType.Boolean)
                        .WithRequired(true)),

                new SlashCommandBuilder()
                    .WithName("send-apworld")
                    .WithDescription(Resource.SCSendApworldDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("file")
                        .WithDescription(Resource.SCSendApworldChooseDescription)
                        .WithType(ApplicationCommandOptionType.Attachment)
                        .WithRequired(true)),

                new SlashCommandBuilder()
                    .WithName("generate")
                    .WithDescription(Resource.SCGenerateDescription)
                    .AddOption(new SlashCommandOptionBuilder()
                            .WithName("skip-prog-balancing")
                            .WithDescription("skip-prog-balancing")
                            .WithType(ApplicationCommandOptionType.Boolean)
                            .WithRequired(true)),

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

    private static SlashCommandOptionBuilder ItemsOption(string item)
    {
        return new SlashCommandOptionBuilder()
            .WithName(item)
            .WithDescription(Resource.SCChooseAnItem)
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .WithAutocomplete(true);
    }

    private static SlashCommandOptionBuilder PortalRevokeOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("revoke")
            .WithDescription("Revoke the current portal link instead of issuing a new one")
            .WithType(ApplicationCommandOptionType.Boolean)
            .WithRequired(false);
    }

    #endregion
}
