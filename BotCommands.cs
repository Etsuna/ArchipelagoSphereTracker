using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

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
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("add-alias")
                .WithDescription("Add Alias")
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

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
                    }
                ),

            new SlashCommandBuilder()
                .WithName("delete-url")
                .WithDescription("Delete Url, clean Alias and Recap"),

            new SlashCommandBuilder()
                .WithName("status-games-list")
                .WithDescription("status for all games"),

            new SlashCommandBuilder()
                .WithName("recap-all")
                .WithDescription("His own recap list of items for all the games"),

            new SlashCommandBuilder()
                .WithName("recap")
                .WithDescription("Recap List of items for a specific game")
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("recap-and-clean")
                .WithDescription("Recap and clean List of items for a specific game")
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("clean")
                .WithDescription("Recap and clean List of items for a specific game")
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("clean-all")
                .WithDescription("Recap and clean his own recap list of items for all the games"),


            new SlashCommandBuilder()
                 .WithName("hint-from-finder")
                 .WithDescription("Get a hint from finder")
                 .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                 .WithName("hint-for-receiver")
                 .WithDescription("Get a hint for receiver")
                 .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("list-items")
                .WithDescription("List all items for alias")
                .AddOption(BuildAliasOption(guild.Id.ToString(), Declare.aliasChoices))
                .AddOption(BuildListItemsOption()),
        };

            var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();
            await Declare.client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
        }

        Declare.client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    public static SlashCommandOptionBuilder BuildAliasOption(string guild, Dictionary<string, Dictionary<string, Dictionary<string, string>>> aliasChoices)
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName("alias")
            .WithDescription("Choose an alias")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        if (!aliasChoices.ContainsKey(guild))
            return optionBuilder;

        Dictionary<string, Dictionary<string, string>> filtered = aliasChoices[guild];

        Dictionary<string, string> merged = filtered
            .SelectMany(pair => pair.Value)
            .ToLookup(pair => pair.Key, pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var sorted = merged.OrderBy(pair => pair.Key)
                           .ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var alias in sorted)
        {
            optionBuilder.AddChoice(alias.Key, alias.Value);
        }

        return optionBuilder;
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

                    if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel.";
                    }
                    else
                    {
                        var channelAliases = Declare.receiverAliases[guildId][channelId];

                        if (channelAliases.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else
                        {
                            message = "Voici le tableau des utilisateurs :\n";
                            foreach (var kvp in channelAliases)
                            {
                                var user = await Declare.client.GetUserAsync(ulong.Parse(kvp.Value));
                                message += $"| {user.Username} | {kvp.Key} |\n";
                            }
                        }
                    }
                    break;

                case "delete-alias":
                    bool HasValidChannelData(string guildId, string channelId)
                    {
                        return Declare.receiverAliases.ContainsKey(guildId) && Declare.receiverAliases[guildId].ContainsKey(channelId);
                    }

                    if (!HasValidChannelData(guildId, channelId))
                    {
                        message = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                    }
                    else
                    {
                        var channelAliases = Declare.receiverAliases[guildId][channelId];

                        if (channelAliases.Count == 0)
                        {
                            message = "Aucun Alias est enregistré.";
                        }
                        else if (alias != null)
                        {
                            if (channelAliases.TryGetValue(alias, out var value))
                            {
                                if (value == command.User.Id.ToString() || (guildUser != null && guildUser.GuildPermissions.Administrator))
                                {
                                    Declare.receiverAliases[guildId][channelId].Remove(alias);
                                    DataManager.SaveReceiverAliases();

                                    message = value == command.User.Id.ToString()
                                        ? $"Alias '{alias}' supprimé."
                                        : $"ADMIN : Alias '{alias}' supprimé.";

                                    // Check and remove alias from recapList
                                    if (Declare.recapList.ContainsKey(guildId) && Declare.recapList[guildId].ContainsKey(channelId))
                                    {
                                        if (Declare.recapList[guildId][channelId].ContainsKey(value))
                                        {
                                            var subElements = Declare.recapList[guildId][channelId][value];
                                            subElements.RemoveAll(e => e.SubKey == alias);

                                            if (subElements.Count == 0)
                                            {
                                                Declare.recapList[guildId][channelId].Remove(value);
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
                            else
                            {
                                message = $"Aucun alias trouvé pour '{alias}'.";
                            }
                        }
                    }
                    break;

                case "add-alias":
                    receiverId = command.User.Id.ToString();

                    if (!Declare.receiverAliases.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.receiverAliases[guildId] = new Dictionary<string, Dictionary<string, string>>();
                    }

                    if (!Declare.receiverAliases[guildId].ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.receiverAliases[guildId][channelId] = new Dictionary<string, string>();
                    }

                    if (Declare.receiverAliases[guildId][channelId].TryGetValue(alias, out var existingReceiverId))
                    {
                        message = $"L'alias '{alias}' est déjà utilisé par <@{existingReceiverId}>.";
                        break;
                    }

                    Declare.receiverAliases[guildId][channelId][alias] = receiverId;

                    if (!Declare.recapList.ContainsKey(guildId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList[guildId] = new Dictionary<string, Dictionary<string, List<SubElement>>>();
                    }

                    if (!Declare.recapList[guildId].ContainsKey(channelId))
                    {
                        message = $"Aucune Alias trouvé.";
                        Declare.recapList[guildId][channelId] = new Dictionary<string, List<SubElement>>();
                    }

                    if (!Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var recapUserList))
                    {
                        recapUserList = new List<SubElement>();
                        Declare.recapList[guildId][channelId][receiverId] = recapUserList;
                    }

                    var recapUser = recapUserList.FirstOrDefault(e => e.SubKey == alias);
                    if (recapUser == null)
                    {
                        recapUser = new SubElement { SubKey = alias, Values = new List<string>() };
                        recapUserList.Add(recapUser);
                    }

                    var items = Declare.displayedItems[guildId][channelId].Where(item => item.receiver == alias).Select(item => item.item).ToList();
                    recapUser.Values.AddRange(items.Any() ? items : new List<string> { "Aucun élément" });

                    message = $"Alias ajouté : {alias} est maintenant associé à <@{receiverId}> et son récap généré.";

                    DataManager.SaveRecapList();
                    DataManager.SaveReceiverAliases();
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
                    receiverId = command.User.Id.ToString();

                    bool TryGetReceiverData(string guildId, string channelId, out string errorMessage)
                    {
                        if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases[guildId][channelId].ContainsValue(receiverId))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapData(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.ContainsKey(guildId) || !Declare.recapList[guildId].ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var subElements))
                        {
                            if (subElements.Any())
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                foreach (var subElement in subElements)
                                {
                                    string groupedMessage = subElement.Values != null && subElement.Values.Any()
                                        ? string.Join(", ", subElement.Values
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.SubKey}** : {groupedMessage} \n";
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
                        if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases[guildId][channelId].ContainsValue(receiverId))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapDataRecap(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.ContainsKey(guildId) || !Declare.recapList[guildId].ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var subElements))
                        {
                            var getUser = subElements.Any(x => x.SubKey == alias);

                            if (getUser)
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";

                                foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                                {
                                    string groupedMessage = subElement.Values != null && subElement.Values.Any()
                                        ? string.Join(", ", subElement.Values
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.SubKey}** : {groupedMessage} \n";
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
                        if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases[guildId][channelId].ContainsValue(receiverId))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryGetRecapDataRecapAndClean(string guildId, string channelId, string receiverId, out string recapMessage)
                    {
                        if (!Declare.recapList.ContainsKey(guildId) || !Declare.recapList[guildId].ContainsKey(channelId))
                        {
                            recapMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var subElements))
                        {
                            var getUser = subElements.Any(x => x.SubKey == alias);

                            if (getUser)
                            {
                                recapMessage = $"Détails pour <@{receiverId}> :\n";
                                foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                                {
                                    string groupedMessage = subElement.Values != null && subElement.Values.Any()
                                        ? string.Join(", ", subElement.Values
                                            .GroupBy(value => value)
                                            .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()} " : group.Key))
                                        : "Aucun élément";

                                    recapMessage += $"**{subElement.SubKey}** : {groupedMessage} \n";
                                }

                                foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                                {
                                    subElement.Values.Clear();
                                    subElement.Values.Add("Aucun élément");
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
                        if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases[guildId][channelId].ContainsValue(receiverId))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryClearAliasDataClean(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.recapList.ContainsKey(guildId) || !Declare.recapList[guildId].ContainsKey(channelId))
                        {
                            resultMessage = "Il existe aucune liste.";
                            return false;
                        }

                        if (Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var subElements))
                        {
                            var aliasElement = subElements.FirstOrDefault(x => x.SubKey == alias);

                            if (aliasElement != null)
                            {
                                aliasElement.Values.Clear();
                                aliasElement.Values.Add("Aucun élément");
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
                        if (!Declare.receiverAliases.ContainsKey(guildId) || !Declare.receiverAliases[guildId].ContainsKey(channelId))
                        {
                            errorMessage = "Pas d'URL Enregistrée pour ce channel ou aucun alias enregistré.";
                            return false;
                        }

                        if (!Declare.receiverAliases[guildId][channelId].ContainsValue(receiverId))
                        {
                            errorMessage = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    bool TryClearSubElementsCleanAll(string guildId, string channelId, string receiverId, out string resultMessage)
                    {
                        if (!Declare.recapList.ContainsKey(guildId) || !Declare.recapList[guildId].ContainsKey(channelId))
                        {
                            resultMessage = $"L'utilisateur <@{receiverId}> n'existe pas.";
                            return false;
                        }

                        if (Declare.recapList[guildId][channelId].TryGetValue(receiverId, out var subElements))
                        {
                            if (subElements.Any())
                            {
                                foreach (var subElement in subElements)
                                {
                                    subElement.Values.Clear();
                                    subElement.Values.Add("Aucun élément");
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

                    string BuildItemMessage(IEnumerable<IGrouping<string, displayedItemsElement>> filteredItems, bool listByLine)
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

                    if (Declare.displayedItems.ContainsKey(guildId) && Declare.displayedItems[guildId].ContainsKey(channelId))
                    {
                        var filteredItems = Declare.displayedItems[guildId][channelId]
                            .Where(item => item.receiver == receiverId)
                            .GroupBy(item => item.item)
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
                    string BuildHintMessage(IEnumerable<hintStatus> hints, string alias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item from {alias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.receiver}'s {item.item} is at {item.location} in {item.finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    if (Declare.hintStatuses.ContainsKey(guildId) && Declare.hintStatuses[guildId].ContainsKey(channelId))
                    {
                        var hintByFinder = Declare.hintStatuses[guildId][channelId].Where(h => h.finder == alias).ToList();

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
                    string BuildHintMessageReceiver(IEnumerable<hintStatus> hints, string alias)
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Item for {alias} :");

                        foreach (var item in hints)
                        {
                            messageBuilder.AppendLine($"{item.receiver}'s {item.item} is at {item.location} in {item.finder}'s World");
                        }

                        return messageBuilder.ToString();
                    }

                    if (Declare.hintStatuses.TryGetValue(guildId, out var guildHints) &&
                        guildHints.TryGetValue(channelId, out var channelHints))
                    {
                        var hintByReceiver = channelHints.Where(h => h.receiver == alias).ToList();

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
                    var messageBuilder = new StringBuilder("Status for all games :\n");

                    if (Declare.gameStatus.TryGetValue(guildId, out var guildGames) &&
                        guildGames.TryGetValue(channelId, out var channelGames))
                    {
                        foreach (var game in channelGames)
                        {
                            string gameStatus = (game.pourcent != "100.00")
                                ? $"**{game.name} - {game.game} - {game.pourcent}%**\n"
                                : $"~~{game.name} - {game.game} - {game.pourcent}%~~\n";

                            messageBuilder.Append(gameStatus);
                        }
                    }
                    else
                    {
                        messageBuilder.Clear().Append("Pas d'URL Enregistrée pour ce channel.");
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

                    if (channel != null)
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

                        List<IGuildUser> allMembers = new List<IGuildUser>();

                        await foreach (var memberBatch in channel.GetUsersAsync()) 
                        {
                            allMembers.AddRange(memberBatch); 
                        }

                        foreach (var member in allMembers)
                        {
                            await thread.AddUserAsync(member);
                        }

                        Console.WriteLine($"Le thread a été créé avec l'ID : {channelId}");
                    }
                    else
                    {
                        Console.WriteLine("Le canal spécifié n'est pas un canal de texte.");
                    }

                    bool CanAddUrl(string guildId, string channelId, out string existingUrlMessage)
                    {
                        existingUrlMessage = string.Empty;

                        if (Declare.ChannelAndUrl.ContainsKey(guildId) && Declare.ChannelAndUrl[guildId].ContainsKey(channelId))
                        {
                            existingUrlMessage = $"URL déjà définie sur {Declare.ChannelAndUrl[guildId][channelId]}. Supprimez l'url avant d'ajouter une nouvelle url.";
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
                                message = $"Le lien n'est pas bon, utilisez l'url sphere_tracker.";
                            }
                            else
                            {
                                if (!Declare.ChannelAndUrl.ContainsKey(guildId))
                                {
                                    Declare.ChannelAndUrl[guildId] = new Dictionary<string, string>();
                                }

                                Declare.ChannelAndUrl[guildId][channelId] = newUrl;
                                DataManager.SaveChannelAndUrl();
                                message = $"URL définie sur {newUrl}. Messages configurés pour ce canal. Attendez que le programme récupère tous les aliases.";

                                await TrackingDataManager.GetTableDataAsync(guildId, channelId, newUrl, false);
                                if (!Declare.serviceRunning)
                                {
                                    TrackingDataManager.StartTracking();
                                }
                            }
                        }
                        else
                        {
                            message = existingUrlMessage;
                        }
                    }
                    await command.FollowupAsync(message);
                }
                else
                {
                    Console.WriteLine($"La commande a été exécutée dans le canal : {channel.Name}");
                    await command.FollowupAsync("Cette commande doit être exécutée dans un thread.");
                }
            }
            return;
        }


    }

    public static async Task<string> DeleteChannelAndUrl(string? channelId, string? guildId)
    {
        string message;
        if (channelId == null)
        {
            Declare.ChannelAndUrl.Remove(guildId);
        }
        else if (Declare.ChannelAndUrl.TryGetValue(guildId, out var urlSphereTracker) && urlSphereTracker.Remove(channelId))
        {
            if (urlSphereTracker.Count == 0)
            {
                Declare.ChannelAndUrl.Remove(guildId);
            }
        }
        DataManager.SaveChannelAndUrl();

        if (channelId == null)
        {
            Declare.recapList.Remove(guildId);
        }
        else if (Declare.recapList.TryGetValue(guildId, out var recapList) && recapList.Remove(channelId))
        {
            if (recapList.Count == 0)
            {
                Declare.recapList.Remove(guildId);
            }
        }
        DataManager.SaveRecapList();

        if (channelId == null)
        {
            Declare.receiverAliases.Remove(guildId);
        }
        else if (Declare.receiverAliases.TryGetValue(guildId, out var receiverAliases) && receiverAliases.Remove(channelId))
        {
            if (receiverAliases.Count == 0)
            {
                Declare.receiverAliases.Remove(guildId);
            }
        }
        DataManager.SaveReceiverAliases();

        if (channelId == null)
        {
            Declare.displayedItems.Remove(guildId);
        }
        else if (Declare.displayedItems.TryGetValue(guildId, out var displayedItems) && displayedItems.Remove(channelId))
        {
            if (displayedItems.Count == 0)
            {
                Declare.displayedItems.Remove(guildId);
            }
        }
        DataManager.SaveDisplayedItems();

        if (channelId == null)
        {
            Declare.aliasChoices.Remove(guildId);
        }
        else if (Declare.aliasChoices.TryGetValue(guildId, out var aliasChoices) && aliasChoices.Remove(channelId))
        {
            if (aliasChoices.Count == 0)
            {
                Declare.aliasChoices.Remove(guildId);
            }
        }
        DataManager.SaveAliasChoices();

        if (channelId == null)
        {
            Declare.gameStatus.Remove(guildId);
        }
        else if (Declare.gameStatus.TryGetValue(guildId, out var gameStatus) && gameStatus.Remove(channelId))
        {
            if (gameStatus.Count == 0)
            {
                Declare.gameStatus.Remove(guildId);
            }
        }
        DataManager.SaveGameStatus();

        if (channelId == null)
        {
            Declare.hintStatuses.Remove(guildId);
        }
        else if (Declare.hintStatuses.TryGetValue(guildId, out var hintStatuses) && hintStatuses.Remove(channelId))
        {
            if (hintStatuses.Count == 0)
            {
                Declare.hintStatuses.Remove(guildId);
            }
        }
        DataManager.SaveHintStatus();

        message = "URL Supprimée.";
        await RegisterCommandsAsync();
        return message;
    }
}
