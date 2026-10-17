using ArchipelagoSphereTracker.src.Resources;

public enum PreparationState { Open, Closed, Validating, Generating, Generated, Attaching, Cancelled }
public sealed record PreparationSlot(string Name, string Game);
public sealed record PreparationYamlUpload(string FileName, string Content);
public sealed record PreparationFile(string Id, string UserId, string FileName, string Content, string Hash,
    string Validation, string? Error, IReadOnlyList<PreparationSlot> Slots);
public sealed record PreparationParticipant(string UserId, string DisplayName, bool Ready);
public sealed record PreparationRoom(string Id, string GuildId, string ParentChannelId, string ChannelId,
    string OwnerId, string Title, bool IsPrivate, int MaximumPlayers, PreparationState State,
    string? MessageId, string? RunId, string? ZipPath, string? TrackingChannelId, string? LastError,
    IReadOnlyList<PreparationParticipant> Participants, IReadOnlyList<PreparationFile> Files, string? LastRunStatus = null)
{
    public bool CanGenerate => (State is PreparationState.Open or PreparationState.Closed) &&
        Participants.Count > 0 && Participants.All(player => player.Ready &&
            Files.Any(file => file.UserId == player.UserId) &&
            Files.Where(file => file.UserId == player.UserId).All(file => file.Validation == "Valid"));
    public bool CanEdit => State is PreparationState.Open or PreparationState.Closed;
}
public sealed class PreparationException(string message) : Exception(message);
public sealed record PreparationGenerationResult(bool Success, string Message, string? ZipPath = null);
public interface IPreparationGenerator
{
    Task<PreparationGenerationResult> RunAsync(string inputDirectory, string outputDirectory, bool testOnly, bool skipBalancing);
}

public static class PreparationSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS RoomPreparations (
            Id TEXT PRIMARY KEY, GuildId TEXT NOT NULL, ParentChannelId TEXT NOT NULL,
            ChannelId TEXT NOT NULL, OwnerId TEXT NOT NULL, Title TEXT NOT NULL,
            IsPrivate INTEGER NOT NULL, MaximumPlayers INTEGER NOT NULL,
            State TEXT NOT NULL CHECK(State IN ('Open','Closed','Validating','Generating','Generated','Attaching','Cancelled')),
            MessageId TEXT, RunId TEXT, ZipPath TEXT, TrackingChannelId TEXT, LastError TEXT,
            CreatedAtUtc TEXT NOT NULL, UNIQUE(GuildId,ChannelId));
        CREATE TABLE IF NOT EXISTS PreparationParticipants (
            PreparationId TEXT NOT NULL REFERENCES RoomPreparations(Id) ON DELETE CASCADE,
            UserId TEXT NOT NULL, DisplayName TEXT NOT NULL, Ready INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY(PreparationId,UserId));
        CREATE TABLE IF NOT EXISTS PreparationFiles (
            Id TEXT PRIMARY KEY, PreparationId TEXT NOT NULL, UserId TEXT NOT NULL,
            FileName TEXT NOT NULL, Content TEXT NOT NULL, Hash TEXT NOT NULL,
            Validation TEXT NOT NULL CHECK(Validation IN ('Pending','Valid','Invalid')),
            Error TEXT, SlotsJson TEXT NOT NULL,
            FOREIGN KEY(PreparationId,UserId) REFERENCES PreparationParticipants(PreparationId,UserId) ON DELETE CASCADE,
            UNIQUE(PreparationId,UserId,FileName));
        CREATE TABLE IF NOT EXISTS PreparationRuns (
            Id TEXT PRIMARY KEY, PreparationId TEXT NOT NULL REFERENCES RoomPreparations(Id) ON DELETE CASCADE,
            SnapshotJson TEXT NOT NULL, State TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS idx_preparations_guild ON RoomPreparations(GuildId,State);
        CREATE TABLE IF NOT EXISTS PreparationArchiveDeliveries (
            RunId TEXT PRIMARY KEY REFERENCES PreparationRuns(Id) ON DELETE CASCADE,
            MessageId TEXT NOT NULL, DeliveredAtUtc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS PreparationReminders (
            PreparationId TEXT PRIMARY KEY REFERENCES RoomPreparations(Id) ON DELETE CASCADE,
            LastSentAtUtc TEXT NOT NULL);
        """;
}
