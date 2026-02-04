using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Text.RegularExpressions;

public static class BotCommands
{
    private static readonly SemaphoreSlim RegisterCommandsLock = new(1, 1);
    private static readonly TimeSpan RegisterCommandsDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RegisterCommandsCooldown = TimeSpan.FromSeconds(10);
    private static DateTimeOffset _lastRegisterCommandsAt = DateTimeOffset.MinValue;
    private static bool _handlersRegistered;

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
        await RegisterCommandsLock.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastRegisterCommandsAt < RegisterCommandsCooldown)
            {
                return;
            }

            _lastRegisterCommandsAt = now;

            var commands = SlashCommandDefinitions.GetAll();
            var builtCommands = commands
                .Select(cmd => cmd.Build() as ApplicationCommandProperties)
                .ToArray();

            foreach (var guild in Declare.Client.Guilds)
            {
                Console.WriteLine("Registering commands for guild: " + guild.Name);
                await Declare.Client.Rest.BulkOverwriteGuildCommands(builtCommands, guild.Id);
                await Task.Delay(RegisterCommandsDelay);
            }

            if (!_handlersRegistered)
            {
                Console.WriteLine("Registering command handlers");
                Declare.Client.SlashCommandExecuted += HandleSlashCommandAsync;
                Declare.Client.AutocompleteExecuted += HandleAutocompleteAsync;
                _handlersRegistered = true;
            }
        }
        finally
        {
            RegisterCommandsLock.Release();
        }
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
                Console.WriteLine(string.Format(Resource.BotCommandFailed, result.ErrorReason));
        }
    }

    public static async Task SendMessageAsync(string message, string channelIdStr)
    {
        try
        {
            if (!ulong.TryParse(channelIdStr, out var channelId)) return;

            if (Declare.Client.GetChannel(channelId) is not IMessageChannel channel)
            {
                Console.WriteLine(string.Format(Resource.BotChannelNotFound, channelIdStr));
                foreach (var guild in Declare.Client.Guilds)
                {
                    foreach (var textChannel in guild.TextChannels)
                        Console.WriteLine(string.Format(Resource.BotChannelId, textChannel.Name, textChannel.Id));
                }
                return;
            }

            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(Resource.BotSendingError, ex.Message));
        }
    }

    public static async Task SendFileAsync(string channelId, Stream stream, string fileName, string? caption = null)
    {
        var chan = Declare.Client.GetChannel(ulong.Parse(channelId)) as IMessageChannel
                   ?? throw new InvalidOperationException("Channel introuvable");
        await chan.SendFileAsync(stream, fileName, caption);
    }

    #endregion

    #region Slash Command Handler

    public static async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var isThread = command.Channel is IThreadChannel;
        await command.DeferAsync(ephemeral: isThread);

        _ = Task.Run(async () =>
        {
            try
            {
                var guildUser = command.User as IGuildUser;
                string channelId = command.ChannelId?.ToString() ?? string.Empty;
                string guildId = command.GuildId?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(channelId))
                {
                    await command.FollowupAsync(Resource.BotCommandOutsideServer, ephemeral: true);
                    return;
                }

                string? realAlias = command.Data.Options?.FirstOrDefault()?.Value as string;
                var aliasMatch = !string.IsNullOrEmpty(realAlias) ? Regex.Match(realAlias, @"(?<=\()\s*([^)]+?)\s*(?=\)\s*$)") : Match.Empty;
                var alias = aliasMatch.Success ? aliasMatch.Groups[1].Value : realAlias;

                const int maxLength = 1999;
                string message;

                if (isThread)
                {
                    message = await HandleThreadedCommand(command, guildUser, alias, realAlias, channelId, guildId);
                    await SendPaginatedMessageAsync(command, message, maxLength);
                }
                else
                {
                    message = await HandleChannelCommand(command, guildUser, alias, channelId, guildId);
                    if (!string.IsNullOrWhiteSpace(message))
                        await command.FollowupAsync(message);
                    else
                        await command.FollowupAsync(Resource.BotCommandDone);
                }
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"❌ {ex.Message}", ephemeral: true,
                    options: new RequestOptions { Timeout = 10000 });
            }
        });
    }

    private static async Task<string> HandleThreadedCommand(SocketSlashCommand command, IGuildUser? user, string? alias, string? realAlias, string channelId, string guildId)
    {
        var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");

        if (!checkChannel)
        {
            return Resource.NoUrlRegistered;
        }

        return command.CommandName switch
        {
            "get-aliases" => await AliasClass.GetAlias(channelId, guildId),
            "delete-alias" => await AliasClass.DeleteAlias(command, user, alias, channelId, guildId),
            "add-alias" => await AliasClass.AddAlias(command, alias, channelId, guildId),
            "delete-url" => await UrlClass.DeleteUrl(user, channelId, guildId),
            "recap-all" => await RecapAndCleanClass.RecapAll(command, channelId, guildId),
            "recap" => await RecapAndCleanClass.Recap(command, alias, channelId, guildId),
            "recap-and-clean" => await RecapAndCleanClass.RecapAndClean(command,  alias, channelId, guildId),
            "clean" => await RecapAndCleanClass.Clean(command, alias, channelId, guildId),
            "clean-all" => await RecapAndCleanClass.CleanAll(command, alias, channelId, guildId),
            "list-items" => await HelperClass.ListItems(command, user?.Id.ToString() ?? "", alias, channelId, guildId),
            "hint-from-finder" => await HintClass.HintForFinder(realAlias, channelId, guildId),
            "hint-for-receiver" => await HintClass.HintForReceiver(realAlias, channelId, guildId),
            "status-games-list" => await HelperClass.StatusGameList(channelId, guildId),
            "info" => await HelperClass.Info(channelId, guildId),
            "get-patch" => await HelperClass.GetPatch(command, channelId, guildId),
            "portal-link" => await WebPortalLinkAsync(channelId, guildId, command.User.Id.ToString()),
            "portal-url" => await WebPortalCommandsLinkAsync(guildId, channelId),
            "update-frequency-check" => await ChannelsAndUrlsCommands.UpdateFrequencyCheck(command, channelId, guildId),
            "excluded-item" => await ExcludedItemsCommands.AddExcludedItemAsync(command, alias, channelId, guildId),
            "excluded-item-list" => await ExcludedItemsCommands.GetExcludedItemsByAliasAsync(command, channelId, guildId),
            "delete-excluded-item" => await ExcludedItemsCommands.DeleteExcludedItemAsync(command, channelId, guildId, alias),
            "update-silent-option" => await ChannelsAndUrlsCommands.UpdateSilentOption(command, channelId, guildId),
            _ => Resource.BotCommandChannel
        };
    }

    private static async Task<string> HandleChannelCommand(SocketSlashCommand command, IGuildUser? user, string? alias, string channelId, string guildId)
    {
        return command.CommandName switch
        {
            "add-url" => await UrlClass.AddUrl(command, user, channelId, guildId, (ITextChannel)command.Channel),
            "portal-url" => await WebPortalCommandsLinkAsync(guildId, channelId),
            "list-yamls" => YamlClass.ListYamls(channelId),
            "backup-yamls" => await YamlClass.BackupYamls(command, channelId),
            "delete-yaml" => YamlClass.DeleteYaml(command, channelId),
            "clean-yamls" => YamlClass.CleanYamls(channelId),
            "send-yaml" => await YamlClass.SendYaml(command, channelId),
            "download-template" => await YamlClass.DownloadTemplate(command),
            "list-apworld" => ApworldClass.ListApworld(),
            "apworlds-info" => string.Format(Resource.ApworldInfo, Declare.ApworldInfoSheet),
            "backup-apworld" => await ApworldClass.BackupApworld(command),
            "send-apworld" => await ApworldClass.SendApworld(command),
            "generate" => await GenerationClass.GenerateAsync(command, channelId),
            "test-generate" => await GenerationClass.TestGenerateAsync(command, channelId),
            "generate-with-zip" => await GenerationClass.GenerateWithZip(command, channelId),
            "discord" => Resource.Discord, 
            _ => Resource.BotCommandThread
        };
    }
    private static async Task<string> WebPortalLinkAsync(string channelId, string guildId, string userId)
    {
        if (!Declare.EnableWebPortal)
        {
            return Resource.WebPortalDisabled;
        }

        var portalUrl = await WebPortalPages.EnsureUserPageAsync(guildId, channelId, userId);
        return string.IsNullOrWhiteSpace(portalUrl)
            ? Resource.WebPortalDisabled
            : string.Format(Resource.WebPortalLink, portalUrl);
    }

    private static async Task<string> WebPortalCommandsLinkAsync(string guildId, string channelId)
    {
        if (!Declare.EnableWebPortal)
        {
            return Resource.WebPortalDisabled;
        }

        var portalUrl = await WebPortalPages.EnsureCommandsPageAsync(guildId, channelId);
        return string.IsNullOrWhiteSpace(portalUrl)
            ? Resource.WebPortalDisabled
            : string.Format(Resource.WebPortalCommandsLink, portalUrl);
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
        var addedAlias = interaction.Data.Options?.FirstOrDefault(o => o.Name == "added-alias")?.Value as string ?? "";

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

            "yamlfile" => () => Task.FromResult(
                    Directory.Exists(YamlPath(channelId))
                        ? Directory.GetFiles(YamlPath(channelId), "*.yaml").Select(f => Path.GetFileName(f)!).AsEnumerable()
                        : Enumerable.Empty<string>()),

            "template" => () => Task.FromResult(
                    Directory.Exists(TemplatePath())
                        ? Directory.GetFiles(TemplatePath(), "*.yaml").Select(f => Path.GetFileName(f)!).AsEnumerable()
                        : Enumerable.Empty<string>()),

            "items" => async () => (await ExcludedItemsCommands.GetItemNamesForAliasAsync(guildId, channelId, addedAlias)).AsEnumerable(),

            "delete-items" => async () => (await ExcludedItemsCommands.GetExcludedItemsByAliasAsync(guildId, channelId, addedAlias)).AsEnumerable(),

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
}
