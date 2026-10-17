using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public sealed class PreparationProcessRunnerTests
{
    private sealed class ChildScript : IDisposable
    {
        private readonly string _path;
        public ProcessStartInfo Start { get; }
        public ChildScript(string windows, string unix)
        {
            _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + (OperatingSystem.IsWindows() ? ".ps1" : ".sh"));
            File.WriteAllText(_path, OperatingSystem.IsWindows() ? windows : unix);
            Start = new ProcessStartInfo(OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")
                : "/bin/sh");
            if (OperatingSystem.IsWindows())
            {
                Start.ArgumentList.Add("-NoProfile"); Start.ArgumentList.Add("-NonInteractive");
                Start.ArgumentList.Add("-ExecutionPolicy"); Start.ArgumentList.Add("Bypass"); Start.ArgumentList.Add("-File");
            }
            Start.ArgumentList.Add(_path);
        }
        public void Dispose() => File.Delete(_path);
    }

    [Fact]
    public async Task Missing_world_error_returns_without_waiting_for_interactive_confirmation()
    {
        using var script = new ChildScript(
            "[Console]::Error.WriteLine('Exception: No world found to handle game Ocarina of Time. Check your installation.'); $null = [Console]::ReadLine(); exit 1",
            "echo 'Exception: No world found to handle game Ocarina of Time. Check your installation.' >&2; read answer; exit 1");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(10));
        Assert.False(result.Success); Assert.False(result.TimedOut);
        Assert.Contains("Ocarina of Time", result.Diagnostics);
        Assert.Contains("APWorld", PreparationProcessRunner.FailureMessage(result));
    }

    [Fact]
    public async Task Fatal_validation_error_stops_a_process_that_keeps_running_and_releases_the_queue()
    {
        using var script = new ChildScript(
            "[Console]::Error.WriteLine('Exception reading settings in file player.yaml'); [Console]::Error.WriteLine('ValueError: option is invalid'); Start-Sleep -Seconds 60",
            "echo 'Exception reading settings in file player.yaml' >&2; echo 'ValueError: option is invalid' >&2; sleep 60");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(10));
        Assert.False(result.Success); Assert.False(result.TimedOut);
        Assert.Contains("ValueError: option is invalid", result.Diagnostics);
    }

    [Fact]
    public async Task Startup_log_volume_does_not_hide_the_error_on_stderr()
    {
        using var script = new ChildScript(
            "for ($i=0; $i -lt 500; $i++) { [Console]::Out.WriteLine('Startup log message padding padding padding padding padding') }; [Console]::Error.WriteLine('ValueError: option is invalid'); exit 1",
            "i=0; while [ $i -lt 500 ]; do echo 'Startup log message padding padding padding padding padding'; i=$((i+1)); done; echo 'ValueError: option is invalid' >&2; exit 1");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(10));
        Assert.False(result.Success); Assert.Contains("ValueError: option is invalid", result.Diagnostics);
        Assert.InRange(result.Diagnostics.Length, 1, 12001);
    }

    [Fact]
    public async Task A_fatal_diagnostic_is_not_treated_as_success_even_with_exit_code_zero()
    {
        using var script = new ChildScript(
            "[Console]::Error.WriteLine('Exception reading weights in file player.yaml'); exit 0",
            "echo 'Exception reading weights in file player.yaml' >&2; exit 0");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(10));
        Assert.False(result.Success); Assert.False(result.TimedOut);
        Assert.Contains("player.yaml", result.Diagnostics);
    }

    [Fact]
    public async Task An_unrelated_world_import_warning_is_not_a_failed_player_validation()
    {
        using var script = new ChildScript(
            "[Console]::Error.WriteLine('Traceback (most recent call last):'); [Console]::Error.WriteLine('ModuleNotFoundError: unrelated_optional_world'); [Console]::Out.WriteLine('Generation finished successfully'); exit 0",
            "echo 'Traceback (most recent call last):' >&2; echo 'ModuleNotFoundError: unrelated_optional_world' >&2; echo 'Generation finished successfully'; exit 0");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(10));
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Timeout_retains_available_diagnostics()
    {
        using var script = new ChildScript("[Console]::Out.WriteLine('validation started'); Start-Sleep -Seconds 60", "echo 'validation started'; sleep 60");
        var result = await PreparationProcessRunner.RunAsync(script.Start, TimeSpan.FromSeconds(2));
        Assert.False(result.Success); Assert.True(result.TimedOut);
        Assert.Contains("validation started", result.Diagnostics);
    }
}
