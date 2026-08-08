using Avalonia;

namespace AST.Companion;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var directory = Path.Combine(root, "AST.Companion");
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "startup-error.log"),
                    $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}");
            }
            catch
            {
                // Best effort only: never mask the original startup exception.
            }

            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
