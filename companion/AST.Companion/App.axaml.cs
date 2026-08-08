using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Windows.Input;

namespace AST.Companion;

public sealed partial class App : Application
{
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private bool _quitting;

    public ICommand ToggleAsterCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ToggleTopmostCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand QuitCommand { get; }

    public App()
    {
        ToggleAsterCommand = new RelayCommand(() => _mainWindow?.ToggleVisibility());
        SettingsCommand = new RelayCommand(ShowSettings);
        ToggleTopmostCommand = new RelayCommand(() =>
        {
            if (_mainWindow is not null)
                _ = _mainWindow.SetAlwaysOnTopAsync(!_mainWindow.AlwaysOnTop);
        });
        ReconnectCommand = new RelayCommand(() =>
        {
            if (_mainWindow is not null)
                _ = _mainWindow.ReconnectAsync();
        });
        QuitCommand = new RelayCommand(Quit);
        DataContext = this;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;
            _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowSettings()
    {
        if (_mainWindow is null || _quitting)
            return;

        _settingsWindow ??= new SettingsWindow(_mainWindow);
        _settingsWindow.RefreshFromMain();

        if (!_settingsWindow.IsVisible)
            _settingsWindow.Show();

        _settingsWindow.Activate();
    }

    public void Quit()
    {
        if (_quitting)
            return;

        _quitting = true;
        _settingsWindow?.RequestExit();
        _mainWindow?.RequestExit();
        _desktop?.Shutdown();
    }
}
