using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AST.Companion;

public sealed class SettingsWindow : Window
{
    private readonly MainWindow _main;
    private readonly TextBox _portalUrl;
    private readonly CheckBox _alwaysOnTop;
    private readonly TextBlock _status;
    private bool _allowClose;

    public SettingsWindow(MainWindow main)
    {
        _main = main;
        Title = "AST Companion — Paramètres";
        Width = 520;
        Height = 560;
        MinWidth = 460;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        _portalUrl = new TextBox
        {
            Watermark = "https://ast-bot.com/portal/guild/channel/token/",
            Text = main.PortalUrl
        };
        _alwaysOnTop = new CheckBox
        {
            Content = "Aster toujours au-dessus du jeu",
            IsChecked = main.AlwaysOnTop
        };
        _status = new TextBlock
        {
            Text = main.StatusDisplay,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#5B644F"))
        };

        Content = BuildLayout();
        Closing += OnClosing;
        _main.CompanionStatusChanged += OnCompanionStatusChanged;
    }

    public void RefreshFromMain()
    {
        _portalUrl.Text = _main.PortalUrl;
        _alwaysOnTop.IsChecked = _main.AlwaysOnTop;
        _status.Text = _main.StatusDisplay;
    }

    public void RequestExit()
    {
        _allowClose = true;
        Close();
    }

    private Control BuildLayout()
    {
        var connect = new Button { Content = "Enregistrer et connecter" };
        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false;
            try
            {
                await _main.ConfigureAsync(_portalUrl.Text?.Trim() ?? string.Empty, _alwaysOnTop.IsChecked == true);
                _status.Text = _main.StatusDisplay;
                _main.ShowAster();
            }
            finally
            {
                connect.IsEnabled = true;
            }
        };

        var reconnect = new Button { Content = "Reconnecter AST" };
        reconnect.Click += async (_, _) =>
        {
            reconnect.IsEnabled = false;
            try
            {
                await _main.ReconnectAsync();
                _status.Text = _main.StatusDisplay;
            }
            finally
            {
                reconnect.IsEnabled = true;
            }
        };

        var showAster = new Button { Content = "Afficher Aster" };
        showAster.Click += (_, _) => _main.ShowAster();

        var quit = new Button { Content = "Quitter AST Companion" };
        quit.Click += (_, _) => (Application.Current as App)?.Quit();

        _alwaysOnTop.IsCheckedChanged += async (_, _) =>
        {
            await _main.SetAlwaysOnTopAsync(_alwaysOnTop.IsChecked == true);
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { connect, reconnect, showAster }
        };

        var history = new ListBox
        {
            ItemsSource = _main.History,
            MinHeight = 220,
            MaxHeight = 260
        };

        var cardBrush = new SolidColorBrush(Color.Parse("#F7F0DE"));
        var borderBrush = new SolidColorBrush(Color.Parse("#C9B889"));

        var connectionCard = new Border
        {
            Background = cardBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Connexion AST", FontSize = 18, FontWeight = FontWeight.Bold },
                    new TextBlock
                    {
                        Text = "Colle le lien de ton portail utilisateur AST. Le token reste enregistré uniquement sur ce PC.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.75
                    },
                    _portalUrl,
                    _alwaysOnTop,
                    actions,
                    _status
                }
            }
        };

        var historyCard = new Border
        {
            Background = cardBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Derniers événements", FontSize = 16, FontWeight = FontWeight.Bold },
                    history
                }
            }
        };

        var root = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Aster",
                    FontSize = 26,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#53663D"))
                },
                new TextBlock
                {
                    Text = "AST Companion reste dans la zone de notification. Fermer cette fenêtre ne quitte pas le programme.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.70
                },
                connectionCard,
                historyCard,
                quit
            }
        };

        return new ScrollViewer { Content = root };
    }

    private void OnCompanionStatusChanged(object? sender, EventArgs e)
    {
        _status.Text = _main.StatusDisplay;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            _main.CompanionStatusChanged -= OnCompanionStatusChanged;
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
