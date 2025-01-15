using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

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

    public static async Task SendMessageAsync(string message)
    {
        if (Declare.channelId == 0)
        {
            Console.WriteLine("Aucun canal configuré pour l'envoi des messages.");
            return;
        }

        try
        {
            var channel = Declare.client.GetChannel(Declare.channelId) as IMessageChannel;

            if (channel == null)
            {
                Console.WriteLine($"Le canal avec l'ID {Declare.channelId} est introuvable ou inaccessible.");
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

            await channel.SendMessageAsync(message);
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
                .AddOption(BuildAliasOption(Declare.aliasChoices)),

             new SlashCommandBuilder()
                .WithName("add-alias")
                .WithDescription("Add Alias")
                .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("add-url")
                .WithDescription("Add Url")
                .AddOption("url", ApplicationCommandOptionType.String, "The URL to track", isRequired: true),

            new SlashCommandBuilder()
                .WithName("delete-url")
                .WithDescription("Delete Url, clean Alias and Recap"),

            new SlashCommandBuilder()
                .WithName("recap")
                .WithDescription("Recap List of items")
                .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("recap-and-clean")
                .WithDescription("Recap and clean List of items")
                .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("clean")
                .WithDescription("Recap and clean List of items")
                .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                 .WithName("hint-by-finder")
                 .WithDescription("Get a hint by finder")
                 .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                 .WithName("hint-by-receiver")
                 .WithDescription("Get a hint by receiver")
                 .AddOption(BuildAliasOption(Declare.aliasChoices)),

            new SlashCommandBuilder()
                .WithName("list-items")
                .WithDescription("List all items for alias")
                .AddOption(BuildAliasOption(Declare.aliasChoices))
                .AddOption(BuildListItemsOption()),
        };

        foreach (var guild in Declare.client.Guilds)
        {
            var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();
            await Declare.client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
        }

        Declare.client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    public static SlashCommandOptionBuilder BuildAliasOption(IDictionary<string, string> aliasChoices)
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName("alias")
            .WithDescription("Choose an alias")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var alias in aliasChoices)
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

        switch (command.CommandName)
        {
            case "get-aliases":
                DataManager.LoadReceiverAliases();
                if (Declare.receiverAliases.Count == 0)
                {
                    message = "Aucun Alias est enregistré.";
                }
                else
                {
                    message = "Voici le tableau des utilisateurs :\n";
                    foreach (var kvp in Declare.receiverAliases)
                    {
                        var user = await Declare.client.GetUserAsync(ulong.Parse(kvp.Value));
                        message += $"| {user.Username} | {kvp.Key} |\n";
                    }
                }
                break;

            case "delete-alias":
                DataManager.LoadReceiverAliases();
                if (Declare.receiverAliases.Count == 0)
                {
                    message = "Aucun Alias est enregistré.";
                }
                else
                {
                    if (alias != null)
                    {
                        if (Declare.receiverAliases.TryGetValue(alias, out var value))
                        {
                            if (value == command.User.Id.ToString())
                            {
                                Declare.receiverAliases.Remove(alias);
                                DataManager.SaveReceiverAliases();
                                message = $"Alias '{alias}' supprimé.";

                                if (Declare.recapList.ContainsKey(value))
                                {
                                    DataManager.LoadRecapList();
                                    var subElements = Declare.recapList[value];
                                    subElements.RemoveAll(e => e.SubKey == alias);

                                    if (subElements.Count == 0)
                                    {
                                        Declare.recapList.Remove(value);
                                    }

                                    DataManager.SaveRecapList();
                                }

                            }
                            else if (guildUser != null && guildUser.GuildPermissions.Administrator)
                            {
                                Declare.receiverAliases.Remove(alias);
                                DataManager.SaveReceiverAliases();
                                message = $"ADMIN : Alias '{alias}' supprimé.";

                                if (Declare.recapList.ContainsKey(value))
                                {
                                    DataManager.LoadRecapList();
                                    var subElements = Declare.recapList[value];
                                    subElements.RemoveAll(e => e.SubKey == alias);

                                    if (subElements.Count == 0)
                                    {
                                        Declare.recapList.Remove(value);
                                    }

                                    DataManager.SaveRecapList();
                                }
                            }
                            else
                            {
                                message = $"Vous n'êtes pas le détenteur de cet alias : '{alias}'. Suppression non effectuée..";
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
                DataManager.LoadReceiverAliases();

                if (alias != null)
                {
                    receiverId = command.User.Id.ToString();

                    if (!Declare.receiverAliases.ContainsKey(alias))
                    {
                        Declare.receiverAliases[alias] = receiverId;
                        DataManager.SaveReceiverAliases();
                        message = $"Alias ajouté : {alias} est maintenant associé à <@{receiverId}>.";

                        DataManager.LoadRecapList();
                        if (!Declare.recapList.ContainsKey(receiverId))
                        {
                            Declare.recapList[receiverId] = new List<SubElement>();
                        }

                        var recapUser = Declare.recapList[receiverId].Find(e => e.SubKey == alias);
                        if (recapUser == null)
                        {
                            Declare.recapList[receiverId].Add(new SubElement
                            {
                                SubKey = alias,
                                Values = new List<string> { "Aucun élément" }
                            });
                        }
                        DataManager.SaveRecapList();
                    }
                    else
                    {
                        message = $"L'alias '{alias}' est déjà utilisé par <@{Declare.receiverAliases[alias]}>.";
                    }
                }
                break;

            case "add-url":
                if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                {
                    message = "Seuls les administrateurs sont autorisés à ajouter une URL.";
                }
                else if (!string.IsNullOrEmpty(Declare.urlSphereTracker))
                {
                    message = $"URL déjà définie sur {Declare.urlSphereTracker}. Supprimez l'url avant d'ajouter une nouvelle url.";
                }
                else
                {
                    var newUrl = command.Data.Options.FirstOrDefault()?.Value as string;
                    if (!string.IsNullOrEmpty(newUrl))
                    {
                        if (!newUrl.Contains("sphere_tracker"))
                        {
                            message = $"Le lien n'est pas bon, utilisez l'url sphere_tracker.";
                        }
                        else
                        {
                            Declare.urlSphereTracker = newUrl;
                            Declare.channelId = command.Channel.Id;
                            DataManager.SaveUrlAndChannel();
                            message = $"URL définie sur {Declare.urlSphereTracker}. Messages configurés pour ce canal.";
                            TrackingDataManager.StartTracking();
                        }
                    }
                }
                break;

            case "delete-url":
                if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                {
                    message = "Seuls les administrateurs sont autorisés à supprimer une URL.";
                }
                else
                {
                    try
                    {
                        if (File.Exists(Declare.urlChannelFile))
                        {
                            File.Delete(Declare.urlChannelFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message = $"Erreur lors de la suppression du fichier urlChannelFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(Declare.displayedItemsFile))
                        {
                            File.Delete(Declare.displayedItemsFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier displayedItemsFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(Declare.recapListFile))
                        {
                            File.Delete(Declare.recapListFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier recapListFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(Declare.aliasFile))
                        {
                            File.Delete(Declare.aliasFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier aliasFile : {ex.Message}";
                        Console.WriteLine(message);
                    }

                    try
                    {
                        if (File.Exists(Declare.aliasChoicesFile))
                        {
                            File.Delete(Declare.aliasChoicesFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier aliasFile : {ex.Message}";
                        Console.WriteLine(message);
                    }

                    try
                    {
                        if (File.Exists(Declare.gameStatusFile))
                        {
                            File.Delete(Declare.gameStatusFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier aliasFile : {ex.Message}";
                        Console.WriteLine(message);
                    }

                    try
                    {
                        if (File.Exists(Declare.hintStatusFile))
                        {
                            File.Delete(Declare.hintStatusFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier aliasFile : {ex.Message}";
                        Console.WriteLine(message);
                    }

                    if (string.IsNullOrEmpty(Declare.urlSphereTracker))
                    {
                        message = "Aucune URL définie.";
                    }
                    else
                    {
                        Declare.urlSphereTracker = string.Empty;
                        Declare.urlTracker = string.Empty;

                        Declare.recapList.Clear();
                        Declare.receiverAliases.Clear();
                        Declare.displayedItems.Clear();
                        Declare.aliasChoices.Clear();
                        Declare.gameStatus.Clear();
                        Declare.hintStatuses.Clear();

                        message = "URL Supprimée.";
                        await RegisterCommandsAsync();
                    }
                }
                break;

            case "recap":
                DataManager.LoadReceiverAliases();
                receiverId = command.User.Id.ToString();

                if (!Declare.receiverAliases.ContainsValue(receiverId))
                {
                    message = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                }
                else
                {
                    DataManager.LoadRecapList();
                    if (Declare.recapList == null)
                    {
                        message = "Il existe aucune liste.";
                    }
                    else if (Declare.recapList.TryGetValue(receiverId, out var subElements))
                    {
                        var getUser = subElements.Any(x => x.SubKey == alias);

                        if (getUser)
                        {
                            message = $"Détails pour <@{receiverId}> :\n";

                            foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                            {
                                if (subElement.Values != null && subElement.Values.Any())
                                {
                                    var groupedValues = subElement.Values
                                        .GroupBy(value => value)
                                        .Select(group => new { Value = group.Key, Count = group.Count() });

                                    string groupedMessage = string.Join(", ", groupedValues.Select(g =>
                                        g.Count > 1 ? $"{g.Count} x {g.Value}" : g.Value));

                                    message += $"**{subElement.SubKey}** : {groupedMessage} \n";
                                }
                                else
                                {
                                    message += $"**{subElement.SubKey}** : Aucun élément \n";
                                }
                            }
                        }
                        else
                        {
                            message = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                        }
                    }
                    else
                    {
                        message = $"L'utilisateur <@{receiverId}> n'existe pas.";
                    }
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

            case "recap-and-clean":
                DataManager.LoadReceiverAliases();
                receiverId = command.User.Id.ToString();

                if (!Declare.receiverAliases.ContainsValue(receiverId))
                {
                    message = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                }
                else
                {
                    DataManager.LoadRecapList();
                    if (Declare.recapList.TryGetValue(receiverId, out var subElements))
                    {
                        message = $"Détails pour <@{receiverId}> :\n";

                        var getUser = subElements.Any(x => x.SubKey == alias);

                        if (getUser)
                        {
                            foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                            {


                                if (subElement.Values != null && subElement.Values.Any())
                                {
                                    var groupedValues = subElement.Values
                                        .GroupBy(value => value)
                                        .Select(group => new { Value = group.Key, Count = group.Count() });

                                    string groupedMessage = string.Join(", ", groupedValues.Select(g =>
                                        g.Count > 1 ? $"{g.Count} x {g.Value}" : g.Value));

                                    message += $"**{subElement.SubKey}** : {groupedMessage} \n";
                                }
                                else
                                {
                                    message += $"**{subElement.SubKey}** : Aucun élément \n";
                                }
                            }
                        }
                        else
                        {
                            message = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";

                        }

                        foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                        {
                            if (getUser)
                            {
                                subElement.Values.Clear();
                                subElement.Values.Add("Aucun élément");
                            }
                        }

                        DataManager.SaveRecapList();
                        DataManager.LoadRecapList();
                    }
                    else
                    {
                        message = $"L'utilisateur <@{receiverId}> n'existe pas.";
                    }
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
                DataManager.LoadReceiverAliases();
                receiverId = command.User.Id.ToString();


                if (!Declare.receiverAliases.ContainsValue(receiverId))
                {
                    message = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                }
                else
                {
                    DataManager.LoadRecapList();
                    if (Declare.recapList.TryGetValue(receiverId, out var subElements))
                    {
                        var getUser = subElements.Any(x => x.SubKey == alias);

                        if (getUser)
                        {

                            foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                            {
                                if (subElement.Values != null && subElement.Values.Any())
                                {
                                    var groupedValues = subElement.Values
                                        .GroupBy(value => value)
                                        .Select(group => new { Value = group.Key, Count = group.Count() });

                                    string groupedMessage = string.Join(", ", groupedValues.Select(g =>
                                        g.Count > 1 ? $"{g.Count} x {g.Value}" : g.Value));

                                }
                            }

                            foreach (var subElement in subElements.Where(x => x.SubKey == alias))
                            {
                                subElement.Values.Clear();
                                subElement.Values.Add("Aucun élément");
                            }

                            DataManager.SaveRecapList();
                            DataManager.LoadRecapList();
                        }
                        else
                        {
                            message = $"L'utilisateur <@{receiverId}> n'est pas enregistré avec l'alias: {alias}.";
                        }
                    }
                    else
                    {
                        message = $"L'utilisateur <@{receiverId}> n'existe pas.";
                    }
                }

                if (!string.IsNullOrEmpty(message))
                {
                    await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 });
                }
                else
                {
                    await command.FollowupAsync($"Clean pour Alias {alias} effectué", options: new RequestOptions { Timeout = 10000 });
                }
                break;


            case "list-items":
                DataManager.LoadDisplayedItems();

                receiverId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;
                bool listByLine = (bool)command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value;

                var filteredItems = Declare.displayedItems
                .Where(item => item.receiver == receiverId)
                .GroupBy(item => item.item)
                .Select(group => new
                {
                    ItemName = group.Key,
                    Count = group.Count()
                })
                .OrderBy(group => group.ItemName)
                .ToList();

                if (filteredItems.Count != 0)
                {
                    message = $"Items pour {receiverId} :\n";

                    for (int i = 0; i < filteredItems.Count; i++)
                    {
                        var groupedItem = filteredItems[i];

                        if (groupedItem.Count > 1)
                        {
                            message += $"{groupedItem.Count} x {groupedItem.ItemName}";
                        }
                        else
                        {
                            message += $"{groupedItem.ItemName}";
                        }

                        if (i < filteredItems.Count - 1)
                        {
                            message += listByLine ? "\n" : ", ";
                        }
                    }

                    while (message.Length > maxMessageLength)
                    {
                        string messagePart = message.Substring(0, maxMessageLength);
                        await command.FollowupAsync(messagePart);
                        message = message.Substring(maxMessageLength);
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        await command.FollowupAsync(message);
                    }
                }
                break;
            case "hint-by-finder":
                var hintByFinder = Declare.hintStatuses.Where(h => h.finder == alias).ToList();

                if (hintByFinder.Count != 0)
                {
                    foreach (var item in hintByFinder)
                    {
                        message = $"Item: {item.item}, Found By: {item.finder} For: {item.receiver}, Game: {item.game}, Location: {item.location}, Entrance: {item.entrance}\n!";
                    }
                }
                else
                {
                    message = "No hint found for this finder";
                }
                break;
            case "hint-by-receiver":
                var hintByReceiver = Declare.hintStatuses.Where(h => h.receiver == alias).ToList();

                if (hintByReceiver.Count != 0)
                {
                    foreach (var item in hintByReceiver)
                    {
                        message = $"Item: {item.item}, Found By: {item.finder} For: {item.receiver}, Game: {item.game}, Location: {item.location}, Entrance: {item.entrance}\n!";
                    }
                }
                else
                {
                    message = "No hint found for this receiver";
                }
                break;
            default:
                message = "Commande inconnue.";
                break;

        }
        if (!(command.CommandName.Contains("recap") || command.CommandName.Contains("list")))
        {
            await command.FollowupAsync(message);
        }
    }
}
