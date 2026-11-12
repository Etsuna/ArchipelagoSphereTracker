using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;

public static class PreparationDiscord
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BoardGates = new();
    private static readonly MemoryCache CreationChoices = new(new MemoryCacheOptions { SizeLimit = 256 });
    private sealed record CreationChoice(ulong Guild, ulong Channel, ulong User, string? Title, string? WebHost, string? Hours,
        bool? TrackInThread = null, ulong? PreparationChannel = null);
    private static int _subscribed;
    private static int _schedulerStarted;
    public static void StartScheduler()
    {
        if (!Declare.IsArchipelagoMode || Interlocked.Exchange(ref _schedulerStarted,1) != 0) return;
        _ = Task.Run(async () => {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    if (Declare.Client.ConnectionState == ConnectionState.Connected)
                        await PreparationService.Instance.RunSchedulesOnceAsync(SendScheduledNoticeAsync);
                }
                catch (Exception error) { Console.WriteLine($"[Preparation] Scheduler retry ({error.GetType().Name})."); }
            } while (await timer.WaitForNextTickAsync());
        });
    }

    private static async Task SendScheduledNoticeAsync(PreparationRoom room, IReadOnlyList<PreparationReminder> users, bool due)
    {
        var target = Declare.Client.GetChannel(ulong.Parse(room.ChannelId)) as IMessageChannel
            ?? await Declare.Client.Rest.GetChannelAsync(ulong.Parse(room.ChannelId)) as IMessageChannel
            ?? throw new PreparationException(Resource.PrepSourceChannel);
        if (due)
            await target.SendMessageAsync(room.LastError ?? string.Format(Text("PrepTimerStarted"),room.Participants.Count,users.Count), allowedMentions:AllowedMentions.None);
        else
            foreach (var batch in users.Chunk(40))
            {
                var ids = batch.Select(user => ulong.Parse(user.UserId)).ToList();
                await target.SendMessageAsync(string.Format(Text("PrepTimerReminder"),string.Join(" ",ids.Select(id => $"<@{id}>")),room.Schedule!.Deadline.ToUnixTimeSeconds()),
                    allowedMentions:new AllowedMentions { UserIds=ids });
            }
    }
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 0)
        {
            PreparationService.Instance.Changed += UpdateBoardAsync;
            PreparationService.Instance.Removed += ClearBoardAsync;
            PreparationService.Instance.RegistrationsClosed += RemindAfterCloseAsync;
            PreparationService.Instance.EveryoneReady += NotifyManagersAsync;
        }
    }

    public static async Task RefreshBoardsAsync()
    {
        if (!Declare.IsArchipelagoMode) return;
        var rooms = new List<(string Guild, string Channel)>();
        await using (var connection = await Db.OpenReadAsync())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT GuildId,ChannelId FROM RoomPreparations WHERE State!='Cancelled'";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rooms.Add((reader[0].ToString()!, reader[1].ToString()!));
        }
        foreach (var (guild, channel) in rooms)
            try { if (ulong.TryParse(guild, out var id) && Declare.Client.GetGuild(id) != null) await UpdateBoardAsync(guild, channel); }
            catch { /* A removed thread must not prevent other preparations from being restored. */ }
    }

    public static SlashCommandBuilder Command()
    {
        var action = new SlashCommandOptionBuilder().WithName(Resource.PrepOptionAction)
            .WithDescription(Resource.PrepActionDescription).WithType(ApplicationCommandOptionType.String).WithRequired(false);
        foreach (var (label, value) in new[] {
            (Resource.PrepCreate,"create"), (Resource.PrepRefresh,"status"), (Resource.PrepJoin,"join"),
            (Resource.PrepLeave,"leave"), (Resource.PrepReady,"ready"), (Resource.PrepNotReady,"not-ready"),
            (Resource.PrepClose,"close"), (Resource.PrepReopen,"reopen"), (Resource.PrepTest,"test"),
            (Resource.PrepGenerate,"generate"), (Resource.PrepCancel,"cancel"), (Text("PrepRemind"),"remind"),
            (Text("PrepRetryDelivery"),"deliver"),
            (Text("PrepTimer"),"schedule"),
            (Text("PrepFinishHostingManually"),"manual-delivery"),
            (Resource.PrepInvite,"invite"), (Resource.PrepRemove,"remove") })
            action.AddChoice(label, value);
        return new SlashCommandBuilder().WithName("organize-room").WithDescription(Resource.PrepDescription)
            .AddOption(action)
            .AddOption(Resource.PrepOptionName, ApplicationCommandOptionType.String, Resource.PrepOptionNameDescription, isRequired:false)
            .AddOption(Resource.PrepOptionFile, ApplicationCommandOptionType.Attachment, Resource.PrepFileDescription, isRequired:false)
            .AddOption(Resource.PrepOptionUser, ApplicationCommandOptionType.User, Resource.PrepUserDescription, isRequired:false)
            .AddOption(Resource.PrepOptionYamlName, ApplicationCommandOptionType.String, Resource.PrepYamlNameDescription, isRequired:false)
            .AddOption(Text("PrepOptionHours"), ApplicationCommandOptionType.Integer, Text("PrepTimerHelp"), isRequired:false, minValue:0, maxValue:720)
            .AddOption(new SlashCommandOptionBuilder().WithName("channel").WithDescription(Text("PrepExistingRoom"))
                .WithType(ApplicationCommandOptionType.Channel).AddChannelType(ChannelType.Text))
            .AddOption(new SlashCommandOptionBuilder().WithName("tracking").WithDescription(Text("PrepTrackingDestination"))
                .WithType(ApplicationCommandOptionType.String)
                .AddChoice(Text("PrepTrackInChannel"),"Channel").AddChoice(Text("PrepTrackInThread"),"Thread"))
            .AddOption("webhost",ApplicationCommandOptionType.String,Text("PrepWebHost"),isRequired:false);
    }

    private static async Task<AstAuthorizationContext?> AuthorizeAsync(ulong guild, ulong channel, IUser user, bool creating = false)
    {
        if (!Declare.IsArchipelagoMode) return null;
        var context = await AstAuthorizationService.CreateDiscordContextAsync(guild.ToString(), channel.ToString(), user.Id, user as IGuildUser);
        return context != null && AstAuthorizationService.CanUseArchipelagoTools(context) &&
            (!creating || AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, context)) ? context : null;
    }

    public static async Task HandleSlashAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not { } guild || command.ChannelId is not { } channel)
        { await command.RespondAsync(Resource.BotCommandOutsideServer, ephemeral:true); return; }
        if (await AuthorizeAsync(guild, channel, command.User) == null)
        { await command.RespondAsync(Declare.IsArchipelagoMode ? AstAuthorizationService.DeniedMessage : Resource.PrepArchOnly, ephemeral:true); return; }
        object? Value(string key) => command.Data.Options.FirstOrDefault(option => option.Name == key)?.Value;
        var attachment = Value(Resource.PrepOptionFile) as IAttachment;
        var title = Value(Resource.PrepOptionName) as string;
        var target = Value("channel") as IChannel;
        var webHost = Value("webhost") as string;
        var timer = Value(Text("PrepOptionHours"))?.ToString();
        bool? trackInThread = Value("tracking") is string tracking ? tracking == "Thread" : null;
        var action = Value(Resource.PrepOptionAction) as string;
        if (action == null)
        {
            if (attachment != null) action = "upload";
            else if (!string.IsNullOrWhiteSpace(title) || target != null || webHost != null || trackInThread != null)
                action = "create";
            else action = await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString()) == null ? "create" : "status";
        }
        if (action == "create")
        {
            var choice = new CreationChoice(guild, channel, command.User.Id, title, webHost, timer, trackInThread);
            if (target == null)
                await ShowDestinationChoiceAsync(command, choice);
            else
                await ContinueCreationAsync(command, choice with { PreparationChannel = target.Id });
            return;
        }
        if (await TryPromptForWebHostSessionAsync(command, guild, channel, action, hours:timer)) return;
        await command.DeferAsync(ephemeral:true);
        try
        {
            string message;
            if (action == "upload" && attachment != null)
            {
                message = await UploadAttachmentAsync(guild.ToString(), channel.ToString(), command.User as IGuildUser, attachment);
            }
            else if (action == "schedule")
            {
                var hours = PreparationService.ParseTimerHours(Value(Text("PrepOptionHours"))?.ToString()) ?? throw new PreparationException(Text("PrepTimerInvalid"));
                await PreparationService.Instance.ScheduleAsync(guild.ToString(),channel.ToString(),command.User.Id.ToString(),hours);
                message = Resource.PrepSaved;
            }
            else if (action == "remove")
            {
                var room = await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString());
                var file = room?.Files.FirstOrDefault(file => file.UserId == command.User.Id.ToString() && file.FileName == Value(Resource.PrepOptionYamlName) as string)
                    ?? throw new PreparationException(Resource.PrepFileNotFound);
                await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, command.User.Id.ToString(), guild.ToString(), channel.ToString(), SecurityAuditAction.YamlDelete);
                await PreparationService.Instance.RemoveFileAsync(guild.ToString(), channel.ToString(), command.User.Id.ToString(), file.Id);
                audit.Succeed(); message = Resource.PrepSaved;
            }
            else if (action == "invite")
            {
                var invitee = Value(Resource.PrepOptionUser) as IGuildUser ?? throw new PreparationException(Resource.PrepInvalidSettings);
                await InviteAsync(guild.ToString(), channel.ToString(), command.User.Id.ToString(), invitee.Id.ToString());
                message = Resource.PrepSaved;
            }
            else message = await ExecuteAsync(guild.ToString(), channel.ToString(), command.User as IGuildUser, action, Value(Resource.PrepOptionUrl) as string);
            await command.FollowupAsync(message, ephemeral:true, allowedMentions:AllowedMentions.None);
        }
        catch (PreparationException error) { await command.FollowupAsync(error.Message, ephemeral:true, allowedMentions:AllowedMentions.None); }
        catch { await command.FollowupAsync(Resource.PrepGenerationFailed, ephemeral:true); }
    }

    public static async Task HandleButtonAsync(SocketMessageComponent component)
    {
        if (component.Data.CustomId.StartsWith("astprep-webhost:", StringComparison.Ordinal))
        {
            await OpenWebHostSessionModalAsync(component);
            return;
        }
        if (component.Data.CustomId == "astprep-create")
        {
            if (component.GuildId is { } guild && component.ChannelId is { } sourceChannel)
                await ShowDestinationChoiceAsync(component, new(guild, sourceChannel, component.User.Id, null, null, null));
            return;
        }
        if (component.Data.CustomId.StartsWith("astprep-tracking:", StringComparison.Ordinal))
        {
            var trackingParts = component.Data.CustomId.Split(':');
            var choice = trackingParts.Length == 3 ? TakeChoice(component, trackingParts[2]) : null;
            if (choice?.PreparationChannel == null)
                await component.RespondAsync(Resource.AstSetupThisSessionExpiredOrDoesNotBelongToYou, ephemeral:true);
            else
                await ContinueCreationAsync(component, choice with { TrackInThread = trackingParts[1] == "Thread" });
            return;
        }
        var parts = component.Data.CustomId.Split(':');
        if (parts.Length != 3 || parts[0] != "astprep" || !Guid.TryParseExact(parts[1], "N", out _)) return;
        if (component.GuildId is { } hostGuild && component.ChannelId is { } hostChannel &&
            await TryPromptForWebHostSessionAsync(component, hostGuild, hostChannel, parts[2], expectedRoom:parts[1])) return;
        if (parts[2] == "schedule")
        {
            if (component.GuildId is not { } guild || component.ChannelId is not { } channel ||
                await AuthorizeAsync(guild,channel,component.User) == null ||
                !await PreparationService.Instance.CanManageAsync(guild.ToString(),channel.ToString(),component.User.Id.ToString()) ||
                (await PreparationService.Instance.GetAsync(guild.ToString(),channel.ToString()))?.Id != parts[1])
            { await component.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral:true); return; }
            await component.RespondWithModalAsync(new ModalBuilder().WithTitle(Text("PrepTimer")).WithCustomId("astprep-timer:"+parts[1])
                .AddTextInput(Text("PrepTimerHours"),"hours",TextInputStyle.Short,placeholder:"72",required:true,maxLength:3).Build());
            return;
        }
        await component.DeferAsync(ephemeral:true);
        try
        {
            if (component.GuildId is not { } guild || component.ChannelId is not { } channel || await AuthorizeAsync(guild, channel, component.User) == null)
                throw new PreparationException(AstAuthorizationService.DeniedMessage);
            var room = await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString());
            if (room == null || room.Id != parts[1]) throw new PreparationException(Resource.PrepNotFound);
            var action = parts[2];
            if (action == "ready-toggle") action = room.Participants.Any(player => player.UserId == component.User.Id.ToString() && player.Ready) ? "not-ready" : "ready";
            if (action == "close-toggle") action = room.State == PreparationState.Open ? "close" : "reopen";
            var message = await ExecuteAsync(guild.ToString(), channel.ToString(), component.User as IGuildUser, action);
            await component.FollowupAsync(message, ephemeral:true, allowedMentions:AllowedMentions.None);
        }
        catch (PreparationException error) { await component.FollowupAsync(error.Message, ephemeral:true, allowedMentions:AllowedMentions.None); }
        catch { await component.FollowupAsync(Resource.PrepGenerationFailed, ephemeral:true); }
    }

    public static async Task<string> UploadAttachmentAsync(string guild, string channel, IGuildUser? actor, IAttachment attachment)
    {
        if (!Declare.IsArchipelagoMode) throw new PreparationException(Resource.PrepArchOnly);
        if (actor == null) throw new PreparationException(AstAuthorizationService.DeniedMessage);
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(guild, channel, actor.Id, actor);
        if (authorization == null || !AstAuthorizationService.CanUseArchipelagoTools(authorization)) throw new PreparationException(AstAuthorizationService.DeniedMessage);
        await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, actor.Id.ToString(), guild, channel, SecurityAuditAction.YamlUpload);
        var limit = Path.GetExtension(attachment.Filename).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? PreparationUploadReader.MaximumZipBytes : PreparationService.MaximumYamlBytes;
        if (attachment.Size <= 0 || attachment.Size > limit) throw new PreparationException(limit == PreparationUploadReader.MaximumZipBytes
            ? Text("PrepInvalidYamlZip") : Resource.PrepInvalidYaml);
        using var response = await TrackingDataManager.Http.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var message = await PreparationService.Instance.UploadStreamAsync(guild, channel, actor.Id.ToString(), attachment.Filename, stream);
        audit.Succeed();
        return message;
    }

    private static string StoreChoice(CreationChoice choice)
    {
        var id = Guid.NewGuid().ToString("N");
        CreationChoices.Set(id, choice,
            new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromMinutes(10)));
        return id;
    }

    private static async Task ShowDestinationChoiceAsync(IDiscordInteraction interaction, CreationChoice choice)
    {
        if (await AuthorizeAsync(choice.Guild,choice.Channel,interaction.User,creating:true) == null)
        { await interaction.RespondAsync(AstAuthorizationService.DeniedMessage,ephemeral:true); return; }
        var id = StoreChoice(choice);
        var components = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder().WithCustomId($"astprep-existing-room:{id}")
                .WithPlaceholder(Text("PrepExistingRoom")).WithType(ComponentType.ChannelSelect)
                .WithChannelTypes(ChannelType.Text));
        await interaction.RespondAsync(Text("PrepDestination"),components:components.Build(),ephemeral:true);
    }

    public static async Task HandleSelectAsync(SocketMessageComponent component)
    {
        const string prefix = "astprep-existing-room:";
        if (!component.Data.CustomId.StartsWith(prefix, StringComparison.Ordinal) ||
            !ulong.TryParse(component.Data.Values.SingleOrDefault(), out var selected)) return;
        var choice = TakeChoice(component, component.Data.CustomId[prefix.Length..]);
        if (choice == null)
            await component.RespondAsync(Resource.AstSetupThisSessionExpiredOrDoesNotBelongToYou, ephemeral:true);
        else
            await ContinueCreationAsync(component, choice with { PreparationChannel = selected });
    }

    private static CreationChoice? TakeChoice(SocketMessageComponent component, string id)
    {
        lock (CreationChoices)
        {
            if (!CreationChoices.TryGetValue(id, out CreationChoice? choice) || choice == null ||
                choice.Guild != component.GuildId || choice.Channel != component.ChannelId || choice.User != component.User.Id)
                return null;
            CreationChoices.Remove(id);
            return choice;
        }
    }

    private static async Task ContinueCreationAsync(IDiscordInteraction interaction, CreationChoice choice)
    {
        if (await AuthorizeAsync(choice.Guild, choice.Channel, interaction.User, creating:true) == null)
        { await interaction.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral:true); return; }
        var target = choice.PreparationChannel!.Value;
        if (choice.TrackInThread == null)
        {
            var id = StoreChoice(choice);
            var components = new ComponentBuilder()
                .WithButton(Text("PrepTrackInChannel"),$"astprep-tracking:Channel:{id}")
                .WithButton(Text("PrepTrackInThread"),$"astprep-tracking:Thread:{id}");
            await interaction.RespondAsync(Text("PrepTrackingDestination"), components:components.Build(), ephemeral:true);
            return;
        }
        var trackInThread = choice.TrackInThread.Value;
        if (string.IsNullOrWhiteSpace(choice.Title))
        {
            var modal = new ModalBuilder().WithTitle(Resource.PrepTitle)
                .WithCustomId($"astprep-modal:{target}:{(trackInThread ? "Thread" : "Channel")}")
                .AddTextInput(Resource.PrepName, "name", TextInputStyle.Short, maxLength:80, required:true)
                .AddTextInput(Text("PrepTimerHours"),"hours",TextInputStyle.Short,placeholder:"72",maxLength:3,required:false,value:choice.Hours)
                .AddTextInput(Text("PrepWebHost"),"webhost",TextInputStyle.Short,placeholder:"https://archipelago.gg",maxLength:250,required:false,value:choice.WebHost);
            await interaction.RespondWithModalAsync(modal.Build());
            return;
        }
        await interaction.DeferAsync(ephemeral:true);
        try
        {
            var room = await CreateAsync(choice.Guild.ToString(), target.ToString(), interaction.User as IGuildUser,
                choice.Title, PreparationService.ParseTimerHours(choice.Hours), choice.WebHost, trackInThread);
            await interaction.FollowupAsync(string.Format(Resource.PrepCreated, room.ChannelId), ephemeral:true, allowedMentions:AllowedMentions.None);
        }
        catch (PreparationException error)
        {
            await interaction.FollowupAsync(error.Message, ephemeral:true, allowedMentions:AllowedMentions.None);
        }
        catch
        {
            await interaction.FollowupAsync(Resource.PrepGenerationFailed, ephemeral:true);
        }
    }

    private static async Task<bool> TryPromptForWebHostSessionAsync(IDiscordInteraction interaction, ulong guild, ulong channel,
        string action, string? expectedRoom = null, string? hours = null)
    {
        if (action is not ("generate" or "schedule" or "deliver") || (action == "schedule" && hours == "0")) return false;
        var service = PreparationService.Instance;
        var room = await service.GetAsync(guild.ToString(), channel.ToString());
        if (room is not { NeedsWebHostSession: true } || (expectedRoom != null && room.Id != expectedRoom)) return false;
        if (action == "deliver" &&
            (room.State != PreparationState.Generated || await service.GetHostingSessionAsync(room) != null)) return false;
        if (await AuthorizeAsync(guild, channel, interaction.User) == null ||
            !await service.CanManageAsync(room.GuildId, room.ChannelId, interaction.User.Id.ToString()))
        {
            await interaction.RespondAsync(Resource.PrepOwnerOnly, ephemeral:true);
            return true;
        }

        var components = new ComponentBuilder()
            .WithButton(Resource.PrepWebHostSessionLink, style:ButtonStyle.Link, url:PreparationWebHost.SessionPage(room.Hosting!.Origin))
            .WithButton(Resource.PrepWebHostSessionEnter, $"astprep-webhost:{action}:{room.Id}:{hours}", ButtonStyle.Primary);
        await interaction.RespondAsync(Resource.PrepWebHostSessionPrompt, components:components.Build(), ephemeral:true);
        return true;
    }

    private static async Task OpenWebHostSessionModalAsync(SocketMessageComponent component)
    {
        var parts = component.Data.CustomId.Split(':');
        if (parts.Length != 4 || parts[1] is not ("generate" or "schedule" or "deliver") ||
            component.GuildId is not { } guild || component.ChannelId is not { } channel ||
            await AuthorizeAsync(guild, channel, component.User) == null ||
            (await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString())) is not { NeedsWebHostSession:true } room ||
            room.Id != parts[2] || !await PreparationService.Instance.CanManageAsync(room.GuildId, room.ChannelId, component.User.Id.ToString()))
        {
            await component.RespondAsync(Resource.AstSetupThisSessionExpiredOrDoesNotBelongToYou, ephemeral:true);
            return;
        }

        var settingTimer = parts[1] == "schedule";
        var modal = new ModalBuilder().WithTitle(Resource.PrepWebHostSessionLabel)
            .WithCustomId($"astprep-webhost:{parts[1]}:{room.Id}");
        if (settingTimer)
            modal.AddTextInput(Resource.PrepTimerHours, "hours", placeholder:"72", maxLength:3, required:true,
                value:parts[3].Length > 0 ? parts[3] : null);
        modal.AddTextInput(Resource.PrepWebHostSessionLabel, "webhost-user-id", maxLength:36, required:!settingTimer);
        await component.RespondWithModalAsync(modal.Build());
    }

    public static async Task HandleModalAsync(SocketModal modal)
    {
        if (modal.Data.CustomId != "astprep-modal" && !modal.Data.CustomId.StartsWith("astprep-modal:",StringComparison.Ordinal) &&
            !modal.Data.CustomId.StartsWith("astprep-timer:",StringComparison.Ordinal) &&
            !modal.Data.CustomId.StartsWith("astprep-webhost:",StringComparison.Ordinal)) return;
        await modal.DeferAsync(ephemeral:true);
        try
        {
            var settingTimer = modal.Data.CustomId.StartsWith("astprep-timer:",StringComparison.Ordinal);
            var webHostModal = modal.Data.CustomId.StartsWith("astprep-webhost:",StringComparison.Ordinal);
            if (modal.GuildId is not { } guild || modal.ChannelId is not { } channel ||
                await AuthorizeAsync(guild, channel, modal.User, creating:!settingTimer && !webHostModal) == null)
                throw new PreparationException(AstAuthorizationService.DeniedMessage);
            string Value(string name) => modal.Data.Components.FirstOrDefault(field => field.CustomId == name)?.Value ?? string.Empty;
            if (webHostModal)
            {
                var parts = modal.Data.CustomId.Split(':');
                if (parts.Length != 3 || (await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString()))?.Id != parts[2])
                    throw new PreparationException(Resource.PrepNotFound);
                string response;
                if (parts[1] == "schedule")
                {
                    var hostedHours = PreparationService.ParseTimerHours(Value("hours")) ??
                        throw new PreparationException(Text("PrepTimerInvalid"));
                    await PreparationService.Instance.ScheduleAsync(guild.ToString(), channel.ToString(), modal.User.Id.ToString(),
                        hostedHours, webHostUserId:Value("webhost-user-id"), expectedRoom:parts[2]);
                    response = Resource.PrepSaved;
                }
                else if (parts[1] is "generate" or "deliver")
                    response = await ExecuteAsync(guild.ToString(), channel.ToString(), modal.User as IGuildUser, parts[1],
                        webHostUserId:Value("webhost-user-id"), expectedRoom:parts[2]);
                else throw new PreparationException(Resource.PrepInvalidSettings);
                await modal.FollowupAsync(response, ephemeral:true, allowedMentions:AllowedMentions.None);
                return;
            }
            var hours = PreparationService.ParseTimerHours(Value("hours"));
            if (settingTimer)
            {
                if ((await PreparationService.Instance.GetAsync(guild.ToString(),channel.ToString()))?.Id != modal.Data.CustomId[14..])
                    throw new PreparationException(Resource.PrepNotFound);
                await PreparationService.Instance.ScheduleAsync(guild.ToString(),channel.ToString(),modal.User.Id.ToString(),hours);
                await modal.FollowupAsync(Resource.PrepSaved,ephemeral:true);
                return;
            }
            var destination = modal.Data.CustomId.Split(':');
            if (destination.Length != 3)
                throw new PreparationException(Resource.AstSetupThisSessionExpiredOrDoesNotBelongToYou);
            var room = await CreateAsync(guild.ToString(), destination[1], modal.User as IGuildUser,
                Value("name"), hours, Value("webhost"), destination[2] == "Thread");
            await modal.FollowupAsync(string.Format(Resource.PrepCreated, room.ChannelId), ephemeral:true);
        }
        catch (PreparationException error) { await modal.FollowupAsync(error.Message, ephemeral:true, allowedMentions:AllowedMentions.None); }
        catch { await modal.FollowupAsync(Resource.PrepGenerationFailed, ephemeral:true); }
    }

    internal static bool CanPostBoard(IGuildUser bot, ITextChannel channel)
    {
        var permissions = bot.GetPermissions(channel);
        return permissions.ViewChannel && permissions.SendMessages && permissions.EmbedLinks && permissions.ReadMessageHistory;
    }

    public static async Task<PreparationRoom> CreateAsync(string guild, string channelId, IGuildUser? owner, string title,
        int? timerHours = null, string? webHost = null, bool trackInThread = false)
    {
        if (!Declare.IsArchipelagoMode) throw new PreparationException(Resource.PrepArchOnly);
        if (owner == null || !ulong.TryParse(channelId, out var id) ||
            (Declare.Client.GetChannel(id) as ITextChannel ?? await Declare.Client.Rest.GetChannelAsync(id) as ITextChannel) is not { ChannelType: ChannelType.Text } channel ||
            channel.GuildId.ToString() != guild)
            throw new PreparationException(Resource.PrepSourceChannel);
        channelId = channel.Id.ToString();
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(guild, channelId, owner.Id, owner);
        if (authorization == null || !AstAuthorizationService.CanUseArchipelagoTools(authorization) || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
            throw new PreparationException(AstAuthorizationService.DeniedMessage);
        if (Declare.Client.GetGuild(channel.GuildId)?.CurrentUser is not { } bot || !CanPostBoard(bot, channel))
            throw new PreparationException(Resource.PrepChannelUnavailable);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 80) throw new PreparationException(Resource.PrepInvalidSettings);
        if (timerHours is < 0 or > 720) throw new PreparationException(Text("PrepTimerInvalid"));
        var hostingOrigin = PreparationWebHost.ParseOrigin(webHost);
        if (hostingOrigin != null && !trackInThread &&
            await DatabaseCommands.CheckIfChannelExistsAsync(guild,channelId,"ChannelsAndUrlsTable"))
            throw new PreparationException(Resource.URLAlreadySet);
        await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, owner.Id.ToString(), guild, channelId, SecurityAuditAction.RoomAdd);
        var service = PreparationService.Instance;
        await service.CreateAsync(guild, channelId, channelId, owner.Id.ToString(), title, isPrivate:false,
            timerHours:timerHours, webHost:webHost, trackInThread:trackInThread);
        await UpdateBoardAsync(guild, channelId);
        audit.Succeed();
        return (await service.GetAsync(guild, channelId))!;
    }

    public static async Task<string> ExecuteAsync(string guild, string channel, IGuildUser? actor, string action, string? url = null, bool skipBalancing = false,
        SecurityAuditSource source = SecurityAuditSource.Discord, string? webHostUserId = null, string? expectedRoom = null)
    {
        if (!Declare.IsArchipelagoMode) throw new PreparationException(Resource.PrepArchOnly);
        if (actor == null) throw new PreparationException(AstAuthorizationService.DeniedMessage);
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(guild, channel, actor.Id, actor);
        if (authorization == null || !AstAuthorizationService.CanUseArchipelagoTools(authorization)) throw new PreparationException(AstAuthorizationService.DeniedMessage);
        var service = PreparationService.Instance;
        var user = actor.Id.ToString();
        if (action == "portal")
        {
            return await WebPortalPages.EnsurePreparationPageAsync(guild, channel, user) ?? Resource.WebPortalUnavailable;
        }
        if (action == "status")
        {
            var room = await service.GetAsync(guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
            await UpdateBoardAsync(guild, channel);
            var details = string.Join("\n", room.Files.Where(file => file.UserId == user).Select(file =>
                $"{Safe(file.FileName)} : {PreparationTexts.Validation(file.Validation)}" + (file.Error != null ? "\n" + Safe(file.Error[..Math.Min(file.Error.Length, 350)]) : "")));
            return (await WebPortalPages.EnsurePreparationPageAsync(guild, channel, user) ?? PreparationTexts.State(room.State)) +
                "\n" + details[..Math.Min(details.Length, 1500)];
        }
        await using var audit = await SecurityAuditScope.StartAsync(source, user, guild, channel,
            action is "generate" or "test" ? SecurityAuditAction.Generation : SecurityAuditAction.RoomSettingsUpdate);
        var message = Resource.PrepSaved;
        if (action == "remind") await RemindAsync(guild, channel, actor.Id.ToString());
        else if (action == "deliver") await RetryDeliveryAsync(guild, channel, actor.Id.ToString(), webHostUserId, expectedRoom);
        else if (action == "manual-delivery") await service.FinishHostingManuallyAsync(guild,channel,user);
        else message = await service.ApplyActionAsync(guild, channel, user, actor.Nickname ?? actor.Username, action, skipBalancing,
            webHostUserId, expectedRoom);
        audit.Succeed();
        return message;
    }

    public static async Task AttachAsync(string guild, string channel, IGuildUser actor, string? url)
    {
        var service = PreparationService.Instance;
        var room = await service.GetAsync(guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
        if (string.IsNullOrWhiteSpace(url)) throw new PreparationException(Resource.URLNotValid);
        if (!ulong.TryParse(room.ParentChannelId, out var parentId) || Declare.Client.GetChannel(parentId) is not ITextChannel parent)
            throw new PreparationException(Resource.PrepSourceChannel);
        await service.BeginAttachAsync(guild, channel, actor.Id.ToString());
        string? tracking = null;
        try
        {
            var result = await UrlClass.AddUrlFromSetupAsync(new(url, room.Title, room.IsPrivate ? "Private" : "Public",
                false, "5m", actor, "automatic"), room.ParentChannelId, guild, parent);
            if (!result.Success || result.TrackingChannelId == null) throw new PreparationException(result.Message);
            tracking = result.TrackingChannelId;
        }
        finally { await service.CompleteAttachAsync(guild, channel, actor.Id.ToString(), tracking); }
        if (tracking != null && ulong.TryParse(tracking, out var trackingId))
        {
            var target = Declare.Client.GetChannel(trackingId) as IThreadChannel ?? await Declare.Client.Rest.GetChannelAsync(trackingId) as IThreadChannel;
            if (target != null)
                foreach (var player in room.Participants)
                    if (ulong.TryParse(player.UserId, out var userId))
                        try
                        {
                            var member = Declare.Client.GetGuild(ulong.Parse(guild))?.GetUser(userId) as IGuildUser
                                ?? await Declare.Client.Rest.GetGuildUserAsync(ulong.Parse(guild), userId);
                            if (member != null) await target.AddUserAsync(member);
                        }
                        catch { /* The room remains linked if a participant left the guild. */ }
        }
    }

    public static async Task InviteAsync(string guild, string channel, string organizer, string userId)
    {
        if (!Declare.IsArchipelagoMode) throw new PreparationException(Resource.PrepArchOnly);
        var room = await PreparationService.Instance.GetAsync(guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
        if (!await PreparationService.Instance.CanManageAsync(guild,channel,organizer)) throw new PreparationException(Resource.PrepOwnerOnly);
        if (room.State != PreparationState.Open || !ulong.TryParse(userId, out var memberId)) throw new PreparationException(Resource.PrepRegistrationsClosed);
        await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, organizer, guild, channel, SecurityAuditAction.RoomSettingsUpdate);
        var member = await Declare.Client.Rest.GetGuildUserAsync(ulong.Parse(guild), memberId);
        var target = await ResolveMessageChannelAsync(channel);
        if (member == null || !room.IsPrivate) throw new PreparationException(Resource.PrepInvalidSettings);
        if (target is IThreadChannel thread)
            await thread.AddUserAsync(member);
        else if (target is ITextChannel text)
        {
            var permissions = text.GetPermissionOverwrite(member) ?? new OverwritePermissions();
            try { await text.AddPermissionOverwriteAsync(member, permissions.Modify(viewChannel:PermValue.Allow)); }
            catch (Discord.Net.HttpException error) when (error.HttpCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new PreparationException(Text("PrepInviteChannelForbidden"));
            }
        }
        else throw new PreparationException(Resource.PrepSourceChannel);
        audit.Succeed();
    }

    private static string Safe(string text) => Regex.Replace(text.Replace("@", "@\u200b").Replace('\n',' ').Replace('\r',' '), @"([*_`|~])", @"\$1");
    private static string Text(string key) => Resource.ResourceManager.GetString(key) ?? key;

    public static async Task RemindAsync(string guild, string channel, string organizer)
    {
        await PreparationService.Instance.RemindAsync(guild, channel, organizer, users => SendRemindersAsync(channel,users));
    }

    private static Task RemindAfterCloseAsync(PreparationRoom room)
        => SendRemindersAsync(room.ChannelId, room.Participants.Where(player => !player.Ready)
            .Select(player => new PreparationReminder(player.UserId, room.Files.Any(file => file.UserId == player.UserId))).ToArray());

    private static async Task SendRemindersAsync(string channel, IReadOnlyList<PreparationReminder> users)
    {
        if (users.Count == 0) return;
        var target = await ResolveMessageChannelAsync(channel);
        foreach (var group in users.GroupBy(user => user.HasYaml))
            foreach (var batch in group.Chunk(40))
            {
                var ids = batch.Select(user => ulong.Parse(user.UserId)).ToList();
                var mentions = string.Join(" ", ids.Select(id => $"<@{id}>"));
                var template = Text(group.Key ? "PrepReminderMessage" : "PrepReminderMissingYamlMessage");
                await target.SendMessageAsync(string.Format(template, mentions),
                    allowedMentions:new AllowedMentions { UserIds = ids });
            }
    }

    private static async Task NotifyManagersAsync(PreparationRoom room)
    {
        var ids = new HashSet<ulong> { ulong.Parse(room.OwnerId) };
        var managers = await AstRoleBindingsCommands.GetGuildManagersAsync(room.GuildId);
        var managerIds = managers.Select(binding => binding.UserId).Append(Declare.InstanceOwnerUserId);
        foreach (var user in managerIds.Distinct(StringComparer.Ordinal))
        {
            if (!ulong.TryParse(user,out var id) || ids.Contains(id)) continue;
            var access = await AstAuthorizationService.CreateDiscordContextAsync(room.GuildId,room.ChannelId,id);
            if (access != null && AstAuthorizationService.CanUseArchipelagoTools(access) &&
                AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager,access)) ids.Add(id);
        }
        var target = await ResolveMessageChannelAsync(room.ChannelId);
        foreach (var batch in ids.Chunk(40))
            await target.SendMessageAsync(string.Format(Text("PrepEveryoneReady"),string.Join(" ",batch.Select(id => $"<@{id}>"))),
                allowedMentions:new AllowedMentions { UserIds=batch.ToList() });
    }

    private static async Task<IMessageChannel> ResolveMessageChannelAsync(string channel)
        => Declare.Client.GetChannel(ulong.Parse(channel)) as IMessageChannel
            ?? await Declare.Client.Rest.GetChannelAsync(ulong.Parse(channel)) as IMessageChannel
            ?? throw new PreparationException(Resource.PrepSourceChannel);

    public static async Task RetryDeliveryAsync(string guild, string channel, string organizer, string? webHostUserId = null,
        string? expectedRoom = null)
    {
        await PreparationService.Instance.PrepareDeliveryRetryAsync(guild, channel, organizer, webHostUserId, expectedRoom);
        await UpdateBoardAsync(guild, channel,retryHosting:true);
    }

    private static async Task ClearBoardAsync(PreparationRoom room, bool cancelled)
    {
        if (!ulong.TryParse(room.ChannelId, out var channelId) || !ulong.TryParse(room.MessageId, out var messageId)) return;
        var channel = Declare.Client.GetChannel(channelId) as IMessageChannel
            ?? await Declare.Client.Rest.GetChannelAsync(channelId) as IMessageChannel;
        if (channel != null && await channel.GetMessageAsync(messageId) is IUserMessage message)
            await message.ModifyAsync(properties => {
                properties.Content = Text(cancelled ? "PrepDeletedCancelled" : "PrepDeletedGenerated") +
                    (cancelled ? "" : HostingSummary(room) + TrackingSummary(room));
                properties.Embeds = Array.Empty<Embed>();
                properties.Components = new ComponentBuilder().Build();
                properties.AllowedMentions = AllowedMentions.None;
            });
    }
    public static Embed Board(PreparationRoom room)
    {
        var text = new StringBuilder();
        text.AppendLine(PreparationTexts.State(room.State));
        if (room.Hosting is { } hosting)
            text.AppendLine(string.Format(Text("PrepWebHostBoard"),hosting.Origin) + HostingSummary(room));
        if (room.TrackInThread is { } trackInThread)
            text.AppendLine(Text(trackInThread ? "PrepTrackInThread" : "PrepTrackInChannel"));
        if (room.Schedule is { State:"Scheduled" } timer)
            text.AppendLine(string.Format(Text("PrepTimerBoard"),timer.Deadline.ToUnixTimeSeconds()));
        text.AppendLine(string.Format(Resource.PrepSummary, room.Participants.Count, room.MaximumPlayers,
            room.Participants.Count(player => player.Ready), room.Files.Sum(file => file.Slots.Count)));
        text.AppendLine();
        text.AppendLine(Resource.PrepInstructions);
        foreach (var player in room.Participants.Take(20))
        {
            var slots = room.Files.Where(file => file.UserId == player.UserId).SelectMany(file => file.Slots).ToArray();
            var files = room.Files.Where(file => file.UserId == player.UserId).ToArray();
            var validation = files.Length == 0 ? Resource.PrepNoFiles :
                files.Any(file => file.Validation == "Invalid") ? Resource.PrepInvalid :
                files.Any(file => file.Validation == "Pending") ? Resource.PrepPending : Resource.PrepValid;
            text.AppendLine($"• {Safe(player.DisplayName)} · {slots.Length} · {validation} · {(player.Ready ? Resource.PrepReady : Resource.PrepNotReady)}");
        }
        if (room.Participants.Count == 0) text.AppendLine(Resource.PrepNoPlayers);
        if (room.LastError != null && !(room.State == PreparationState.Generated && room.Hosting != null))
            text.AppendLine(Resource.PrepGenerationFailed);
        if (room.LastRunStatus == "Validated") text.AppendLine(Resource.PrepTestPassed);
        if (room.State == PreparationState.Generated) text.AppendLine(Resource.PrepGenerated);
        if (room.TrackingChannelId != null) text.AppendLine($"{Resource.PrepTracking} : <#{room.TrackingChannelId}>");
        return new EmbedBuilder().WithTitle(Safe(room.Title)).WithDescription(text.ToString()[..Math.Min(text.Length, 4000)])
            .WithColor(new Color(172,153,237)).Build();
    }

    public static MessageComponent Buttons(PreparationRoom room)
    {
        string Id(string action) => $"astprep:{room.Id}:{action}";
        var buttons = new ComponentBuilder()
            .WithButton(Resource.PrepJoin, Id("join"), ButtonStyle.Primary, disabled:room.State != PreparationState.Open, row:0)
            .WithButton(Resource.PrepToggleReady, Id("ready-toggle"), ButtonStyle.Success, disabled:!room.CanEdit, row:0)
            .WithButton(Resource.PrepLeave, Id("leave"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:0)
            .WithButton(Resource.PrepPortal, Id("portal"), ButtonStyle.Secondary, row:1)
            .WithButton(Text("PrepRemind"), Id("remind"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:1)
            .WithButton(Text("PrepRetryDelivery"), Id("deliver"), ButtonStyle.Secondary, disabled:room.State != PreparationState.Generated, row:1)
            .WithButton(Text("PrepTimer"),Id("schedule"),ButtonStyle.Secondary,disabled:!room.CanEdit,row:1)
            .WithButton(room.State == PreparationState.Open ? Resource.PrepClose : Resource.PrepReopen, Id("close-toggle"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:2)
            .WithButton(Resource.PrepTest, Id("test"), ButtonStyle.Secondary, disabled:!room.CanEdit || room.Files.Count == 0, row:2)
            .WithButton(Resource.PrepGenerate, Id("generate"), ButtonStyle.Success, disabled:!room.CanGenerate, row:2)
            .WithButton(Resource.PrepCancel, Id("cancel"), ButtonStyle.Danger, disabled:!room.CanEdit, row:2);
        if (room.State == PreparationState.Generated && room.Hosting is { FinishManually: false } && room.TrackingChannelId == null)
            buttons.WithButton(Text("PrepFinishHostingManually"),Id("manual-delivery"),ButtonStyle.Secondary,row:3);
        return buttons.Build();
    }

    private static string HostingSummary(PreparationRoom room)
    {
        if (room.Hosting == null) return "";
        var result = room.Hosting.RoomUrl is { } url ? "\n" + url : "";
        return room.State == PreparationState.Generated && room.LastError != null ? result + "\n" + room.LastError : result;
    }

    private static string TrackingSummary(PreparationRoom room)
    {
        if (room.TrackingChannelId is { } tracking)
            return $"\n{Resource.PrepTracking} <#{tracking}>";
        if (room.Hosting is { FinishManually: false })
            return "";
        return "\n" + Text(room.TrackInThread switch
        {
            true => "PrepManualTrackInThread",
            false => "PrepManualTrackInChannel",
            null => "PrepManualTrack"
        });
    }

    private static async Task HostAndTrackAsync(PreparationRoom room, string archive, ITextChannel channel)
    {
        var service = PreparationService.Instance;
        var hosting = room.Hosting!;
        var url = hosting.RoomUrl;
        if (url == null)
        {
            var origin = PreparationWebHost.ParseOrigin(hosting.Origin)!;
            var session = await service.GetHostingSessionAsync(room) ??
                throw new PreparationException(Resource.PrepWebHostSessionRequired);
            url = await PreparationWebHost.HostAsync(origin, session, archive,
                new(hosting.SeedId, hosting.UploadStarted, seed => service.SaveWebHostUploadAsync(room, seed)));
            await service.SaveHostedRoomAsync(room,url);
        }
        var target = room.TrackInThread == true ? await GetOrCreateTrackingThreadAsync(room, channel) : channel;
        var tracking = await UrlClass.AddUrlFromSetupAsync(new(url,room.Title,"Channel",false,"5m",null,
            PreparedSlotOwners:hosting.SlotOwners),target.Id.ToString(),room.GuildId,target);
        if (!tracking.Success || tracking.TrackingChannelId != target.Id.ToString())
            throw new PreparationException(tracking.Message);
        await service.SaveHostedTrackingAsync(room,tracking.TrackingChannelId,tracking.PreparedAliases > 0);
    }

    private static async Task<IThreadChannel> GetOrCreateTrackingThreadAsync(PreparationRoom room, ITextChannel channel)
    {
        if (!ulong.TryParse(room.MessageId, out var boardId))
            throw new PreparationException(Resource.PrepNotFound);

        // A thread started from a message has the same ID as that message.
        // Resolve it before creating one so retries always use the original destination.
        var thread = Declare.Client.GetChannel(boardId) as IThreadChannel;
        if (thread == null)
        {
            try { thread = await Declare.Client.Rest.GetChannelAsync(boardId) as IThreadChannel; }
            catch (Discord.Net.HttpException error) when (error.HttpCode == System.Net.HttpStatusCode.NotFound) { }
        }
        if (thread == null)
        {
            var board = await channel.GetMessageAsync(boardId) ?? throw new PreparationException(Resource.PrepNotFound);
            return await channel.CreateThreadAsync(room.Title, type:ThreadType.PublicThread,
                autoArchiveDuration:ThreadArchiveDuration.OneWeek, message:board);
        }
        if (thread.GuildId != channel.GuildId ||
            thread is SocketThreadChannel socketThread && socketThread.ParentChannel.Id != channel.Id ||
            thread is Discord.Rest.RestThreadChannel restThread && restThread.ParentChannelId != channel.Id)
            throw new PreparationException(Resource.PrepSourceChannel);
        if (thread.IsArchived) await thread.ModifyAsync(properties => properties.Archived = false);
        return thread;
    }

    public static Task UpdateBoardAsync(string guild, string channel) => UpdateBoardAsync(guild,channel,false);

    private static async Task UpdateBoardAsync(string guild, string channel, bool retryHosting)
    {
        var gate = BoardGates.GetOrAdd(guild + ":" + channel, _ => new(1,1));
        await gate.WaitAsync();
        try
        {
            var room = await PreparationService.Instance.GetAsync(guild, channel);
            if (room == null || !ulong.TryParse(channel, out var id)) return;
            var target = Declare.Client.GetChannel(id) as IMessageChannel ?? await Declare.Client.Rest.GetChannelAsync(id) as IMessageChannel;
            if (target is IThreadChannel thread && thread.IsArchived && (room.CanEdit || room.State == PreparationState.Generated))
                await thread.ModifyAsync(properties => properties.Archived = false);
            if (target == null) return;
            if (!ulong.TryParse(room.MessageId, out var messageId))
            {
                var created = await target.SendMessageAsync(embed:Board(room), components:Buttons(room), allowedMentions:AllowedMentions.None);
                messageId = created.Id;
                await PreparationService.Instance.SaveMessageAsync(guild, channel, messageId.ToString());
            }
            else if (await target.GetMessageAsync(messageId) is IUserMessage message)
                await message.ModifyAsync(properties => { properties.Embed = Board(room); properties.Components = Buttons(room); properties.AllowedMentions = AllowedMentions.None; });
            if (room.State == PreparationState.Generated)
                await PreparationService.Instance.DeliverArchiveAsync(guild, channel, async (generated, archive) =>
                {
                    var server = Declare.Client.GetGuild(ulong.Parse(guild)) as IGuild ?? await Declare.Client.Rest.GetGuildAsync(ulong.Parse(guild));
                    var tooLarge = new FileInfo(archive).Length > (long)server.MaxUploadLimit;
                    async Task<IUserMessage> PublishDownloadAsync()
                    {
                        if (!Declare.EnableWebPortal) throw new PreparationException(Text("PrepDeliveryUnavailable"));
                        var token = await PreparationService.Instance.CreateTemporaryDownloadAsync(archive);
                        var url = WebPortalPages.GetPreparationDownloadUrl(token);
                        return await target.SendMessageAsync(Text("PrepArchiveLink") + HostingSummary(generated) + TrackingSummary(generated),
                            components:new ComponentBuilder().WithButton(Resource.PrepDownload, style:ButtonStyle.Link, url:url).Build(),
                            allowedMentions:AllowedMentions.None);
                    }
                    IUserMessage sent;
                    if (tooLarge)
                        sent = await PublishDownloadAsync();
                    else
                    {
                        try
                        {
                            sent = await target.SendFileAsync(archive, Text("PrepArchiveSent") + HostingSummary(generated) + TrackingSummary(generated), allowedMentions:AllowedMentions.None);
                        }
                        catch (Discord.Net.HttpException)
                        {
                            sent = await PublishDownloadAsync();
                        }
                    }
                    return sent.Id.ToString();
                }, target is ITextChannel textChannel ? (generated,archive) => HostAndTrackAsync(generated,archive,textChannel) : null,retryHosting);
            if (room.Hosting != null && room.State == PreparationState.Generated &&
                await PreparationService.Instance.GetAsync(guild,channel) is { } retained &&
                await target.GetMessageAsync(messageId) is IUserMessage retainedMessage)
                await retainedMessage.ModifyAsync(properties => { properties.Embed=Board(retained); properties.Components=Buttons(retained); });
        }
        finally { gate.Release(); }
    }
}
