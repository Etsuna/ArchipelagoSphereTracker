using Discord;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        var command = new SlashCommandBuilder()
            .WithName("ast")
            .WithDescription(Declare.Language == "fr"
                ? "Ouvrir votre centre de commandes AST privé"
                : "Open your private AST command center")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("file")
                .WithDescription(Declare.Language == "fr"
                    ? Declare.IsArchipelagoMode
                        ? "Importer un YAML, ZIP, APWorld ou spoiler log"
                        : "Importer un spoiler log"
                    : Declare.IsArchipelagoMode
                        ? "Import a YAML, ZIP, APWorld or spoiler log"
                        : "Import a spoiler log")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

        if (Declare.IsArchipelagoMode)
        {
            command.AddOption(new SlashCommandOptionBuilder()
                .WithName("skip-prog-balancing")
                .WithDescription(Declare.Language == "fr"
                    ? "Ignorer l’équilibrage lors d’une génération"
                    : "Skip progression balancing during generation")
                .WithType(ApplicationCommandOptionType.Boolean)
                .WithRequired(false));
        }

        yield return command;
    }
}
