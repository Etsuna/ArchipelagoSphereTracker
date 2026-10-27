using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;

public static class PreparationDiscord
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BoardGates = new();
    private static int _subscribed;
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 0)
        {
            PreparationService.Instance.Changed += UpdateBoardAsync;
            PreparationService.Instance.Removed += ClearBoardAsync;
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
            (Resource.PrepInvite,"invite"), (Resource.PrepRemove,"remove") })
            action.AddChoice(label, value);
        return new SlashCommandBuilder().WithName("organize-room").WithDescription(Resource.PrepDescription)
            .AddOption(action)
            .AddOption(Resource.PrepOptionName, ApplicationCommandOptionType.String, Resource.PrepOptionNameDescription, isRequired:false)
            .AddOption(Resource.PrepOptionFile, ApplicationCommandOptionType.Attachment, Resource.PrepFileDescription, isRequired:false)
            .AddOption(Resource.PrepOptionUser, ApplicationCommandOptionType.User, Resource.PrepUserDescription, isRequired:false)
            .AddOption(Resource.PrepOptionYamlName, ApplicationCommandOptionType.String, Resource.PrepYamlNameDescription, isRequired:false);
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
        var room = await PreparationService.Instance.GetAsync(guild.ToString(), channel.ToString());
        var action = Value(Resource.PrepOptionAction) as string ?? (attachment != null ? "upload" : room == null ? "create" : "status");
        if (action == "create" && string.IsNullOrWhiteSpace(Value(Resource.PrepOptionName) as string))
        { await ShowCreateModalAsync(command, guild, channel); return; }
        await command.DeferAsync(ephemeral:true);
        try
        {
            string message;
            if (action == "create")
            {
                if (await AuthorizeAsync(guild, channel, command.User, creating:true) == null) throw new PreparationException(AstAuthorizationService.DeniedMessage);
                var created = await CreateAsync(guild.ToString(), channel.ToString(), command.User as IGuildUser,
                    (string)Value(Resource.PrepOptionName)!);
                message = string.Format(Resource.PrepCreated, created.ChannelId);
            }
            else if (action == "upload" && attachment != null)
            {
                message = await UploadAttachmentAsync(guild.ToString(), channel.ToString(), command.User as IGuildUser, attachment);
            }
            else if (action == "remove")
            {
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
        if (component.Data.CustomId == "astprep-create")
        {
            if (component.GuildId is { } guild && component.ChannelId is { } sourceChannel)
                await ShowCreateModalAsync(component, guild, sourceChannel);
            return;
        }
        var parts = component.Data.CustomId.Split(':');
        if (parts.Length != 3 || parts[0] != "astprep" || !Guid.TryParseExact(parts[1], "N", out _)) return;
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

    private static async Task ShowCreateModalAsync(IDiscordInteraction interaction, ulong guild, ulong channel)
    {
        if (await AuthorizeAsync(guild, channel, interaction.User, creating:true) == null)
        { await interaction.RespondAsync(AstAuthorizationService.DeniedMessage, ephemeral:true); return; }
        var modal = new ModalBuilder().WithTitle(Resource.PrepTitle).WithCustomId("astprep-modal")
            .AddTextInput(Resource.PrepName, "name", TextInputStyle.Short, maxLength:80, required:true);
        await interaction.RespondWithModalAsync(modal.Build());
    }

    public static async Task HandleModalAsync(SocketModal modal)
    {
        if (modal.Data.CustomId != "astprep-modal") return;
        await modal.DeferAsync(ephemeral:true);
        try
        {
            if (modal.GuildId is not { } guild || modal.ChannelId is not { } channel || await AuthorizeAsync(guild, channel, modal.User, creating:true) == null)
                throw new PreparationException(AstAuthorizationService.DeniedMessage);
            string Value(string name) => modal.Data.Components.FirstOrDefault(field => field.CustomId == name)?.Value ?? string.Empty;
            var room = await CreateAsync(guild.ToString(), channel.ToString(), modal.User as IGuildUser, Value("name"));
            await modal.FollowupAsync(string.Format(Resource.PrepCreated, room.ChannelId), ephemeral:true);
        }
        catch (PreparationException error) { await modal.FollowupAsync(error.Message, ephemeral:true, allowedMentions:AllowedMentions.None); }
        catch { await modal.FollowupAsync(Resource.PrepGenerationFailed, ephemeral:true); }
    }

    public static async Task<PreparationRoom> CreateAsync(string guild, string parent, IGuildUser? owner, string title)
    {
        if (!Declare.IsArchipelagoMode) throw new PreparationException(Resource.PrepArchOnly);
        if (owner == null || !ulong.TryParse(parent, out var parentId) || Declare.Client.GetChannel(parentId) is not ITextChannel channel ||
            channel is not IGuildChannel guildChannel || guildChannel.GuildId.ToString() != guild)
            throw new PreparationException(Resource.PrepSourceChannel);
        var authorization = await AstAuthorizationService.CreateDiscordContextAsync(guild, parent, owner.Id, owner);
        if (authorization == null || !AstAuthorizationService.CanUseArchipelagoTools(authorization) || !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, authorization))
            throw new PreparationException(AstAuthorizationService.DeniedMessage);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 80) throw new PreparationException(Resource.PrepInvalidSettings);
        await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, owner.Id.ToString(), guild, parent, SecurityAuditAction.RoomAdd);
        var sourceThread = channel as IThreadChannel;
        if (sourceThread?.IsArchived == true)
            await sourceThread.ModifyAsync(properties => properties.Archived = false);
        var trackingParent = channel switch {
            SocketThreadChannel socketThread => socketThread.ParentChannel.Id.ToString(),
            Discord.Rest.RestThreadChannel restThread => restThread.ParentChannelId.ToString(),
            _ => parent
        };
        var room = await PreparationService.Instance.CreateAsync(guild, trackingParent,
            parent, owner.Id.ToString(), title, sourceThread?.Type == ThreadType.PrivateThread);
        var message = await channel.SendMessageAsync(embed:Board(room), components:Buttons(room), allowedMentions:AllowedMentions.None);
        await PreparationService.Instance.SaveMessageAsync(guild, room.ChannelId, message.Id.ToString());
        audit.Succeed();
        return (await PreparationService.Instance.GetAsync(guild, room.ChannelId))!;
    }

    public static async Task<string> ExecuteAsync(string guild, string channel, IGuildUser? actor, string action, string? url = null, bool skipBalancing = false,
        SecurityAuditSource source = SecurityAuditSource.Discord)
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
        else if (action == "deliver") await RetryDeliveryAsync(guild, channel, actor.Id.ToString());
        else message = await service.ApplyActionAsync(guild, channel, user, actor.Nickname ?? actor.Username, action, skipBalancing);
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
            if (!result.Success || result.ThreadChannelId == null) throw new PreparationException(result.Message);
            tracking = result.ThreadChannelId;
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
        if (room.OwnerId != organizer) throw new PreparationException(Resource.PrepOwnerOnly);
        if (room.State != PreparationState.Open || !ulong.TryParse(userId, out var memberId)) throw new PreparationException(Resource.PrepRegistrationsClosed);
        await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Discord, organizer, guild, channel, SecurityAuditAction.RoomSettingsUpdate);
        var member = await Declare.Client.Rest.GetGuildUserAsync(ulong.Parse(guild), memberId);
        var thread = Declare.Client.GetChannel(ulong.Parse(channel)) as IThreadChannel ?? await Declare.Client.Rest.GetChannelAsync(ulong.Parse(channel)) as IThreadChannel;
        if (member == null || thread == null) throw new PreparationException(Resource.PrepInvalidSettings);
        await thread.AddUserAsync(member);
        audit.Succeed();
    }

    private static string Safe(string text) => Regex.Replace(text.Replace("@", "@\u200b").Replace('\n',' ').Replace('\r',' '), @"([*_`|~])", @"\$1");
    private static string Text(string key) => Resource.ResourceManager.GetString(key) ?? key;

    public static async Task RemindAsync(string guild, string channel, string organizer)
    {
        await PreparationService.Instance.RemindAsync(guild, channel, organizer, async users => {
            var target = Declare.Client.GetChannel(ulong.Parse(channel)) as IMessageChannel
                ?? await Declare.Client.Rest.GetChannelAsync(ulong.Parse(channel)) as IMessageChannel
                ?? throw new PreparationException(Resource.PrepSourceChannel);
            foreach (var group in users.Chunk(40))
            {
                var ids = group.Select(ulong.Parse).ToList();
                var mentions = string.Join(" ", ids.Select(id => $"<@{id}>"));
                await target.SendMessageAsync(string.Format(Text("PrepReminderMessage"), mentions),
                    allowedMentions:new AllowedMentions { UserIds = ids });
            }
        });
    }

    public static async Task RetryDeliveryAsync(string guild, string channel, string organizer)
    {
        var room = await PreparationService.Instance.GetAsync(guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
        if (room.OwnerId != organizer) throw new PreparationException(Resource.PrepOwnerOnly);
        if (room.State != PreparationState.Generated) throw new PreparationException(Resource.PrepLocked);
        await UpdateBoardAsync(guild, channel);
    }

    private static async Task ClearBoardAsync(PreparationRoom room, bool cancelled)
    {
        if (!ulong.TryParse(room.ChannelId, out var channelId) || !ulong.TryParse(room.MessageId, out var messageId)) return;
        var channel = Declare.Client.GetChannel(channelId) as IMessageChannel
            ?? await Declare.Client.Rest.GetChannelAsync(channelId) as IMessageChannel;
        if (channel != null && await channel.GetMessageAsync(messageId) is IUserMessage message)
            await message.ModifyAsync(properties => {
                properties.Content = Text(cancelled ? "PrepDeletedCancelled" : "PrepDeletedGenerated");
                properties.Embeds = Array.Empty<Embed>();
                properties.Components = new ComponentBuilder().Build();
                properties.AllowedMentions = AllowedMentions.None;
            });
    }
    public static Embed Board(PreparationRoom room)
    {
        var text = new StringBuilder();
        text.AppendLine(PreparationTexts.State(room.State));
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
        if (room.LastError != null) text.AppendLine(Resource.PrepGenerationFailed);
        if (room.LastRunStatus == "Validated") text.AppendLine(Resource.PrepTestPassed);
        if (room.State == PreparationState.Generated) text.AppendLine(Resource.PrepGenerated);
        if (room.TrackingChannelId != null) text.AppendLine($"{Resource.PrepTracking} : <#{room.TrackingChannelId}>");
        return new EmbedBuilder().WithTitle(Safe(room.Title)).WithDescription(text.ToString()[..Math.Min(text.Length, 4000)])
            .WithColor(new Color(172,153,237)).Build();
    }

    public static MessageComponent Buttons(PreparationRoom room)
    {
        string Id(string action) => $"astprep:{room.Id}:{action}";
        return new ComponentBuilder()
            .WithButton(Resource.PrepJoin, Id("join"), ButtonStyle.Primary, disabled:room.State != PreparationState.Open, row:0)
            .WithButton(Resource.PrepToggleReady, Id("ready-toggle"), ButtonStyle.Success, disabled:!room.CanEdit, row:0)
            .WithButton(Resource.PrepLeave, Id("leave"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:0)
            .WithButton(Resource.PrepPortal, Id("portal"), ButtonStyle.Secondary, row:1)
            .WithButton(Text("PrepRemind"), Id("remind"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:1)
            .WithButton(Text("PrepRetryDelivery"), Id("deliver"), ButtonStyle.Secondary, disabled:room.State != PreparationState.Generated, row:1)
            .WithButton(room.State == PreparationState.Open ? Resource.PrepClose : Resource.PrepReopen, Id("close-toggle"), ButtonStyle.Secondary, disabled:!room.CanEdit, row:2)
            .WithButton(Resource.PrepTest, Id("test"), ButtonStyle.Secondary, disabled:!room.CanEdit || room.Files.Count == 0, row:2)
            .WithButton(Resource.PrepGenerate, Id("generate"), ButtonStyle.Success, disabled:!room.CanGenerate, row:2)
            .WithButton(Resource.PrepCancel, Id("cancel"), ButtonStyle.Danger, disabled:!room.CanEdit, row:2).Build();
    }

    public static async Task UpdateBoardAsync(string guild, string channel)
    {
        var gate = BoardGates.GetOrAdd(guild + ":" + channel, _ => new(1,1));
        await gate.WaitAsync();
        try
        {
            var room = await PreparationService.Instance.GetAsync(guild, channel);
            if (room?.MessageId == null || !ulong.TryParse(channel, out var id) || !ulong.TryParse(room.MessageId, out var messageId)) return;
            var target = Declare.Client.GetChannel(id) as IMessageChannel ?? await Declare.Client.Rest.GetChannelAsync(id) as IMessageChannel;
            if (target is IThreadChannel thread && thread.IsArchived && (room.CanEdit || room.State == PreparationState.Generated))
                await thread.ModifyAsync(properties => properties.Archived = false);
            if (target != null && await target.GetMessageAsync(messageId) is IUserMessage message)
                await message.ModifyAsync(properties => { properties.Embed = Board(room); properties.Components = Buttons(room); properties.AllowedMentions = AllowedMentions.None; });
            if (target != null)
                await PreparationService.Instance.DeliverArchiveAsync(guild, channel, async (generated, archive) =>
                {
                    var server = Declare.Client.GetGuild(ulong.Parse(guild)) as IGuild ?? await Declare.Client.Rest.GetGuildAsync(ulong.Parse(guild));
                    var tooLarge = new FileInfo(archive).Length > (long)server.MaxUploadLimit;
                    async Task<IUserMessage> PublishDownloadAsync()
                    {
                        if (!Declare.EnableWebPortal) throw new PreparationException(Text("PrepDeliveryUnavailable"));
                        var token = await PreparationService.Instance.CreateTemporaryDownloadAsync(archive);
                        var url = WebPortalPages.GetPreparationDownloadUrl(token);
                        return await target.SendMessageAsync(Text("PrepArchiveLink"),
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
                            sent = await target.SendFileAsync(archive, Text("PrepArchiveSent"), allowedMentions:AllowedMentions.None);
                        }
                        catch (Discord.Net.HttpException)
                        {
                            sent = await PublishDownloadAsync();
                        }
                    }
                    return sent.Id.ToString();
                });
        }
        finally { gate.Release(); }
    }
}
