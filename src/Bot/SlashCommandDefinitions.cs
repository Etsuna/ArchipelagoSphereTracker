using Discord;
using ArchipelagoSphereTracker.src.Resources;

public static class SlashCommandDefinitions
{
    public static IEnumerable<SlashCommandBuilder> GetAll()
    {
        var command = new SlashCommandBuilder()
            .WithName("ast")
            .WithDescription(Resource.SlashOpenYourPrivateASTCommandCenter)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("file")
                .WithDescription(Declare.IsArchipelagoMode
                    ? Resource.SlashImportYamlZipApworldOrSpoilerLog
                    : Resource.SlashImportSpoilerLog)
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

        if (Declare.IsArchipelagoMode)
        {
            command.AddOption(new SlashCommandOptionBuilder()
                .WithName("skip-prog-balancing")
                .WithDescription(Resource.SlashSkipProgressionBalancingDuringGeneration)
                .WithType(ApplicationCommandOptionType.Boolean)
                .WithRequired(false));
        }

        yield return command;
    }
}
