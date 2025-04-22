using Discord;
using Discord.Commands;
using Discord.WebSocket;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
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
                .WithName("alias")
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
                }),

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
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("recap-and-clean")
            .WithDescription("Recap and clean List of items for a specific game")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
                .WithDescription("Choose an alias")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithAutocomplete(true)),

        new SlashCommandBuilder()
            .WithName("clean")
            .WithDescription("Recap and clean List of items for a specific game")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("alias")
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

            if(string.IsNullOrWhiteSpace(channelId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            if (guildId == null || !Declare.AliasChoices.Guild.ContainsKey(guildId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            if (!Declare.AliasChoices.Guild[guildId].Channel.ContainsKey(channelId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var aliases = Declare.AliasChoices.Guild[guildId].Channel[channelId].aliasChoices.Keys;

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

        if (interaction.Data.Current.Name == "file")
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            string? guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();

            if(string.IsNullOrWhiteSpace(channelId))
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

            List<ApWorldJsonList> sections;
            try
            {
                sections = Declare.ApworldsInfo;
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

            var allTitles = sections.Select(s => s.Title).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            var filteredTitles = allTitles
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


        /*if (interaction.Data.Current.Name == "roles")
        {
            string guildId = interaction.GuildId?.ToString();
            if (guildId == null)
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var getguild = Declare.Client.GetGuild(ulong.Parse(guildId));

            if (getguild != null)
            {
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

                var filteredRoles = getguild.Roles
                    .Where(r => r.Name.ToLower().Contains(userInput))
                    .OrderBy(r => r.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new AutocompleteResult(r.Name, r.Name))
                    .ToArray();

                if (filteredRoles.Length == 0 && pageNumber > 1)
                {
                    pageNumber = (getguild.Roles.Count() / pageSize) + 1;
                    filteredRoles = getguild.Roles
                        .OrderBy(r => r.Name)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .Select(r => new AutocompleteResult(r.Name, r.Name))
                        .ToArray();
                }

                await interaction.RespondAsync(filteredRoles);
            }
            else
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
            }
        }*/
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
        var receiverId = "";
        string message = "";
        const int maxMessageLength = 1999;
        var alias = command.Data.Options.FirstOrDefault()?.Value as string;
        var channelId = command.ChannelId.ToString();
        var guildId = command.GuildId.ToString();

        if(string.IsNullOrWhiteSpace(guildId))
        {
            await command.RespondAsync("Cette commande ne peut pas être exécutée en dehors d'un serveur.");
            return;
        }

        if(string.IsNullOrWhiteSpace(channelId))
        {
            await command.RespondAsync("Cette commande ne peut pas être exécutée en dehors d'un serveur.");
            return;
        }

        if (command.Channel is IThreadChannel threadChannel)
        {
            await command.DeferAsync(ephemeral: true);

            switch (command.CommandName)
            {
                case "get-aliases":

                    if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) ||
                        !guild.Channel.TryGetValue(channelId, out var channelAliases))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    else if (channelAliases.receiverAlias.Count == 0)
                    {
                        message = "Aucun Alias est enregistré.";
                    }
                    else
                    {
                        var sb = new StringBuilder("Voici le tableau des utilisateurs :\n");
                        foreach (var kvp in channelAliases.receiverAlias)
                        {
                            foreach (var value in kvp.Value)
                            {
                                var user = await Declare.Client.GetUserAsync(ulong.Parse(value.Key));
                                sb.AppendLine($"| {user.Username} | {kvp.Key} |");
                            }
                        }
                        message = sb.ToString();
                    }
                    break;

                case "delete-alias":
                    bool HasValidChannelData(string guildId, string channelId)
                    {
                        return Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) && guild.Channel.ContainsKey(channelId);
                    }

                    if (!HasValidChannelData(guildId, channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                    }
                    else
                    {
                        channelAliases = Declare.ReceiverAliases.Guild[guildId].Channel[channelId];

                        if (channelAliases.receiverAlias.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else if (alias != null)
                        {
                            if (channelAliases.receiverAlias.TryGetValue(alias, out var values))
                            {
                                foreach (var value in values.Keys)
                                {
                                    if (value == command.User.Id.ToString() || (guildUser != null && guildUser.GuildPermissions.Administrator))
                                    {
                                        channelAliases.receiverAlias.Remove(alias);
                                        DataManager.SaveReceiverAliases();

                                        message = value == command.User.Id.ToString()
                                            ? $"Alias '{alias}' supprimé."
                                            : $"ADMIN : Alias '{alias}' supprimé.";

                                        if (Declare.RecapList.Guild.TryGetValue(guildId, out var recapGuild) && recapGuild.Channel.TryGetValue(channelId, out var recapChannel))
                                        {
                                            if (recapChannel.Aliases.TryGetValue(value, out var subElements))
                                            {
                                                subElements.RemoveAll(e => e.Alias == alias);

                                                if (subElements.Count == 0)
                                                {
                                                    recapChannel.Aliases.Remove(value);
                                                }
                                                DataManager.SaveRecapList();
                                            }
                                        }
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
                    receiverId = command.User.Id.ToString();
                    var skipUselessMention = command.Data.Options.ElementAtOrDefault(1)?.Value as bool? ?? false;

                    if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var channelReceiverAliases))
                    {
                        message = $"Aucune Alias trouvé.";
                        channelReceiverAliases = new ChannelReceiverAliases();
                        Declare.ReceiverAliases.Guild[guildId] = channelReceiverAliases;
                    }

                    if (!channelReceiverAliases.Channel.TryGetValue(channelId, out var receiverAlias))
                    {
                        message = $"Aucune Alias trouvé.";
                        receiverAlias = new ReceiverAlias();
                        channelReceiverAliases.Channel[channelId] = receiverAlias;
                    }

                    if(string.IsNullOrWhiteSpace(alias))
                    {
                        message = "L'alias ne peut pas être vide.";
                        break;
                    }

                    if (!receiverAlias.receiverAlias.TryGetValue(alias, out var aliasList))
                    {
                        message = $"Aucune Alias trouvé.";
                        aliasList = new Dictionary<string, bool>();
                        receiverAlias.receiverAlias[alias] = aliasList;
                    }

                    if (aliasList.Keys.Contains(receiverId))
                    {
                        message = $"L'alias '{alias}' est déjà enregistré pour <@{receiverId}>.";
                        break;
                    }

                    aliasList.Add(receiverId, skipUselessMention);

                    if (!Declare.RecapList.Guild.TryGetValue(guildId, out var channelRecapList))
                    {
                        message = $"Aucune Alias trouvé.";
                        channelRecapList = new ChannelRecapList();
                        Declare.RecapList.Guild[guildId] = channelRecapList;
                    }

                    if (!channelRecapList.Channel.TryGetValue(channelId, out var userRecapList))
                    {
                        message = $"Aucune Alias trouvé.";
                        userRecapList = new UserRecapList();
                        channelRecapList.Channel[channelId] = userRecapList;
                    }

                    if (!userRecapList.Aliases.TryGetValue(receiverId, out var recapUserList))
                    {
                        recapUserList = new List<RecapList>();
                        userRecapList.Aliases[receiverId] = recapUserList;
                    }

                    var recapUser = recapUserList.FirstOrDefault(e => e.Alias == alias);
                    if (recapUser == null)
                    {
                        recapUser = new RecapList { Alias = alias, Items = new List<string>() };
                        recapUserList.Add(recapUser);
                    }

                    var items = Declare.DisplayedItems.Guild[guildId].Channel[channelId]
                        .Where(item => item.Receiver == alias)
                        .Select(item => item.Item)
                        .ToList();
                    recapUser.Items.AddRange(items.Any() ? items : new List<string> { "Aucun élément" });

                    message = $"Alias ajouté : {alias} est maintenant associé à <@{receiverId}> et son récap généré.";

                    DataManager.SaveRecapList();
                    DataManager.SaveReceiverAliases();
                    break;

                /*case "add-roles":
                    var getRole = command.Data.Options.ElementAtOrDefault(1)?.Value as string;
                    receiverId = Declare.Client.GetGuild(ulong.Parse(guildId)).Roles.FirstOrDefault(x => x.Name == getRole).Id.ToString();

                    Declare.ReceiverAliases.Guild[guildId].Channel[channelId].receiverAlias[alias] = receiverId;

                    if (!Declare.RecapList.Guild.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.RecapList.Guild[guildId] = new ChannelRecapList();
                    }

                    if (!Declare.RecapList.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.RecapList.Guild[guildId].Channel[channelId] = new UserRecapList();
                    }

                    if (!Declare.RecapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out recapUserList))
                    {
                        recapUserList = new List<RecapList>();
                        Declare.RecapList.Guild[guildId].Channel[channelId].Aliases[receiverId] = recapUserList;
                    }

                    recapUser = recapUserList.FirstOrDefault(e => e.Alias == alias);
                    if (recapUser == null)
                    {
                        recapUser = new RecapList { Alias = alias, Items = new List<string>() };
                        recapUserList.Add(recapUser);
                    }

                    items = Declare.DisplayedItems.Guild[guildId].Channel[channelId].Where(item => item.Receiver == alias).Select(item => item.Item).ToList();
                    recapUser.Items.AddRange(items.Any() ? items : new List<string> { "Aucun élément" });

                    var getMembers = Declare.Client.GetGuild(ulong.Parse(guildId)).Roles.FirstOrDefault(x => x.Name == getRole).Members;


                    var guild = Declare.Client.GetGuild(ulong.Parse(guildId));
                    var role = guild.Roles.FirstOrDefault(x => x.Name == getRole);

                    if (role != null)
                    {
                        var members = guild.Users.Where(user => user.Roles.Contains(role));
                        var mentions = string.Join(" ", members.Select(member => $"<@{member.Id}>"));

                        message = $"Alias ajouté : {alias} est maintenant associé à {mentions} et son récap généré.";
                        DataManager.SaveRecapList();
                        DataManager.SaveReceiverAliases();
                    }
                    else
                    {
                        message = $"Le rôle {getRole} n'existe pas.";
                    }
                   
                    break;*/

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
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverData(string guildId, string channelId, out string errorMessage)
                    {
                        if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                            return false;
                        }

                        if (!channel.receiverAlias.Any(x => x.Value.Keys.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = string.Empty;
                        return true;
                    }

                    bool TryGetRecapData(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.RecapList.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (channel.Aliases.TryGetValue(receiverId, out var subElements) && subElements.Any())
                        {
                            var sb = new StringBuilder($"Détails pour <@{receiverId}> :\n");
                            foreach (var subElement in subElements)
                            {
                                string groupedMessage = subElement.Items != null && subElement.Items.Any()
                                    ? string.Join(", ", subElement.Items
                                        .GroupBy(value => value)
                                        .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                    : "Aucun élément";

                                sb.AppendLine($"**{subElement.Alias}** : {groupedMessage}");
                            }
                            recapMessage = sb.ToString();
                            return true;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias.";
                        return false;
                    }

                    if (TryGetReceiverData(guildId, channelId, out string errorMessage))
                    {
                        if (TryGetRecapData(guildId, channelId, receiverId, out string recapMessage))
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
                        message = errorMessage;
                    }
                    break;

                case "recap":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasData(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!channel.receiverAlias.Any(x => x.Value.Keys.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = string.Empty;
                        return true;
                    }

                    bool TryGetRecapDataRecap(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.RecapList.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (channel.Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var getUser = subElements.Any(x => x.Alias == alias);

                            if (getUser)
                            {
                                var groupedMessages = subElements
                                    .Where(x => x.Alias == alias)
                                    .Select(subElement => subElement.Items != null && subElement.Items.Any()
                                        ? $"**{subElement.Alias}** : {string.Join(", ", subElement.Items.GroupBy(value => value).Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))} \n"
                                        : $"**{subElement.Alias}** : Aucun élément \n");

                                recapMessage = $"Détails pour <@{receiverId}> :\n{string.Join("", groupedMessages)}";
                                return true;
                            }

                            recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    if (TryGetReceiverAliasData(guildId, channelId, receiverId, out string errorMessageRecap))
                    {
                        if (TryGetRecapDataRecap(guildId, channelId, receiverId, out string recapMessage))
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
                        message = errorMessageRecap;
                    }
                    break;

                case "recap-and-clean":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasDataRecapAndClean(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!channel.receiverAlias.Any(x => x.Value.Keys.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = string.Empty;
                        return true;
                    }

                    bool TryGetRecapDataRecapAndClean(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.RecapList.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (channel.Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var userSubElements = subElements.Where(x => x.Alias == alias).ToList();
                            if (userSubElements.Any())
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                recapMessage += string.Join("\n", userSubElements.Select(subElement =>
                                {
                                    string groupedMessage = subElement.Items != null && subElement.Items.Any()
                                        ? string.Join(", ", subElement.Items
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";
                                    return $"**{subElement.Alias}** : {groupedMessage}";
                                }));

                                foreach (var subElement in userSubElements)
                                {
                                    subElement.Items.Clear();
                                    subElement.Items.Add("Aucun élément");
                                }

                                DataManager.SaveRecapList();
                                return true;
                            }

                            recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    if (TryGetReceiverAliasDataRecapAndClean(guildId, channelId, receiverId, out string errorMessageRecapAndClean))
                    {
                        if (TryGetRecapDataRecapAndClean(guildId, channelId, receiverId, out string recapMessage))
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
                        message = errorMessageRecapAndClean;
                    }
                    break;

                case "clean":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasDataClean(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!channel.receiverAlias.Any(x => x.Value.Keys.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = string.Empty;
                        return true;
                    }

                    bool TryClearAliasDataClean(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.RecapList.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            resultMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (channel.Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var aliasElement = subElements.FirstOrDefault(x => x.Alias == alias);

                            if (aliasElement != null)
                            {
                                aliasElement.Items.Clear();
                                aliasElement.Items.Add("Aucun élément");
                                DataManager.SaveRecapList();
                                resultMessage = string.Empty;
                                return true;
                            }

                            resultMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    if (TryGetReceiverAliasDataClean(guildId, channelId, receiverId, out message))
                    {
                        if (!TryClearAliasDataClean(guildId, channelId, receiverId, out string clearMessage))
                        {
                            message = clearMessage;
                        }
                        else
                        {
                            message = $"Clean pour Alias {alias} effectué";
                        }
                    }
                    break;

                case "clean-all":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasDataCleanAll(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!channel.receiverAlias.Any(x => x.Value.Keys.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = string.Empty;
                        return true;
                    }

                    bool TryClearSubElementsCleanAll(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.RecapList.Guild.TryGetValue(guildId, out var guild) || !guild.Channel.TryGetValue(channelId, out var channel))
                        {
                            resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                            return false;
                        }

                        if (channel.Aliases.TryGetValue(receiverId, out var subElements) && subElements.Any())
                        {
                            foreach (var subElement in subElements)
                            {
                                subElement.Items.Clear();
                                subElement.Items.Add("Aucun élément");
                            }
                            DataManager.SaveRecapList();
                            resultMessage = string.Empty;
                            return true;
                        }

                        resultMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                        return false;
                    }

                    if (TryGetReceiverAliasDataCleanAll(guildId, channelId, receiverId, out message))
                    {
                        if (!TryClearSubElementsCleanAll(guildId, channelId, receiverId, out string cleanMessage))
                        {
                            message = cleanMessage;
                        }
                        else
                        {
                            message = $"Clean All pour <@{receiverId}> effectué";
                        }
                    }
                    break;

                case "list-items":
                    receiverId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;
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

                    if (Declare.DisplayedItems.Guild.TryGetValue(guildId, out var guildListItem) && guildListItem.Channel.TryGetValue(channelId, out var channelListItem))
                    {
                        var filteredItems = channelListItem
                            .Where(item => item.Receiver == receiverId)
                            .GroupBy(item => item.Item)
                            .OrderBy(group => group.Key)
                            .ToList();

                        if (filteredItems.Any())
                        {
                            message = $"Items pour {receiverId} :\n{BuildItemMessage(filteredItems, listByLine)}";
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
                    if (string.IsNullOrEmpty(alias))
                    {
                        message = "Alias non spécifié.";
                        break;
                    }

                    string BuildHintMessage(IEnumerable<HintStatus> hints, string alias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item from {alias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    if (Declare.HintStatuses.Guild.TryGetValue(guildId, out var guildFinder) && guildFinder.Channel.TryGetValue(channelId, out var channelFinder))
                    {
                        var hintByFinder = channelFinder.Where(h => h.Finder == alias).ToList();

                        message = hintByFinder.Count > 0
                            ? BuildHintMessage(hintByFinder, alias)
                            : "No hint found for this finder";
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou Aucun hint.";
                    }

                    break;

                case "hint-for-receiver":
                    if (string.IsNullOrEmpty(alias))
                    {
                        message = "Alias non spécifié.";
                        break;
                    }

                    string BuildHintMessageReceiver(List<HintStatus> hints, string alias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item for {alias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    if (Declare.HintStatuses.Guild.TryGetValue(guildId, out var guildHints) &&
                        guildHints.Channel.TryGetValue(channelId, out var channelHints))
                    {
                        var hintByReceiver = new List<HintStatus>();

                        foreach (var hint in channelHints)
                        {
                            if (hint.Receiver == alias)
                            {
                                hintByReceiver.Add(hint);
                            }
                        }

                        message = hintByReceiver.Count > 0
                            ? BuildHintMessageReceiver(hintByReceiver, alias)
                            : "No hint found for this receiver";
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou Aucun hint.";
                    }
                    break;

                case "status-games-list":
                    message = "Status for all games :\n";

                    if (Declare.GameStatus.Guild.TryGetValue(guildId, out var guildGames) &&
                        guildGames.Channel.TryGetValue(channelId, out var channelGames))
                    {
                        foreach (var game in channelGames)
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
                    break;

                case "info":
                    if (Declare.ChannelAndUrl.Guild.TryGetValue(guildId, out var guildUrl) &&
                        guildUrl.Channel.TryGetValue(channelId, out var channelUrl) && !string.IsNullOrEmpty(channelUrl.Room))
                    {
                        using HttpClient client = new();
                        HtmlDocument doc = new();
                        string pageContent = await client.GetStringAsync(channelUrl.Room);

                        doc.LoadHtml(pageContent);

                        string checkPort = doc.DocumentNode.InnerText;

                        Match match = Regex.Match(checkPort, @"/connect archipelago\.gg:(\d+)");
                        message += "Info:\n";
                        message += $"Room : {Declare.ChannelAndUrl.Guild[guildId].Channel[channelId].Room}\n";
                        message += $"Tracker : {Declare.ChannelAndUrl.Guild[guildId].Channel[channelId].Tracker}\n";
                        message += $"SphereTracker : {Declare.ChannelAndUrl.Guild[guildId].Channel[channelId].SphereTracker}\n";
                        if (match.Success)
                        {
                            var port = match.Groups[1].Value;

                            message += $"Port : {port}";
                            if (channelUrl.Port != port)
                            {
                                channelUrl.Port = port;
                                DataManager.SaveChannelAndUrl();
                            }
                        }


                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    break;

                case "get-patch":
                    receiverId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;

                    if(string.IsNullOrWhiteSpace(receiverId))
                    {
                        message = "Receiver ID non spécifié.";
                        break;
                    }

                    if (Declare.ChannelAndUrl.Guild.TryGetValue(guildId, out var guildPatches) &&
                        guildPatches.Channel.TryGetValue(channelId, out var channelPatches) && channelPatches.Aliases.TryGetValue(receiverId, out var channelAlias))
                    {

                        message += $"Patch Pour {receiverId}, {channelAlias.GameName} : {channelAlias.Patch}\n\n";
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
                        bool IsValidUrl(string url) => url.Contains(baseUrl + "/room");

                        HtmlDocument doc = new();

                        bool CanAddUrl(string guildId, string channelId, out string existingUrlMessage)
                        {
                            existingUrlMessage = string.Empty;

                            if (Declare.ChannelAndUrl.Guild.TryGetValue(guildId, out var guild) && guild.Channel.ContainsKey(channelId))
                            {
                                existingUrlMessage = $"URL déjà définie sur {guild.Channel[channelId]}. Supprimez l'url avant d'ajouter une nouvelle url.";
                                return false;
                            }

                            return true;
                        }

                        async Task<bool> IsAllUrlIsValidAsync(string newUrl)
                        {
                            using HttpClient client = new();
                            string pageContent = await client.GetStringAsync(newUrl);

                            doc.LoadHtml(pageContent);

                            string checkPort = doc.DocumentNode.InnerText;

                            Match match = Regex.Match(checkPort, @"/connect archipelago\.gg:(\d+)");

                            if (match.Success)
                            {
                                port = match.Groups[1].Value;
                                Console.WriteLine($"Port trouvé : {port}");
                            }

                            trackerUrl = doc.DocumentNode.SelectSingleNode("//a[contains(text(),'Multiworld Tracker')]")?.GetAttributeValue("href", "Non trouvé");
                            sphereTrackerUrl = doc.DocumentNode.SelectSingleNode("//a[contains(text(),'Sphere Tracker')]")?.GetAttributeValue("href", "Non trouvé");

                            if (trackerUrl == null || sphereTrackerUrl == null || string.IsNullOrEmpty(port))
                            {
                                return false;
                            }

                            if (!string.IsNullOrEmpty(trackerUrl) && trackerUrl != "Non trouvé" && !trackerUrl.StartsWith("http"))
                            {
                                trackerUrl = baseUrl + trackerUrl;
                            }

                            if (!string.IsNullOrEmpty(sphereTrackerUrl) && sphereTrackerUrl != "Non trouvé" && !sphereTrackerUrl.StartsWith("http"))
                            {
                                sphereTrackerUrl = baseUrl + sphereTrackerUrl;
                            }

                            return true;
                        }

                        if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                        {
                            message = "Seuls les administrateurs sont autorisés à ajouter une URL.";
                        }
                        else
                        {
                            if (CanAddUrl(guildId, channelId, out string existingUrlMessage))
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
                                else if (!await IsAllUrlIsValidAsync(newUrl))
                                {
                                    message = $"Sphere_Tracker, Tracker ou le port ne sont pas trouvé. Ajout annulé.";
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

                                    List<IGuildUser> allMembers = new List<IGuildUser>();

                                    await foreach (var memberBatch in channel.GetUsersAsync())
                                    {
                                        allMembers.AddRange(memberBatch);
                                    }

                                    foreach (var member in allMembers)
                                    {
                                        await thread.AddUserAsync(member);
                                    }

                                    if (!Declare.ChannelAndUrl.Guild.ContainsKey(guildId))
                                    {
                                        Declare.ChannelAndUrl.Guild[guildId] = new ChannelAndUrl();
                                    }

                                    if (!Declare.ChannelAndUrl.Guild[guildId].Channel.ContainsKey(channelId))
                                    {
                                        Declare.ChannelAndUrl.Guild[guildId].Channel[channelId] = new UrlAndChannel();
                                    }

                                    var channelData = Declare.ChannelAndUrl.Guild[guildId].Channel[channelId];
                                    channelData.Room = newUrl;
                                    if (!string.IsNullOrEmpty(trackerUrl))
                                    {
                                        channelData.Tracker = trackerUrl;
                                    }
                                    else
                                    {
                                        channelData.Tracker = "Non trouvé";
                                    }
                                    if(!string.IsNullOrEmpty(sphereTrackerUrl))
                                    {
                                        channelData.SphereTracker = sphereTrackerUrl;
                                    }
                                    else
                                    {
                                        channelData.SphereTracker = "Non trouvé";
                                    }
                                    channelData.Port = port;

                                    var rows = doc.DocumentNode.SelectNodes("//table//tr");

                                    if (rows != null)
                                    {
                                        foreach (var row in rows)
                                        {
                                            var columns = row.SelectNodes("td");
                                            if (columns != null && columns.Count >= 4)
                                            {
                                                string gameAlias = columns[1].InnerText.Trim();
                                                string gameName = columns[2].InnerText.Trim();
                                                var downloadLinkNode = columns[3].SelectSingleNode(".//a");
                                                string downloadLink = downloadLinkNode != null ? downloadLinkNode.GetAttributeValue("href", "Aucun fichier") : "Aucun fichier";

                                                Console.WriteLine($"Nom: {gameAlias} | Téléchargement: {downloadLink}");

                                                if (downloadLinkNode != null)
                                                {
                                                    if (!channelData.Aliases.ContainsKey(gameAlias))
                                                    {
                                                        channelData.Aliases[gameAlias] = new UrlAndChannelPatch();
                                                    }
                                                    channelData.Aliases[gameAlias].GameName = gameName;
                                                    channelData.Aliases[gameAlias].Patch = baseUrl + downloadLink;
                                                }
                                            }
                                        }
                                    }
                                    DataManager.SaveChannelAndUrl();
                                    message = $"URL définie sur {newUrl}. Messages configurés pour ce canal. Attendez que le programme récupère tous les aliases.";

                                    await TrackingDataManager.setAliasAndGameStatusAsync(guildId, channelId, trackerUrl);
                                    await TrackingDataManager.checkGameStatus(guildId, channelId, trackerUrl);
                                    await TrackingDataManager.GetTableDataAsync(guildId, channelId, sphereTrackerUrl);
                                }
                            }
                            else
                            {
                                message = existingUrlMessage;
                            }
                        }

                        break;
                    case "list-yamls":
                        string playersFolderChannel = Path.Combine(Program.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
                        if (Directory.Exists(playersFolderChannel))
                        {
                            var listYamls = Directory.EnumerateFiles(playersFolderChannel, "*.yaml");

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

                        using (HttpClient httpClient = new HttpClient())
                        {
                            var response = await httpClient.GetAsync(attachment.Url);
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
                        }
                        break;

                    case "download-template":
                        var yamlFile = command.Data.Options.FirstOrDefault()?.Value as string;

                        if(string.IsNullOrEmpty(yamlFile))
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
                            var listAppworld = Directory.EnumerateFiles(apworldPath, "*.apworld");

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

                        List<ApWorldJsonList> sections;
                        try
                        {
                            sections = Declare.ApworldsInfo;
                        }
                        catch
                        {
                           message = "Erreur lors du chargement du JSON.";
                           break;
                        }

                        var selectedSection = sections.FirstOrDefault(s => s.Title == infoSelected);

                        if (selectedSection == null)
                        {
                            message = "Titre non trouvé.";
                            break;
                        }

                        message = $"**{selectedSection.Title}**\n\n";

                        foreach (var item in selectedSection.Items)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Link))
                            {
                                message += $"• {item.Text} — [Link]({item.Link})\n";
                            }
                            else
                            {
                                message += $"• {item.Text}\n";
                            }
                        }

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

                        using (HttpClient httpClient = new HttpClient())
                        using (var response = await httpClient.GetAsync(attachment.Url))
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

                        using (HttpClient httpClient = new HttpClient())
                        using (var response = await httpClient.GetAsync(attachment.Url))
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
        string message;
        if (channelId == null)
        {
            Declare.ChannelAndUrl.Guild.Remove(guildId);
        }
        else if (Declare.ChannelAndUrl.Guild.TryGetValue(guildId, out var urlSphereTracker) && urlSphereTracker.Channel.Remove(channelId))
        {
            if (urlSphereTracker.Channel.Count == 0)
            {
                Declare.ChannelAndUrl.Guild.Remove(guildId);
            }
        }
        DataManager.SaveChannelAndUrl();

        if (channelId == null)
        {
            Declare.RecapList.Guild.Remove(guildId);
        }
        else if (Declare.RecapList.Guild.TryGetValue(guildId, out var recapList) && recapList.Channel.Remove(channelId))
        {
            if (recapList.Channel.Count == 0)
            {
                Declare.RecapList.Guild.Remove(guildId);
            }
        }
        DataManager.SaveRecapList();

        if (channelId == null)
        {
            Declare.ReceiverAliases.Guild.Remove(guildId);
        }
        else if (Declare.ReceiverAliases.Guild.TryGetValue(guildId, out var receiverAliases) && receiverAliases.Channel.Remove(channelId))
        {
            if (receiverAliases.Channel.Count == 0)
            {
                Declare.ReceiverAliases.Guild.Remove(guildId);
            }
        }
        DataManager.SaveReceiverAliases();

        if (channelId == null)
        {
            Declare.DisplayedItems.Guild.Remove(guildId);
        }
        else if (Declare.DisplayedItems.Guild.TryGetValue(guildId, out var displayedItems) && displayedItems.Channel.Remove(channelId))
        {
            if (displayedItems.Channel.Count == 0)
            {
                Declare.DisplayedItems.Guild.Remove(guildId);
            }
        }
        DataManager.SaveDisplayedItems();

        if (channelId == null)
        {
            Declare.AliasChoices.Guild.Remove(guildId);
        }
        else if (Declare.AliasChoices.Guild.TryGetValue(guildId, out var aliasChoices) && aliasChoices.Channel.Remove(channelId))
        {
            if (aliasChoices.Channel.Count == 0)
            {
                Declare.AliasChoices.Guild.Remove(guildId);
            }
        }
        DataManager.SaveAliasChoices();

        if (channelId == null)
        {
            Declare.GameStatus.Guild.Remove(guildId);
        }
        else if (Declare.GameStatus.Guild.TryGetValue(guildId, out var gameStatus) && gameStatus.Channel.Remove(channelId))
        {
            if (gameStatus.Channel.Count == 0)
            {
                Declare.GameStatus.Guild.Remove(guildId);
            }
        }
        DataManager.SaveGameStatus();

        if (channelId == null)
        {
            Declare.HintStatuses.Guild.Remove(guildId);
        }
        else if (Declare.HintStatuses.Guild.TryGetValue(guildId, out var hintStatuses) && hintStatuses.Channel.Remove(channelId))
        {
            if (hintStatuses.Channel.Count == 0)
            {
                Declare.HintStatuses.Guild.Remove(guildId);
            }
        }
        DataManager.SaveHintStatus();

        message = "URL Supprimée.";
        await RegisterCommandsAsync();
        return message;
    }
}
