using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Text.RegularExpressions;

public static class BotCommands
{
    #region Setup

    public static Task InstallCommandsAsync()
    {
        Declare.Services = new ServiceCollection()
            .AddSingleton(Declare.Client)
            .BuildServiceProvider();

        return Task.CompletedTask;
    }

    public static async Task RegisterCommandsAsync()
    {
        var commands = SlashCommandDefinitions.GetAll();
        var builtCommands = commands
            .Select(cmd => cmd.Build() as ApplicationCommandProperties)
            .ToArray();

        var registrationTasks = Declare.Client.Guilds
            .Select(guild => Declare.Client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id));

        await Task.WhenAll(registrationTasks);

        Declare.Client.SlashCommandExecuted += HandleSlashCommandAsync;
        Declare.Client.AutocompleteExecuted += HandleAutocompleteAsync;
    }

    #endregion

    #region Message Handling

    public static async Task MessageReceivedAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message || message.Author.IsBot) return;

        int argPos = 0;
        if (message.HasStringPrefix("/", ref argPos))
        {
            var context = new SocketCommandContext(Declare.Client, message);
            var result = await Declare.CommandService.ExecuteAsync(context, message.Content[argPos..], Declare.Services);

            if (!result.IsSuccess)
                Console.WriteLine($"Command failed: {result.ErrorReason}");
        }
    }

    public static async Task SendMessageAsync(string message, string channelIdStr)
    {
        try
        {
            if (!ulong.TryParse(channelIdStr, out var channelId)) return;

            if (Declare.Client.GetChannel(channelId) is not IMessageChannel channel)
            {
                Console.WriteLine($"Channel not found ({channelIdStr})");
                foreach (var guild in Declare.Client.Guilds)
                {
                    foreach (var textChannel in guild.TextChannels)
                        Console.WriteLine($"Channel : {textChannel.Name} (ID: {textChannel.Id})");
                }
                return;
            }

            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sending error: {ex.Message}");
        }
    }

    #endregion

    #region Slash Command Handler

    public static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var guildUser = command.User as IGuildUser;
        string userId = command.User.Id.ToString();
        string? channelId = command.ChannelId.ToString();
        string guildId = command.GuildId?.ToString() ?? "";
        string message = "";
        string? realAlias = command.Data.Options.FirstOrDefault()?.Value as string;
        var aliasMatch = !string.IsNullOrEmpty(realAlias) ? Regex.Match(realAlias, @"\(([^)]+)\)$") : Match.Empty;
        var alias = aliasMatch.Success ? aliasMatch.Groups[1].Value : realAlias;
        const int maxLength = 1999;

        if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(channelId))
        {
            await command.RespondAsync("This command can’t be executed outside of a server.");
            return;
        }

        var isThread = command.Channel is IThreadChannel;
        await command.DeferAsync(ephemeral: isThread);

        if (isThread)
        {
            message = await HandleThreadedCommand(command, guildUser, message, alias, realAlias, channelId, guildId);
            await SendPaginatedMessageAsync(command, message, maxLength);
        }
        else
        {
            message = await HandleChannelCommand(command, guildUser, alias, channelId, guildId);

            if (!string.IsNullOrWhiteSpace(message))
            {
                await command.FollowupAsync(message);
            }
        }
    }

    private static async Task<string> HandleThreadedCommand(SocketSlashCommand command, IGuildUser? user, string message, string? alias, string? realAlias, string channelId, string guildId)
    {
        return command.CommandName switch
        {
            "get-aliases" => await AliasClass.GetAlias(message, channelId, guildId),
            "delete-alias" => await AliasClass.DeleteAlias(command, user, message, alias, channelId, guildId),
            "add-alias" => await AliasClass.AddAlias(command, message, alias, channelId, guildId),
            "delete-url" => await UrlClass.DeleteUrl(user, message, channelId, guildId),
            "recap-all" => await RecapAndCleanClass.RecapAll(command, message, channelId, guildId),
            "recap" => await RecapAndCleanClass.Recap(command, message, alias, channelId, guildId),
            "recap-and-clean" => await RecapAndCleanClass.RecapAndClean(command, message, alias, channelId, guildId),
            "clean" => await RecapAndCleanClass.Clean(command, message, alias, channelId, guildId),
            "clean-all" => await RecapAndCleanClass.CleanAll(command, message, alias, channelId, guildId),
            "list-items" => await HelperClass.ListItems(command, user?.Id.ToString() ?? "", message, alias, channelId, guildId),
            "hint-from-finder" => await HintClass.HintForFinder(message, realAlias, channelId, guildId),
            "hint-for-receiver" => await HintClass.HintForReceiver(message, realAlias, channelId, guildId),
            "status-games-list" => await HelperClass.StatusGameList(message, channelId, guildId),
            "info" => await HelperClass.Info(message, channelId, guildId),
            "get-patch" => await HelperClass.GetPatch(command, message, channelId, guildId),
            _ => "This command must be executed in a channel."
        };
    }

    private static async Task<string> HandleChannelCommand(SocketSlashCommand command, IGuildUser? user, string? alias, string channelId, string guildId)
    {
        return command.CommandName switch
        {
            "add-url" => await UrlClass.AddUrl(command, user, "", channelId, guildId, (ITextChannel)command.Channel),
            "list-yamls" => YamlClass.ListYamls(channelId),
            "backup-yamls" => await YamlClass.BackupYamls(command, "", channelId),
            "delete-yaml" => YamlClass.DeleteYaml(command, "", channelId),
            "clean-yamls" => YamlClass.CleanYamls(channelId),
            "send-yaml" => await YamlClass.SendYaml(command, "", channelId),
            "download-template" => await YamlClass.DownloadTemplate(command, ""),
            "list-apworld" => ApworldClass.ListApworld(""),
            "apworlds-info" => await ApworldClass.ApworldsInfo(command, ""),
            "backup-apworld" => await ApworldClass.BackupApworld(command, ""),
            "send-apworld" => await ApworldClass.SendApworld(command, ""),
            "generate" => GenerationClass.Generate(command, "", channelId),
            "test-generate" => GenerationClass.TestGenerate(command, "", channelId),
            "generate-with-zip" => await GenerationClass.GenerateWithZip(command, "", channelId),
            _ => "This command must be executed in a thread."
        };
    }

    private static async Task SendPaginatedMessageAsync(SocketSlashCommand command, string message, int maxLength)
    {
        while (message.Length > maxLength)
        {
            var splitIndex = message.LastIndexOf("\n", maxLength);
            if (splitIndex < 0) splitIndex = maxLength;

            var part = message[..splitIndex].Trim();
            message = message[(splitIndex + 1)..].Trim();

            await command.FollowupAsync(part, ephemeral: true, options: new RequestOptions { Timeout = 10000 });
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            await command.FollowupAsync(message, ephemeral: true, options: new RequestOptions { Timeout = 10000 });
        }
    }

    #endregion

    #region Autocomplete Handler

    public static async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var name = interaction.Data.Current.Name;
        var input = interaction.Data.Current.Value?.ToString()?.ToLower() ?? "";
        var channelId = interaction.ChannelId.ToString();
        var guildId = interaction.GuildId?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(guildId))
        {
            await interaction.RespondAsync(Array.Empty<AutocompleteResult>());
            return;
        }

        Func<Task<IEnumerable<string>>> fetcher = name switch
        {
            "alias" => async () => (await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId)).AsEnumerable(),

            "added-alias" => async () =>
                (await ReceiverAliasesCommands.GetReceiver(guildId, channelId)).Distinct(StringComparer.OrdinalIgnoreCase).AsEnumerable(),

            "file" => () => Task.FromResult(
                    Directory.Exists(YamlPath(channelId))
                        ? Directory.GetFiles(YamlPath(channelId), "*.yaml").Select(f => Path.GetFileName(f)!).AsEnumerable()
                        : Enumerable.Empty<string>()),

            "template" => () => Task.FromResult(
                    Directory.Exists(TemplatePath())
                        ? Directory.GetFiles(TemplatePath(), "*.yaml").Select(f => Path.GetFileName(f)!).AsEnumerable()
                        : Enumerable.Empty<string>()),

            "apworldsinfo" => async () => (await ApWorldListCommands.GetAllTitles()).AsEnumerable(),

            _ => () => Task.FromResult(Enumerable.Empty<string>())
        };

        var allItems = (await fetcher()).ToList();
        var results = FilterWithPagination(allItems, input);

        await interaction.RespondAsync(results);
    }

    private static string YamlPath(string channelId) => Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", channelId, "yaml");
    private static string TemplatePath() => Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", "Templates");

    private static AutocompleteResult[] FilterWithPagination(List<string> all, string input)
    {
        int pageSize = 25;
        int page = 1;

        if (input.StartsWith(">") && int.TryParse(input[1..], out var p) && p > 0)
        {
            page = p;
            input = "";
        }

        var filtered = all
            .Where(x => x.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AutocompleteResult(x, x))
            .ToArray();

        return filtered;
    }

    #endregion

    #region Options Builders

    public static SlashCommandOptionBuilder BuildListItemsOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("list-by-line")
            .WithDescription("Display items line by line (true) or comma separated (false).")
            .WithType(ApplicationCommandOptionType.Boolean)
            .WithRequired(true);
    }

    #endregion
}
