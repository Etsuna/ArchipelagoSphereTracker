using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

public sealed class GuiMainWindow : Window
{
    private readonly TextBox _token = new() { PasswordChar = '•' };
    private readonly ComboBox _language = new() { ItemsSource = new[] { "en", "fr" }, SelectedIndex = 0 };
    private readonly TextBox _webPort = new();
    private readonly TextBox _webBaseUrl = new();
    private readonly ComboBox _exportMetrics = new() { ItemsSource = new[] { "false", "true" }, SelectedIndex = 0 };
    private readonly TextBox _metricsPort = new();

    private readonly ComboBox _mode = new() { ItemsSource = new[] { "--normalmode", "--archipelagomode", "--bigasync", "--updatebdd" }, SelectedIndex = 0 };
    private readonly TextBlock _status = new() { Text = "Stopped" };
    private readonly TextBlock _message = new();
    private readonly TextBox _logs = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public GuiMainWindow()
    {
        Title = "ArchipelagoSphereTracker - GUI Admin";
        Width = 1200;
        Height = 950;
        MinWidth = 1000;
        MinHeight = 700;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse("#EEF3FF"));

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Avalonia.Thickness(16)
        };

        root.Children.Add(new TextBlock
        {
            Text = "ArchipelagoSphereTracker Desktop GUI (FR/EN)",
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1C2B5A")),
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        });

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(content, 1);

        content.Children.Add(BuildConfigPanel());
        content.Children.Add(BuildControlPanel());
        Grid.SetColumn(content.Children[1], 1);

        var logsPanel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0, 12, 0, 0) };
        logsPanel.Children.Add(new TextBlock { Text = "Live logs", Foreground = new SolidColorBrush(Color.Parse("#34446E")), FontWeight = FontWeight.Medium });
        _logs.Height = 320;
        _logs.Background = new SolidColorBrush(Color.Parse("#F8FAFF"));
        _logs.BorderBrush = new SolidColorBrush(Color.Parse("#C9D6F5"));
        _logs.BorderThickness = new Avalonia.Thickness(1);
        logsPanel.Children.Add(_logs);

        Grid.SetColumnSpan(logsPanel, 2);
        Grid.SetRow(logsPanel, 1);
        content.Children.Add(logsPanel);

        root.Children.Add(content);
        Content = root;

        LoadConfig();
        StartRefreshTimer();
    }

    private Control BuildConfigPanel()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0, 0, 8, 0) };
        panel.Children.Add(new TextBlock { Text = "Configuration .env", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#2A3F78")) });

        panel.Children.Add(new TextBlock { Text = "DISCORD_TOKEN" });
        panel.Children.Add(_token);
        panel.Children.Add(new TextBlock { Text = "LANGUAGE" });
        panel.Children.Add(_language);
        panel.Children.Add(new TextBlock { Text = "WEB_PORT" });
        panel.Children.Add(_webPort);
        panel.Children.Add(new TextBlock { Text = "WEB_BASE_URL" });
        panel.Children.Add(_webBaseUrl);
        panel.Children.Add(new TextBlock { Text = "EXPORT_METRICS" });
        panel.Children.Add(_exportMetrics);
        panel.Children.Add(new TextBlock { Text = "METRICS_PORT" });
        panel.Children.Add(_metricsPort);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var saveBtn = CreatePrimaryButton("Save .env");
        saveBtn.Click += (_, _) => SaveConfig();
        var testBtn = CreateSecondaryButton("Test Discord");
        testBtn.Click += async (_, _) => await TestDiscordAsync();
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(testBtn);
        panel.Children.Add(buttons);

        return WrapInCard(panel);
    }

    private Control BuildControlPanel()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(8, 0, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "Contrôle bot / serveur", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#2A3F78")) });
        panel.Children.Add(new TextBlock { Text = "Mode de démarrage" });
        panel.Children.Add(_mode);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var startBtn = CreatePrimaryButton("Start");
        startBtn.Click += (_, _) =>
        {
            var selectedMode = _mode.SelectedItem?.ToString() ?? "--normalmode";
            var (ok, msg) = GuiBotController.StartBot(selectedMode);
            _message.Text = ok ? $"✅ {msg}" : $"❌ {msg}";
            RefreshStatus();
        };
        var stopBtn = CreateSecondaryButton("Stop");
        stopBtn.Click += (_, _) =>
        {
            var (ok, msg) = GuiBotController.StopBot();
            _message.Text = ok ? $"✅ {msg}" : $"❌ {msg}";
            RefreshStatus();
        };

        controls.Children.Add(startBtn);
        controls.Children.Add(stopBtn);
        panel.Children.Add(controls);

        panel.Children.Add(new TextBlock { Text = "Status" });
        panel.Children.Add(_status);
        panel.Children.Add(_message);

        return WrapInCard(panel);
    }

    private void LoadConfig()
    {
        var cfg = GuiBotController.GetConfig();
        _token.Text = cfg.GetValueOrDefault("DISCORD_TOKEN", string.Empty);
        _language.SelectedItem = cfg.GetValueOrDefault("LANGUAGE", "en");
        _webPort.Text = cfg.GetValueOrDefault("WEB_PORT", "5199");
        _webBaseUrl.Text = cfg.GetValueOrDefault("WEB_BASE_URL", string.Empty);
        _exportMetrics.SelectedItem = cfg.GetValueOrDefault("EXPORT_METRICS", "false");
        _metricsPort.Text = cfg.GetValueOrDefault("METRICS_PORT", string.Empty);
    }

    private void SaveConfig()
    {
        GuiBotController.SaveConfig(new Dictionary<string, string>
        {
            ["DISCORD_TOKEN"] = _token.Text ?? string.Empty,
            ["LANGUAGE"] = _language.SelectedItem?.ToString() ?? "en",
            ["WEB_PORT"] = _webPort.Text ?? "5199",
            ["WEB_BASE_URL"] = _webBaseUrl.Text ?? string.Empty,
            ["EXPORT_METRICS"] = _exportMetrics.SelectedItem?.ToString() ?? "false",
            ["METRICS_PORT"] = _metricsPort.Text ?? string.Empty
        });

        _message.Text = "✅ .env sauvegardé.";
        RefreshStatus();
    }

    private async Task TestDiscordAsync()
    {
        _message.Text = "Testing...";
        RefreshStatus();
        var (ok, msg) = await GuiBotController.TestDiscordTokenAsync(_token.Text ?? string.Empty);
        _message.Text = ok ? $"✅ {msg}" : $"❌ {msg}";
        RefreshStatus();
    }

    private void StartRefreshTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => RefreshStatus();
        timer.Start();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        _status.Text = GuiBotController.IsRunning() ? "Running" : "Stopped";
        _status.Foreground = GuiBotController.IsRunning()
            ? new SolidColorBrush(Color.Parse("#0F8A4A"))
            : new SolidColorBrush(Color.Parse("#B13A59"));
        _logs.Text = GuiBotController.ReadLogs();
        _logs.CaretIndex = _logs.Text?.Length ?? 0;
    }

    private static Border WrapInCard(Control child)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D5E0FA")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Avalonia.Thickness(14),
            Child = child
        };
    }

    private static Button CreatePrimaryButton(string label)
    {
        return new Button
        {
            Content = label,
            Background = new SolidColorBrush(Color.Parse("#4A7DFF")),
            Foreground = Brushes.White
        };
    }

    private static Button CreateSecondaryButton(string label)
    {
        return new Button
        {
            Content = label,
            Background = new SolidColorBrush(Color.Parse("#E9EEFF")),
            Foreground = new SolidColorBrush(Color.Parse("#243C77")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CDD8F6"))
        };
    }
}
