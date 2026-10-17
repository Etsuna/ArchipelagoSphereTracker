using System.Diagnostics;
using System.Text;
using ArchipelagoSphereTracker.src.Resources;

internal static class PreparationProcessRunner
{
    internal sealed record Result(bool Success, string Diagnostics, bool TimedOut = false);

    internal static async Task<Result> RunAsync(ProcessStartInfo start, TimeSpan timeout)
    {
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = new Process { StartInfo = start };
        var log = new StringBuilder();
        var errors = new StringBuilder();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interactive = false;
        void Capture(string? line, bool isError)
        {
            if (line == null) return;
            lock (log)
            {
                // Keep the end: startup logs must not push the actual error out of the diagnostic.
                var target = isError ? errors : log;
                target.AppendLine(line[..Math.Min(line.Length, 6000)]);
                if (target.Length > 6000) target.Remove(0, target.Length - 6000);
                if (IsFatal(line)) failed.TrySetResult();
                if (line.Contains("Opening file input dialog", StringComparison.OrdinalIgnoreCase))
                {
                    interactive = true;
                    failed.TrySetResult();
                }
            }
        }
        process.OutputDataReceived += (_, args) => Capture(args.Data, false);
        process.ErrorDataReceived += (_, args) => Capture(args.Data, true);
        try
        {
            if (!process.Start()) return new(false, Resource.PrepGenerationFailed);
            // A server-side validation has no keyboard. In particular, Generate.py may prompt on error at exit.
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exit = process.WaitForExitAsync();
            using var stopTimeout = new CancellationTokenSource();
            var deadline = Task.Delay(timeout, stopTimeout.Token);
            var completed = await Task.WhenAny(exit, failed.Task, deadline).ConfigureAwait(false);
            var timedOut = completed == deadline;
            if (completed == failed.Task)
                await Task.WhenAny(exit, Task.Delay(TimeSpan.FromMilliseconds(500))).ConfigureAwait(false);
            if (!process.HasExited) process.Kill(entireProcessTree:true);
            await exit.ConfigureAwait(false);
            await stopTimeout.CancelAsync();
            lock (log)
            {
                var text = (errors.ToString().Trim() + "\n" + log.ToString().Trim()).Trim();
                if (interactive) text = Text("PrepInteractiveValidation") + "\n" + text;
                return new(process.ExitCode == 0 && !failed.Task.IsCompleted && !timedOut, text, timedOut);
            }
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            try { if (process.Id != 0 && !process.HasExited) { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); } }
            catch (Exception stopError) when (stopError is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            lock (log) return new(false, error.Message + "\n" + errors.ToString().Trim() + "\n" + log.ToString().Trim());
        }
    }

    private static bool IsFatal(string line) =>
        line.Contains("Uncaught exception", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Exception reading settings in file", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Exception reading weights in file", StringComparison.OrdinalIgnoreCase) ||
        IsMissingWorld(line);

    internal static bool IsMissingWorld(string text) =>
        text.Contains("No world found to handle game", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("No functional world found to handle game", StringComparison.OrdinalIgnoreCase);

    internal static string FailureMessage(Result result)
    {
        var summary = result.TimedOut ? Resource.GenerationTimeout :
            IsMissingWorld(result.Diagnostics) ? Text("PrepMissingWorld") : Resource.PrepGenerationFailed;
        return string.IsNullOrWhiteSpace(result.Diagnostics) ? summary : summary + "\n" + result.Diagnostics;
    }

    private static string Text(string key) => Resource.ResourceManager.GetString(key) ?? key;
}
