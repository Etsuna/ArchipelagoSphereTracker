using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchipelagoSphereTracker.src.Resources;
using YamlDotNet.RepresentationModel;

public sealed class PreparationService
{
    public static PreparationService Instance { get; } = new(new PreparationGenerator(),
        Path.Combine(Declare.BasePath, "data", "room-preparations"));
    private readonly IPreparationGenerator _generator;
    private readonly string _root;
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private readonly SemaphoreSlim _deliveryGate = new(1, 1);
    private readonly ConcurrentDictionary<string, Task> _jobs = new();
    public event Func<string, string, Task>? Changed;
    public const int MaximumYamlBytes = 1024 * 1024;

    public PreparationService(IPreparationGenerator generator, string root)
    {
        _generator = generator;
        _root = Path.GetFullPath(root);
    }

    private static SQLiteCommand Query(SQLiteConnection connection, string sql, params (string, object?)[] values)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in values) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }

    private static async Task Execute(SQLiteConnection connection, string sql, params (string, object?)[] values)
    {
        using var command = Query(connection, sql, values);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<PreparationRoom?> GetAsync(string guildId, string channelId)
    {
        await using var connection = await Db.OpenReadAsync().ConfigureAwait(false);
        return await LoadAsync(connection, guildId, channelId).ConfigureAwait(false);
    }

    private static async Task<PreparationRoom?> LoadAsync(SQLiteConnection connection, string guildId, string channelId)
    {
        using var query = Query(connection, "SELECT * FROM RoomPreparations WHERE GuildId=@g AND ChannelId=@c", ("@g", guildId), ("@c", channelId));
        PreparationRoom room;
        using (var reader = await query.ExecuteReaderAsync().ConfigureAwait(false))
        {
            if (!await reader.ReadAsync().ConfigureAwait(false)) return null;
            string Text(string key) => reader[key]?.ToString() ?? string.Empty;
            string? Nullable(string key) => reader[key] is DBNull ? null : Text(key);
            room = new(Text("Id"), guildId, Text("ParentChannelId"), channelId, Text("OwnerId"), Text("Title"),
                Convert.ToInt32(reader["IsPrivate"]) != 0, Convert.ToInt32(reader["MaximumPlayers"]),
                Enum.Parse<PreparationState>(Text("State")), Nullable("MessageId"), Nullable("RunId"), Nullable("ZipPath"),
                Nullable("TrackingChannelId"), Nullable("LastError"), [], []);
        }
        var participants = new List<PreparationParticipant>();
        using (var command = Query(connection, "SELECT * FROM PreparationParticipants WHERE PreparationId=@id ORDER BY UserId", ("@id", room.Id)))
        using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false))
                participants.Add(new(reader["UserId"].ToString()!, reader["DisplayName"].ToString()!, Convert.ToInt32(reader["Ready"]) != 0));
        var files = new List<PreparationFile>();
        using (var command = Query(connection, "SELECT * FROM PreparationFiles WHERE PreparationId=@id ORDER BY UserId,FileName", ("@id", room.Id)))
        using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false))
                files.Add(new(reader["Id"].ToString()!, reader["UserId"].ToString()!, reader["FileName"].ToString()!,
                    reader["Content"].ToString()!, reader["Hash"].ToString()!, reader["Validation"].ToString()!,
                    reader["Error"] is DBNull ? null : reader["Error"].ToString(),
                    JsonSerializer.Deserialize<List<PreparationSlot>>(reader["SlotsJson"].ToString()!) ?? []));
        string? lastRunStatus = null;
        if (room.RunId != null)
        {
            using var run = Query(connection, "SELECT State FROM PreparationRuns WHERE Id=@run", ("@run", room.RunId));
            lastRunStatus = (await run.ExecuteScalarAsync())?.ToString();
        }
        return room with { Participants = participants, Files = files, LastRunStatus = lastRunStatus };
    }

    public async Task<PreparationRoom> CreateAsync(string guild, string parent, string channel, string owner, string title, bool isPrivate, int maximumPlayers)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 80 || maximumPlayers is < 1 or > 200) throw new PreparationException(Resource.PrepInvalidSettings);
        await Db.WriteAsync(async connection =>
        {
            if (await LoadAsync(connection, guild, channel) != null) throw new PreparationException(Resource.PrepAlreadyExists);
            await Execute(connection, """
                INSERT INTO RoomPreparations(Id,GuildId,ParentChannelId,ChannelId,OwnerId,Title,IsPrivate,MaximumPlayers,State,CreatedAtUtc)
                VALUES(@id,@g,@p,@c,@o,@t,@private,@max,'Open',@now)
                """, ("@id", Guid.NewGuid().ToString("N")), ("@g", guild), ("@p", parent), ("@c", channel),
                ("@o", owner), ("@t", title), ("@private", isPrivate ? 1 : 0), ("@max", maximumPlayers),
                ("@now", DateTimeOffset.UtcNow.ToString("O")));
        });
        return (await GetAsync(guild, channel))!;
    }

    private static void Editable(PreparationRoom room)
    {
        if (!room.CanEdit) throw new PreparationException(Resource.PrepLocked);
    }
    private static void Owner(PreparationRoom room, string actor)
    {
        if (room.OwnerId != actor) throw new PreparationException(Resource.PrepOwnerOnly);
    }
    private static void Participant(PreparationRoom room, string actor)
    {
        if (!room.Participants.Any(player => player.UserId == actor)) throw new PreparationException(Resource.PrepJoinFirst);
    }

    private async Task MutateAsync(string guild, string channel, Func<SQLiteConnection, PreparationRoom, Task> work)
    {
        await Db.WriteAsync(async connection =>
        {
            var room = await LoadAsync(connection, guild, channel) ?? throw new PreparationException(Resource.PrepNotFound);
            await work(connection, room);
        });
        await NotifyAsync(guild, channel);
    }

    public Task JoinAsync(string guild, string channel, string actor, string name) => MutateAsync(guild, channel, async (connection, room) =>
    {
        if (room.Participants.Any(player => player.UserId == actor)) return;
        if (room.State != PreparationState.Open) throw new PreparationException(Resource.PrepRegistrationsClosed);
        if (room.Participants.Count >= room.MaximumPlayers) throw new PreparationException(Resource.PrepFull);
        await Execute(connection, "INSERT INTO PreparationParticipants(PreparationId,UserId,DisplayName,Ready) VALUES(@id,@u,@n,0)",
            ("@id", room.Id), ("@u", actor), ("@n", name[..Math.Min(name.Length, 100)]));
    });

    public Task LeaveAsync(string guild, string channel, string actor) => MutateAsync(guild, channel, async (connection, room) =>
    {
        Editable(room);
        await Execute(connection, "DELETE FROM PreparationParticipants WHERE PreparationId=@id AND UserId=@u", ("@id", room.Id), ("@u", actor));
        await InvalidateTestAsync(connection, room.Id);
    });

    public Task ReadyAsync(string guild, string channel, string actor, bool ready) => MutateAsync(guild, channel, async (connection, room) =>
    {
        Editable(room); Participant(room, actor);
        var files = room.Files.Where(file => file.UserId == actor).ToList();
        if (ready && (files.Count == 0 || files.Any(file => file.Validation != "Valid")))
            throw new PreparationException(Resource.PrepNeedValidYaml);
        await Execute(connection, "UPDATE PreparationParticipants SET Ready=@ready WHERE PreparationId=@id AND UserId=@u",
            ("@ready", ready ? 1 : 0), ("@id", room.Id), ("@u", actor));
    });

    public Task SetStateAsync(string guild, string channel, string actor, PreparationState state) => MutateAsync(guild, channel, async (connection, room) =>
    {
        Owner(room, actor); Editable(room);
        if (state is not (PreparationState.Open or PreparationState.Closed or PreparationState.Cancelled))
            throw new PreparationException(Resource.PrepInvalidSettings);
        await Execute(connection, "UPDATE RoomPreparations SET State=@state,LastError=NULL WHERE Id=@id", ("@state", state.ToString()), ("@id", room.Id));
    });

    public Task RemoveFileAsync(string guild, string channel, string actor, string fileId) => MutateAsync(guild, channel, async (connection, room) =>
    {
        Editable(room); Participant(room, actor);
        if (!room.Files.Any(file => file.Id == fileId && file.UserId == actor)) throw new PreparationException(Resource.PrepFileNotFound);
        await Execute(connection, "DELETE FROM PreparationFiles WHERE Id=@f AND PreparationId=@id AND UserId=@u", ("@f", fileId), ("@id", room.Id), ("@u", actor));
        await InvalidateTestAsync(connection, room.Id);
        await Execute(connection, "UPDATE PreparationParticipants SET Ready=0 WHERE PreparationId=@id AND UserId=@u", ("@id", room.Id), ("@u", actor));
    });

    public static async Task<string> ReadYamlAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (memory.Length + read > MaximumYamlBytes) throw new PreparationException(Resource.PrepInvalidYaml);
            await memory.WriteAsync(buffer.AsMemory(0, read));
        }
        try
        {
            var content = new UTF8Encoding(false, true).GetString(memory.ToArray()).TrimStart('\uFEFF');
            if (content.Contains('\0')) throw new PreparationException(Resource.PrepInvalidYaml);
            return content;
        }
        catch (DecoderFallbackException) { throw new PreparationException(Resource.PrepInvalidYaml); }
    }

    private static Task InvalidateTestAsync(SQLiteConnection connection, string roomId) =>
        Execute(connection, "UPDATE RoomPreparations SET RunId=NULL,LastError=NULL WHERE Id=@id", ("@id", roomId));

    public static IReadOnlyList<PreparationSlot> ParseSlots(string content)
    {
        if (Encoding.UTF8.GetByteCount(content) > MaximumYamlBytes) throw new PreparationException(Resource.PrepInvalidYaml);
        try
        {
            var parser = new YamlDotNet.Core.Parser(new StringReader(content));
            var depth = 0;
            var nodes = 0;
            while (parser.MoveNext())
            {
                if (++nodes > 100000) throw new PreparationException(Resource.PrepInvalidYaml);
                if (parser.Current is YamlDotNet.Core.Events.MappingStart or YamlDotNet.Core.Events.SequenceStart)
                    if (++depth > 64) throw new PreparationException(Resource.PrepInvalidYaml);
                if (parser.Current is YamlDotNet.Core.Events.MappingEnd or YamlDotNet.Core.Events.SequenceEnd) depth--;
            }
            var yaml = new YamlStream();
            yaml.Load(new StringReader(content));
            if (yaml.Documents.Count is < 1 or > 20) throw new PreparationException(Resource.PrepInvalidYaml);
            string Label(YamlNode? node, string fallback) => node switch
            {
                YamlScalarNode scalar when !string.IsNullOrWhiteSpace(scalar.Value) => scalar.Value[..Math.Min(scalar.Value.Length, 160)],
                YamlMappingNode mapping when mapping.Children.Count > 0 => string.Join(" / ", mapping.Children.Keys.OfType<YamlScalarNode>().Take(5).Select(key => key.Value)),
                _ => fallback
            };
            return yaml.Documents.Select((document, index) =>
            {
                if (document.RootNode is not YamlMappingNode mapping || !mapping.Children.TryGetValue(new YamlScalarNode("game"), out var game))
                    throw new PreparationException(Resource.PrepInvalidYaml);
                mapping.Children.TryGetValue(new YamlScalarNode("name"), out var name);
                return new PreparationSlot(Label(name, string.Format(Resource.PrepDefaultSlot, index + 1)), Label(game, "?"));
            }).ToArray();
        }
        catch (YamlDotNet.Core.YamlException) { throw new PreparationException(Resource.PrepInvalidYaml); }
        catch (ArgumentException) { throw new PreparationException(Resource.PrepInvalidYaml); }
    }

    public async Task<string> UploadStreamAsync(string guild, string channel, string actor, string fileName, Stream stream)
    {
        await UploadAsync(guild, channel, actor, fileName, await ReadYamlAsync(stream));
        return string.Format(Resource.ResourceManager.GetString("PrepYamlReceived")!, fileName);
    }

    public async Task UploadAsync(string guild, string channel, string actor, string fileName, string content)
    {
        if (_jobs.Count >= 200) throw new PreparationException(Resource.PrepTooManyFiles);
        if (!(FileUploadSecurity.TryGetSafeFileName(fileName, ".yaml", out _) || FileUploadSecurity.TryGetSafeFileName(fileName, ".yml", out _)))
            throw new PreparationException(Resource.PrepInvalidYaml);
        var slots = ParseSlots(content);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        var fileId = Guid.NewGuid().ToString("N");
        await MutateAsync(guild, channel, async (connection, room) =>
        {
            Editable(room); Participant(room, actor);
            await InvalidateTestAsync(connection, room.Id);
            var own = room.Files.Where(file => file.UserId == actor && file.FileName != fileName).ToArray();
            if (own.Length >= 20 || room.Files.Where(file => file.UserId != actor || file.FileName != fileName).Sum(file => file.Slots.Count) + slots.Count > 200 ||
                room.Files.Where(file => file.UserId != actor || file.FileName != fileName).Sum(file => Encoding.UTF8.GetByteCount(file.Content)) + Encoding.UTF8.GetByteCount(content) > 16 * 1024 * 1024)
                throw new PreparationException(Resource.PrepTooManyFiles);
            await Execute(connection, "DELETE FROM PreparationFiles WHERE PreparationId=@id AND UserId=@u AND FileName=@name",
                ("@id", room.Id), ("@u", actor), ("@name", fileName));
            await Execute(connection, """
                INSERT INTO PreparationFiles(Id,PreparationId,UserId,FileName,Content,Hash,Validation,SlotsJson)
                VALUES(@f,@id,@u,@name,@content,@hash,'Pending',@slots)
                """, ("@f", fileId), ("@id", room.Id), ("@u", actor), ("@name", fileName), ("@content", content),
                ("@hash", hash), ("@slots", JsonSerializer.Serialize(slots)));
            await Execute(connection, "UPDATE PreparationParticipants SET Ready=0 WHERE PreparationId=@id AND UserId=@u", ("@id", room.Id), ("@u", actor));
        });
        Launch(fileId, () => ValidateFileAsync(guild, channel, fileId, hash));
    }

    private void Launch(string key, Func<Task> action)
    {
        var task = Task.Run(action);
        _jobs[key] = task;
        _ = task.ContinueWith(completed => { _jobs.TryRemove(key, out _); _ = completed.Exception; }, TaskScheduler.Default);
    }
    public async Task WaitForJobsAsync() { while (!_jobs.IsEmpty) await Task.WhenAll(_jobs.Values.ToArray()); }

    private string JobDirectory(string roomId, string jobId) => Path.Combine(_root,
        Guid.ParseExact(roomId, "N").ToString("N"), Guid.ParseExact(jobId, "N").ToString("N"));
    private static async Task WriteInputAsync(string directory, IReadOnlyList<PreparationFile> files)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in files)
            await File.WriteAllTextAsync(Path.Combine(directory, Guid.ParseExact(file.Id, "N").ToString("N") + ".yaml"), file.Content, new UTF8Encoding(false));
    }

    private async Task ValidateFileAsync(string guild, string channel, string fileId, string hash)
    {
        await _processGate.WaitAsync();
        PreparationGenerationResult result;
        string? temporaryInput = null;
        try
        {
            var room = await GetAsync(guild, channel);
            var file = room?.Files.FirstOrDefault(file => file.Id == fileId && file.Hash == hash);
            if (room == null || file == null || !room.CanEdit) return;
            var directory = JobDirectory(room.Id, fileId);
            var input = Path.Combine(directory, "input");
            temporaryInput = Path.Combine(input, file.Id + ".yaml");
            await WriteInputAsync(input, [file]);
            result = await _generator.RunAsync(input, Path.Combine(directory, "output"), true, false);
        }
        catch { result = new(false, Resource.PrepGenerationFailed); }
        finally
        {
            if (temporaryInput != null)
                try { File.Delete(temporaryInput); } catch (IOException) { }
            _processGate.Release();
        }
        await Db.WriteAsync(async connection =>
        {
            using var command = Query(connection, """
                UPDATE PreparationFiles SET Validation=@v,Error=@e WHERE Id=@f AND Hash=@h
                """, ("@v", result.Success ? "Valid" : "Invalid"), ("@e", result.Success ? null : result.Message), ("@f", fileId), ("@h", hash));
            await command.ExecuteNonQueryAsync();
        });
        await NotifyAsync(guild, channel);
    }

    public async Task GenerateAsync(string guild, string channel, string actor, bool skipBalancing, bool testOnly = false)
    {
        var runId = Guid.NewGuid().ToString("N");
        PreparationRoom? frozen = null;
        await MutateAsync(guild, channel, async (connection, room) =>
        {
            Owner(room, actor);
            Editable(room);
            if (testOnly ? room.Files.Count == 0 || room.Files.Any(file => file.Validation != "Valid") : !room.CanGenerate)
                throw new PreparationException(Resource.PrepNotEveryoneReady);
            frozen = room;
            await Execute(connection, """
                INSERT INTO PreparationRuns(Id,PreparationId,SnapshotJson,State,CreatedAtUtc) VALUES(@run,@id,@snapshot,'Validating',@now)
                """, ("@run", runId), ("@id", room.Id), ("@snapshot", JsonSerializer.Serialize(room.Files)), ("@now", DateTimeOffset.UtcNow.ToString("O")));
            await Execute(connection, "UPDATE RoomPreparations SET State='Validating',RunId=@run,LastError=NULL WHERE Id=@id", ("@run", runId), ("@id", room.Id));
        });
        Launch(runId, () => RunGenerationAsync(frozen!, runId, skipBalancing, testOnly));
    }

    public async Task<string> ApplyActionAsync(string guild, string channel, string actor, string displayName, string action, bool skipBalancing = false)
    {
        switch (action)
        {
            case "join": await JoinAsync(guild, channel, actor, displayName); break;
            case "leave": await LeaveAsync(guild, channel, actor); break;
            case "ready": await ReadyAsync(guild, channel, actor, true); break;
            case "not-ready": await ReadyAsync(guild, channel, actor, false); break;
            case "close": await SetStateAsync(guild, channel, actor, PreparationState.Closed); break;
            case "reopen": await SetStateAsync(guild, channel, actor, PreparationState.Open); break;
            case "cancel": await SetStateAsync(guild, channel, actor, PreparationState.Cancelled); break;
            case "generate": await GenerateAsync(guild, channel, actor, skipBalancing); break;
            case "test": await GenerateAsync(guild, channel, actor, skipBalancing, testOnly:true); break;
            default: throw new PreparationException(Resource.PrepInvalidSettings);
        }
        return action is "generate" or "test" ? Resource.PrepQueued : Resource.PrepSaved;
    }

    private async Task RunGenerationAsync(PreparationRoom frozen, string runId, bool skipBalancing, bool testOnly)
    {
        await _processGate.WaitAsync();
        PreparationGenerationResult result;
        try
        {
            var directory = JobDirectory(frozen.Id, runId);
            var input = Path.Combine(directory, "input");
            await WriteInputAsync(input, frozen.Files);
            result = await _generator.RunAsync(input, Path.Combine(directory, "validation"), true, skipBalancing);
            if (result.Success && !testOnly)
            {
                await Db.WriteAsync(async connection =>
                {
                    await Execute(connection, "UPDATE RoomPreparations SET State='Generating' WHERE Id=@id AND RunId=@run", ("@id", frozen.Id), ("@run", runId));
                    await Execute(connection, "UPDATE PreparationRuns SET State='Generating' WHERE Id=@run", ("@run", runId));
                });
                await NotifyAsync(frozen.GuildId, frozen.ChannelId);
                result = await _generator.RunAsync(input, Path.Combine(directory, "output"), false, skipBalancing);
                if (result.Success && (result.ZipPath == null || !File.Exists(result.ZipPath))) result = new(false, Resource.GenerationZipNotFound);
            }
        }
        catch { result = new(false, Resource.PrepGenerationFailed); }
        finally { _processGate.Release(); }
        await Db.WriteAsync(async connection =>
        {
            await Execute(connection, "UPDATE RoomPreparations SET State=@state,ZipPath=@zip,LastError=@error WHERE Id=@id AND RunId=@run",
                ("@state", result.Success && !testOnly ? "Generated" : "Closed"), ("@zip", result.Success && !testOnly ? result.ZipPath : null),
                ("@error", result.Success ? null : result.Message), ("@id", frozen.Id), ("@run", runId));
            await Execute(connection, "UPDATE PreparationRuns SET State=@state WHERE Id=@run", ("@state", result.Success ? (testOnly ? "Validated" : "Generated") : "Failed"), ("@run", runId));
        });
        await NotifyAsync(frozen.GuildId, frozen.ChannelId);
    }

    public Task SaveMessageAsync(string guild, string channel, string messageId) => MutateAsync(guild, channel,
        (connection, room) => Execute(connection, "UPDATE RoomPreparations SET MessageId=@m WHERE Id=@id", ("@m", messageId), ("@id", room.Id)));

    public Task BeginAttachAsync(string guild, string channel, string actor) => MutateAsync(guild, channel, (connection, room) =>
    {
        Owner(room, actor);
        if (room.State != PreparationState.Generated || room.TrackingChannelId != null) throw new PreparationException(Resource.PrepLocked);
        return Execute(connection, "UPDATE RoomPreparations SET State='Attaching' WHERE Id=@id", ("@id", room.Id));
    });

    public Task CompleteAttachAsync(string guild, string channel, string actor, string? trackingChannelId) => MutateAsync(guild, channel, (connection, room) =>
    {
        Owner(room, actor);
        if (room.State != PreparationState.Attaching) throw new PreparationException(Resource.PrepLocked);
        return Execute(connection, "UPDATE RoomPreparations SET State='Generated',TrackingChannelId=@tracking WHERE Id=@id", ("@tracking", trackingChannelId), ("@id", room.Id));
    });

    public string? GetArchivePath(PreparationRoom room, string actor)
    {
        if (room.State != PreparationState.Generated || room.ZipPath == null || room.RunId == null ||
            (room.OwnerId != actor && !room.Participants.Any(player => player.UserId == actor))) return null;
        var expected = Path.GetFullPath(Path.Combine(JobDirectory(room.Id, room.RunId), "output")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(room.ZipPath);
        return path.StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? path : null;
    }

    public async Task DeliverArchiveAsync(string guild, string channel, Func<PreparationRoom, string, Task<string>> send)
    {
        await _deliveryGate.WaitAsync();
        try
        {
            var room = await GetAsync(guild, channel);
            if (room == null) return;
            var archive = GetArchivePath(room, room.OwnerId);
            if (archive == null) return;
            await using (var connection = await Db.OpenReadAsync())
            {
                using var query = Query(connection, "SELECT MessageId FROM PreparationArchiveDeliveries WHERE RunId=@run", ("@run", room.RunId));
                if (await query.ExecuteScalarAsync() != null) return;
            }
            var messageId = await send(room, archive);
            await Db.WriteAsync(connection => Execute(connection,
                "INSERT INTO PreparationArchiveDeliveries(RunId,MessageId,DeliveredAtUtc) VALUES(@run,@message,@now)",
                ("@run", room.RunId), ("@message", messageId), ("@now", DateTimeOffset.UtcNow.ToString("O"))));
        }
        finally { _deliveryGate.Release(); }
    }

    public async Task RecoverInterruptedAsync()
    {
        await Db.WriteAsync(async connection =>
        {
            await Execute(connection, "UPDATE RoomPreparations SET State='Closed',LastError=@e WHERE State IN ('Validating','Generating')", ("@e", Resource.PrepInterrupted));
            await Execute(connection, "UPDATE RoomPreparations SET State='Generated',LastError=@e WHERE State='Attaching'", ("@e", Resource.PrepInterrupted));
            await Execute(connection, "UPDATE PreparationFiles SET Validation='Invalid',Error=@e WHERE Validation='Pending'", ("@e", Resource.PrepInterrupted));
            await Execute(connection, "UPDATE PreparationParticipants SET Ready=0 WHERE PreparationId IN (SELECT PreparationId FROM PreparationFiles WHERE Validation!='Valid')");
            await Execute(connection, "UPDATE PreparationRuns SET State='Interrupted' WHERE State IN ('Validating','Generating')");
        });
    }

    private async Task NotifyAsync(string guild, string channel)
    {
        if (Changed == null) return;
        foreach (Func<string,string,Task> handler in Changed.GetInvocationList())
            try { await handler(guild, channel); } catch { /* Discord diagnostics cannot roll back a durable action. */ }
    }
}
