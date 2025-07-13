using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

public static class BotCommands
{
    public static Task InstallCommandsAsync()
    {
        Declare.Services = new ServiceCollection()
            .AddSingleton(Declare.Client)
            .BuildServiceProvider();
        return Task.CompletedTask;
    }

    public static async Task MessageReceivedAsync(SocketMessage arg)
    {
        var message = arg as SocketUserMessage;
        var argPos = 0;

        if (message?.Author.IsBot ?? true) return;

        if (message.HasStringPrefix("/", ref argPos))
        {
            var context = new SocketCommandContext(Declare.Client, message);

            var result = await Declare.CommandService.ExecuteAsync(context, message.Content.Substring(argPos), Declare.Services);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"Commande échouée: {result.ErrorReason}");
            }
        }
    }

    public static async Task SendMessageAsync(string message, string channel)
    {
        try
        {
            var getChannel = ulong.Parse(channel);
            var channelId = Declare.Client.GetChannel(getChannel) as IMessageChannel;

            if (channelId == null)
            {
                Console.WriteLine($"Le canal avec l'ID {channel} est introuvable ou inaccessible.");
                Console.WriteLine("Voici les canaux accessibles par le bot :");

                foreach (var guild in Declare.Client.Guilds)
                {
                    foreach (var textChannel in guild.TextChannels)
                    {
                        Console.WriteLine($"Canal accessible : {textChannel.Name} (ID : {textChannel.Id})");
                    }
                }

                return;
            }

            await channelId.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi du message : {ex.Message}");
        }
    }
    public static async Task RegisterCommandsAsync()
    {
        var commands = new List<SlashCommandBuilder>
    {
        new SlashCommandBuilder()
            .WithName("get-aliases")
            .WithDescription("Get Aliases"),

        new SlashCommandBuilder()
            .WithName("delete-alias")
            .WithDescription("Delete Alias")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("added-alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("add-alias")
            .WithDescription("Add Alias")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("skip_useless_mention")
                .WithDescription("Set if you want to skip useless mention")
                .WithType(ApplicationCommandOptionType.Boolean)
                .WithRequired(true)
                ),

        new SlashCommandBuilder()
            .WithName("add-url")
            .WithDescription("Add a URL and create a thread.")
            .AddOption("url", ApplicationCommandOptionType.String, "The URL to track", isRequired: true)
            .AddOption("thread-name", ApplicationCommandOptionType.String, "Name of the thread to create", isRequired: true)
            .AddOption(
                "thread-type",
                ApplicationCommandOptionType.String,
                "Specify if the thread is public or private",
                isRequired: true,
                choices: new ApplicationCommandOptionChoiceProperties[]
                {
                    new ApplicationCommandOptionChoiceProperties { Name = "Public", Value = "Public" },
                    new ApplicationCommandOptionChoiceProperties { Name = "Private", Value = "Private" }
                })
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("silent")
                .WithDescription("Set to true if you want to have only message when an alias is set.")
                .WithType(ApplicationCommandOptionType.Boolean)
                .WithRequired(true)
                ),

        new SlashCommandBuilder()
            .WithName("delete-url")
            .WithDescription("Delete Url, clean Alias and Recap"),

        new SlashCommandBuilder()
            .WithName("status-games-list")
            .WithDescription("Status for all games"),

        new SlashCommandBuilder()
            .WithName("recap-all")
            .WithDescription("His own recap list of items for all the games"),

        new SlashCommandBuilder()
            .WithName("info")
            .WithDescription("get all infos for your Archipelago."),

        new SlashCommandBuilder()
            .WithName("get-patch")
            .WithDescription("patch for alias.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("recap")
            .WithDescription("Recap List of items for a specific game")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("added-alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("recap-and-clean")
            .WithDescription("Recap and clean List of items for a specific game")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("added-alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("clean")
            .WithDescription("Recap and clean List of items for a specific game")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("added-alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("clean-all")
            .WithDescription("Recap and clean his own recap list of items for all the games"),

        new SlashCommandBuilder()
            .WithName("hint-from-finder")
            .WithDescription("Get a hint from finder")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("hint-for-receiver")
            .WithDescription("Get a hint for receiver")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("list-items")
            .WithDescription("List all items for alias")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true))
            .AddOption(BuildListItemsOption()),

        new SlashCommandBuilder()
            .WithName("list-yamls")
            .WithDescription("Liste tous les yamls du channel"),

        new SlashCommandBuilder()
            .WithName("list-apworld")
            .WithDescription("Liste tous les yamls du channel"),

        new SlashCommandBuilder()
            .WithName("apworlds-info")
            .WithDescription("Liste toutes les info des apworlds")
            .AddOption(new SlashCommandOptionBuilder()
                    .WithName("apworldsinfo")
                    .WithDescription("Choisissez un APWorld pour avoir ses infos")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("backup-yamls")
            .WithDescription("backup tous les yamls du channel"),

        new SlashCommandBuilder()
            .WithName("backup-apworld")
            .WithDescription("backup tous les yamls du channel"),

        new SlashCommandBuilder()
            .WithName("download-template")
            .WithDescription("download-template")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("template")
                .WithDescription("Choisissez un fichier YAML à télécharger")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("delete-yaml")
            .WithDescription("Supprime un fichier YAML spécifique du channel")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("file")
                .WithDescription("Choisissez un fichier YAML à supprimer")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("clean-yamls")
            .WithDescription("Clean tous les yamls du channel"),

        new SlashCommandBuilder()
            .WithName("send-yaml")
            .WithDescription("Envoyer ou remplacer le yaml sur le server pour la génération")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("fichier")
                .WithDescription("Téléchargez un fichier YAML")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(true)),

        new SlashCommandBuilder()
            .WithName("generate-with-zip")
            .WithDescription("Génère un multiworld à partir d'un fichier ZIP")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("fichier")
                .WithDescription("Téléchargez un fichier ZIP contenant les fichiers YML")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(true)),

        new SlashCommandBuilder()
            .WithName("send-apworld")
            .WithDescription("Ajouter ou remplacer un apworld au server")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("fichier")
                .WithDescription("Téléchargez un fichier apworld")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(true)),

        new SlashCommandBuilder()
            .WithName("generate")
            .WithDescription("Génère un multiworld à partir des yamls déjà présent sur le server")
    };

        var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();

        var tasks = Declare.Client.Guilds.Select(guild => Declare.Client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id));
        await Task.WhenAll(tasks);

        Declare.Client.SlashCommandExecuted += HandleSlashCommandAsync;
        Declare.Client.AutocompleteExecuted += HandleAutocompleteAsync;
    }


    private static async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        if (interaction.Data.Current.Name == "alias")
        {
            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();

            if (string.IsNullOrWhiteSpace(channelId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            if (guildId == null)
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var aliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId);

            string userInput = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";

            int pageSize = 25;
            int pageNumber = 1;

            if (userInput.StartsWith(">"))
            {
                if (int.TryParse(userInput.TrimStart('>'), out int parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                    userInput = "";
                }
            }

            var filteredAliases = aliases
                .Where(a => a.ToLower().Contains(userInput))
                .OrderBy(a => a)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AutocompleteResult(a, a))
                .ToArray();

            if (filteredAliases.Length == 0 && pageNumber > 1)
            {
                pageNumber = (aliases.Count / pageSize) + 1;
                filteredAliases = aliases
                    .OrderBy(a => a)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AutocompleteResult(a, a))
                    .ToArray();
            }

            await interaction.RespondAsync(filteredAliases);
        }

        if (interaction.Data.Current.Name == "added-alias")
        {
            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();

            if (string.IsNullOrWhiteSpace(channelId) || guildId == null)
            {
                await interaction.RespondAsync(Array.Empty<AutocompleteResult>());
                return;
            }

            var aliases = (await ReceiverAliasesCommands.GetReceiver(guildId, channelId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string userInput = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";

            int pageSize = 25;
            int pageNumber = 1;

            if (userInput.StartsWith(">") &&
                int.TryParse(userInput.TrimStart('>'), out int parsedPage) && parsedPage > 0)
            {
                pageNumber = parsedPage;
                userInput = "";
            }

            var filteredAliases = aliases
                .Where(a => a.ToLower().Contains(userInput))
                .OrderBy(a => a)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AutocompleteResult(a, a))
                .ToArray();

            if (filteredAliases.Length == 0 && pageNumber > 1)
            {
                pageNumber = (aliases.Count / pageSize) + 1;
                filteredAliases = aliases
                    .OrderBy(a => a)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AutocompleteResult(a, a))
                    .ToArray();
            }

            await interaction.RespondAsync(filteredAliases);
        }

        if (interaction.Data.Current.Name == "file")
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();

            if (string.IsNullOrWhiteSpace(channelId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            string directoryPath = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

            if (guildId == null || !Directory.Exists(directoryPath))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var yamlFiles = Directory.GetFiles(directoryPath, "*.yaml")
                .Select(Path.GetFileName)
                .ToList();

            string userInput = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";

            int pageSize = 25;
            int pageNumber = 1;

            // Gestion de la pagination avec ">N"
            if (userInput.StartsWith(">"))
            {
                if (int.TryParse(userInput.TrimStart('>'), out int parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                    userInput = "";
                }
            }

            var filteredYamlFiles = yamlFiles
            .Where(f => f != null && f.ToLower().Contains(userInput))
            .OrderBy(f => f)
            .ToList();

            int totalFiles = filteredYamlFiles.Count;
            int totalPages = (int)Math.Ceiling((double)totalFiles / pageSize);

            if (pageNumber > totalPages) pageNumber = totalPages > 0 ? totalPages : 1;

            var paginatedFiles = filteredYamlFiles
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new AutocompleteResult(f, f))
                .ToArray();

            await interaction.RespondAsync(paginatedFiles);
        }

        if (interaction.Data.Current.Name == "template")
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();
            string directoryPath = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", "Templates");

            if (guildId == null || !Directory.Exists(directoryPath))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var yamlFiles = Directory.GetFiles(directoryPath, "*.yaml")
                .Select(Path.GetFileName)
                .ToList();

            string userInput = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";

            int pageSize = 25;
            int pageNumber = 1;

            // Gestion de la pagination avec ">N"
            if (userInput.StartsWith(">"))
            {
                if (int.TryParse(userInput.TrimStart('>'), out int parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                    userInput = "";
                }
            }

            var filteredYamlFiles = yamlFiles
            .Where(f => f != null && f.ToLower().Contains(userInput))
            .OrderBy(f => f)
            .ToList();

            int totalFiles = filteredYamlFiles.Count;
            int totalPages = (int)Math.Ceiling((double)totalFiles / pageSize);

            if (pageNumber > totalPages) pageNumber = totalPages > 0 ? totalPages : 1;

            var paginatedFiles = filteredYamlFiles
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new AutocompleteResult(f, f))
                .ToArray();

            await interaction.RespondAsync(paginatedFiles);
        }

        if (interaction.Data.Current.Name == "apworldsinfo")
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();

            if (guildId == null)
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            List<string> sections;
            try
            {
                sections = await ApWorldListCommands.GetAllTitles();
            }
            catch
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            string userInput = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";

            int pageSize = 25;
            int pageNumber = 1;

            if (userInput.StartsWith(">"))
            {
                if (int.TryParse(userInput.TrimStart('>'), out int parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                    userInput = "";
                }
            }
            var filteredTitles = sections
                .Where(t => t.ToLower().Contains(userInput))
                .OrderBy(t => t)
                .ToList();

            int totalTitles = filteredTitles.Count;
            int totalPages = (int)Math.Ceiling((double)totalTitles / pageSize);

            if (pageNumber > totalPages) pageNumber = totalPages > 0 ? totalPages : 1;

            var paginatedTitles = filteredTitles
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new AutocompleteResult(t, t))
                .ToArray();

            await interaction.RespondAsync(paginatedTitles);
        }
    }

    public static SlashCommandOptionBuilder BuildListItemsOption()
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName("list-by-line")
            .WithDescription("Choose whether to display items line by line (true) or comma separated (false).")
            .WithType(ApplicationCommandOptionType.Boolean)
            .WithRequired(true);

        return optionBuilder;
    }

    public static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var guildUser = command.User as IGuildUser;
        var userId = "";
        string message = "";
        const int maxMessageLength = 1999;
        var realAlias = command.Data.Options.FirstOrDefault()?.Value as string;
        
        var matchCustomAlias = !string.IsNullOrEmpty(realAlias) ? Regex.Match(realAlias, @"\(([^)]+)\)$") : Match.Empty;
        var alias = matchCustomAlias.Success ? matchCustomAlias.Groups[1].Value : realAlias;

        var channelId = command.ChannelId.ToString();
        var guildId = command.GuildId.ToString();

        if (string.IsNullOrWhiteSpace(guildId))
        {
            await command.RespondAsync("Cette commande ne peut pas être exécutée en dehors d'un serveur.");
            return;
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            await command.RespondAsync("Cette commande ne peut pas être exécutée en dehors d'un serveur.");
            return;
        }

        if (command.Channel is IThreadChannel threadChannel)
        {
            var requestOption = new RequestOptions
            {
                Timeout = 300000
            };

            await command.DeferAsync(ephemeral: true, requestOption);

            switch (command.CommandName)
            {
                case "get-aliases":

                    var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
                    var getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                    if (!checkChannel)
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    else if (getReceiverAliases.Count == 0)
                    {
                        message = "Aucun Alias est enregistré.";
                    }
                    else
                    {
                        var sb = new StringBuilder("Voici le tableau des utilisateurs :\n");
                        foreach (var getReceiverAliase in getReceiverAliases)
                        {
                            var getUserIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, getReceiverAliase);

                            foreach (var value in getUserIds)
                            {
                                var user = await Declare.Client.GetUserAsync(ulong.Parse(value.UserId));
                                sb.AppendLine($"| {user.Username} | {getReceiverAliase} | Useless Item Skip: {value.IsEnabled.ToString()}");
                            }
                        }
                        message = sb.ToString();
                    }
                    break;

                case "delete-alias":
                    async Task<bool> HasValidChannelDataAsync(string guildId, string channelId)
                    {
                        return checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
                    }

                    if (!await HasValidChannelDataAsync(guildId, channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                    }
                    else
                    {
                        getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                        if (getReceiverAliases.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else if (alias != null)
                        {
                            var getUserId = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, alias);

                            if (getUserId != null)
                            {
                                message = $"Aucun alias trouvé pour '{alias}'.";
                                foreach (var value in getUserId.Select(x => x.UserId))
                                {
                                    if (value == command.User.Id.ToString() || (guildUser != null && guildUser.GuildPermissions.Administrator))
                                    {
                                        await ReceiverAliasesCommands.DeleteReceiverAlias(guildId, channelId, alias);

                                        message = value == command.User.Id.ToString()
                                            ? $"Alias '{alias}' supprimé."
                                            : $"ADMIN : Alias '{alias}' supprimé.";

                                        await RecapListCommands.DeleteAliasAndRecapListAsync(guildId, channelId, value, alias);
                                    }
                                    else
                                    {
                                        message = $"Vous n'êtes pas le détenteur de cet alias : '{alias}'. Suppression non effectuée.";
                                    }
                                }
                            }
                            else
                            {
                                message = $"Aucun alias trouvé pour '{alias}'.";
                            }
                        }
                    }
                    break;

                case "add-alias":
                    userId = command.User.Id.ToString();
                    var skipUselessMention = command.Data.Options.ElementAtOrDefault(1)?.Value as bool? ?? false;

                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        message = "L'alias ne peut pas être vide.";
                        break;
                    }

                    var getReceiverAlias = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);

                    if (getReceiverAlias.Contains(userId))
                    {
                        message = $"L'alias '{alias}' est déjà enregistré pour <@{userId}>.";
                        break;
                    }

                    await ReceiverAliasesCommands.InsertReceiverAlias(guildId, channelId, alias, userId, skipUselessMention);

                    var checkRecapList = await RecapListCommands.CheckIfExists(guildId, channelId, userId, alias);
                    if (!checkRecapList)
                    {
                        await RecapListCommands.AddOrEditRecapListAsync(guildId, channelId, userId, alias);
                    }

                    var getAliasItems = await DisplayItemCommands.GetAliasItems(guildId, channelId, alias);
                    if (getAliasItems != null)
                    {
                        await RecapListCommands.AddOrEditRecapListItemsAsync(guildId, channelId, alias, getAliasItems);
                    }

                    message = $"Alias ajouté : {alias} est maintenant associé à <@{userId}> et son récap généré.";

                    break;

                case "delete-url":
                    if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                    {
                        message = "Seuls les administrateurs sont autorisés à supprimer une URL.";
                    }
                    else
                    {
                        message = await DeleteChannelAndUrl(channelId, guildId);
                    }
                    break;

                case "recap-all":
                    userId = command.User.Id.ToString();

                    async Task<string> TryGetReceiverData(string guildId, string channelId)
                    {
                        var errorMessage = string.Empty;
                        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable");

                        if (!checkIfChannelExistsAsync)
                        {
                            return errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                        }

                        var getUserId = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);

                        if (!getUserId.Any(x => x.Contains(userId)))
                        {
                            return errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                        }

                        return errorMessage = string.Empty;
                    }

                    bool TryGetRecapData(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        var checkIfExists = RecapListCommands.CheckIfExistsWithoutAlias(guildId, channelId, userId).Result;
                        if (!checkIfExists)
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        var getUserAliasesWithItemsAsync = ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId).Result;

                        if (getUserAliasesWithItemsAsync.Any())
                        {
                            var sb = new StringBuilder($"Détails pour <@{receiverId}> :\n");
                            foreach (var subElement in getUserAliasesWithItemsAsync)
                            {
                                string groupedMessage = subElement.Value != null && subElement.Value.Any()
                                    ? string.Join(", ", subElement.Value
                                        .GroupBy(value => value)
                                        .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                    : "Aucun élément";

                                sb.AppendLine($"**{subElement.Key}** : {groupedMessage}");
                            }
                            recapMessage = sb.ToString();
                            return true;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias.";
                        return false;
                    }

                    var tryGetReceiverData = await TryGetReceiverData(guildId, channelId);

                    if (string.IsNullOrWhiteSpace(tryGetReceiverData))
                    {
                        if (TryGetRecapData(guildId, channelId, userId, out string recapMessage))
                        {
                            message = recapMessage;
                        }
                        else
                        {
                            message = recapMessage;
                        }
                    }
                    else
                    {
                        message = tryGetReceiverData;
                    }
                    break;

                case "recap":
                    userId = command.User.Id.ToString();

                    async Task<string> TryGetReceiverAliasData(string guildId, string channelId, string receiverId)
                    {
                        var errorMessage = string.Empty;
                        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable");

                        if (!checkIfChannelExistsAsync)
                        {
                            return errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                        }

                        var getUserId = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);

                        if (!getUserId.Any(x => x.Contains(receiverId)))
                        {
                            return errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                        }

                        return errorMessage = string.Empty;
                    }

                    bool TryGetRecapDataRecap(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (alias == null)
                        {
                            recapMessage = "L'alias ne peut pas être vide.";
                            return false;
                        }

                        var checkIfExists = RecapListCommands.CheckIfExists(guildId, channelId, userId, alias).Result;
                        if (!checkIfExists)
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (alias == null)
                        {
                            recapMessage = "L'alias ne peut pas être vide.";
                            return false;
                        }

                        var getUserAliasesWithItemsAsync = ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId, alias).Result;

                        if (getUserAliasesWithItemsAsync.Any())
                        {
                            var getUser = getUserAliasesWithItemsAsync.Any(x => x.Key == alias);

                            if (getUser)
                            {
                                var groupedMessages = getUserAliasesWithItemsAsync
                                    .Where(x => x.Key == alias)
                                    .Select(subElement => subElement.Value != null && subElement.Value.Any()
                                        ? $"**{subElement.Key}** : {string.Join(", ", subElement.Value.GroupBy(value => value).Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))} \n"
                                        : $"**{subElement.Key}** : Aucun élément \n");

                                recapMessage = $"Détails pour <@{receiverId}> :\n{string.Join("", groupedMessages)}";
                                return true;
                            }

                            recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    var tryGetReceiverAliasData = await TryGetReceiverAliasData(guildId, channelId, userId);

                    if (string.IsNullOrWhiteSpace(tryGetReceiverAliasData))
                    {
                        if (TryGetRecapDataRecap(guildId, channelId, userId, out string recapMessage))
                        {
                            message = recapMessage;
                        }
                        else
                        {
                            message = recapMessage;
                        }
                    }
                    else
                    {
                        message = tryGetReceiverAliasData;
                    }
                    break;

                case "recap-and-clean":
                    userId = command.User.Id.ToString();

                    async Task<string> TryGetReceiverAliasDataRecapAndClean(string guildId, string channelId, string receiverId)
                    {
                        var errorMessage = string.Empty;
                        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable");

                        if (!checkIfChannelExistsAsync)
                        {
                            return errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                        }

                        var getUserId = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);

                        if (!getUserId.Any(x => x.Contains(receiverId)))
                        {
                            return errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                        }

                        return errorMessage = string.Empty;
                    }

                    bool TryGetRecapDataRecapAndClean(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (alias == null)
                        {
                            recapMessage = "L'alias ne peut pas être vide.";
                            return false;
                        }

                        var checkIfExists = RecapListCommands.CheckIfExists(guildId, channelId, userId, alias).Result;
                        if (!checkIfExists)
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (alias == null)
                        {
                            recapMessage = "L'alias ne peut pas être vide.";
                            return false;
                        }

                        var getUserAliasesWithItemsAsync = ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId, alias).Result;

                        if (getUserAliasesWithItemsAsync.Any())
                        {
                            var userSubElements = getUserAliasesWithItemsAsync.Where(x => x.Key == alias).ToList();
                            if (userSubElements.Any())
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                recapMessage += string.Join("\n", userSubElements.Select(subElement =>
                                {
                                    string groupedMessage = subElement.Value != null && subElement.Value.Any()
                                        ? string.Join(", ", subElement.Value
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";
                                    return $"**{subElement.Key}** : {groupedMessage}";
                                }));

                                Task.Run(async () => await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias)).Wait();

                                return true;
                            }

                            recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    var tryGetReceiverAliasDataRecapAndClean = await TryGetReceiverAliasDataRecapAndClean(guildId, channelId, userId);

                    if (string.IsNullOrWhiteSpace(tryGetReceiverAliasDataRecapAndClean))
                    {
                        if (TryGetRecapDataRecapAndClean(guildId, channelId, userId, out string recapMessage))
                        {
                            message = recapMessage;
                        }
                        else
                        {
                            message = recapMessage;
                        }
                    }
                    else
                    {
                        message = tryGetReceiverAliasDataRecapAndClean;
                    }
                    break;

                case "clean":
                    userId = command.User.Id.ToString();

                    async Task<string> TryGetReceiverAliasDataClean(string guildId, string channelId, string alias)
                    {
                        var errorMessage = string.Empty;
                        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable");

                        if (!checkIfChannelExistsAsync)
                        {
                            return errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                        }

                        var getUserIds = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);

                        if (!getUserIds.Any(x => x.Contains(userId)))
                        {
                            return errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                        }

                        return errorMessage = string.Empty;
                    }

                    bool TryClearAliasDataClean(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        var checkIfExists = RecapListCommands.CheckIfExists(guildId, channelId, userId, alias).Result;
                        if (!checkIfExists)
                        {
                            resultMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(alias))
                        {
                            resultMessage = "L'alias ne peut pas être vide.";
                            return false;
                        }

                        var getUserAliasesWithItemsAsync = ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId, alias).Result;


                        if (getUserAliasesWithItemsAsync.Any())
                        {
                            var aliasElement = getUserAliasesWithItemsAsync.Keys.FirstOrDefault(x => x.Equals(alias));

                            if (aliasElement != null)
                            {
                                Task.Run(async () => await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias)).Wait();
                                resultMessage = string.Empty;
                                return true;
                            }

                            resultMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    if (alias == null)
                    {
                        message = "L'alias ne peut pas être vide.";
                        break;
                    }

                    var tryGetReceiverAliasDataClean = await TryGetReceiverAliasDataClean(guildId, channelId, alias);

                    if (string.IsNullOrWhiteSpace(tryGetReceiverAliasDataClean))
                    {
                        if (!TryClearAliasDataClean(guildId, channelId, userId, out string clearMessage))
                        {
                            message = clearMessage;
                        }
                        else
                        {
                            message = $"Clean pour Alias {alias} effectué";
                        }
                    }
                    else
                    {
                        message = $"Il n'existe pas de list pour: {alias}.";
                    }
                    break;

                case "clean-all":
                    userId = command.User.Id.ToString();

                    async Task<string> TryGetReceiverAliasDataCleanAll(string guildId, string channelId, string receiverId)
                    {
                        var errorMessage = string.Empty;
                        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "RecapListTable");

                        if (!checkIfChannelExistsAsync)
                        {
                            return errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                        }

                        var getReceiver = await ReceiverAliasesCommands.GetUserIds(guildId, channelId);

                        if (!getReceiver.Any(x => x.Contains(receiverId)))
                        {
                            return errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                        }

                        return errorMessage = string.Empty;
                    }

                    bool TryClearSubElementsCleanAll(string guildId, string channelId, string receiverId, out string resultMessage)
                    {

                        var checkIfExists = RecapListCommands.CheckIfExistsWithoutAlias(guildId, channelId, userId).Result;
                        if (!checkIfExists)
                        {
                            resultMessage = "Il existe aucune liste.";
                            return false;
                        }

                        var getUserAliasesWithItemsAsync = ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId).Result;

                        if (getUserAliasesWithItemsAsync.Any())
                        {

                            Task.Run(async () => await RecapListCommands.DeleteAliasAndItemsForUserIdAsync(guildId, channelId, userId)).Wait();
                            resultMessage = string.Empty;
                            return true;
                        }

                        resultMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                        return false;
                    }

                    var tryGetReceiverAliasDataCleanAll = await TryGetReceiverAliasDataCleanAll(guildId, channelId, userId);

                    if (string.IsNullOrWhiteSpace(tryGetReceiverAliasDataCleanAll))
                    {
                        if (!TryClearSubElementsCleanAll(guildId, channelId, userId, out string cleanMessage))
                        {
                            message = cleanMessage;
                        }
                        else
                        {
                            message = $"Clean All pour <@{userId}> effectué";
                        }
                    }
                    break;

                case "list-items":
                    bool listByLine = command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value as bool? ?? false;

                    string BuildItemMessage(IEnumerable<IGrouping<string, DisplayedItem>> filteredItems, bool listByLine)
                    {
                        var messageBuilder = new StringBuilder();
                        bool isFirst = true;

                        foreach (var groupedItem in filteredItems)
                        {
                            if (!isFirst)
                            {
                                messageBuilder.Append(listByLine ? "\n" : ", ");
                            }
                            messageBuilder.Append(groupedItem.Count() > 1
                                ? $"{groupedItem.Key} x {groupedItem.Count()}"
                                : groupedItem.Key);
                            isFirst = false;
                        }

                        return messageBuilder.ToString();
                    }

                    var checkIfChannelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "DisplayedItemTable");

                    if (checkIfChannelExists)
                    {
                        if (string.IsNullOrWhiteSpace(alias))
                        {
                            message = "Erreur d'Alias.";
                            break;
                        }

                        var getGameStatusTextsAsync = await DisplayItemCommands.GetUserItemsGroupedAsync(guildId, channelId, alias);
                        var filteredItems = getGameStatusTextsAsync
                                    .GroupBy(item => item.Item)
                                    .OrderBy(group => group.Key);


                        if (filteredItems.Any())
                        {
                            message = $"Items pour {userId} :\n{BuildItemMessage(filteredItems, listByLine)}";
                        }
                        else
                        {
                            await command.FollowupAsync("Pas d'items");
                        }
                    }
                    else
                    {
                        await command.FollowupAsync("Pas d'URL Enregistrée pour ce channel");
                    }
                    break;

                case "hint-from-finder":
                    if (string.IsNullOrEmpty(realAlias))
                    {
                        message = "Alias non spécifié.";
                        break;
                    }

                    string BuildHintMessageFinder(IEnumerable<HintStatus> hints, string realAlias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item from {realAlias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    var getHintStatusForFinder = await HintStatusCommands.GetHintStatusForFinder(guildId, channelId, realAlias);

                    if (getHintStatusForFinder.Any())
                    {
                        var hintByFinder = new List<HintStatus>();

                        foreach (var hint in getHintStatusForFinder)
                        {
                            if (hint.Finder == realAlias)
                            {
                                hintByFinder.Add(hint);
                            }
                        }

                        message = hintByFinder.Count > 0
                            ? BuildHintMessageFinder(hintByFinder, realAlias)
                            : "No hint found for this finder";
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou Aucun hint.";
                    }
                    break;

                case "hint-for-receiver":
                    if (string.IsNullOrEmpty(realAlias))
                    {
                        message = "Alias non spécifié.";
                        break;
                    }

                    string BuildHintMessageReceiver(List<HintStatus> hints, string realAlias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item for {realAlias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    var getHintStatusForReceiver = await HintStatusCommands.GetHintStatusForReceiver(guildId, channelId, realAlias);

                    if (getHintStatusForReceiver.Any())
                    {
                        var hintByReceiver = new List<HintStatus>();

                        foreach (var hint in getHintStatusForReceiver)
                        {
                            if (hint.Receiver == realAlias)
                            {
                                hintByReceiver.Add(hint);
                            }
                        }

                        message = hintByReceiver.Count > 0
                            ? BuildHintMessageReceiver(hintByReceiver, realAlias)
                            : "No hint found for this receiver";
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou Aucun hint.";
                    }
                    break;

                case "status-games-list":
                    var getGameStatusForGuildAndChannelAsync = await GameStatusCommands.GetGameStatusForGuildAndChannelAsync(guildId, channelId);
                    var (urlTracker, urlSphereTracker, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);

                    if(silent)
                    {
                        message = "Status for all games, Thread is silent, Only for added aliases :\n";
                        getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                        if (getReceiverAliases.Count == 0)
                        {
                            message += "Aucun Alias est enregistré.";
                        }
                        else
                        {
                            getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

                            if (getReceiverAliases.Count == 0)
                            {
                                message += "Aucun Alias est enregistré.";
                            }
                            else
                            {
                                var filteredGameStatus = getGameStatusForGuildAndChannelAsync
                                 .Where(x =>
                                 {
                                     if (getReceiverAliases == null) return false;

                                     var match = Regex.Match(x.Name, @"\(([^)]+)\)$");
                                     var alias = match.Success ? match.Groups[1].Value : x.Name;

                                     return getReceiverAliases.Contains(alias);
                                 });

                                foreach (var game in filteredGameStatus)
                                {
                                    string gameStatus = (game.Percent != "100.00")
                                        ? $"**{game.Name} - {game.Game} - {game.Percent}%**\n"
                                        : $"~~{game.Name} - {game.Game} - {game.Percent}%~~\n";
                                    message += gameStatus;
                                }
                            }
                        }
                    }
                    else
                    {
                        message = "Status for all games :\n";

                        if (getGameStatusForGuildAndChannelAsync.Any())
                        {
                            foreach (var game in getGameStatusForGuildAndChannelAsync)
                            {
                                string gameStatus = (game.Percent != "100.00")
                                    ? $"**{game.Name} - {game.Game} - {game.Percent}%**\n"
                                    : $"~~{game.Name} - {game.Game} - {game.Percent}%~~\n";

                                message += gameStatus;
                            }
                        }
                        else
                        {
                            message = "Pas d'URL Enregistrée pour ce channel.";
                        }
                    }
                    break;

                case "info":
                    (urlTracker, urlSphereTracker, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guildId, channelId);

                    if (!string.IsNullOrEmpty(room))
                    {
                        using HttpClient client = new();
                        string pageContent = await client.GetStringAsync(room);

                        string? port = null;
                        var match = Regex.Match(pageContent, @"/connect archipelago\.gg:(\d+)", RegexOptions.Singleline);

                        message += "Info:\n";
                        message += $"Room : {room}\n";
                        message += $"Tracker : {urlTracker}\n";
                        message += $"SphereTracker : {urlSphereTracker}\n";
                        message += $"Silent : {silent}\n";

                        if (match.Success)
                        {
                            port = match.Groups[1].Value;
                            message += $"Port : {port}\n";
                        }
                        else
                        {
                            message += "Port non trouvé.\n";
                        }
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    break;

                case "get-patch":
                    userId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;

                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        message = "Receiver ID non spécifié.";
                        break;
                    }

                    var getNameAndPatch = await ChannelsAndUrlsCommands.GetPatchAndGameNameForAlias(guildId, channelId, userId);

                    if (!string.IsNullOrEmpty(getNameAndPatch))
                    {
                        message += $"Patch Pour {userId}, {getNameAndPatch}\n\n";
                    }
                    else
                    {
                        message = "Pas de patch pour ce user.";
                    }
                    break;

                default:
                    message = "Commande inconnue.";
                    break;

            }

            while (message.Length > maxMessageLength)
            {
                int splitIndex = message.LastIndexOf(", ", maxMessageLength, StringComparison.Ordinal);
                if (splitIndex == -1)
                {
                    splitIndex = message.LastIndexOf("\n", maxMessageLength, StringComparison.Ordinal);
                }
                if (splitIndex == -1)
                {
                    splitIndex = maxMessageLength;
                }

                string messagePart = message.Substring(0, splitIndex);
                await command.FollowupAsync(messagePart, options: new RequestOptions { Timeout = 10000 }, ephemeral: true);

                message = message.Substring(splitIndex + 1).Trim();
            }

            if (!string.IsNullOrEmpty(message))
            {
                await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 }, ephemeral: true);
            }
        }
        else
        {
            await command.DeferAsync(ephemeral: false);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            var channel = command.Channel as ITextChannel;
            if (channel != null)
            {
                switch (command.CommandName)
                {
                    case "add-url":
                        string baseUrl = "https://archipelago.gg";
                        string? trackerUrl = null;
                        string? sphereTrackerUrl = null;
                        string port = string.Empty;
                        var silent = command.Data.Options.ElementAtOrDefault(3)?.Value as bool? ?? false;

                        bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

                        async Task<bool> CanAddUrlAsync(string guildId, string channelId)
                        {
                            var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
                            return !checkIfChannelExistsAsync;
                        }

                        async Task<(bool isValid, string pageContent)> IsAllUrlIsValidAsync(string newUrl)
                        {
                            using HttpClient client = new();
                            string pageContent = await client.GetStringAsync(newUrl);

                            var portMatch = Regex.Match(pageContent, @"/connect archipelago\.gg:(\d+)");
                            if (portMatch.Success)
                            {
                                port = portMatch.Groups[1].Value;
                                Console.WriteLine($"Port trouvé : {port}");
                            }
                            else
                            {
                                Console.WriteLine("Port non trouvé.");
                                return (false, pageContent);
                            }

                            trackerUrl = ExtractUrl(pageContent, "Multiworld Tracker");
                            sphereTrackerUrl = ExtractUrl(pageContent, "Sphere Tracker");

                            if (string.IsNullOrEmpty(trackerUrl) || string.IsNullOrEmpty(sphereTrackerUrl) || string.IsNullOrEmpty(port))
                            {
                                return (false, pageContent);
                            }

                            if (!trackerUrl.StartsWith("http"))
                            {
                                trackerUrl = baseUrl + trackerUrl;
                            }
                            if (!sphereTrackerUrl.StartsWith("http"))
                            {
                                sphereTrackerUrl = baseUrl + sphereTrackerUrl;
                            }

                            return (true, pageContent);
                        }

                        string? ExtractUrl(string htmlContent, string linkText)
                        {
                            var match = Regex.Match(htmlContent, $@"<a[^>]*>.*{linkText}.*</a>", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                var hrefMatch = Regex.Match(match.Value, @"href=""(.*?)""");
                                if (hrefMatch.Success)
                                {
                                    return hrefMatch.Groups[1].Value;
                                }
                            }
                            return null;
                        }

                        if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                        {
                            message = "Seuls les administrateurs sont autorisés à ajouter une URL.";
                        }
                        else
                        {
                            if (await CanAddUrlAsync(guildId, channelId))
                            {
                                var newUrl = command.Data.Options.FirstOrDefault()?.Value as string;

                                if (string.IsNullOrEmpty(newUrl))
                                {
                                    message = "URL vide non autorisée.";
                                }
                                else if (!IsValidUrl(newUrl))
                                {
                                    message = $"Le lien est incorrect, utilisez l'url de la room.";
                                }
                                else
                                {
                                    var (isValid, pageContent) = await IsAllUrlIsValidAsync(newUrl);

                                    if (!isValid)
                                    {
                                        message = $"Sphere_Tracker, Tracker ou le port ne sont pas trouvés. Ajout annulé.";
                                    }
                                    else
                                    {
                                        string? threadTitle = command.Data.Options.ElementAt(1).Value.ToString();
                                        string? threadType = command.Data.Options.ElementAt(2).Value.ToString();

                                        ThreadType type = threadType switch
                                        {
                                            "Private" => ThreadType.PrivateThread,
                                            "Public" => ThreadType.PublicThread,
                                            _ => ThreadType.PublicThread
                                        };

                                        var thread = await channel.CreateThreadAsync(
                                            threadTitle,
                                            autoArchiveDuration: ThreadArchiveDuration.OneWeek,
                                            type: type
                                        );

                                        await thread.SendMessageAsync($"Le thread a été créé: {thread.Name}, Attendez que le bot soit Ready.");

                                        channelId = thread.Id.ToString();

                                        await foreach (var memberBatch in channel.GetUsersAsync())
                                        {
                                            foreach (var member in memberBatch)
                                            {
                                                await thread.AddUserAsync(member);
                                            }
                                        }

                                        var rowsMatch = Regex.Matches(pageContent, @"<tr[^>]*>.*?</tr>", RegexOptions.Singleline);
                                        var patchLinkList = new List<Patch>();

                                        foreach (Match rowMatch in rowsMatch)
                                        {
                                            var columnsMatch = Regex.Matches(rowMatch.Value, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline);
                                            if (columnsMatch.Count >= 4)
                                            {
                                                string gameAliasHtml = WebUtility.HtmlDecode(columnsMatch[1].Groups[1].Value.Trim());
                                                var gameAliasMatch = Regex.Match(gameAliasHtml, @">([^<]+)<");
                                                string gameAlias = gameAliasMatch.Success ? gameAliasMatch.Groups[1].Value : gameAliasHtml;

                                                string gameName = WebUtility.HtmlDecode(columnsMatch[2].Groups[1].Value.Trim());

                                                string downloadLinkHtml = WebUtility.HtmlDecode(columnsMatch[3].Groups[1].Value.Trim());
                                                var downloadLinkMatch = Regex.Match(downloadLinkHtml, @"href=\""(.*?)\""");
                                                string downloadLink = downloadLinkMatch.Success ? downloadLinkMatch.Groups[1].Value.Trim() : "Aucun fichier";

                                                if (string.IsNullOrEmpty(downloadLink) || downloadLink.Equals("Aucun fichier"))
                                                {
                                                    continue;
                                                }

                                                Console.WriteLine($"Nom: {gameAlias} | Téléchargement: {downloadLink}");

                                                var patchLink = new Patch
                                                {
                                                    GameAlias = gameAlias,
                                                    GameName = gameName,
                                                    PatchLink = baseUrl + downloadLink
                                                };

                                                patchLinkList.Add(patchLink);
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(trackerUrl) && !string.IsNullOrEmpty(sphereTrackerUrl))
                                        {
                                            await ChannelsAndUrlsCommands.AddOrEditUrlChannelAsync(guildId, channelId, newUrl, trackerUrl, sphereTrackerUrl, silent);
                                            await ChannelsAndUrlsCommands.AddOrEditUrlChannelPathAsync(guildId, channelId, patchLinkList);
                                            await TrackingDataManager.SetAliasAndGameStatusAsync(guildId, channelId, trackerUrl, silent);
                                            await TrackingDataManager.CheckGameStatusAsync(guildId, channelId, trackerUrl, silent);
                                            await TrackingDataManager.GetTableDataAsync(guildId, channelId, sphereTrackerUrl, silent);
                                            await BotCommands.SendMessageAsync("BOT Ready!", channelId);

                                        }

                                        message = $"URL définie sur {newUrl}. Messages configurés pour ce canal. Attendez que le programme récupère tous les aliases.";
                                    }
                                }
                            }
                            else
                            {
                                message = "URL déjà définie sur ce channel. Supprimez l'url avant d'ajouter une nouvelle url.";
                            }
                        }

                        break;

                    case "list-yamls":
                        string playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
                        if (Directory.Exists(playersFolderChannel))
                        {
                            var listYamls = Directory.EnumerateFiles(playersFolderChannel, "*.yaml").OrderBy(path => Path.GetFileName(path));

                            if (listYamls.Any())
                            {
                                var sb = new StringBuilder("Liste de Yamls\n");
                                foreach (var yams in listYamls)
                                {
                                    var yamsFileName = Path.GetFileName(yams);
                                    sb.AppendLine(yamsFileName);
                                }
                                message += sb.ToString();
                            }
                            else
                            {
                                message += "❌ Aucun fichier YAML trouvé !";
                            }
                        }
                        else
                        {
                            message += "❌ Aucun fichier YAML trouvé !";
                        }
                        break;

                    case "backup-yamls":
                        playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
                        if (Directory.Exists(playersFolderChannel))
                        {
                            var backupFolder = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "backup");
                            if (!Directory.Exists(backupFolder))
                            {
                                Directory.CreateDirectory(backupFolder);
                            }

                            var zipPath = Path.Combine(backupFolder, $"backup_yaml_{channelId}.zip");

                            if (File.Exists(zipPath))
                            {
                                File.Delete(zipPath);
                            }

                            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                            {
                                var files = Directory.GetFiles(playersFolderChannel, "*.yaml");
                                foreach (var file in files)
                                {
                                    var fileName = Path.GetFileName(file);
                                    zipArchive.CreateEntryFromFile(file, fileName);
                                }
                            }

                            await command.FollowupWithFileAsync(zipPath, $"backup_yaml_{channelId}.zip");

                            File.Delete(zipPath);
                        }
                        else
                        {
                            message += "❌ Aucun fichier YAML trouvé !";
                        }
                        break;

                    case "delete-yaml":
                        var fileSelected = command.Data.Options.FirstOrDefault()?.Value as string;
                        playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

                        if (!string.IsNullOrEmpty(fileSelected))
                        {
                            var deletedfilePath = Path.Combine(playersFolderChannel, fileSelected);

                            if (File.Exists(deletedfilePath))
                            {
                                try
                                {
                                    File.Delete(deletedfilePath);
                                    message += $"Le fichier `{fileSelected}` a été supprimé avec succès. ✅";
                                }
                                catch (Exception ex)
                                {
                                    message += $"Erreur lors de la suppression du fichier `{fileSelected}`: {ex.Message} ❌";
                                }
                            }
                            else
                            {
                                message += $"Le fichier `{fileSelected}` n'existe pas. ❌";
                            }
                        }
                        else
                        {
                            message += "Aucun fichier sélectionné. ❌";
                        }
                        break;

                    case "clean-yamls":
                        playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId);
                        if (Directory.Exists(playersFolderChannel))
                        {
                            try
                            {
                                Directory.Delete(playersFolderChannel, true);
                                message = "Tous les fichiers YAML ont été supprimés.";
                            }
                            catch (IOException ex)
                            {
                                message = $"Erreur lors de la suppression des fichiers : {ex.Message}";
                            }
                        }
                        else
                        {
                            message = "Aucun fichier YAML trouvé.";
                        }
                        break;

                    case "send-yaml":
                        var attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
                        if (attachment == null || !attachment.Filename.EndsWith(".yaml"))
                        {
                            message = "❌ Vous devez envoyer un fichier YAML !";
                            break;
                        }

                        playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");

                        if (!Directory.Exists(playersFolderChannel))
                        {
                            Directory.CreateDirectory(playersFolderChannel);
                        }

                        string filePath = Path.Combine(playersFolderChannel, attachment.Filename);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        using (var response = await Declare.HttpClient.GetAsync(attachment.Url))
                            if (response.IsSuccessStatusCode)
                            {
                                await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                                {
                                    await response.Content.CopyToAsync(fs);
                                }
                                message = $"Fichier `{attachment.Filename}` envoyé.";
                            }
                            else
                            {
                                message = "❌ Échec du téléchargement du fichier.";
                            }
                        break;

                    case "download-template":
                        var yamlFile = command.Data.Options.FirstOrDefault()?.Value as string;

                        if (string.IsNullOrEmpty(yamlFile))
                        {
                            message = "❌ Aucun fichier sélectionné.";
                            break;
                        }

                        string templatePath = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", "Templates", yamlFile);

                        if (File.Exists(templatePath))
                        {
                            await command.FollowupWithFileAsync(templatePath, yamlFile);
                        }
                        else
                        {
                            message = "❌ le fichier n'existe pas !";
                        }
                        break;

                    case "list-apworld":
                        string apworldPath = Path.Combine(Program.BasePath, "extern", "Archipelago", "custom_worlds");
                        if (Directory.Exists(apworldPath))
                        {
                            var listAppworld = Directory.EnumerateFiles(apworldPath, "*.apworld").OrderBy(path => Path.GetFileName(path));

                            if (listAppworld.Any())
                            {
                                var sb = new StringBuilder("Liste de apworld\n");
                                foreach (var apworld in listAppworld)
                                {
                                    var apworldFileName = Path.GetFileName(apworld);
                                    sb.AppendLine($"`{apworldFileName}`");
                                }
                                message += sb.ToString();
                            }
                            else
                            {
                                message += "❌ Aucun fichier apworld trouvé !";
                            }
                        }
                        break;

                    case "apworlds-info":
                        var infoSelected = command.Data.Options.FirstOrDefault()?.Value as string;

                        if (infoSelected == null)
                        {
                            message = "❌ Aucun fichier sélectionné.";
                            break;
                        }

                        var selectedSection = await ApWorldListCommands.GetItemsByTitleAsync(infoSelected);

                        if (string.IsNullOrWhiteSpace(selectedSection))
                        {
                            message = "❌ Aucun fichier sélectionné.";
                            break;
                        }
                        message = selectedSection;

                        if (string.IsNullOrWhiteSpace(selectedSection))
                        {
                            message = "❌ Aucun fichier sélectionné.";
                            break;
                        }
                        message = selectedSection;
                        break;

                    case "backup-apworld":
                        apworldPath = Path.Combine(Program.BasePath, "extern", "Archipelago", "custom_worlds");
                        if (Directory.Exists(apworldPath))
                        {
                            var backupFolder = Path.Combine(Program.BasePath, "extern", "Archipelago", "backup");
                            if (!Directory.Exists(backupFolder))
                            {
                                Directory.CreateDirectory(backupFolder);
                            }

                            var zipPath = Path.Combine(backupFolder, $"backup_apworld.zip");

                            if (File.Exists(zipPath))
                            {
                                File.Delete(zipPath);
                            }

                            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                            {
                                var files = Directory.GetFiles(apworldPath, "*.apworld");
                                foreach (var file in files)
                                {
                                    var fileName = Path.GetFileName(file);
                                    zipArchive.CreateEntryFromFile(file, fileName);
                                }
                            }

                            await command.FollowupWithFileAsync(zipPath, $"backup_apworld.zip");

                            File.Delete(zipPath);
                        }
                        else
                        {
                            message += "❌ Aucun fichier APWORLD trouvé !";
                        }
                        break;

                    case "send-apworld":
                        attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
                        if (attachment == null || !attachment.Filename.EndsWith(".apworld"))
                        {
                            message = "❌ Vous devez envoyer un fichier APWORLD !";
                            break;
                        }

                        var customWorldPath = Path.Combine(Program.BasePath, "extern", "Archipelago", "custom_worlds");

                        Directory.CreateDirectory(customWorldPath);

                        filePath = Path.Combine(customWorldPath, attachment.Filename);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        using (var response = await Declare.HttpClient.GetAsync(attachment.Url))
                        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                        Program.GenerateYamls();
                        message = $"Fichier `{attachment.Filename}` envoyé.";
                        break;

                    case "generate":
                        var basePath = Path.Combine(Program.BasePath, "extern", "Archipelago");
                        playersFolderChannel = Path.Combine(basePath, "Players", channelId, "yaml");
                        var outputFolder = Path.Combine(basePath, "output", channelId, "yaml");
                        var venvPath = Path.Combine(basePath, "venv");
                        var pythonScript = Path.Combine(basePath, "Generate.py");
                        var requirementsFile = Path.Combine(basePath, "requirements.txt");

                        var pythonExecutable = isWindows
                            ? Path.Combine(venvPath, "Scripts", "python.exe")
                            : Path.Combine(venvPath, "bin", "python3");

                        if (Directory.Exists(outputFolder))
                        {
                            Directory.Delete(outputFolder, true);
                        }

                        Directory.CreateDirectory(playersFolderChannel);

                        var ymlFiles = Directory.GetFiles(playersFolderChannel, "*.yaml");

                        if (ymlFiles.Length == 0)
                        {
                            message = "❌ Aucun fichier YAML trouvé !";
                            break;
                        }

                        var forceYesCommand = isWindows
                        ? $"cmd /c echo yes | \"{pythonExecutable}\" \"{pythonScript}\" --player_files_path \"{playersFolderChannel}\" --outputpath \"{outputFolder}\""
                        : $"bash -c 'yes | \"{pythonExecutable}\" \"{pythonScript}\" --player_files_path \"{playersFolderChannel}\" --outputpath \"{outputFolder}\"'";

                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = isWindows ? "cmd.exe" : "/bin/bash",
                            Arguments = isWindows ? $"/c {forceYesCommand}" : $"-c \"{forceYesCommand}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                bool errorDetected = false;
                                StringBuilder errorMessage = new StringBuilder();

                                using (Process process = new Process { StartInfo = startInfo })
                                {
                                    process.OutputDataReceived += (sender, e) =>
                                    {
                                        if (!string.IsNullOrEmpty(e.Data))
                                        {
                                            Console.WriteLine($"🟢 **Log** : {e.Data}\n");
                                            if (e.Data.Contains("Opening file input dialog"))
                                            {
                                                errorMessage.AppendLine($"❌ **Erreur** : {e.Data}");
                                                errorDetected = true;
                                                if (!process.HasExited) process.Kill();
                                            }
                                        }
                                    };

                                    process.ErrorDataReceived += (sender, e) =>
                                    {
                                        if (!string.IsNullOrEmpty(e.Data))
                                        {
                                            errorMessage.AppendLine($"❌ **Erreur** : {e.Data}");
                                            if (e.Data.Contains("ValueError") || e.Data.Contains("Exception") || e.Data.Contains("FileNotFoundError"))
                                            {
                                                errorDetected = true;
                                                if (!process.HasExited) process.Kill();
                                            }
                                        }
                                    };

                                    process.Start();
                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    if (!process.WaitForExit(600000) && !errorDetected)
                                    {
                                        if (!process.HasExited) process.Kill();
                                        errorMessage.AppendLine("⏳ **Timeout** : Processus arrêté après 10min.");
                                        errorDetected = true;
                                    }
                                }

                                if (errorDetected)
                                {
                                    await command.FollowupAsync(errorMessage.ToString());
                                    return;
                                }

                                if (!Directory.Exists(outputFolder))
                                {
                                    await command.FollowupAsync($"❌ Le dossier de sortie {outputFolder} n'existe pas.");
                                    return;
                                }

                                var zipFile = Directory.GetFiles(outputFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
                                if (zipFile != null)
                                {
                                    var zipFileName = Path.GetFileName(zipFile);
                                    await command.FollowupWithFileAsync(zipFile, zipFileName);
                                    Directory.Delete(playersFolderChannel, true);
                                    Directory.Delete(outputFolder, true);
                                }
                                else
                                {
                                    await command.FollowupAsync("❌ Aucun fichier ZIP trouvé.");
                                }
                            }
                            catch (Exception ex)
                            {
                                await command.FollowupAsync($"🚨 **Erreur Critique** : {ex.Message}");
                            }
                        });

                        break;

                    case "generate-with-zip":
                        attachment = command.Data.Options.FirstOrDefault()?.Value as IAttachment;
                        if (attachment == null || !attachment.Filename.EndsWith(".zip"))
                        {
                            await command.FollowupAsync("❌ Vous devez envoyer un fichier ZIP contenant les fichiers YAML !");
                            break;
                        }

                        basePath = Path.Combine(Program.BasePath, "extern", "Archipelago");
                        playersFolderChannel = Path.Combine(basePath, "Players", channelId, "zip");
                        outputFolder = Path.Combine(basePath, "output", channelId, "zip");
                        venvPath = Path.Combine(basePath, "venv");
                        pythonScript = Path.Combine(basePath, "Generate.py");
                        requirementsFile = Path.Combine(basePath, "requirements.txt");
                        filePath = Path.Combine(playersFolderChannel, attachment.Filename);

                        pythonExecutable = isWindows
                            ? Path.Combine(venvPath, "Scripts", "python.exe")
                            : Path.Combine(venvPath, "bin", "python3");

                        if (Directory.Exists(playersFolderChannel))
                        {
                            Directory.Delete(playersFolderChannel, true);
                        }
                        if (Directory.Exists(outputFolder))
                        {
                            Directory.Delete(outputFolder, true);
                        }

                        Directory.CreateDirectory(playersFolderChannel);

                        using (var response = await Declare.HttpClient.GetAsync(attachment.Url))
                        using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fs);
                        }

                        ZipFile.ExtractToDirectory(filePath, playersFolderChannel);

                        var removeNoYaml = Directory.GetFiles(playersFolderChannel);
                        foreach (var file in removeNoYaml)
                        {
                            if (!file.EndsWith(".yaml"))
                            {
                                var fileName = Path.GetFileName(file);
                                await command.FollowupAsync($"ℹ️ **Info** : `{fileName}` n'est pas un fichier YAML. Il a été supprimé avant la génération\n");
                                File.Delete(file);
                            }
                        }

                        ymlFiles = Directory.GetFiles(playersFolderChannel, "*.yaml");
                        File.Delete(filePath);

                        if (!ymlFiles.Any())
                        {
                            await command.FollowupAsync("❌ Aucun fichier YAML trouvé dans l'archive !");
                            break;
                        }

                        var arguments = $"\"{pythonScript}\" --player_files_path \"{playersFolderChannel}\" --outputpath \"{outputFolder}\"";

                        ProcessStartInfo startInfoWithZip = new ProcessStartInfo
                        {
                            FileName = pythonExecutable,
                            Arguments = arguments,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                bool errorDetected = false;
                                var errorMessage = new StringBuilder();

                                using (Process process = new Process { StartInfo = startInfoWithZip })
                                {
                                    process.OutputDataReceived += (sender, e) =>
                                    {
                                        if (!string.IsNullOrEmpty(e.Data))
                                        {
                                            Console.WriteLine($"🟢 **Log** : {e.Data}\n");
                                            if (e.Data.Contains("Opening file input dialog"))
                                            {
                                                errorMessage.AppendLine($"❌ **Erreur** : {e.Data}");
                                                errorDetected = true;
                                                if (!process.HasExited) process.Kill();
                                            }
                                        }
                                    };

                                    process.ErrorDataReceived += (sender, e) =>
                                    {
                                        if (!string.IsNullOrEmpty(e.Data))
                                        {
                                            errorMessage.AppendLine($"❌ **Erreur** : {e.Data}");
                                            if (e.Data.Contains("ValueError") || e.Data.Contains("Exception") || e.Data.Contains("FileNotFoundError"))
                                            {
                                                errorDetected = true;
                                                if (!process.HasExited) process.Kill();
                                            }
                                        }
                                    };

                                    process.Start();
                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    if (!process.WaitForExit(600000) && !errorDetected)
                                    {
                                        if (!process.HasExited) process.Kill();
                                        errorMessage.AppendLine("⏳ **Timeout** : Processus arrêté après 10min.");
                                        errorDetected = true;
                                    }
                                }

                                if (errorDetected)
                                {
                                    await command.FollowupAsync(errorMessage.ToString());
                                    return;
                                }

                                if (!Directory.Exists(outputFolder))
                                {
                                    await command.FollowupAsync($"❌ Le dossier de sortie {outputFolder} n'existe pas.");
                                    return;
                                }

                                var zipFile = Directory.GetFiles(outputFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
                                if (zipFile != null)
                                {
                                    var zipFileName = Path.GetFileName(zipFile);
                                    await command.FollowupWithFileAsync(zipFile, zipFileName);
                                    Directory.Delete(playersFolderChannel, true);
                                    Directory.Delete(outputFolder, true);
                                }
                                else
                                {
                                    await command.FollowupAsync("❌ Aucun fichier ZIP trouvé.");
                                }
                            }
                            catch (Exception ex)
                            {
                                await command.FollowupAsync($"🚨 **Erreur Critique** : {ex.Message}");
                            }
                        });

                        break;


                    default:
                        Console.WriteLine($"La commande a été exécutée dans le canal : {channel.Name}");
                        message = "Cette commande doit être exécutée dans un channel.";
                        break;
                }
            }
            await command.FollowupAsync(message);
        }
    }

    public static async Task<string> DeleteChannelAndUrl(string? channelId, string guildId)
    {
        string message = string.Empty;

        if (string.IsNullOrEmpty(channelId))
        {
            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guildId);
        }
        else
        {
            await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId);
        }

        message = "URL Supprimée.";
        await RegisterCommandsAsync();
        return message;
    }
}
