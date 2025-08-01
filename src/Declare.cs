using Discord.WebSocket;
using Discord.Commands;

public class Declare
{
    public static string Version = "0.6.2";
    public static string BotVersion = "3.2.1";

    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;
    public static readonly bool IsDev = (Environment.GetEnvironmentVariable("IS_DEV") ?? "false").ToLower() == "true";
    public static readonly bool TelemetryEnabled = (Environment.GetEnvironmentVariable("TELEMETRY") ?? "true").ToLower() == "true";

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
    public const string DatabaseFile = "AST.db";
    public static HttpClient HttpClient = new HttpClient();
    public static string ProgramID { get; set; } = "";

    public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");

    public static string DownloadWinUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{Version}/Setup.Archipelago.{Version}.exe";
    public static string DownloadLinuxUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{Version}/Archipelago_{Version}_linux-x86_64.tar.gz";
    public static string DownloadInnoExtractor = $"https://constexpr.org/innoextract/files/innoextract-1.9-windows.zip";

    public static string ArchivePath = Path.Combine(BasePath, "archive");
    public static string TempExtractPath = Path.Combine(BasePath, "tempExtract");
    public static string BddPath = Path.Combine(BasePath, "AST.db");
    public static string ExternalFolder = Path.Combine(BasePath, "extern");
    public static string ScanItemsPath = "ArchipelagoSphereTracker.apworld.scan_items.apworld";
    public static string GenerateTemplatesPath = "ArchipelagoSphereTracker.apworld.generate_templates.apworld";

    public static string VersionFile = Path.Combine(ExternalFolder, "versionFile.txt");
    public static string ExtractPath = Path.Combine(ExternalFolder, "Archipelago");
    public static string BackupPath = Path.Combine(ExternalFolder, $"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

    public static string ItemCategoryPath = Path.Combine(ExtractPath, "ItemCategory");
    public static string PlayersPath = Path.Combine(ExtractPath, "Players");
    public static string CustomPath = Path.Combine(ExtractPath, "custom_worlds");

    public static string RomBackupPath = Path.Combine(BackupPath, "rom_backup");
    public static string ApworldsBackupPath = Path.Combine(BackupPath, "apworlds_backup");
    public static string PlayersBackup = Path.Combine(BackupPath, "players_backup");
}
