using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Text;
using System.Threading.Channels;

public static class BotCommands
{
    public static async Task InstallCommandsAsync()
    {
        Declare.services = new ServiceCollection()
            .AddSingleton(Declare.client)
            .BuildServiceProvider();
    }

    public static async Task MessageReceivedAsync(SocketMessage arg)
    {
        var message = arg as SocketUserMessage;
        var argPos = 0;

        if (message?.Author.IsBot ?? true) return;

        if (message.HasStringPrefix("/", ref argPos))
        {
            var context = new SocketCommandContext(Declare.client, message);

            var result = await Declare.commandService.ExecuteAsync(context, message.Content.Substring(argPos), Declare.services);

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
            var channelId = Declare.client.GetChannel(getChannel) as IMessageChannel;

            if (channelId == null)
            {
                Console.WriteLine($"Le canal avec l'ID {channel} est introuvable ou inaccessible.");
                Console.WriteLine("Voici les canaux accessibles par le bot :");

                foreach (var guild in Declare.client.Guilds)
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
        foreach (var guild in Declare.client.Guilds)
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
                    .WithAutocomplete(true)),

            /*new SlashCommandBuilder()
                .WithName("add-roles")
                .WithDescription("Add all users as Alias on a specific role")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("alias")
                    .WithDescription("Choose an alias")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("roles")
                    .WithDescription("Choose a roles")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .WithAutocomplete(true)),*/

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
        };

            var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();
            await Declare.client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
        }

        Declare.client.SlashCommandExecuted += HandleSlashCommandAsync;
        Declare.client.AutocompleteExecuted += HandleAutocompleteAsync;
    }

    private static async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {

        if (interaction.Data.Current.Name == "alias")
        {
            string guildId = interaction.GuildId?.ToString();
            var channelId = interaction.ChannelId.ToString();
            if (guildId == null || !Declare.aliasChoices.Guild.ContainsKey(guildId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            if (!Declare.aliasChoices.Guild[guildId].Channel.ContainsKey(channelId))
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var aliases = Declare.aliasChoices.Guild[guildId].Channel[channelId].aliasChoices
                .GroupBy(pair => pair.Value)
                .ToDictionary(group => group.Key, group => group.First().Value);

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
                .Where(a => a.Key.ToLower().Contains(userInput))
                .OrderBy(a => a.Key)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AutocompleteResult(a.Key, a.Value))
                .ToArray();

            if (filteredAliases.Length == 0 && pageNumber > 1)
            {
                pageNumber = (aliases.Count / pageSize) + 1;
                filteredAliases = aliases
                    .OrderBy(a => a.Key)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AutocompleteResult(a.Key, a.Value))
                    .ToArray();
            }

            await interaction.RespondAsync(filteredAliases);
        }

        /*if (interaction.Data.Current.Name == "roles")
        {
            string guildId = interaction.GuildId?.ToString();
            if (guildId == null)
            {
                await interaction.RespondAsync(new AutocompleteResult[0]);
                return;
            }

            var getguild = Declare.client.GetGuild(ulong.Parse(guildId));

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
        await command.DeferAsync();

        var guildUser = command.User as IGuildUser;
        var receiverId = "";
        string message = "";
        const int maxMessageLength = 1999;
        var alias = command.Data.Options.FirstOrDefault()?.Value as string;
        var channelId = command.ChannelId.ToString();
        var guildId = command.GuildId.ToString();

        if (command.Channel is IThreadChannel threadChannel)
        {
            switch (command.CommandName)
            {
                case "get-aliases":

                    if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    else
                    {
                        var channelAliases = Declare.receiverAliases.Guild[guildId].Channel[channelId];

                        if (channelAliases.receiverAlias.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else
                        {
                            message = "Voici le tableau des utilisateurs :\n";
                            foreach (var kvp in channelAliases.receiverAlias)
                            {
                                foreach(var value in kvp.Value)
                                {
                                    var user = await Declare.client.GetUserAsync(ulong.Parse(value));
                                    message += $"| {user.Username} | {kvp.Key} |\n";
                                }
                            }
                        }
                    }
                    break;

                case "delete-alias":
                    bool HasValidChannelData(string guildId, string channelId)
                    {
                        return Declare.receiverAliases.Guild.ContainsKey(guildId) && Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId);
                    }

                    if (!HasValidChannelData(guildId, channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                    }
                    else
                    {
                        var channelAliases = Declare.receiverAliases.Guild[guildId].Channel[channelId];

                        if (channelAliases.receiverAlias.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else if (alias != null)
                        {
                            if (channelAliases.receiverAlias.TryGetValue(alias, out var values))
                            {
                                foreach(var value in values)
                                {
                                    if (value == command.User.Id.ToString() || (guildUser != null && guildUser.GuildPermissions.Administrator))
                                    {
                                        Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Remove(alias);
                                        DataManager.SaveReceiverAliases();

                                        message = value == command.User.Id.ToString()
                                            ? $"Alias '{alias}' supprimé."
                                            : $"ADMIN : Alias '{alias}' supprimé.";

                                        // Check and remove alias from recapList
                                        if (Declare.recapList.Guild.ContainsKey(guildId) && Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                                        {
                                            if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.ContainsKey(value))
                                            {
                                                var subElements = Declare.recapList.Guild[guildId].Channel[channelId].Aliases[value];
                                                subElements.RemoveAll(e => e.Alias == alias);

                                                if (subElements.Count == 0)
                                                {
                                                    Declare.recapList.Guild[guildId].Channel[channelId].Aliases.Remove(value);
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

                    if (!Declare.receiverAliases.Guild.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.receiverAliases.Guild[guildId] = new ChannelReceiverAliases();
                    }

                    if (!Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.receiverAliases.Guild[guildId].Channel[channelId] = new ReceiverAlias();
                    }

                    if(!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.ContainsKey(alias))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias[alias] = new List<string>();
                    }

                    if (Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias[alias].Contains(receiverId))
                    {
                        message = $"L'alias '{alias}' est déjà enregistré pour <@{receiverId}>.";
                        break;
                    }

                    Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias[alias].Add(receiverId);

                    if (!Declare.recapList.Guild.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList.Guild[guildId] = new ChannelRecapList();
                    }

                    if (!Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList.Guild[guildId].Channel[channelId] = new UserRecapList();
                    }

                    if (!Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var recapUserList))
                    {
                        recapUserList = new List<RecapList>();
                        Declare.recapList.Guild[guildId].Channel[channelId].Aliases[receiverId] = recapUserList;
                    }

                    var recapUser = recapUserList.FirstOrDefault(e => e.Alias == alias);
                    if (recapUser == null)
                    {
                        recapUser = new RecapList { Alias = alias, Items = new List<string>() };
                        recapUserList.Add(recapUser);
                    }

                    var items = Declare.displayedItems.Guild[guildId].Channel[channelId].Where(item => item.Receiver == alias).Select(item => item.Item).ToList();
                    recapUser.Items.AddRange(items.Any() ? items : new List<string> { "Aucun élément" });

                    message = $"Alias ajouté : {alias} est maintenant associé à <@{receiverId}> et son récap généré.";

                    DataManager.SaveRecapList();
                    DataManager.SaveReceiverAliases();
                    break;

                /*case "add-roles":
                    var getRole = command.Data.Options.ElementAtOrDefault(1)?.Value as string;
                    receiverId = Declare.client.GetGuild(ulong.Parse(guildId)).Roles.FirstOrDefault(x => x.Name == getRole).Id.ToString();

                    Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias[alias] = receiverId;

                    if (!Declare.recapList.Guild.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList.Guild[guildId] = new ChannelRecapList();
                    }

                    if (!Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList.Guild[guildId].Channel[channelId] = new UserRecapList();
                    }

                    if (!Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out recapUserList))
                    {
                        recapUserList = new List<RecapList>();
                        Declare.recapList.Guild[guildId].Channel[channelId].Aliases[receiverId] = recapUserList;
                    }

                    recapUser = recapUserList.FirstOrDefault(e => e.Alias == alias);
                    if (recapUser == null)
                    {
                        recapUser = new RecapList { Alias = alias, Items = new List<string>() };
                        recapUserList.Add(recapUser);
                    }

                    items = Declare.displayedItems.Guild[guildId].Channel[channelId].Where(item => item.Receiver == alias).Select(item => item.Item).ToList();
                    recapUser.Items.AddRange(items.Any() ? items : new List<string> { "Aucun élément" });

                    var getMembers = Declare.client.GetGuild(ulong.Parse(guildId)).Roles.FirstOrDefault(x => x.Name == getRole).Members;


                    var guild = Declare.client.GetGuild(ulong.Parse(guildId));
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
                        if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Any(x => x.Value.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapData(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.Guild.ContainsKey(guildId) || !Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            if (subElements.Any())
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                foreach (var subElement in subElements)
                                {
                                    string groupedMessage = subElement.Items != null && subElement.Items.Any()
                                        ? string.Join(", ", subElement.Items
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.Alias}** : {groupedMessage} \n";
                                }
                                return true;
                            }

                            recapMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias.";
                            return false;
                        }

                        recapMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                        return false;
                    }

                    if (TryGetReceiverData(guildId, channelId, out string errorMessage))
                    {
                        if (TryGetRecapData(guildId, channelId, receiverId, out string recapMessage))
                        {
                            message = recapMessage;

                            while (message.Length > maxMessageLength)
                            {
                                string messagePart = message.Substring(0, maxMessageLength);
                                await command.FollowupAsync(messagePart, options: new RequestOptions { Timeout = 10000 });
                                message = message.Substring(maxMessageLength);
                            }
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

                    if (!string.IsNullOrEmpty(message))
                    {
                        await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 });
                    }

                    break;

                case "recap":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasData(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Any(x => x.Value.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapDataRecap(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.Guild.ContainsKey(guildId) || !Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var getUser = subElements.Any(x => x.Alias == alias);

                            if (getUser)
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";

                                foreach (var subElement in subElements.Where(x => x.Alias == alias))
                                {
                                    string groupedMessage = subElement.Items != null && subElement.Items.Any()
                                        ? string.Join(", ", subElement.Items
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.Alias}** : {groupedMessage} \n";
                                }
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

                            while (message.Length > maxMessageLength)
                            {
                                string messagePart = message.Substring(0, maxMessageLength);
                                await command.FollowupAsync(messagePart, options: new RequestOptions { Timeout = 10000 });
                                message = message.Substring(maxMessageLength);
                            }
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

                    if (!string.IsNullOrEmpty(message))
                    {
                        await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 });
                    }
                    break;

                case "recap-and-clean":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasDataRecapAndClean(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Any(x => x.Value.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapDataRecapAndClean(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.Guild.ContainsKey(guildId) || !Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var getUser = subElements.Any(x => x.Alias == alias);

                            if (getUser)
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                foreach (var subElement in subElements.Where(x => x.Alias == alias))
                                {
                                    string groupedMessage = subElement.Items != null && subElement.Items.Any()
                                        ? string.Join(", ", subElement.Items
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()} " : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.Alias}** : {groupedMessage} \n";
                                }

                                foreach (var subElement in subElements.Where(x => x.Alias == alias))
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

                    while (message.Length > maxMessageLength)
                    {
                        string messagePart = message.Substring(0, maxMessageLength);
                        await command.FollowupAsync(messagePart, options: new RequestOptions { Timeout = 10000 });
                        message = message.Substring(maxMessageLength);
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 });
                    }
                    break;

                case "clean":
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverAliasDataClean(string guildId, string channelId, string receiverId, out string errorMessage)
                    {
                        if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Any(x => x.Value.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryClearAliasDataClean(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.recapList.Guild.ContainsKey(guildId) || !Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            resultMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            var aliasElement = subElements.FirstOrDefault(x => x.Alias == alias);

                            if (aliasElement != null)
                            {
                                aliasElement.Items.Clear();
                                aliasElement.Items.Add("Aucun élément");
                                DataManager.SaveRecapList();
                                resultMessage = null;
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
                        if (!Declare.receiverAliases.Guild.ContainsKey(guildId) || !Declare.receiverAliases.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases.Guild[guildId].Channel[channelId].receiverAlias.Any(x => x.Value.Contains(receiverId)))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryClearSubElementsCleanAll(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.recapList.Guild.ContainsKey(guildId) || !Declare.recapList.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                            return false;
                        }

                        if (Declare.recapList.Guild[guildId].Channel[channelId].Aliases.TryGetValue(receiverId, out var subElements))
                        {
                            if (subElements.Any())
                            {
                                foreach (var subElement in subElements)
                                {
                                    subElement.Items.Clear();
                                    subElement.Items.Add("Aucun élément");
                                }
                                DataManager.SaveRecapList();
                                resultMessage = null;
                                return true;
                            }
                            resultMessage = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                            return false;
                        }

                        resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
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
                    bool listByLine = (bool)command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value;

                    string BuildItemMessage(IEnumerable<IGrouping<string, DisplayedItem>> filteredItems, bool listByLine)
                    {
                        var messageBuilder = new StringBuilder();

                        for (int i = 0; i < filteredItems.Count(); i++)
                        {
                            var groupedItem = filteredItems.ElementAt(i);
                            messageBuilder.Append(groupedItem.Count() > 1
                                ? $"{groupedItem.Key} x {groupedItem.Count()}"
                                : groupedItem.Key);

                            if (i < filteredItems.Count() - 1)
                            {
                                messageBuilder.Append(listByLine ? "\n" : ", ");
                            }
                        }

                        return messageBuilder.ToString();
                    }

                    async Task SendMessage(string message)
                    {
                        while (message.Length > maxMessageLength)
                        {
                            var messagePart = message.Substring(0, maxMessageLength);
                            await command.FollowupAsync(messagePart);
                            message = message.Substring(maxMessageLength);
                        }

                        if (!string.IsNullOrEmpty(message))
                        {
                            await command.FollowupAsync(message);
                        }
                    }

                    if (Declare.displayedItems.Guild.ContainsKey(guildId) && Declare.displayedItems.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        var filteredItems = Declare.displayedItems.Guild[guildId].Channel[channelId]
                            .Where(item => item.Receiver == receiverId)
                            .GroupBy(item => item.Item)
                            .OrderBy(group => group.Key)
                            .ToList();

                        if (filteredItems.Any())
                        {
                            message = $"Items pour {receiverId} :\n{BuildItemMessage(filteredItems, listByLine)}";
                            await SendMessage(message);
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

                    if (Declare.hintStatuses.Guild.ContainsKey(guildId) && Declare.hintStatuses.Guild[guildId].Channel.ContainsKey(channelId))
                    {
                        var hintByFinder = Declare.hintStatuses.Guild[guildId].Channel[channelId].Where(h => h.Finder == alias).ToList();

                        message = hintByFinder.Any()
                            ? BuildHintMessage(hintByFinder, alias)
                            : "No hint found for this finder";
                    }
                    else
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou Aucun hint.";
                    }
                    break;

                case "hint-for-receiver":
                    string BuildHintMessageReceiver(IEnumerable<HintStatus> hints, string alias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item for {alias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.Receiver}'s {item.Item} is at {item.Location} in {item.Finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    if (Declare.hintStatuses.Guild.TryGetValue(guildId, out var guildHints) &&
                        guildHints.Channel.TryGetValue(channelId, out var channelHints))
                    {
                        var hintByReceiver = channelHints.Where(h => h.Receiver == alias).ToList();

                        message = hintByReceiver.Any()
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

                    if (Declare.gameStatus.Guild.TryGetValue(guildId, out var guildGames) &&
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

                default:
                    message = "Commande inconnue.";
                    break;

            }
            if (!(command.CommandName.Contains("recap") || command.CommandName.Contains("list-items")))
            {
                await command.FollowupAsync(message);
            }
        }
        else
        {
            var channel = command.Channel as ITextChannel;
            if (channel != null)
            {
                if (command.CommandName.Equals("add-url"))
                {
                    bool IsValidUrl(string url) => url.Contains("sphere_tracker");

                    bool CanAddUrl(string guildId, string channelId, out string existingUrlMessage)
                    {
                        existingUrlMessage = string.Empty;

                        if (Declare.ChannelAndUrl.Guild.ContainsKey(guildId) && Declare.ChannelAndUrl.Guild[guildId].Channel.ContainsKey(channelId))
                        {
                            existingUrlMessage = $"URL déjà définie sur {Declare.ChannelAndUrl.Guild[guildId].Channel[channelId]}. Supprimez l'url avant d'ajouter une nouvelle url.";
                            return false;
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
                                message = $"Le lien est incorrect, utilisez l'url sphere_tracker.";
                            }
                            else
                            {
                                string threadTitle = command.Data.Options.ElementAt(1).Value.ToString();
                                string threadType = command.Data.Options.ElementAt(2).Value.ToString();

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

                                Declare.ChannelAndUrl.Guild[guildId].Channel[channelId] = newUrl;
                                DataManager.SaveChannelAndUrl();
                                message = $"URL définie sur {newUrl}. Messages configurés pour ce canal. Attendez que le programme récupère tous les aliases.";
                            }
                        }
                        else
                        {
                            message = existingUrlMessage;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"La commande a été exécutée dans le canal : {channel.Name}");
                    message = "Cette commande doit être exécutée dans un channel.";
                }
            }
            await command.FollowupAsync(message);
        }
    }

    public static async Task<string> DeleteChannelAndUrl(string? channelId, string? guildId)
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
            Declare.recapList.Guild.Remove(guildId);
        }
        else if (Declare.recapList.Guild.TryGetValue(guildId, out var recapList) && recapList.Channel.Remove(channelId))
        {
            if (recapList.Channel.Count == 0)
            {
                Declare.recapList.Guild.Remove(guildId);
            }
        }
        DataManager.SaveRecapList();

        if (channelId == null)
        {
            Declare.receiverAliases.Guild.Remove(guildId);
        }
        else if (Declare.receiverAliases.Guild.TryGetValue(guildId, out var receiverAliases) && receiverAliases.Channel.Remove(channelId))
        {
            if (receiverAliases.Channel.Count == 0)
            {
                Declare.receiverAliases.Guild.Remove(guildId);
            }
        }
        DataManager.SaveReceiverAliases();

        if (channelId == null)
        {
            Declare.displayedItems.Guild.Remove(guildId);
        }
        else if (Declare.displayedItems.Guild.TryGetValue(guildId, out var displayedItems) && displayedItems.Channel.Remove(channelId))
        {
            if (displayedItems.Channel.Count == 0)
            {
                Declare.displayedItems.Guild.Remove(guildId);
            }
        }
        DataManager.SaveDisplayedItems();

        if (channelId == null)
        {
            Declare.aliasChoices.Guild.Remove(guildId);
        }
        else if (Declare.aliasChoices.Guild.TryGetValue(guildId, out var aliasChoices) && aliasChoices.Channel.Remove(channelId))
        {
            if (aliasChoices.Channel.Count == 0)
            {
                Declare.aliasChoices.Guild.Remove(guildId);
            }
        }
        DataManager.SaveAliasChoices();

        if (channelId == null)
        {
            Declare.gameStatus.Guild.Remove(guildId);
        }
        else if (Declare.gameStatus.Guild.TryGetValue(guildId, out var gameStatus) && gameStatus.Channel.Remove(channelId))
        {
            if (gameStatus.Channel.Count == 0)
            {
                Declare.gameStatus.Guild.Remove(guildId);
            }
        }
        DataManager.SaveGameStatus();

        if (channelId == null)
        {
            Declare.hintStatuses.Guild.Remove(guildId);
        }
        else if (Declare.hintStatuses.Guild.TryGetValue(guildId, out var hintStatuses) && hintStatuses.Channel.Remove(channelId))
        {
            if (hintStatuses.Channel.Count == 0)
            {
                Declare.hintStatuses.Guild.Remove(guildId);
            }
        }
        DataManager.SaveHintStatus();

        message = "URL Supprimée.";
        await RegisterCommandsAsync();
        return message;
    }
}
