using Discord;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        return new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder().WithName("get-aliases").WithDescription("Get Aliases"),

            new SlashCommandBuilder()
                .WithName("delete-alias")
                .WithDescription("Delete Alias")
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("add-alias")
                .WithDescription("Add Alias")
                .AddOption(AliasOption("alias"))
                .AddOption(BooleanOption("skip_useless_mention", "Set if you want to skip useless mention")),

            new SlashCommandBuilder()
                .WithName("add-url")
                .WithDescription("Add a URL and create a thread.")
                .AddOption("url", ApplicationCommandOptionType.String, "The URL to track", isRequired: true)
                .AddOption("thread-name", ApplicationCommandOptionType.String, "Name of the thread to create", isRequired: true)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("thread-type")
                    .WithDescription("Specify if the thread is public or private")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("Public", "Public")
                    .AddChoice("Private", "Private"))
                .AddOption(BooleanOption("silent", "Only send message when an alias is set")),

            new SlashCommandBuilder().WithName("delete-url").WithDescription("Delete Url, clean Alias and Recap"),
            new SlashCommandBuilder().WithName("status-games-list").WithDescription("Status for all games"),
            new SlashCommandBuilder().WithName("recap-all").WithDescription("Recap list of items for all games"),
            new SlashCommandBuilder().WithName("info").WithDescription("Get all infos for your Archipelago."),

            new SlashCommandBuilder()
                .WithName("get-patch")
                .WithDescription("Patch for alias")
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("recap")
                .WithDescription("Recap list of items for a specific game")
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("recap-and-clean")
                .WithDescription("Recap and clean list of items for a specific game")
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder()
                .WithName("clean")
                .WithDescription("Clean list of items for a specific game")
                .AddOption(AliasOption("added-alias")),

            new SlashCommandBuilder().WithName("clean-all").WithDescription("Clean all recap items"),

            new SlashCommandBuilder()
                .WithName("hint-from-finder")
                .WithDescription("Get a hint from finder")
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("hint-for-receiver")
                .WithDescription("Get a hint for receiver")
                .AddOption(AliasOption("alias")),

            new SlashCommandBuilder()
                .WithName("list-items")
                .WithDescription("List all items for alias")
                .AddOption(AliasOption("alias"))
                .AddOption(BooleanOption("list-by-line", "Display items line by line (true) or comma separated (false).")),

            new SlashCommandBuilder().WithName("list-yamls").WithDescription("List all YAML files for the channel"),
            new SlashCommandBuilder().WithName("list-apworld").WithDescription("List all APWorlds"),

            new SlashCommandBuilder()
                .WithName("apworlds-info")
                .WithDescription("List info for specific APWorld")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("apworldsinfo")
                    .WithDescription("Choose an APWorld")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),

            new SlashCommandBuilder().WithName("backup-yamls").WithDescription("Backup all YAMLs for the channel"),
            new SlashCommandBuilder().WithName("backup-apworld").WithDescription("Backup all APWorlds for the channel"),

            new SlashCommandBuilder()
                .WithName("download-template")
                .WithDescription("Download a YAML template")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("template")
                    .WithDescription("Choose a YAML file to download")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),

            new SlashCommandBuilder()
                .WithName("delete-yaml")
                .WithDescription("Delete a specific YAML file")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("file")
                    .WithDescription("Choose a YAML file to delete")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),

            new SlashCommandBuilder().WithName("clean-yamls").WithDescription("Clean all YAMLs in the channel"),

            new SlashCommandBuilder()
                .WithName("send-yaml")
                .WithDescription("Send or replace a YAML file for generation")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("fichier")
                    .WithDescription("Upload a YAML file")
                    .WithType(ApplicationCommandOptionType.Attachment)
                    .WithRequired(true)),

            new SlashCommandBuilder()
                .WithName("generate-with-zip")
                .WithDescription("Generate multiworld from a ZIP")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("fichier")
                    .WithDescription("Upload a ZIP containing YAMLs")
                    .WithType(ApplicationCommandOptionType.Attachment)
                    .WithRequired(true)),

            new SlashCommandBuilder()
                .WithName("send-apworld")
                .WithDescription("Send or replace an APWorld file")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("fichier")
                    .WithDescription("Upload an APWorld file")
                    .WithType(ApplicationCommandOptionType.Attachment)
                    .WithRequired(true)),

            new SlashCommandBuilder().WithName("generate").WithDescription("Generate multiworld from existing YAMLs"),
            new SlashCommandBuilder().WithName("test-generate").WithDescription("Test generation of multiworld from existing YAMLs")
        };
    }

    #region Helper Methods

    private static SlashCommandOptionBuilder AliasOption(string name)
    {
        return new SlashCommandOptionBuilder()
            .WithName(name)
            .WithDescription("Choose an alias")
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
