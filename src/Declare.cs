using Discord.Commands;
using Discord.WebSocket;
using System.Reflection;

public class Declare
{
    public static string Version = "0.6.6";
#if RC
    public static string ReleaseVersion = $"{Version}-rc1";
#else
    public static string ReleaseVersion = Version;
#endif
    public static string BotVersion = GetLocalSemVer();
    public static string BddVersion = "5.0.5";

    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? string.Empty;
    public static readonly bool ExportMetrics = (Environment.GetEnvironmentVariable("EXPORT_METRICS") ?? "false").Trim().ToLower() == "true";
    public static readonly string MetricsPort = Environment.GetEnvironmentVariable("METRICS_PORT") ?? string.Empty;
    public static readonly string UserIdForBigAsync = Environment.GetEnvironmentVariable("USER_ID_FOR_BIG_ASYNC") ?? string.Empty;
    public static readonly string AllowDiscordGuildId = Environment.GetEnvironmentVariable("ALLOW_DISCORD") ?? string.Empty;
    public static readonly bool EnableWebPortal = (Environment.GetEnvironmentVariable("ENABLE_WEB_PORTAL") ?? "true").Trim().ToLower() == "true";
    public static readonly string WebPortalPort = Environment.GetEnvironmentVariable("WEB_PORT") ?? "5199";
    public static readonly string WebPortalBaseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL") ?? string.Empty;


    public static readonly string Language = (Environment.GetEnvironmentVariable("LANGUAGE") ?? "en").ToLowerInvariant();
    public static List<string> AddedChannelId = new List<string>();
    public static readonly int MaxPlayer = 100;
    public static readonly int MaxThreadByGuild = 3;
    public static bool IsArchipelagoMode { get; set; } = false;
    public static bool IsBigAsync { get; set; } = false;
    public static bool UpdateBdd { get; set; } = false;

    public static CancellationTokenSource Cts = new CancellationTokenSource();
    public static DiscordSocketClient Client = new DiscordSocketClient();
    public static CommandService CommandService = new CommandService();
    public static IServiceProvider Services = default!;
    public static HashSet<string> WarnedThreads = new HashSet<string>();
    public const string DatabaseFile = "AST.db";
    public static HttpClient HttpClient = new HttpClient();
    public static string ProgramID { get; set; } = "";

    public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");

    public static string DownloadWinUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{ReleaseVersion}/Setup.Archipelago.{Version}.exe";
    public static string DownloadLinuxUrl = $"https://github.com/ArchipelagoMW/Archipelago/releases/download/{ReleaseVersion}/Archipelago_{Version}_linux-x86_64.tar.gz";

    public static string ArchipelagoLinuxTarGz = $"Archipelago_{Version}_linux-x86_64.tar.gz";

    public static string ArchivePath = Path.Combine(BasePath, "archive");
    public static string TempExtractPath = Path.Combine(BasePath, "tempExtract");
    public static string BddPath = Path.Combine(BasePath, "AST.db");
    public static string ExternalFolder = Path.Combine(BasePath, "extern");
    public static string WebPortalPath = Path.Combine(ExternalFolder, "portal");

    public static string VersionFile = Path.Combine(ExternalFolder, "versionFile.txt");
    public static string ExtractPath = Path.Combine(ExternalFolder, "Archipelago");
    public static string BackupPath = Path.Combine(ExternalFolder, $"backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

    public static string ItemCategoryPath = Path.Combine(ExtractPath, "ItemCategory");
    public static string PlayersPath = Path.Combine(ExtractPath, "Players");
    public static string CustomPath = Path.Combine(ExtractPath, "custom_worlds");
    public static string OutputPath = Path.Combine(ExtractPath, "output");
    public static string TempPath = Path.Combine(ExtractPath, "tmp");

    public static string RomBackupPath = Path.Combine(BackupPath, "rom_backup");
    public static string ApworldsBackupPath = Path.Combine(BackupPath, "apworlds_backup");
    public static string PlayersBackup = Path.Combine(BackupPath, "players_backup");

    public static string ApworldInfoSheet = "https://docs.google.com/spreadsheets/d/1iuzDTOAvdoNe8Ne8i461qGNucg5OuEoF-Ikqs8aUQZw/edit?usp=sharing";

    public static string GetLocalSemVer()
    {
        var asm = Assembly.GetEntryAssembly()!;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info)
            ? asm.GetName().Version?.ToString() ?? "0.0.0"
            : Normalize(info);
    }

    private static string Normalize(string v)
        => v.Trim().TrimStart('v', 'V').Split('+', '-', ' ').FirstOrDefault() ?? "0.0.0";
}
