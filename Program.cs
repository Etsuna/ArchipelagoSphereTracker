using Discord;
using Discord.WebSocket;
using Discord.Commands;
using HtmlAgilityPack;
using Newtonsoft.Json;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Text.Encodings.Web;

class Program
{
    private static readonly string displayedItemsFile = "displayedItems.json";
    private static readonly string aliasFile = "aliases.json";
    private static readonly string urlChannelFile = "url_channel.json";
    private static readonly string discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");


    private static string url = string.Empty;
    private static ulong channelId = 0;
    private static Dictionary<string, string> receiverAliases = new Dictionary<string, string>();
    private static CancellationTokenSource cts;
    private static DiscordSocketClient client;
    private static CommandService commandService;
    private static IServiceProvider services;

    static async Task Main(string[] args)
    {
        Env.Load();

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        client = new DiscordSocketClient(config);
        commandService = new CommandService();

        client.Log += LogAsync;
        client.Ready += ReadyAsync;
        client.MessageReceived += MessageReceivedAsync;

        await InstallCommandsAsync();

        LoadReceiverAliases();
        LoadUrlAndChannel();

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
        StartTracking();
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
            .AddOption("alias", ApplicationCommandOptionType.String, "The alias to delete", isRequired: true),

        new SlashCommandBuilder()
            .WithName("add-alias")
            .WithDescription("Add Alias")
            .AddOption("alias", ApplicationCommandOptionType.String, "The alias to add", isRequired: true),

        new SlashCommandBuilder()
            .WithName("add-url")
            .WithDescription("Add Url")
            .AddOption("url", ApplicationCommandOptionType.String, "The URL to track", isRequired: true),

        new SlashCommandBuilder()
            .WithName("delete-url")
            .WithDescription("Delete Url")
    };

        foreach (var guild in client.Guilds)
        {
            var builtCommands = commands.Select(cmd => cmd.Build()).ToArray();
            await client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
        }

        client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        switch (command.CommandName)
        {
            case "get-aliases":
                if (receiverAliases.Count == 0)
                {
                    await command.RespondAsync("Aucun Alias est enregistré.");
                    return;
                }

                var tableMessage = "Voici le tableau des utilisateurs :\n";
                foreach (var kvp in receiverAliases)
                {
                    var user = await client.GetUserAsync(ulong.Parse(kvp.Value));
                    tableMessage += $"| {user.Username} | {kvp.Key} |\n";
                }

                await command.RespondAsync(tableMessage);
                break;

            case "delete-alias":
                var aliasToDelete = command.Data.Options.FirstOrDefault()?.Value as string;
                if (aliasToDelete != null)
                {
                    // Vérifier si l'alias existe et le supprimer
                    if (receiverAliases.ContainsKey(aliasToDelete))
                    {
                        receiverAliases.Remove(aliasToDelete);
                        SaveReceiverAliases();
                        await command.RespondAsync($"Alias '{aliasToDelete}' supprimé.");
                    }
                    else
                    {
                        await command.RespondAsync($"Aucun alias trouvé pour '{aliasToDelete}'.");
                    }
                }
                break;

            case "add-alias":
                var aliasToAdd = command.Data.Options.FirstOrDefault()?.Value as string;
                if (aliasToAdd != null)
                {
                    var receiverId = command.User.Id.ToString();

                    // Vérifier si l'alias existe déjà
                    if (!receiverAliases.ContainsKey(aliasToAdd))
                    {
                        receiverAliases[aliasToAdd] = receiverId;
                        SaveReceiverAliases();
                        await command.RespondAsync($"Alias ajouté : {aliasToAdd} est maintenant associé à <@{receiverId}>.");
                    }
                    else
                    {
                        await command.RespondAsync($"L'alias '{aliasToAdd}' est déjà utilisé par <@{receiverAliases[aliasToAdd]}>.");
                    }
                }
                break;

            case "add-url":
                if(!string.IsNullOrEmpty(url))
                {
                    await command.RespondAsync($"URL déjà définie sur {url}. Supprimez l'url avant d'ajoutez une nouvelle url.");
                    break;
                }
                var newUrl = command.Data.Options.FirstOrDefault()?.Value as string;
                if (!string.IsNullOrEmpty(newUrl))
                {
                    url = newUrl;
                    channelId = command.Channel.Id;
                    SaveUrlAndChannel();
                    await command.RespondAsync($"URL définie sur {url}. Messages configurés pour ce canal.");
                    StartTracking(true);
                }
                break;

            case "delete-url":
                if (string.IsNullOrEmpty(url))
                {
                    await command.RespondAsync($"Aucune URL est définie.");
                    break;
                }
                                
                try
                {
                    if (File.Exists(urlChannelFile))
                    {
                        File.Delete(urlChannelFile);
                    }
                    url = string.Empty;
                    await command.RespondAsync($"URL Supprimée.");
                }
                catch (Exception ex)
                {
                    await command.RespondAsync($"Erreur lors de la suppression du fichier : {ex.Message}");
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

    static void StartTracking(bool skipPreviousItems = false)
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
            var displayedItems = LoadDisplayedItems();
            var client = new HttpClient();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        Console.WriteLine("Aucune URL définie. Arrêt du suivi.");
                        break;
                    }

                    LoadReceiverAliases();
                    var newData = await GetTableDataAsync(url, client);
                    await CompareAndSendChangesAsync(oldData, newData, displayedItems, skipPreviousItems);
                    if (skipPreviousItems == true)
                    {
                        skipPreviousItems = false;
                    }
                    oldData = newData;
                    SaveDisplayedItems(displayedItems);

                    await Task.Delay(60000, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Suivi annulé.");
            }
        }, token);
    }

    static async Task<Dictionary<string, string>> GetTableDataAsync(string url, HttpClient client)
    {
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

                string key = $"{sphere} - {finder} - {receiver} - {item} - {location} - {game}";

                string value = finder.Equals(receiver)
                    ? $"{finder} found their {item} ({location})"
                    : receiverAliases.TryGetValue(receiver, out var userId)
                        ? $"{finder} sent {item} to <@{userId}> {receiver} ({location})"
                        : $"{finder} sent {item} to {receiver} ({location})";

                data[key] = value;
            }
        }

        return data;
    }

    static async Task CompareAndSendChangesAsync(Dictionary<string, string> oldData, Dictionary<string, string> newData, HashSet<string> displayedItems, bool skipPreviousItems)
    {
        foreach (var newItem in newData)
        {
            if (!oldData.ContainsKey(newItem.Key) || oldData[newItem.Key] != newItem.Value)
            {
                if (!displayedItems.Contains(newItem.Key))
                {
                    if (!skipPreviousItems)
                    {
                        await SendMessageAsync(newItem.Value);
                    }
                    displayedItems.Add(newItem.Key);
                }
            }
        }
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

    static HashSet<string> LoadDisplayedItems()
    {
        if (File.Exists(displayedItemsFile))
        {
            var json = File.ReadAllText(displayedItemsFile);
            return JsonConvert.DeserializeObject<HashSet<string>>(json);
        }
        return new HashSet<string>();
    }

    static void SaveDisplayedItems(HashSet<string> displayedItems)
    {
        var json = JsonConvert.SerializeObject(displayedItems);
        File.WriteAllText(displayedItemsFile, json);
    }

    static void LoadReceiverAliases()
    {
        if (File.Exists(aliasFile))
        {
            var json = File.ReadAllText(aliasFile);
            receiverAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }

    static void SaveReceiverAliases()
    {
        // Sauvegarder le dictionnaire en JSON
        var json = JsonConvert.SerializeObject(receiverAliases, Formatting.Indented);
        File.WriteAllText(aliasFile, json);
    }

    static void LoadUrlAndChannel()
    {
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
}