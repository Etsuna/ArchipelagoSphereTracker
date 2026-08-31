using Discord;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        yield return new SlashCommandBuilder()
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
                .WithRequired(false));
    }
}
