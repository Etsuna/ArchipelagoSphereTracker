using System.Globalization;
using System.Text.Json;
using System.Data.SQLite;
using ArchipelagoSphereTracker.src.Resources;

public sealed partial class PreparationService
{
    private static async Task<bool> ResolveManagerAsync(string guild, string channel, string user)
    {
        if (!ulong.TryParse(user, out var id)) return false;
        var context = await AstAuthorizationService.CreateDiscordContextAsync(guild, channel, id);
        return context != null && AstAuthorizationService.CanUseArchipelagoTools(context) &&
            AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, context);
    }

    public async Task<bool> CanManageAsync(string guild, string channel, string actor)
    {
        var room = await GetAsync(guild, channel);
        return room != null && (room.OwnerId == actor || await _manager(guild, channel, actor));
    }

    private async Task MutateManagedAsync(string guild, string channel, string actor,
        Func<SQLiteConnection,PreparationRoom,Task> work, bool notify = true, string? expectedRoom = null)
    {
        var original = await GetAsync(guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
        if (expectedRoom != null && original.Id != expectedRoom) throw new PreparationException(Resource.PrepNotFound);
        // Resolve live permissions before taking the SQLite writer. Never turn an
        // authorization for an old preparation into permission on its replacement.
        var manager = original.OwnerId != actor && await _manager(guild, channel, actor);
        await Db.WriteAsync(async connection => {
            var current = await LoadAsync(connection,guild,channel) ?? throw new PreparationException(Resource.PrepNotFound);
            if (current.Id != original.Id) throw new PreparationException(Resource.PrepNotFound);
            if (current.OwnerId != actor && !manager) throw new PreparationException(Resource.PrepOwnerOnly);
            await work(connection, current);
        });
        if (notify) await NotifyAsync(guild,channel);
    }

    private static int ReminderStage(TimeSpan remaining) => remaining <= TimeSpan.FromHours(1) ? 3 :
        remaining <= TimeSpan.FromHours(6) ? 2 : remaining <= TimeSpan.FromHours(24) ? 1 : 0;

    public static int? ParseTimerHours(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (!int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) || hours is < 0 or > 720)
            throw new PreparationException(Text("PrepTimerInvalid"));
        return hours;
    }

    public Task ScheduleAsync(string guild, string channel, string actor, int? hours, bool skipBalancing = false,
        string? webHostUserId = null, string? expectedRoom = null)
        => MutateManagedAsync(guild, channel, actor, async (connection, room) => {
            if (!room.CanEdit) throw new PreparationException(Resource.PrepLocked);
            if (hours is < 0 or > 720) throw new PreparationException(Text("PrepTimerInvalid"));
            if (hours is null or 0)
            {
                await Execute(connection, "DELETE FROM PreparationSchedules WHERE PreparationId=@id", ("@id",room.Id));
                if (room.NeedsWebHostSession) await ClearHostingCredentialAsync(connection, room.Id);
                return;
            }
            if (room.NeedsWebHostSession) await SaveHostingCredentialAsync(connection, room, webHostUserId);
            await SaveScheduleAsync(connection, room.Id, hours.Value, skipBalancing);
        }, expectedRoom:expectedRoom);

    private Task SaveScheduleAsync(SQLiteConnection connection, string roomId, int hours, bool skipBalancing)
    {
        var duration = TimeSpan.FromHours(hours);
        return Execute(connection, """
            INSERT INTO PreparationSchedules(PreparationId,DeadlineUtc,State,SkipBalancing,ReminderStage)
            VALUES(@id,@due,'Scheduled',@skip,@stage)
            ON CONFLICT(PreparationId) DO UPDATE SET DeadlineUtc=@due,State='Scheduled',SkipBalancing=@skip,ReminderStage=@stage,ExcludedJson='[]'
            """, ("@id",roomId), ("@due",(_clock.GetUtcNow()+duration).ToString("O")),
            ("@skip",skipBalancing ? 1 : 0), ("@stage",ReminderStage(duration)));
    }

    /// <summary>Persist each reminder/claim before Discord or process work. A timer
    /// never blindly retries generation after a failure or an interrupted process.</summary>
    public async Task RunSchedulesOnceAsync(Func<PreparationRoom,IReadOnlyList<PreparationReminder>,bool,Task> send,
        CancellationToken ct = default)
    {
        var rooms = new List<(string Guild,string Channel)>();
        await using (var connection = await Db.OpenReadAsync())
        {
            using var query = Query(connection, """
                SELECT room.GuildId,room.ChannelId FROM PreparationSchedules schedule
                JOIN RoomPreparations room ON room.Id=schedule.PreparationId
                WHERE schedule.State='Scheduled' AND room.State IN ('Open','Closed')
                ORDER BY schedule.DeadlineUtc
                """);
            using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) rooms.Add((reader.GetString(0),reader.GetString(1)));
        }
        foreach (var (guild, channel) in rooms)
        {
            ct.ThrowIfCancellationRequested();
            PreparationRoom? notice = null, frozen = null;
            IReadOnlyList<PreparationReminder> users = [];
            string? run = null;
            var due = false;
            try
            {
                await Db.WriteAsync(async connection => {
                    var room = await LoadAsync(connection, guild, channel);
                    if (room is not { CanEdit: true, Schedule.State: "Scheduled" }) return;
                    var remaining = room.Schedule.Deadline - _clock.GetUtcNow();
                    due = remaining <= TimeSpan.Zero;
                    if (!due)
                    {
                        var stage = ReminderStage(remaining);
                        if (stage <= room.Schedule.ReminderStage) return;
                        await Execute(connection, "UPDATE PreparationSchedules SET ReminderStage=@stage WHERE PreparationId=@id", ("@stage",stage),("@id",room.Id));
                        users = room.Participants.Where(player => !player.Ready || !room.Files.Any(file => file.UserId==player.UserId) ||
                                room.Files.Any(file => file.UserId==player.UserId && file.Validation!="Valid"))
                            .Select(player => new PreparationReminder(player.UserId,room.Files.Any(file => file.UserId==player.UserId))).ToArray();
                        if (users.Count > 0)
                        {
                            using var cooldown = Query(connection,"SELECT LastSentAtUtc FROM PreparationReminders WHERE PreparationId=@id",("@id",room.Id));
                            var last = (await cooldown.ExecuteScalarAsync())?.ToString();
                            if (DateTimeOffset.TryParse(last,CultureInfo.InvariantCulture,out var timestamp) && timestamp > _clock.GetUtcNow().AddMinutes(-5)) return;
                            await Execute(connection,"INSERT OR REPLACE INTO PreparationReminders(PreparationId,LastSentAtUtc) VALUES(@id,@now)",
                                ("@id",room.Id),("@now",_clock.GetUtcNow().ToString("O")));
                            notice = room;
                        }
                        return;
                    }
                    var eligible = room.Participants.Where(player => player.Ready && room.Files.Any(file => file.UserId==player.UserId) &&
                        room.Files.Where(file => file.UserId==player.UserId).All(file => file.Validation=="Valid")).ToArray();
                    var included = eligible.Select(player => player.UserId).ToHashSet(StringComparer.Ordinal);
                    users = room.Participants.Where(player => !included.Contains(player.UserId))
                        .Select(player => new PreparationReminder(player.UserId,room.Files.Any(file => file.UserId==player.UserId))).ToArray();
                    string? failure = null;
                    if (eligible.Length == 0) failure = Text("PrepTimerEmpty");
                    else if (room.NeedsWebHostSession && await ReadHostingSessionAsync(connection, room) == null)
                        failure = Resource.PrepWebHostTimerMissingSession;
                    await Execute(connection, "UPDATE PreparationSchedules SET State=@state,ExcludedJson=@excluded WHERE PreparationId=@id",
                        ("@state",failure != null ? "Failed" : "Started"),("@excluded",JsonSerializer.Serialize(users)),("@id",room.Id));
                    if (failure != null)
                    {
                        if (room.NeedsWebHostSession) await ClearHostingCredentialAsync(connection, room.Id);
                        await Execute(connection, "UPDATE RoomPreparations SET State='Closed',LastError=@error WHERE Id=@id", ("@error",failure),("@id",room.Id));
                        notice = room with { State=PreparationState.Closed, LastError=failure };
                        return;
                    }
                    run = Guid.NewGuid().ToString("N");
                    frozen = room with { Participants=eligible, Files=room.Files.Where(file => included.Contains(file.UserId)).ToArray(), LastError=null };
                    await Execute(connection, """
                        INSERT INTO PreparationRuns(Id,PreparationId,SnapshotJson,State,CreatedAtUtc)
                        VALUES(@run,@id,@snapshot,'Validating',@now)
                        """, ("@run",run),("@id",room.Id),("@snapshot",JsonSerializer.Serialize(frozen.Files)),("@now",_clock.GetUtcNow().ToString("O")));
                    await Execute(connection, "UPDATE RoomPreparations SET State='Validating',RunId=@run,LastError=NULL WHERE Id=@id", ("@run",run),("@id",room.Id));
                    notice = frozen;
                });
                if (notice != null)
                {
                    try { await send(notice, users, due).WaitAsync(TimeSpan.FromSeconds(30),ct); }
                    catch (Exception error) { Console.WriteLine($"[Preparation] Scheduled notice failed ({error.GetType().Name})."); }
                    // A failed notice cannot leave a durably claimed generation stuck.
                    if (frozen != null && run != null)
                    {
                        var job = run;
                        var input = frozen;
                        Launch(job, () => RunGenerationAsync(input, job, input.Schedule!.SkipBalancing, false));
                    }
                    await NotifyAsync(guild, channel);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error) { Console.WriteLine($"[Preparation] Timer processing failed ({error.GetType().Name})."); }
        }
    }
}
