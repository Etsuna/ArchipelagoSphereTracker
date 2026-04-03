using Discord;
using Discord.WebSocket;
using DotNetEnv;
using System.Diagnostics;

public static class GuiBotController
{
    private static Process? _botProcess;
    private static readonly object LockObj = new();
    private static readonly List<string> Logs = new();

    public static Dictionary<string, string> GetConfig()
    {
        return GuiConfigManager.ReadEnv();
    }

    public static void SaveConfig(Dictionary<string, string> values)
    {
        GuiConfigManager.SaveEnv(values);
        Env.Load(GuiConfigManager.EnvPath);
        AddLog("Configuration .env sauvegardée.");
    }

    public static async Task<(bool ok, string message)> TestDiscordTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "DISCORD_TOKEN est vide.");

        var config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.None };
        using var client = new DiscordSocketClient(config);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.Ready += () =>
            {
                readyTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            using var reg = cts.Token.Register(() => readyTcs.TrySetCanceled(cts.Token));
            await readyTcs.Task;

            await client.StopAsync();
            await client.LogoutAsync();
            AddLog("Test Discord réussi.");
            return (true, "Connexion Discord réussie.");
        }
        catch (Exception ex)
        {
            AddLog($"Test Discord échoué: {ex.Message}");
            return (false, $"Connexion Discord échouée: {ex.Message}");
        }
    }

    public static (bool ok, string message) StartBot(string mode)
    {
        lock (LockObj)
        {
            if (_botProcess != null && !_botProcess.HasExited)
                return (false, "Le bot est déjà démarré.");

            try
            {
                var startInfo = BuildStartInfo(mode);
                _botProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _botProcess.OutputDataReceived += (_, e) => AddLog(e.Data);
                _botProcess.ErrorDataReceived += (_, e) => AddLog(e.Data);
                _botProcess.Exited += (_, _) => AddLog("Process bot arrêté.");
                _botProcess.Start();
                _botProcess.BeginOutputReadLine();
                _botProcess.BeginErrorReadLine();
                AddLog($"Bot démarré en mode {mode}");
                return (true, "Bot démarré.");
            }
            catch (Exception ex)
            {
                AddLog($"Erreur démarrage bot: {ex.Message}");
                return (false, $"Erreur: {ex.Message}");
            }
        }
    }

    public static (bool ok, string message) StopBot()
    {
        lock (LockObj)
        {
            if (_botProcess == null || _botProcess.HasExited)
                return (true, "Le bot est déjà arrêté.");

            try
            {
                _botProcess.Kill(true);
                AddLog("Bot arrêté depuis le GUI.");
                return (true, "Bot arrêté.");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur arrêt: {ex.Message}");
            }
        }
    }

    public static bool IsRunning()
    {
        lock (LockObj)
            return _botProcess != null && !_botProcess.HasExited;
    }

    public static string ReadLogs()
    {
        lock (LockObj)
            return string.Join(Environment.NewLine, Logs);
    }

    private static ProcessStartInfo BuildStartInfo(string mode)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null.");

        if (processPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{processPath}\" {mode}",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = mode,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    private static void AddLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (LockObj)
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (Logs.Count > 300)
                Logs.RemoveRange(0, Logs.Count - 300);
        }
    }
}
