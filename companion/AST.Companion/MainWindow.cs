using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.ObjectModel;

namespace AST.Companion;

public sealed class MainWindow : Window
{
    private readonly CompanionApiClient _api = new();
    private readonly ObservableCollection<string> _history = new();
    private readonly Queue<AsterNotification> _reactionQueue = new();
    private readonly AsterControl _aster = new();
    private readonly TextBlock _bubbleTitle = new()
    {
        Text = "Aster est prêt",
        FontSize = 20,
        FontWeight = FontWeight.Bold,
        Foreground = new SolidColorBrush(Color.Parse("#33402F")),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _bubbleSubtitle = new()
    {
        Text = "Colle ton lien portail AST pour commencer.",
        FontSize = 14,
        Foreground = new SolidColorBrush(Color.Parse("#5B644F")),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _bubbleDetail = new()
    {
        Text = "",
        FontSize = 12,
        Opacity = 0.72,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _statusDot = new()
    {
        Text = "●",
        Foreground = new SolidColorBrush(Color.Parse("#8C9781")),
        FontSize = 14,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _statusText = new()
    {
        Text = "Non configuré",
        FontSize = 12,
        Opacity = 0.72,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBox _portalUrl = new()
    {
        Watermark = "https://ast-bot.com/portal/guild/channel/token/",
        MinWidth = 320
    };
    private readonly CheckBox _alwaysOnTop = new() { Content = "Toujours au-dessus", IsChecked = true };
    private readonly Button _connectButton = new() { Content = "Connecter" };
    private readonly Button _historyButton = new() { Content = "Historique" };
    private readonly Button _settingsButton = new() { Content = "⚙" };
    private readonly Button _closeButton = new() { Content = "×" };
    private readonly Border _historyPanel;
    private readonly Border _settingsPanel;
    private readonly Border _bubble;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _animationTimer;

    private CompanionSettings _settings = new();
    private CompanionConnection? _connection;
    private Dictionary<string, int>? _knownItemCounts;
    private HashSet<string>? _knownHintIds;
    private CancellationTokenSource? _pollCts;
    private bool _polling;
    private bool _processingReactions;
    private bool _wasConnected;
    private double _animationPhase;

    public MainWindow()
    {
        Title = "AST Companion — Aster";
        Width = 540;
        Height = 360;
        MinWidth = 480;
        MinHeight = 300;
        CanResize = false;
        Background = Brushes.Transparent;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Topmost = true;

        _bubble = CreateBubble();
        _historyPanel = CreateHistoryPanel();
        _settingsPanel = CreateSettingsPanel();
        Content = BuildLayout();

        _historyButton.Click += (_, _) => ToggleHistory();
        _settingsButton.Click += (_, _) => ToggleSettings();
        _closeButton.Click += (_, _) => Close();
        _connectButton.Click += async (_, _) => await SaveAndConnectAsync();
        _alwaysOnTop.IsCheckedChanged += async (_, _) =>
        {
            Topmost = _alwaysOnTop.IsChecked == true;
            _settings.AlwaysOnTop = Topmost;
            await _settings.SaveAsync();
        };

        PointerPressed += OnPointerPressed;
        PositionChanged += async (_, _) =>
        {
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            await _settings.SaveAsync();
        };

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _animationTimer.Tick += (_, _) =>
        {
            _animationPhase += 0.08;
            _aster.Phase = _animationPhase;
            _aster.InvalidateVisual();
        };

        Opened += async (_, _) => await InitializeAsync();
        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            _animationTimer.Stop();
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        };
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(8)
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6,
            Children = { _historyButton, _settingsButton, _closeButton }
        };
        Grid.SetRow(toolbar, 0);

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var asterHost = new Grid
        {
            Width = 220,
            Height = 250,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        asterHost.Children.Add(_aster);
        Grid.SetColumn(asterHost, 0);

        var right = new StackPanel
        {
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 10, 0),
            Children = { _bubble, _historyPanel, _settingsPanel }
        };
        Grid.SetColumn(right, 1);

        stage.Children.Add(asterHost);
        stage.Children.Add(right);
        Grid.SetRow(stage, 1);

        root.Children.Add(toolbar);
        root.Children.Add(stage);
        return root;
    }

    private Border CreateBubble()
    {
        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _statusDot, _statusText }
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F7F0DE")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C9B889")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18, 14),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(80, 30, 35, 25),
                Blur = 18,
                OffsetX = 0,
                OffsetY = 6
            }),
            Child = new StackPanel
            {
                Spacing = 5,
                Children = { _bubbleTitle, _bubbleSubtitle, _bubbleDetail, statusRow }
            }
        };
    }

    private Border CreateHistoryPanel()
    {
        var list = new ListBox
        {
            ItemsSource = _history,
            Height = 150
        };

        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#F7F0DE")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C9B889")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Derniers événements", FontWeight = FontWeight.Bold },
                    list
                }
            }
        };
    }

    private Border CreateSettingsPanel()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(_portalUrl, 0);
        Grid.SetColumn(_connectButton, 1);
        row.Children.Add(_portalUrl);
        row.Children.Add(_connectButton);

        return new Border
        {
            IsVisible = true,
            Background = new SolidColorBrush(Color.Parse("#F7F0DE")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C9B889")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Connexion AST", FontWeight = FontWeight.Bold },
                    row,
                    _alwaysOnTop,
                    new TextBlock
                    {
                        Text = "Utilise le lien de ton portail utilisateur AST. Le token reste enregistré uniquement sur ce PC.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Opacity = 0.65
                    }
                }
            }
        };
    }

    private async Task InitializeAsync()
    {
        _settings = await CompanionSettings.LoadAsync();
        _portalUrl.Text = _settings.PortalUrl;
        _alwaysOnTop.IsChecked = _settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;
        _historyPanel.IsVisible = _settings.ShowHistory;

        if (_settings.WindowX is int x && _settings.WindowY is int y)
            Position = new PixelPoint(x, y);

        _pollTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.PollSeconds, 2, 60));
        _animationTimer.Start();

        if (!string.IsNullOrWhiteSpace(_settings.PortalUrl))
            await ConnectAsync(_settings.PortalUrl, announceSuccess: false);
        else
            SetState(AsterState.Idle, "Salut, moi c'est Aster !", "Donne-moi ton lien portail AST et je surveillerai tes objets.", "Je resterai discrètement à côté de ton jeu.");
    }

    private async Task SaveAndConnectAsync()
    {
        var value = _portalUrl.Text?.Trim() ?? string.Empty;
        _settings.PortalUrl = value;
        _settings.AlwaysOnTop = _alwaysOnTop.IsChecked == true;
        await _settings.SaveAsync();
        await ConnectAsync(value, announceSuccess: true);
    }

    private async Task ConnectAsync(string portalUrl, bool announceSuccess)
    {
        if (!CompanionApiClient.TryParsePortalUrl(portalUrl, out var connection) || connection is null)
        {
            _connection = null;
            _pollTimer.Stop();
            SetOffline("Lien portail invalide.");
            _settingsPanel.IsVisible = true;
            return;
        }

        _connection = connection;
        _knownItemCounts = null;
        _knownHintIds = null;
        _history.Clear();
        _statusText.Text = "Connexion à AST…";
        _statusDot.Foreground = new SolidColorBrush(Color.Parse("#E7C36A"));
        _aster.State = AsterState.Idle;
        _aster.InvalidateVisual();

        var success = await PollAsync();
        if (!success)
            return;

        _settingsPanel.IsVisible = false;
        if (announceSuccess)
            EnqueueReaction(new AsterNotification(AsterState.Reconnect, "Connecté !", "Aster surveille maintenant tes objets.", "Tu peux déplacer Aster où tu veux sur ton écran.", TimeSpan.FromSeconds(4)));

        _pollTimer.Start();
    }

    private async Task<bool> PollAsync()
    {
        if (_polling || _connection is null)
            return false;

        _polling = true;
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            var snapshot = await _api.GetSnapshotAsync(_connection, _pollCts.Token);
            var currentCounts = BuildCounts(snapshot.Items);
            var currentHints = snapshot.Hints.Select(x => x.Identity).ToHashSet(StringComparer.Ordinal);

            if (_knownItemCounts is null)
            {
                _knownItemCounts = currentCounts;
                _knownHintIds = currentHints;
                LoadInitialHistory(snapshot.Items, snapshot.Hints);
                SetConnectedStatus(snapshot.Items.Count);
                _wasConnected = true;
                return true;
            }

            var newItems = FindNewItems(snapshot.Items, _knownItemCounts);
            var newHints = snapshot.Hints
                .Where(x => _knownHintIds is not null && !_knownHintIds.Contains(x.Identity))
                .ToList();

            _knownItemCounts = currentCounts;
            _knownHintIds = currentHints;
            SetConnectedStatus(snapshot.Items.Count);

            if (!_wasConnected)
            {
                _wasConnected = true;
                EnqueueReaction(new AsterNotification(AsterState.Reconnect, "De retour !", "La connexion avec AST est rétablie.", "Aster reprend sa surveillance.", TimeSpan.FromSeconds(4)));
            }

            foreach (var item in newItems)
            {
                AddItemToHistory(item);
                EnqueueReaction(BuildItemNotification(item));
            }

            foreach (var hint in newHints)
            {
                AddHintToHistory(hint);
                EnqueueReaction(new AsterNotification(
                    AsterState.Hint,
                    "Nouvel indice !",
                    hint.Item,
                    BuildHintDetail(hint),
                    TimeSpan.FromSeconds(6)));
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _wasConnected = false;
            SetOffline($"AST indisponible : {ex.Message}");
            return false;
        }
        finally
        {
            _polling = false;
        }
    }

    private AsterNotification BuildItemNotification(CompanionItem item)
    {
        var state = AsterReactions.FromItem(item);
        var finder = string.IsNullOrWhiteSpace(item.Finder) ? "un autre joueur" : item.Finder;
        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Game)) detailParts.Add(item.Game);
        if (!string.IsNullOrWhiteSpace(item.Location)) detailParts.Add(item.Location);
        if (!string.IsNullOrWhiteSpace(item.Alias)) detailParts.Add($"pour {item.Alias}");

        var subtitle = state == AsterState.Trap
            ? $"{item.Item} envoyé par {finder}… aïe."
            : $"{item.Item} — de {finder}";

        return new AsterNotification(
            state,
            AsterReactions.Label(state),
            subtitle,
            string.Join(" • ", detailParts),
            state == AsterState.Trap ? TimeSpan.FromSeconds(7) : TimeSpan.FromSeconds(6));
    }

    private void EnqueueReaction(AsterNotification notification)
    {
        _reactionQueue.Enqueue(notification);
        if (!_processingReactions)
            _ = ProcessReactionQueueAsync();
    }

    private async Task ProcessReactionQueueAsync()
    {
        if (_processingReactions)
            return;

        _processingReactions = true;
        try
        {
            while (_reactionQueue.Count > 0)
            {
                var reaction = _reactionQueue.Dequeue();
                SetState(reaction.State, reaction.Title, reaction.Subtitle, reaction.Detail);
                await Task.Delay(reaction.Duration);
            }

            if (_wasConnected)
                SetState(AsterState.Idle, "Tout va bien", "Aster veille sur tes prochains objets.", "Clique sur Historique pour revoir les derniers événements.");
        }
        finally
        {
            _processingReactions = false;
        }
    }

    private void SetState(AsterState state, string title, string subtitle, string detail)
    {
        _aster.State = state;
        _aster.InvalidateVisual();
        _bubbleTitle.Text = title;
        _bubbleSubtitle.Text = subtitle;
        _bubbleDetail.Text = detail;
    }

    private void SetConnectedStatus(int itemCount)
    {
        _statusDot.Foreground = new SolidColorBrush(Color.Parse("#64A867"));
        _statusText.Text = $"Connecté • {itemCount} objet(s) • {DateTime.Now:HH:mm:ss}";
        if (!_processingReactions)
            SetState(AsterState.Idle, "Tout va bien", "Aster veille sur tes prochains objets.", "Clique sur Historique pour revoir les derniers événements.");
    }

    private void SetOffline(string message)
    {
        _statusDot.Foreground = new SolidColorBrush(Color.Parse("#8C9781"));
        _statusText.Text = message;
        if (!_processingReactions)
            SetState(AsterState.Offline, "Petite pause…", "Je n'arrive pas à joindre AST pour le moment.", "Je réessaierai automatiquement.");
    }

    private void ToggleHistory()
    {
        _historyPanel.IsVisible = !_historyPanel.IsVisible;
        _settingsPanel.IsVisible = false;
        _settings.ShowHistory = _historyPanel.IsVisible;
        _ = _settings.SaveAsync();
    }

    private void ToggleSettings()
    {
        _settingsPanel.IsVisible = !_settingsPanel.IsVisible;
        _historyPanel.IsVisible = false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void LoadInitialHistory(IReadOnlyList<CompanionItem> items, IReadOnlyList<CompanionHint> hints)
    {
        foreach (var item in items.TakeLast(15).Reverse())
            AddItemToHistory(item);

        foreach (var hint in hints.TakeLast(5).Reverse())
            AddHintToHistory(hint);
    }

    private void AddItemToHistory(CompanionItem item)
    {
        var location = string.IsNullOrWhiteSpace(item.Location) ? string.Empty : $" • {item.Location}";
        var finder = string.IsNullOrWhiteSpace(item.Finder) ? string.Empty : $" ← {item.Finder}";
        var alias = string.IsNullOrWhiteSpace(item.Alias) ? string.Empty : $"[{item.Alias}] ";
        _history.Insert(0, $"🎁 {alias}{item.Item}{finder}{location}");
        TrimHistory();
    }

    private void AddHintToHistory(CompanionHint hint)
    {
        _history.Insert(0, $"💡 [{hint.Alias}] {hint.Item} • {hint.Location} • {hint.Game}");
        TrimHistory();
    }

    private void TrimHistory()
    {
        while (_history.Count > 40)
            _history.RemoveAt(_history.Count - 1);
    }

    private static string BuildHintDetail(CompanionHint hint)
    {
        var who = hint.Direction == "receiver"
            ? $"trouvé par {hint.Finder}"
            : $"pour {hint.Receiver}";
        return string.Join(" • ", new[] { who, hint.Location, hint.Game }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static Dictionary<string, int> BuildCounts(IEnumerable<CompanionItem> items)
        => items.GroupBy(x => x.Identity, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

    private static List<CompanionItem> FindNewItems(
        IReadOnlyList<CompanionItem> items,
        IReadOnlyDictionary<string, int> previousCounts)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<CompanionItem>();

        foreach (var item in items)
        {
            seen.TryGetValue(item.Identity, out var occurrence);
            occurrence++;
            seen[item.Identity] = occurrence;

            previousCounts.TryGetValue(item.Identity, out var previous);
            if (occurrence > previous)
                result.Add(item);
        }

        return result;
    }
}
