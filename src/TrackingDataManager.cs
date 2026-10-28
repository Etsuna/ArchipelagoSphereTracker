using ArchipelagoSphereTracker.src.Resources;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using ArchipelagoSphereTracker.Tracking.Persistence;
using ArchipelagoSphereTracker.Tracking.Notifications;
using ArchipelagoSphereTracker.Tracking.Scheduling;
using ArchipelagoSphereTracker.Tracking.V2;
using Discord;
using Discord.WebSocket;
using Sprache;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static partial class TrackingDataManager
{
    public static class RateLimitGuards
    {
        private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSendGates = new();
        public static SemaphoreSlim GetGuildSendGate(ulong guildId, int parallelismPerGuild = 2)
            => _guildSendGates.GetOrAdd(guildId, _ => new SemaphoreSlim(parallelismPerGuild, parallelismPerGuild));

        public static void RemoveGuildSendGate(ulong guildId)
        {
            if (_guildSendGates.TryRemove(guildId, out var gate))
            {
                gate.Dispose();
            }
        }
    }

    private static readonly ConcurrentDictionary<string, byte> InFlight = new();

    private static readonly ConcurrentDictionary<ulong, int> MissingChannelPassCount = new ConcurrentDictionary<ulong, int>();
    private const int MaxChecksBeforeDelete = 1 * 60;
    private static readonly object SchedulerLifecycleLock = new();
    private static CentralRoomScheduler? _centralScheduler;
    private static Task? _trackingTask;
    private static Task? _deliveryTask;

    public static void StartTracking()
    {
        if (Declare.UseLegacyTrackingScheduler)
        {
            lock (SchedulerLifecycleLock)
            {
                if (_trackingTask is { IsCompleted: false })
                    return;
                StartLegacyTracking();
            }
            return;
        }

        lock (SchedulerLifecycleLock)
        {
            if (_trackingTask is { IsCompleted: false })
                return;

            Declare.Cts?.Cancel();
            Declare.Cts?.Dispose();
            Declare.Cts = new CancellationTokenSource();
            var token = Declare.Cts.Token;
            StartDeliveryWorker(token);
            _centralScheduler = new CentralRoomScheduler(
                new SqliteRoomScheduleStore(),
                ProcessScheduledRoomAsync,
                new CentralRoomSchedulerOptions
                {
                    GlobalConcurrency = Declare.TrackingGlobalConcurrency,
                    PerOriginConcurrency = Declare.TrackingOriginConcurrency
                },
                metrics: CentralSchedulerMetrics.Instance,
                completionObserver: LogScheduledRoomCompletion);
            _trackingTask = Task.Run(async () =>
            {
                try
                {
                    await _centralScheduler.RunAsync(token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[TDM][Scheduler] Stopped unexpectedly ({exception.GetType().Name}).");
                }
            }, CancellationToken.None);
        }
    }

    public static async Task StopTrackingAsync()
    {
        Task? task;
        lock (SchedulerLifecycleLock)
        {
            Declare.Cts?.Cancel();
            task = Task.WhenAll(_trackingTask ?? Task.CompletedTask, _deliveryTask ?? Task.CompletedTask);
        }

        if (task != null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static void StartDeliveryWorker(CancellationToken token)
    {
        _deliveryTask = Task.Run(() => RunDeliveryWorkerAsync(token), CancellationToken.None);
    }

    private static async Task RunDeliveryWorkerAsync(CancellationToken token)
    {
        var worker = new TrackingDeliveryWorker(
            new DiscordTrackingEventPublisher(new DiscordTrackingTransport(Declare.Client)),
            destinationType: TrackingDestination.NotificationDestinationType);
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Declare.Client.ConnectionState == ConnectionState.Connected)
                {
                    try { await TrackingReplayService.PrepareNextAsync(token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"[TDM][Replay] Preparation retry ({exception.GetType().Name}).");
                    }
                }
                if (Declare.Client.ConnectionState != ConnectionState.Connected ||
                    !await worker.RunOnceAsync(token).ConfigureAwait(false))
                    await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Console.WriteLine($"[TDM][V2] Delivery worker retry ({exception.GetType().Name}).");
                try { await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
        }
    }

    public static Task<bool> PromoteRoomAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        CentralRoomScheduler? scheduler;
        lock (SchedulerLifecycleLock)
            scheduler = Declare.UseLegacyTrackingScheduler ? null : _centralScheduler;
        return scheduler?.PromoteAsync(guildId, channelId, cancellationToken)
               ?? Task.FromResult(false);
    }

    public static Task<TrackingControlOutcome> PauseRoomAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
        => GetCentralScheduler()?.PauseAsync(guildId, channelId, cancellationToken)
           ?? Task.FromResult(TrackingControlOutcome.Unavailable);

    public static Task<TrackingControlOutcome> ResumeRoomAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
        => GetCentralScheduler()?.ResumeAsync(guildId, channelId, cancellationToken)
           ?? Task.FromResult(TrackingControlOutcome.Unavailable);

    public static Task<TrackingControlOutcome> ForceSyncRoomAsync(
        string guildId,
        string channelId,
        CancellationToken cancellationToken = default)
        => GetCentralScheduler()?.ForceSyncAsync(guildId, channelId, cancellationToken)
           ?? Task.FromResult(TrackingControlOutcome.Unavailable);

    public static RoomHealthSnapshot? GetRoomHealth(string guildId, string channelId)
        => GetCentralScheduler()?.GetHealth(guildId, channelId);

    public static Task<TrackingControlOutcome> RefreshRoomIfStaleAsync(
        string guildId, string channelId, CancellationToken cancellationToken = default)
        => GetCentralScheduler()?.RefreshIfStaleAsync(guildId, channelId, cancellationToken)
           ?? Task.FromResult(TrackingControlOutcome.Unavailable);

    public static IReadOnlyList<RoomHealthSnapshot>? GetGuildHealth(string guildId)
        => GetCentralScheduler()?.GetGuildHealth(guildId);

    public static Task ReloadTrackingConfigurationAsync(CancellationToken cancellationToken = default)
        => GetCentralScheduler()?.ReloadConfigurationAsync(cancellationToken) ?? Task.CompletedTask;

    private static CentralRoomScheduler? GetCentralScheduler()
    {
        lock (SchedulerLifecycleLock)
            return Declare.UseLegacyTrackingScheduler ? null : _centralScheduler;
    }

    private static void StartLegacyTracking()
    {
        const int MaxGuildsParallel = 10;
        const int MaxChannelsParallel = 1;

        if (Declare.Cts != null)
            Declare.Cts.Cancel();

        Declare.Cts = new CancellationTokenSource();
        var token = Declare.Cts.Token;
        StartDeliveryWorker(token);

        _trackingTask = Task.Run(async () =>
        {
            try
            {
                await ChannelConfigCache.LoadAllAsync();
                var nextCacheReloadAt = DateTimeOffset.UtcNow.AddHours(1);

                var backoffSeconds = 2;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (DateTimeOffset.UtcNow >= nextCacheReloadAt)
                        {
                            await ChannelConfigCache.LoadAllAsync();
                            nextCacheReloadAt = DateTimeOffset.UtcNow.AddHours(1);
                        }

                        if (Declare.Client.ConnectionState != ConnectionState.Connected)
                        {
                            await Task.Delay(5000, token);
                            continue;
                        }

                        var uniqueGuilds = ChannelConfigCache.GetAllGuildIds().ToList();

                        await Parallel.ForEachAsync(
                            uniqueGuilds,
                            new ParallelOptions { MaxDegreeOfParallelism = MaxGuildsParallel, CancellationToken = token },
                            async (guild, ctGuild) =>
                            {
                                try
                                {
                                    var guildId = ulong.Parse(guild);

                                    var guildCheck = Declare.Client.GetGuild(guildId);
                                    if (guildCheck == null)
                                    {
                                        var restGuild = await Declare.Client.Rest.GetGuildAsync(guildId);
                                        if (restGuild == null)
                                        {
                                            Console.WriteLine(string.Format(Resource.TDMServerNotFound, guild));
                                            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guild);
                                            WebPortalPages.DeleteGuildPages(guild);
                                            RateLimitGuards.RemoveGuildSendGate(guildId);
                                            Console.WriteLine(Resource.TDMDeletionCompleted);
                                        }
                                        return;
                                    }

                                    var uniqueChannels = ChannelConfigCache.GetChannelIdsForGuild(guild).ToList();

                                    var channelsToProcess = uniqueChannels
                                    .Where(ch => !Declare.AddedChannelId.Contains(ch))
                                    .ToList();

                                    await Parallel.ForEachAsync(
                                    channelsToProcess,
                                    new ParallelOptions { MaxDegreeOfParallelism = MaxChannelsParallel, CancellationToken = token },
                                    async (channel, ctChan) =>
                                    {
                                        var key = $"{guild}:{channel}";
                                        if (!InFlight.TryAdd(key, 0))
                                            return;

                                        try
                                        {
                                            if (!ChannelConfigCache.TryGet(guild, channel, out var cfg))
                                            {
                                                var (tracker, baseUrl, room, silent, checkFrequencyStr, lastCheckStr, port)
                                                    = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guild, channel);

                                                if (string.IsNullOrWhiteSpace(tracker) || string.IsNullOrWhiteSpace(baseUrl))
                                                    return;

                                                var checkFrequency = CheckFrequencyParser.ParseOrDefault(
                                                    checkFrequencyStr,
                                                    TimeSpan.FromMinutes(5),
                                                    TimeSpan.FromMinutes(5),
                                                    null);

                                                DateTimeOffset? last = TryParseIsoOrUnixMs(lastCheckStr);

                                                if (port == null)
                                                {
                                                    port = "0";
                                                }

                                                cfg = new ChannelConfig(tracker, baseUrl, room, silent, checkFrequency, last, port);
                                                ChannelConfigCache.Upsert(guild, channel, cfg);
                                            }

                                            var channelId = ulong.Parse(channel);
                                            var guildChannel = guildCheck.GetChannel(channelId) as SocketGuildChannel;
                                            var thread = guildCheck.ThreadChannels.FirstOrDefault(t => t.Id == channelId);

                                            if (guildChannel is null && thread is null)
                                            {
                                                var restChan = await Declare.Client.Rest.GetChannelAsync(channelId);
                                                if (restChan == null)
                                                {
                                                    Console.WriteLine(string.Format(Resource.TDMChannelNoLongerExists, channel));
                                                    await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                                    WebPortalPages.DeleteChannelPages(guild, channel);
                                                    Console.WriteLine(Resource.TDMDeletionCompleted);
                                                    ChannelConfigCache.Remove(guild, channel);

                                                    MissingChannelPassCount.TryRemove(channelId, out _);
                                                }
                                                else
                                                {
                                                    var count = MissingChannelPassCount.AddOrUpdate(channelId, 1, (_, old) => old + 1);

                                                    if (count >= MaxChecksBeforeDelete)
                                                    {
                                                        Console.WriteLine($"[TDM] Le canal {channelId} est introuvable côté gateway depuis {count} minutes, suppression des données.");
                                                        await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                                        WebPortalPages.DeleteChannelPages(guild, channel);
                                                        Console.WriteLine(Resource.TDMDeletionCompleted);
                                                        ChannelConfigCache.Remove(guild, channel);

                                                        MissingChannelPassCount.TryRemove(channelId, out _);
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine($"[TDM] REST confirme l'existence du canal {channelId}, on saute cette passe ({count}/{MaxChecksBeforeDelete}).");
                                                    }
                                                }
                                                return;
                                            }

                                            var nameForLog = thread?.Name ?? guildChannel!.Name;

                                            var (shouldRun, checkFrequencyTs) = ChannelConfigCache.ShouldRunChecks(cfg);
                                            if (!shouldRun)
                                            {
                                                Console.WriteLine(string.Format(Resource.TDMSkippingCheck, nameForLog, checkFrequencyTs.TotalMinutes));
                                                return;
                                            }
                                            Console.WriteLine(string.Format(Resource.TDMChannelStillExists, nameForLog));

                                            if (thread != null)
                                            {
                                                var lastActivity = await ChannelsAndUrlsCommands.GetLastItemCheckAsync(guild, channel);
                                                if (lastActivity == null)
                                                    lastActivity = SnowflakeUtils.FromSnowflake(thread.Id);

                                                double daysSince = (DateTimeOffset.UtcNow - lastActivity.Value).TotalDays;

                                                if (daysSince < 7)
                                                {
                                                    if (Declare.WarnedThreads.Contains(channel))
                                                    {
                                                        await RateLimitGuards.GetGuildSendGate(guildCheck.Id).WaitAsync(ctChan);
                                                        try
                                                        {
                                                            await BotCommands.SendMessageAsync(string.Format(Resource.TDMNewMessageOnThread, thread.Name), channel);
                                                        }
                                                        finally
                                                        {
                                                            RateLimitGuards.GetGuildSendGate(guildCheck.Id).Release();
                                                        }
                                                        Declare.WarnedThreads.Remove(channel);
                                                    }
                                                }
                                                else if (daysSince < 14)
                                                {
                                                    if (!Declare.WarnedThreads.Contains(channel))
                                                    {
                                                        var baseDate = lastActivity.Value;
                                                        DateTimeOffset deletionDate = baseDate.AddDays(14);

                                                        TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
                                                        DateTimeOffset localDeletionDate = TimeZoneInfo.ConvertTime(deletionDate, frenchTimeZone);

                                                        string formattedDeletionDate = localDeletionDate.ToString(
                                                            "dddd d MMMM yyyy à HH'h'mm",
                                                            CultureInfo.GetCultureInfo($"{Declare.Language}-{Declare.Language.ToUpperInvariant()}"));

                                                        await RateLimitGuards.GetGuildSendGate(guildCheck.Id).WaitAsync(ctChan);
                                                        try
                                                        {
                                                            await BotCommands.SendMessageAsync(
                                                                string.Format(Resource.TDMNoMessage7Days, formattedDeletionDate, thread.Name), channel);
                                                        }
                                                        finally
                                                        {
                                                            RateLimitGuards.GetGuildSendGate(guildCheck.Id).Release();
                                                        }

                                                        Declare.WarnedThreads.Add(channel);
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine(Resource.TDMNoActivity);
                                                    await RateLimitGuards.GetGuildSendGate(guildCheck.Id).WaitAsync(ctChan);
                                                    try
                                                    {
                                                        await BotCommands.SendMessageAsync(
                                                            string.Format(Resource.TDMNoActivity, thread.Name), channel);
                                                    }
                                                    finally
                                                    {
                                                        RateLimitGuards.GetGuildSendGate(guildCheck.Id).Release();
                                                    }
                                                    await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                                    WebPortalPages.DeleteChannelPages(guild, channel);
                                                    Declare.WarnedThreads.Remove(channel);
                                                    ChannelConfigCache.Remove(guild, channel);
                                                    return;
                                                }
                                            }

                                            Console.WriteLine(string.Format(Resource.TDMCheckingItems, nameForLog));
                                            var roomInfo = await UrlClass.RoomInfo(cfg.BaseUrl, cfg.Room);

                                            if (roomInfo != null)
                                            {
                                                if (cfg.Port != roomInfo.LastPort.ToString())
                                                {
                                                    cfg = cfg with { Port = roomInfo.LastPort.ToString() };
                                                    ChannelConfigCache.Upsert(guild, channel, cfg);
                                                    await ChannelsAndUrlsCommands.UpdateChannelPortAsync(guild, channel, roomInfo.LastPort.ToString());

                                                    var gate = RateLimitGuards.GetGuildSendGate(guildCheck.Id);
                                                    await gate.WaitAsync(ctChan);
                                                    try
                                                    {
                                                        var message = string.Format(Resource.NewPort, roomInfo.LastPort.ToString());
                                                        await BotCommands.SendMessageAsync($"@everyone, {message}", channel);
                                                    }
                                                    finally
                                                    {
                                                        gate.Release();
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"[TDM] Impossible de récupérer les informations de la salle pour {nameForLog}.");
                                            }

                                            await GetTableDataAsync(guild, channel, cfg.BaseUrl, cfg.Tracker, cfg.Silent, ctChan);
                                            Console.WriteLine(string.Format(Resource.TDMCheckCompleted, nameForLog));
                                        }
                                        catch (OperationCanceledException) when (ctChan.IsCancellationRequested)
                                        {
                                            throw;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[TDM] Channel processing error for guild {guild} / channel {channel} ({ex.GetType().Name}).");
                                        }
                                        finally
                                        {
                                            InFlight.TryRemove(key, out _);
                                        }
                                    });
                                }
                                catch (OperationCanceledException) when (ctGuild.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[TDM] Guild processing error for {guild} ({ex.GetType().Name}).");
                                }
                            });

                        Console.WriteLine(Resource.TDMWaitingCheck);
                        await Task.Delay(60000, token);
                        backoffSeconds = 2;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        Console.WriteLine(Resource.TDMTrackingCanceled);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TDM] Tracking loop error ({ex.GetType().Name}).");
                        var delay = TimeSpan.FromSeconds(Math.Min(backoffSeconds, 60));
                        backoffSeconds = Math.Min(backoffSeconds * 2, 60);
                        await Task.Delay(delay, token);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Console.WriteLine(Resource.TDMTrackingCanceled);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TDM] Tracking task crashed ({ex.GetType().Name}).");
            }
        }, token);
    }

    private static async Task<RoomPollResult> ProcessScheduledRoomAsync(
        ScheduledRoomDefinition scheduled,
        CancellationToken cancellationToken)
    {
        if (Declare.Client.ConnectionState != ConnectionState.Connected)
            return RoomPollResult.Failed(PollFailureKind.Network, TimeSpan.FromSeconds(5));
        if (Declare.AddedChannelId.Contains(scheduled.ChannelId))
            return RoomPollResult.Failed(PollFailureKind.PartialResponse, TimeSpan.FromSeconds(30));
        if (!ulong.TryParse(scheduled.GuildId, out var guildId) ||
            !ulong.TryParse(scheduled.ChannelId, out var channelId))
        {
            return RoomPollResult.Removed();
        }

        try
        {
            var guild = Declare.Client.GetGuild(guildId);
            if (guild == null)
            {
                var restGuild = await Declare.Client.Rest.GetGuildAsync(guildId).ConfigureAwait(false);
                if (restGuild == null)
                {
                    await DatabaseCommands.DeleteChannelDataByGuildIdAsync(scheduled.GuildId).ConfigureAwait(false);
                    WebPortalPages.DeleteGuildPages(scheduled.GuildId);
                    RateLimitGuards.RemoveGuildSendGate(guildId);
                    return RoomPollResult.Removed();
                }

                return RoomPollResult.Failed(PollFailureKind.PartialResponse, TimeSpan.FromMinutes(1));
            }

            var guildChannel = guild.GetChannel(channelId) as SocketGuildChannel;
            var thread = guild.ThreadChannels.FirstOrDefault(candidate => candidate.Id == channelId);
            if (guildChannel == null && thread == null)
            {
                var restChannel = await Declare.Client.Rest.GetChannelAsync(channelId).ConfigureAwait(false);
                if (restChannel == null)
                {
                    await DeleteScheduledChannelAsync(scheduled.GuildId, scheduled.ChannelId).ConfigureAwait(false);
                    MissingChannelPassCount.TryRemove(channelId, out _);
                    return RoomPollResult.Removed();
                }

                var misses = MissingChannelPassCount.AddOrUpdate(channelId, 1, (_, old) => old + 1);
                if (misses >= MaxChecksBeforeDelete)
                {
                    await DeleteScheduledChannelAsync(scheduled.GuildId, scheduled.ChannelId).ConfigureAwait(false);
                    MissingChannelPassCount.TryRemove(channelId, out _);
                    return RoomPollResult.Removed();
                }

                return RoomPollResult.Failed(PollFailureKind.PartialResponse, TimeSpan.FromMinutes(1));
            }

            MissingChannelPassCount.TryRemove(channelId, out _);
            if (thread != null && !await KeepThreadAsync(scheduled, guildId, thread, cancellationToken).ConfigureAwait(false))
                return RoomPollResult.Removed();

            var roomInfo = await UrlClass.RoomInfo(
                scheduled.BaseUrl,
                scheduled.Room,
                cancellationToken).ConfigureAwait(false);
            if (roomInfo != null && scheduled.Port != roomInfo.LastPort.ToString(CultureInfo.InvariantCulture))
            {
                var port = roomInfo.LastPort.ToString(CultureInfo.InvariantCulture);
                await ChannelsAndUrlsCommands.UpdateChannelPortAsync(
                    scheduled.GuildId,
                    scheduled.ChannelId,
                    port).ConfigureAwait(false);
                if (ChannelConfigCache.TryGet(scheduled.GuildId, scheduled.ChannelId, out var cached))
                    ChannelConfigCache.Upsert(scheduled.GuildId, scheduled.ChannelId, cached with { Port = port });

                var gate = RateLimitGuards.GetGuildSendGate(guildId);
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await BotCommands.SendMessageAsync(
                        $"@everyone, {string.Format(Resource.NewPort, port)}",
                        scheduled.ChannelId).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            }

            return await GetTableDataResultAsync(
                scheduled.GuildId,
                scheduled.ChannelId,
                scheduled.BaseUrl,
                scheduled.Tracker,
                scheduled.Silent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return RoomPollResult.Failed(PollFailureKind.Unexpected);
        }
    }

    private static void LogScheduledRoomCompletion(RoomPollCompletion completion)
    {
        var (guildName, channelName) = ResolveScheduledRoomNames(completion.Room);
        var status = BuildScheduledRoomStatus(completion);
        var nextCheck = completion.State == null
            ? Resource.TDMSyncNoNextCheck
            : string.Format(
                Resource.TDMSyncNextCheck,
                completion.State.NextPollAtUtc.ToLocalTime().ToString("T", CultureInfo.CurrentCulture));

        Console.WriteLine(string.Format(
            Resource.TDMSyncLog,
            guildName,
            channelName,
            completion.Room.ChannelId,
            status,
            completion.Duration.TotalSeconds.ToString("0.00", CultureInfo.CurrentCulture),
            nextCheck));
    }

    private static (string GuildName, string ChannelName) ResolveScheduledRoomNames(
        ScheduledRoomDefinition room)
    {
        var guildName = room.GuildId;
        var channelName = room.ChannelId;

        if (!ulong.TryParse(room.GuildId, out var guildId) ||
            !ulong.TryParse(room.ChannelId, out var channelId))
        {
            return (guildName, channelName);
        }

        var guild = Declare.Client.GetGuild(guildId);
        if (guild == null)
            return (guildName, channelName);

        guildName = SanitizeLogLabel(guild.Name, guildName);
        var channel = guild.ThreadChannels.FirstOrDefault(candidate => candidate.Id == channelId)
            ?? guild.GetChannel(channelId);
        channelName = SanitizeLogLabel(channel?.Name, channelName);
        return (guildName, channelName);
    }

    private static string BuildScheduledRoomStatus(RoomPollCompletion completion)
    {
        var result = completion.Result;
        if (result.RemoveRoom)
            return Resource.TDMSyncTrackingEnded;
        if (!result.Success)
        {
            var failure = result.FailureKind switch
            {
                PollFailureKind.NotFound => Resource.TDMSyncFailureNotFound,
                PollFailureKind.RateLimited => Resource.TDMSyncFailureRateLimited,
                PollFailureKind.ServerError => Resource.TDMSyncFailureServer,
                PollFailureKind.Timeout => Resource.TDMSyncFailureTimeout,
                PollFailureKind.Network => Resource.TDMSyncFailureNetwork,
                PollFailureKind.InvalidContentType => Resource.TDMSyncFailureContentType,
                PollFailureKind.InvalidJson => Resource.TDMSyncFailureJson,
                PollFailureKind.PartialResponse => Resource.TDMSyncFailurePartial,
                PollFailureKind.CircuitOpen => Resource.TDMSyncFailureCircuitOpen,
                _ => Resource.TDMSyncFailureUnexpected
            };
            return string.Format(
                Resource.TDMSyncFailure,
                failure,
                completion.State?.ConsecutiveFailures ?? 1);
        }

        var changes = new List<string>(4);
        AddCountSummary(
            changes,
            result.NewItemCount,
            Resource.TDMSyncOneNewItem,
            Resource.TDMSyncManyNewItems);
        AddCountSummary(
            changes,
            result.NewHintCount,
            Resource.TDMSyncOneNewHint,
            Resource.TDMSyncManyNewHints);
        AddCountSummary(
            changes,
            result.UpdatedHintCount,
            Resource.TDMSyncOneUpdatedHint,
            Resource.TDMSyncManyUpdatedHints);
        AddCountSummary(
            changes,
            result.CompletedGoalCount,
            Resource.TDMSyncOneCompletedGoal,
            Resource.TDMSyncManyCompletedGoals);

        return changes.Count == 0
            ? Resource.TDMSyncNoNewItem
            : string.Join(", ", changes);
    }

    private static void AddCountSummary(
        ICollection<string> summaries,
        int count,
        string singular,
        string plural)
    {
        if (count <= 0)
            return;
        summaries.Add(count == 1 ? singular : string.Format(plural, count));
    }

    private static string SanitizeLogLabel(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static async Task<bool> KeepThreadAsync(
        ScheduledRoomDefinition scheduled,
        ulong guildId,
        SocketThreadChannel thread,
        CancellationToken cancellationToken)
    {
        var lastActivity = await ChannelsAndUrlsCommands.GetLastItemCheckAsync(
            scheduled.GuildId,
            scheduled.ChannelId).ConfigureAwait(false)
            ?? SnowflakeUtils.FromSnowflake(thread.Id);
        var daysSince = (DateTimeOffset.UtcNow - lastActivity).TotalDays;

        if (daysSince < 7)
        {
            if (Declare.WarnedThreads.Remove(scheduled.ChannelId))
            {
                await SendScheduledMessageAsync(
                    guildId,
                    scheduled.ChannelId,
                    string.Format(Resource.TDMNewMessageOnThread, thread.Name),
                    cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        if (daysSince < 14)
        {
            if (Declare.WarnedThreads.Add(scheduled.ChannelId))
            {
                var deletionDate = lastActivity.AddDays(14);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
                var localDeletionDate = TimeZoneInfo.ConvertTime(deletionDate, timeZone);
                var formatted = localDeletionDate.ToString(
                    "dddd d MMMM yyyy à HH'h'mm",
                    CultureInfo.GetCultureInfo($"{Declare.Language}-{Declare.Language.ToUpperInvariant()}"));
                await SendScheduledMessageAsync(
                    guildId,
                    scheduled.ChannelId,
                    string.Format(Resource.TDMNoMessage7Days, formatted, thread.Name),
                    cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        await SendScheduledMessageAsync(
            guildId,
            scheduled.ChannelId,
            string.Format(Resource.TDMNoActivity, thread.Name),
            cancellationToken).ConfigureAwait(false);
        await DeleteScheduledChannelAsync(scheduled.GuildId, scheduled.ChannelId).ConfigureAwait(false);
        Declare.WarnedThreads.Remove(scheduled.ChannelId);
        return false;
    }

    private static async Task SendScheduledMessageAsync(
        ulong guildId,
        string channelId,
        string message,
        CancellationToken cancellationToken)
    {
        var gate = RateLimitGuards.GetGuildSendGate(guildId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await BotCommands.SendMessageAsync(message, channelId).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task DeleteScheduledChannelAsync(string guildId, string channelId)
    {
        await DatabaseCommands.DeleteChannelDataAsync(guildId, channelId).ConfigureAwait(false);
        WebPortalPages.DeleteChannelPages(guildId, channelId);
        ChannelConfigCache.Remove(guildId, channelId);
    }

    internal static readonly HttpClient Http = HttpClientFactory.CreateJsonClient();
    internal static readonly ResilientWebHostClient WebHostClient = new(
        Http,
        new WebHostClientOptions
        {
            GlobalConcurrency = Declare.TrackingGlobalConcurrency,
            PerOriginConcurrency = Declare.TrackingOriginConcurrency
        });

    public static Task GetTableDataAsync(string guild, string channel, string baseUrl, string tracker, bool silent, bool isAddUrl)
        => GetTableDataAsync(guild, channel, baseUrl, tracker, silent, CancellationToken.None, isAddUrl);

    public static async Task GetTableDataAsync(string guild, string channel, string baseUrl, string tracker, bool silent, CancellationToken ctChan, bool isAddUrl = false)
    {
        _ = await GetTableDataResultAsync(
                guild,
                channel,
                baseUrl,
                tracker,
                silent,
                ctChan,
                isAddUrl)
            .ConfigureAwait(false);
    }

    internal static async Task<RoomPollResult> GetTableDataResultAsync(
        string guild,
        string channel,
        string baseUrl,
        string tracker,
        bool silent,
        CancellationToken ctChan,
        bool isAddUrl = false)
    {
        var ctx = await ProcessingContextLoader.LoadOneShotAsync(guild, channel, silent).ConfigureAwait(false);
        if (ctx.SlotIndex.Count == 0)
        {
            var configuration = await ChannelsAndUrlsCommands.GetChannelConfigAsync(guild, channel);
            var room = await UrlClass.RoomInfo(baseUrl, configuration.room, ctChan);
            if (room == null || room.Players.Count == 0) return RoomPollResult.Failed(PollFailureKind.PartialResponse);
            var slots = room.Players.Select((player, index) => (index + 1, player.Name, player.Game)).ToList();
            await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guild, channel, slots);
            ctx = await ProcessingContextLoader.LoadOneShotAsync(guild, channel, silent).ConfigureAwait(false);
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var rootUri) ||
            !Uri.TryCreate(rootUri, $"api/tracker/{tracker}", out var runtimeUri))
        {
            return RoomPollResult.Failed(PollFailureKind.Network);
        }

        var runtime = await WebHostClient.FetchJsonAsync(
            runtimeUri,
            WebHostEndpointKind.RuntimeTracker,
            ctChan).ConfigureAwait(false);
        if (!runtime.Success)
            return runtime.PollResult;
        var json = runtime.Json!;

        string? jsonStatic = null;
        var needsStatic = isAddUrl || Enumerable.Range(1, ctx.SlotIndex.Count)
            .Any(slot => !ctx.TryGetPlayerLocationsTotal(slot, out _));
        if (needsStatic)
        {
            var staticUri = new Uri(rootUri, $"api/static_tracker/{tracker}");
            var staticResult = await WebHostClient.FetchJsonAsync(
                staticUri,
                WebHostEndpointKind.StaticTracker,
                ctChan).ConfigureAwait(false);
            if (!staticResult.Success)
                return staticResult.PollResult;
            jsonStatic = staticResult.Json;
        }

        IReadOnlyDictionary<int, int> totalsBySlot;
        if (needsStatic)
        {
            totalsBySlot = TrackerStreamParser.ParsePlayerLocationTotals(jsonStatic!);
            foreach (var kvp in totalsBySlot)
                ctx.SetPlayerLocationsTotal(kvp.Key, kvp.Value);
        }
        else
        {
            var map = new Dictionary<int, int>();
            for (int slot = 1; slot <= ctx.SlotIndex.Count; slot++)
            {
                if (ctx.TryGetPlayerLocationsTotal(slot, out var total))
                    map[slot] = total;
            }

            totalsBySlot = map;
        }

        return await ProcessTrackerResponseAsync(ctx, json, totalsBySlot, silent, isAddUrl,
            ctChan).ConfigureAwait(false);
    }

    internal static async Task<RoomPollResult> ProcessTrackerResponseAsync(
        ProcessingContext ctx,
        string json,
        IReadOnlyDictionary<int, int> totalsBySlot,
        bool silent,
        bool isAddUrl,
        CancellationToken ctChan = default)
    {
        var guild = ctx.GuildId;
        var channel = ctx.ChannelId;
        var hasRecentActivity = WebHostActivity.HasRecentActivity(json, DateTimeOffset.UtcNow);
        NormalizedRoomSnapshot? normalizedSnapshot = null;
        try
        {
            normalizedSnapshot = WebHostSnapshotParser.FromWebHostResponse(
                ctx,
                json,
                totalsBySlot,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[TDM][Adaptive] Snapshot hash unavailable ({exception.GetType().Name}).");
        }

        if (normalizedSnapshot == null || ctx.SlotIndex.Count == 0)
            return RoomPollResult.Failed(PollFailureKind.PartialResponse);
        var v2Result = await new TrackingV2Store().ApplySnapshotAsync(
            normalizedSnapshot, [TrackingDestination.DiscordNotification(channel)], ctChan,
            enableNotifications: true).ConfigureAwait(false);
        if (isAddUrl && !silent && normalizedSnapshot.Hints.Length > 0)
        {
            var lines = normalizedSnapshot.Hints.Where(hint => hint.FinderSlot != hint.ReceiverSlot)
                .Select(hint => $"{Resource.HintNew}: {hint.ReceiverDisplayName}: " +
                    string.Format(Resource.HintItemNew, hint.ItemDisplayName, hint.LocationDisplayName, hint.FinderDisplayName));
            var content = string.Join("\n", lines).Replace("**", "");
            if (content.Length > 0)
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                await BotCommands.SendFileAsync(channel, stream, $"hints_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            }
        }

        await ChannelsAndUrlsCommands.UpdateLastCheckAsync(guild, channel);

        if (!isAddUrl)
        {
            if (normalizedSnapshot.IsComplete(SnapshotSections.Checks) && normalizedSnapshot.PlayerStates.Length > 0 &&
                normalizedSnapshot.PlayerStates.All(player => player.TotalChecks is > 0 && player.CompletedChecks >= player.TotalChecks) &&
                !await HasPendingNotificationsAsync(guild, channel, ctChan))
            {
                ulong guildIdLong = ulong.Parse(guild);
                await RateLimitGuards.GetGuildSendGate(guildIdLong).WaitAsync(ctChan);
                try
                {
                    await BotCommands.SendMessageAsync(Resource.Allcheckdone, channel);
                }
                finally
                {
                    RateLimitGuards.GetGuildSendGate(guildIdLong).Release();
                }

                await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                WebPortalPages.DeleteChannelPages(guild, channel);
                ChannelConfigCache.Remove(guild, channel);
                return RoomPollResult.Removed();
            }
        }

        var events = v2Result.Events ?? [];
        return RoomPollResult.Ok(
            normalizedSnapshot.ContentHash,
            events.OfType<ItemReceivedEvent>().Count(),
            events.OfType<HintCreatedEvent>().Count(),
            events.OfType<HintUpdatedEvent>().Count(),
            events.OfType<GoalReachedEvent>().Count(),
            hasRecentActivity);
    }

    private static async Task<bool> HasPendingNotificationsAsync(
        string guild,
        string channel,
        CancellationToken cancellationToken)
    {
        await using var connection = await Db.OpenReadAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT EXISTS (
            SELECT 1 FROM EventDeliveries delivery
            JOIN TrackingEvents event ON event.Id = delivery.EventId
            WHERE event.GuildId = @Guild AND event.ChannelId = @Channel
              AND delivery.DestinationType = @Type AND delivery.Status <> 'Delivered'
            UNION ALL
            SELECT 1 FROM TrackingReplayRequests
            WHERE GuildId=@Guild AND ChannelId=@Channel AND Status='Pending');";
        command.Parameters.AddWithValue("@Guild", guild);
        command.Parameters.AddWithValue("@Channel", channel);
        command.Parameters.AddWithValue("@Type", TrackingDestination.NotificationDestinationType);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    /// <summary>
    /// Parse des durées type "12h", "5m", "1d2h30m", "90m", etc.
    /// Min garanti à 5 minutes si tu le passes en paramètre.
    /// </summary>
    public static class CheckFrequencyParser
    {
        private static readonly Regex TokenRegex = new(
            @"(?ix)(?<num>\d+)\s*(?<unit>d|h|m|s)", RegexOptions.Compiled);

        public static bool TryParse(string? input, out TimeSpan result,
                                    TimeSpan? min = null, TimeSpan? max = null)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            long totalSeconds = 0;
            foreach (Match m in TokenRegex.Matches(input))
            {
                if (!long.TryParse(m.Groups["num"].Value, out var n) || n < 0) return false;
                var unit = m.Groups["unit"].Value.ToLowerInvariant();
                long factor = unit switch
                {
                    "d" => 86400,
                    "h" => 3600,
                    "m" => 60,
                    "s" => 1,
                    _ => 0
                };
                if (factor == 0) return false;
                checked { totalSeconds += n * factor; }
            }
            if (totalSeconds <= 0) return false;

            var ts = TimeSpan.FromSeconds(totalSeconds);
            if (min is not null && ts < min.Value) return false;
            if (max is not null && ts > max.Value) return false;

            result = ts;
            return true;
        }

        public static TimeSpan ParseOrDefault(string? input, TimeSpan @default,
                                              TimeSpan? min = null, TimeSpan? max = null)
            => TryParse(input, out var ts, min, max) ? ts : @default;
    }

    private static DateTimeOffset? TryParseIsoOrUnixMs(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(ms); }
            catch { return null; }
        }

        if (DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            return dto;
        }

        return null;
    }
}
