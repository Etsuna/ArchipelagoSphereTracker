using Discord;
using Discord.WebSocket;
using Discord.Commands;
using HtmlAgilityPack;
using Newtonsoft.Json;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Linq;

class Program
{
    private static readonly string displayedItemsFile = "displayedItems.json";
    private static readonly string aliasFile = "aliases.json";
    private static readonly string urlChannelFile = "url_channel.json";
    private static readonly string recapListFile = "recap.json";
    private static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");


    private static string url = string.Empty;
    private static ulong channelId = 0;
    private static Dictionary<string, string> receiverAliases = new Dictionary<string, string>();
    private static Dictionary<string, List<SubElement>> recapList = new Dictionary<string, List<SubElement>>();
    private static IDictionary<string, string> aliasChoices = new Dictionary<string, string>();
    private static List<displayedItemsElement> displayedItems = new List<displayedItemsElement>();
    private static CancellationTokenSource cts;
    private static DiscordSocketClient client;
    private static CommandService commandService;
    private static IServiceProvider services;

    public class SubElement
    {
        public string SubKey { get; set; }
        public List<string> Values { get; set; }
    }

    public class displayedItemsElement
    {
        public string sphere { get; set; }
        public string finder { get; set; }
        public string receiver { get; set; }
        public string item { get; set; }
        public string location { get; set; }
        public string game { get; set; }
    }

    static async Task Main(string[] args)
    {
        Env.Load();

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            UseInteractionSnowflakeDate = false
        };


        client = new DiscordSocketClient(config);
        commandService = new CommandService();

        client.Log += LogAsync;
        client.Ready += ReadyAsync;
        client.MessageReceived += MessageReceivedAsync;

        await InstallCommandsAsync();

        LoadReceiverAliases();
        LoadUrlAndChannel();
        LoadRecapList();
        AddMissingRecapUser();

        await client.LoginAsync(TokenType.Bot, discordToken);
        await client.StartAsync();

        await Task.Delay(-1);
    }

    static async Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
    }

    static async Task ReadyAsync()
    {
        await RegisterCommandsAsync();
        Console.WriteLine("Bot is connected!");
        StartTracking(isLauchedProcess: true);
    }

    static async Task RegisterCommandsAsync()
    {
        var commands = new List<SlashCommandBuilder>
    {
        new SlashCommandBuilder()
            .WithName("get-aliases")
            .WithDescription("Get Aliases"),

        new SlashCommandBuilder()
            .WithName("delete-alias")
            .WithDescription("Delete Alias")
            .AddOption(BuildAliasOption(aliasChoices)),

         new SlashCommandBuilder()
            .WithName("add-alias")
            .WithDescription("Add Alias")
            .AddOption(BuildAliasOption(aliasChoices)),

        new SlashCommandBuilder()
            .WithName("add-url")
            .WithDescription("Add Url")
            .AddOption("url", ApplicationCommandOptionType.String, "The URL to track", isRequired: true),

        new SlashCommandBuilder()
            .WithName("delete-url")
            .WithDescription("Delete Url, clean Alias and Recap"),

        new SlashCommandBuilder()
        .WithName("recap")
        .WithDescription("Recap List of items"),

        new SlashCommandBuilder()
        .WithName("recap-and-clean")
        .WithDescription("Recap and clean List of items"),

        new SlashCommandBuilder()
        .WithName("list-items")
        .WithDescription("List all items for alias")
        .AddOption(BuildAliasOption(aliasChoices))
        .AddOption(BuildListItemsOption())
    };

        foreach (var guild in client.Guilds)
        {
            var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();
            await client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
        }

        client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    static SlashCommandOptionBuilder BuildAliasOption(IDictionary<string, string> aliasChoices)
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName("alias")
            .WithDescription("Choose an alias to add")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var alias in aliasChoices)
        {
            optionBuilder.AddChoice(alias.Key, alias.Value);
        }

        return optionBuilder;
    }

    static SlashCommandOptionBuilder BuildListItemsOption()
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName("list-by-line")
            .WithDescription("Choose whether to display items line by line (true) or comma separated (false).")
            .WithType(ApplicationCommandOptionType.Boolean)
            .WithRequired(true);

        return optionBuilder;
    }

    static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var guildUser = command.User as IGuildUser;
        var receiverId = "";
        string message = "";
        const int maxMessageLength = 1999;

        switch (command.CommandName)
        {
            case "get-aliases":
                await command.DeferAsync();
                LoadReceiverAliases();
                if (receiverAliases.Count == 0)
                {
                    message = "Aucun Alias est enregistré.";
                }
                else
                {
                    message = "Voici le tableau des utilisateurs :\n";
                    foreach (var kvp in receiverAliases)
                    {
                        var user = await client.GetUserAsync(ulong.Parse(kvp.Value));
                        message += $"| {user.Username} | {kvp.Key} |\n";
                    }
                }
                break;

            case "delete-alias":
                await command.DeferAsync();
                LoadReceiverAliases();
                if (receiverAliases.Count == 0)
                {
                    message = "Aucun Alias est enregistré.";
                }
                else
                {
                    var aliasToDelete = command.Data.Options.FirstOrDefault()?.Value as string;
                    if (aliasToDelete != null)
                    {
                        if (receiverAliases.TryGetValue(aliasToDelete, out var value))
                        {
                            if (value == command.User.Id.ToString())
                            {
                                receiverAliases.Remove(aliasToDelete);
                                SaveReceiverAliases();
                                message = $"Alias '{aliasToDelete}' supprimé.";

                                if (recapList.ContainsKey(value))
                                {
                                    LoadRecapList();
                                    var subElements = recapList[value];
                                    subElements.RemoveAll(e => e.SubKey == aliasToDelete);

                                    if (subElements.Count == 0)
                                    {
                                        recapList.Remove(value);
                                    }

                                    SaveRecapList();
                                }

                            }
                            else if (guildUser != null && guildUser.GuildPermissions.Administrator)
                            {
                                receiverAliases.Remove(aliasToDelete);
                                SaveReceiverAliases();
                                message = $"ADMIN : Alias '{aliasToDelete}' supprimé.";

                                if (recapList.ContainsKey(value))
                                {
                                    LoadRecapList();
                                    var subElements = recapList[value];
                                    subElements.RemoveAll(e => e.SubKey == aliasToDelete);

                                    if (subElements.Count == 0)
                                    {
                                        recapList.Remove(value);
                                    }

                                    SaveRecapList();
                                }
                            }
                            else
                            {
                                message = $"Vous n'êtes pas le détenteur de cet alias : '{aliasToDelete}'. Suppression non effectuée..";
                            }
                        }
                        else
                        {
                            message = $"Aucun alias trouvé pour '{aliasToDelete}'.";
                        }
                    }
                }
                break;

            case "add-alias":
                await command.DeferAsync();
                LoadReceiverAliases();
                var aliasToAdd = command.Data.Options.FirstOrDefault()?.Value as string;
                if (aliasToAdd != null)
                {
                    receiverId = command.User.Id.ToString();

                    if (!receiverAliases.ContainsKey(aliasToAdd))
                    {
                        receiverAliases[aliasToAdd] = receiverId;
                        SaveReceiverAliases();
                        message = $"Alias ajouté : {aliasToAdd} est maintenant associé à <@{receiverId}>.";

                        LoadRecapList();
                        if (!recapList.ContainsKey(receiverId))
                        {
                            recapList[receiverId] = new List<SubElement>();
                        }

                        var recapUser = recapList[receiverId].Find(e => e.SubKey == aliasToAdd);
                        if (recapUser == null)
                        {
                            recapList[receiverId].Add(new SubElement
                            {
                                SubKey = aliasToAdd,
                                Values = new List<string> { "Aucun élément" }
                            });
                        }
                        SaveRecapList();
                    }
                    else
                    {
                        message = $"L'alias '{aliasToAdd}' est déjà utilisé par <@{receiverAliases[aliasToAdd]}>.";
                    }
                }
                break;

            case "add-url":
                await command.DeferAsync();
                if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                {
                    message = "Seuls les administrateurs sont autorisés à ajouter une URL.";
                }
                else if (!string.IsNullOrEmpty(url))
                {
                    message = $"URL déjà définie sur {url}. Supprimez l'url avant d'ajouter une nouvelle url.";
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
                            url = newUrl;
                            channelId = command.Channel.Id;
                            SaveUrlAndChannel();
                            message = $"URL définie sur {url}. Messages configurés pour ce canal.";
                            StartTracking();
                        }
                    }
                }
                break;

            case "delete-url":
                await command.DeferAsync();
                if (guildUser != null && !guildUser.GuildPermissions.Administrator)
                {
                    message = "Seuls les administrateurs sont autorisés à supprimer une URL.";
                }
                else
                {
                    try
                    {
                        if (File.Exists(urlChannelFile))
                        {
                            File.Delete(urlChannelFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message = $"Erreur lors de la suppression du fichier urlChannelFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(displayedItemsFile))
                        {
                            File.Delete(displayedItemsFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier displayedItemsFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(recapListFile))
                        {
                            File.Delete(recapListFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier recapListFile : {ex.Message}";
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (File.Exists(aliasFile))
                        {
                            File.Delete(aliasFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"\nErreur lors de la suppression du fichier aliasFile : {ex.Message}";
                        Console.WriteLine(message);
                    }

                    if (string.IsNullOrEmpty(url))
                    {
                        message = "Aucune URL définie.";
                    }
                    else
                    {
                        url = string.Empty;

                        recapList?.Clear();
                        receiverAliases?.Clear();

                        message = "URL Supprimée.";
                        aliasChoices.Clear();
                        await RegisterCommandsAsync();
                    }
                }
                break;

            case "recap":
                await command.DeferAsync();
                LoadReceiverAliases();
                receiverId = command.User.Id.ToString();

                if (!receiverAliases.ContainsValue(receiverId))
                {
                    message = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                }
                else
                {
                    LoadRecapList();
                    if (recapList == null)
                    {
                        message = "Il existe aucune liste.";
                    }
                    else if (recapList.TryGetValue(receiverId, out var subElements))
                    {
                        message = $"Détails pour <@{receiverId}> :\n";
                        foreach (var subElement in subElements)
                        {
                            string values = subElement.Values != null && subElement.Values.Any()
                                ? string.Join(", ", subElement.Values)
                                : "Aucun élément";

                            message += $"**{subElement.SubKey}** : {values} \n";
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
                await command.DeferAsync();
                LoadReceiverAliases();
                receiverId = command.User.Id.ToString();

                if (!receiverAliases.ContainsValue(receiverId))
                {
                    message = "Vous n'avez pas d'alias d'enregistré, utilisez la commande /add-alias pour générer automatiquement un fichier de recap.";
                }
                else
                {
                    LoadRecapList();
                    if (recapList.TryGetValue(receiverId, out var subElements))
                    {
                        message = $"Détails pour <@{receiverId}> :\n";
                        foreach (var subElement in subElements)
                        {
                            string values = subElement.Values != null && subElement.Values.Any()
                                ? string.Join(", ", subElement.Values)
                                : "Aucun élément";

                            message += $"**{subElement.SubKey}** : {values} \n";
                        }

                        foreach (var subElement in subElements)
                        {
                            subElement.Values.Clear();
                            subElement.Values.Add("Aucun élément");
                        }

                        SaveRecapList();
                        LoadRecapList();
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
            case "list-items":
                await command.DeferAsync();
                LoadDisplayedItems();

                receiverId = command.Data.Options.ElementAtOrDefault(0)?.Value as string;
                bool listByLine = (bool)command.Data.Options.FirstOrDefault(o => o.Name == "list-by-line")?.Value;

                var filteredItems = displayedItems
                .Where(item => item.receiver == receiverId)
                .GroupBy(item => item.item)
                .Select(group => new
                {
                    ItemName = group.Key,
                    Count = group.Count()
                })
                .OrderBy(group => group.ItemName)
                .ToList();

                if (filteredItems.Any())
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
                            message += listByLine ? "\n":", ";
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
                }
                if (!(command.CommandName.Contains("recap") || command.CommandName.Contains("list")))
                {
                    await command.FollowupAsync(message, options: new RequestOptions { Timeout = 10000 });
                }
                break;
        }
    }

    static async Task MessageReceivedAsync(SocketMessage arg)
    {
        var message = arg as SocketUserMessage;
        var argPos = 0;

        if (message?.Author.IsBot ?? true) return;

        if (message.HasStringPrefix("/", ref argPos))
        {
            var context = new SocketCommandContext(client, message);

            var result = await commandService.ExecuteAsync(context, message.Content.Substring(argPos), services);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"Commande échouée: {result.ErrorReason}");
            }
        }
    }

    static async Task InstallCommandsAsync()
    {
        services = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
    }

    static void StartTracking(bool isLauchedProcess = false)
    {
        if (cts != null)
        {
            cts.Cancel();
        }

        cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(async () =>
        {
            var oldData = new Dictionary<string, string>();
            LoadDisplayedItems();
            var client = new HttpClient();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (isLauchedProcess)
                    {
                        var trackerUrl = url;
                        trackerUrl.Replace("sphere_", "");

                        var html = await client.GetStringAsync(trackerUrl);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var data = new Dictionary<string, string>();
                        var rows = doc.DocumentNode.SelectNodes("//table//tr");

                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("td");

                            if (cells?.Count == 6)
                            {
                                var Name = cells[1].InnerText.Trim();
                                if (!aliasChoices.ContainsKey(Name))
                                {
                                    aliasChoices.Add(Name, Name);
                                }
                            }
                        }

                        await RegisterCommandsAsync();
                    }

                    if (string.IsNullOrEmpty(url))
                    {
                        Console.WriteLine("Aucune URL définie. Arrêt du suivi.");
                        break;
                    }

                    await GetTableDataAsync(url, client, isLauchedProcess);

                    if (isLauchedProcess == true)
                    {
                        isLauchedProcess = false;
                    }

                    await Task.Delay(60000, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Suivi annulé.");
            }
        }, token);
    }

    static async Task GetTableDataAsync(string url, HttpClient client, bool isLauchedProcess)
    {
        LoadReceiverAliases();
        var html = await client.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var data = new Dictionary<string, string>();
        var rows = doc.DocumentNode.SelectNodes("//table//tr");

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");

            if (cells?.Count == 6)
            {
                var sphere = cells[0].InnerText.Trim();
                var finder = cells[1].InnerText.Trim();
                var receiver = cells[2].InnerText.Trim();
                var item = WebUtility.HtmlDecode(cells[3].InnerText.Trim());
                var location = WebUtility.HtmlDecode(cells[4].InnerText.Trim());
                var game = WebUtility.HtmlDecode(cells[5].InnerText.Trim());

                var newItem = new displayedItemsElement
                {
                    sphere = sphere,
                    finder = finder,
                    receiver = receiver,
                    item = item,
                    location = location,
                    game = game
                };

                bool exists = displayedItems.Any(x =>
                x.sphere == newItem.sphere &&
                x.finder == newItem.finder &&
                x.receiver == newItem.receiver &&
                x.item == newItem.item &&
                x.location == newItem.location &&
                x.game == newItem.game);

                if (!exists)
                {
                    displayedItems.Add(newItem);

                    string value;
                    string userId = null;

                    if (finder.Equals(receiver))
                    {
                        value = $"{finder} found their {item} ({location})";
                    }
                    else if (receiverAliases.TryGetValue(receiver, out userId))
                    {
                        value = $"{finder} sent {item} to <@{userId}> {receiver} ({location})";
                    }
                    else
                    {
                        value = $"{finder} sent {item} to {receiver} ({location})";
                    }

                    if (File.Exists(displayedItemsFile))
                    {
                        if (!string.IsNullOrEmpty(userId))
                        {
                            if (!recapList.ContainsKey(userId))
                            {
                                recapList[userId] = new List<SubElement>();
                            }

                            var itemToAdd = recapList[userId].Find(e => e.SubKey == receiver);
                            if (itemToAdd != null)
                            {
                                itemToAdd.Values.Add(item);
                                itemToAdd.Values.Remove("Aucun élément");
                            }
                            else
                            {
                                recapList[userId].Add(new SubElement
                                {
                                    SubKey = receiver,
                                    Values = new List<string> { item }
                                });

                                if (itemToAdd != null)
                                {
                                    itemToAdd.Values.Remove("Aucun élément");
                                };
                            }
                        }
                    }

                    if (File.Exists(displayedItemsFile))
                    {
                        await SendMessageAsync(value);
                    }
                }
            }
        }
        SaveRecapList();
        SaveDisplayedItems();
    }

    static async Task SendMessageAsync(string message)
    {
        if (channelId == 0)
        {
            Console.WriteLine("Aucun canal configuré pour l'envoi des messages.");
            return;
        }

        try
        {
            var channel = client.GetChannel(channelId) as IMessageChannel;

            if (channel == null)
            {
                Console.WriteLine($"Le canal avec l'ID {channelId} est introuvable ou inaccessible.");
                Console.WriteLine("Voici les canaux accessibles par le bot :");

                foreach (var guild in client.Guilds)
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

    static void LoadDisplayedItems()
    {
        if (File.Exists(displayedItemsFile))
        {
            var json = File.ReadAllText(displayedItemsFile);
            displayedItems = JsonConvert.DeserializeObject<List<displayedItemsElement>>(json);
        }
    }

    static void SaveDisplayedItems()
    {
        var json = JsonConvert.SerializeObject(displayedItems);
        File.WriteAllText(displayedItemsFile, json);
    }

    static void LoadReceiverAliases()
    {
        if (receiverAliases != null)
        {
            receiverAliases.Clear();
        }
        if (File.Exists(aliasFile))
        {
            var json = File.ReadAllText(aliasFile);
            receiverAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }

    static void SaveReceiverAliases()
    {
        var json = JsonConvert.SerializeObject(receiverAliases, Formatting.Indented);
        File.WriteAllText(aliasFile, json);
    }

    static void LoadUrlAndChannel()
    {
        url = string.Empty;
        if (File.Exists(urlChannelFile))
        {
            var json = File.ReadAllText(urlChannelFile);
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            url = data.GetValueOrDefault("url", string.Empty);
            channelId = ulong.TryParse(data.GetValueOrDefault("channelId", "0"), out var id) ? id : 0;
        }
    }

    static void SaveUrlAndChannel()
    {
        var data = new Dictionary<string, string>
        {
            { "url", url },
            { "channelId", channelId.ToString() }
        };
        var json = JsonConvert.SerializeObject(data);
        File.WriteAllText(urlChannelFile, json);
    }

    static void SaveRecapList()
    {
        string json = JsonConvert.SerializeObject(recapList);
        File.WriteAllText(recapListFile, json);
    }

    static void LoadRecapList()
    {
        if (recapList != null)
        {
            recapList.Clear();
        }
        if (File.Exists(recapListFile))
        {
            var json = File.ReadAllText(recapListFile);
            recapList = JsonConvert.DeserializeObject<Dictionary<string, List<SubElement>>>(json);
        }
    }

    static void AddMissingRecapUser()
    {
        LoadRecapList();

        foreach (var alias in receiverAliases)
        {
            var receiverId = alias.Value;
            if (!recapList.ContainsKey(receiverId))
            {
                recapList[receiverId] = new List<SubElement>();
            }

            var recapUser = recapList[receiverId].Find(e => e.SubKey == alias.Key);
            if (recapUser == null)
            {
                recapList[receiverId].Add(new SubElement
                {
                    SubKey = alias.Key,
                    Values = new List<string> { "Aucun élément" }
                });
            }
            SaveRecapList();
        }
    }
}