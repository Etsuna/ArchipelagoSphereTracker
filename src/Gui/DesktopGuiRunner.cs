using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;

public sealed class GuiApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GuiMainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}

public static class DesktopGuiRunner
{
    public static void Run(string[] args)
    {
        GuiConfigManager.EnsureEnvFileExists();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<GuiApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
