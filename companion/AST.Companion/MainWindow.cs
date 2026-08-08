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
    private readonly Queue<AsterNotification> _reactionQueue = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _animationTimer;
    private readonly AsterControl _aster = new();
    private readonly Border _bubble;
    private readonly TextBlock _bubbleTitle;
    private readonly TextBlock _bubbleSubtitle;
    private readonly TextBlock _bubbleDetail;
    private readonly TextBlock _statusText;

    private CompanionSettings _settings = new();
    private CompanionConnection? _connection;
    private Dictionary<string, int>? _knownItemCounts;
    private HashSet<string>? _knownHintIds;
    private CancellationTokenSource? _pollCts;
    private bool _polling;
    private bool _processingReactions;
    private bool _wasConnected;
    private bool _allowClose;
    private double _animationPhase;

    public ObservableCollection<string> History { get; } = new();
    public string PortalUrl => _settings.PortalUrl;
    public bool AlwaysOnTop => _settings.AlwaysOnTop;
    public string StatusDisplay => _statusText.Text ?? string.Empty;
    public event EventHandler? CompanionStatusChanged;

    public MainWindow()
    {
        Title = "AST Companion — Aster";
        Width = 510;
        Height = 255;
        CanResize = false;
        Background = Brushes.Transparent;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;

        _bubbleTitle = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#33402F")),
            TextWrapping = TextWrapping.Wrap
        };
        _bubbleSubtitle = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#4E5849")),
            TextWrapping = TextWrapping.Wrap
        };
        _bubbleDetail = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.72,
            Foreground = new SolidColorBrush(Color.Parse("#4E5849")),
            TextWrapping = TextWrapping.Wrap
        };
        _statusText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.70,
            Foreground = new SolidColorBrush(Color.Parse("#64705F")),
            TextWrapping = TextWrapping.Wrap
        };

        _bubble = CreateBubble();
        Content = BuildPetLayout();

        _aster.PointerPressed += OnAsterPointerPressed;
        _aster.DoubleTapped += (_, _) => (Application.Current as App)?.ShowSettings();

        PositionChanged += async (_, _) =>
        {
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            await _settings.SaveAsync();
        };
        Closing += OnClosing;

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
        Closed += (_, _) => DisposeRuntime();
    }

    private Control BuildPetLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,280"),
            Width = 500,
            Height = 245,
            Background = Brushes.Transparent
        };

        var asterHost = new Grid
        {
            Width = 220,
            Height = 240,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        asterHost.Children.Add(_aster);
        Grid.SetColumn(asterHost, 0);

        _bubble.Width = 270;
        _bubble.HorizontalAlignment = HorizontalAlignment.Left;
        _bubble.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_bubble, 1);

        grid.Children.Add(asterHost);
        grid.Children.Add(_bubble);
        return grid;
    }

    private Border CreateBubble()
    {
        return new Border
        {
            IsVisible = true,
            Background = new SolidColorBrush(Color.Parse("#F7F0DE")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C9B889")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 13),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(70, 20, 25, 18),
                Blur = 14,
                OffsetY = 5
            }),
            Child = new StackPanel
            {
                Spacing = 5,
                Children = { _bubbleTitle, _bubbleSubtitle, _bubbleDetail, _statusText }
            }
        };
    }

    private async Task InitializeAsync()
    {
        _settings = await CompanionSettings.LoadAsync();
        Topmost = _settings.AlwaysOnTop;

        if (_settings.WindowX is int x && _settings.WindowY is int y)
            Position = new PixelPoint(x, y);

        _pollTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.PollSeconds, 2, 60));
        _animationTimer.Start();

        if (!string.IsNullOrWhiteSpace(_settings.PortalUrl))
        {
            await ConnectAsync(_settings.PortalUrl, announceSuccess: false);
        }
        else
        {
            SetState(AsterState.Idle,
                "Salut, moi c'est Aster !",
                "Configure ton lien portail AST pour que je surveille tes objets.",
                "Double-clique sur moi ou utilise l'icône près de l'horloge.");
            (Application.Current as App)?.ShowSettings();
        }
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
            Hide();
        else
            ShowAster();
    }

    public void ShowAster()
    {
        if (!IsVisible)
            Show();
        Activate();
    }

    public void HideAster() => Hide();

    public async Task ConfigureAsync(string portalUrl, bool alwaysOnTop)
    {
        _settings.PortalUrl = portalUrl.Trim();
        _settings.AlwaysOnTop = alwaysOnTop;
        Topmost = alwaysOnTop;
        await _settings.SaveAsync();
        await ConnectAsync(_settings.PortalUrl, announceSuccess: true);
        CompanionStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetAlwaysOnTopAsync(bool value)
    {
        _settings.AlwaysOnTop = value;
        Topmost = value;
        await _settings.SaveAsync();
        CompanionStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ReconnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.PortalUrl))
        {
            (Application.Current as App)?.ShowSettings();
            return;
        }

        await ConnectAsync(_settings.PortalUrl, announceSuccess: true);
    }

    public void RequestExit()
    {
        _allowClose = true;
        Close();
    }

    private async Task ConnectAsync(string portalUrl, bool announceSuccess)
    {
        if (!CompanionApiClient.TryParsePortalUrl(portalUrl, out var connection) || connection is null)
        {
            _connection = null;
            _pollTimer.Stop();
            _wasConnected = false;
            SetOffline("Lien portail invalide.");
            (Application.Current as App)?.ShowSettings();
            return;
        }

        _connection = connection;
        _knownItemCounts = null;
        _knownHintIds = null;
        _statusText.Text = "Connexion à AST…";
        SetAsterOnly(AsterState.Idle);

        if (!await PollAsync())
            return;

        if (announceSuccess)
        {
            EnqueueReaction(new AsterNotification(
                AsterState.Reconnect,
                "Connecté !",
                "Je surveille maintenant tes objets.",
                "Tu peux me déplacer où tu veux.",
                TimeSpan.FromSeconds(4)));
        }
        else
        {
            HideBubbleWhenIdle();
        }

        _pollTimer.Start();
        CompanionStatusChanged?.Invoke(this, EventArgs.Empty);
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
                EnqueueReaction(new AsterNotification(
                    AsterState.Reconnect,
                    "De retour !",
                    "La connexion avec AST est rétablie.",
                    "Je reprends ma surveillance.",
                    TimeSpan.FromSeconds(4)));
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

            CompanionStatusChanged?.Invoke(this, EventArgs.Empty);
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
            CompanionStatusChanged?.Invoke(this, EventArgs.Empty);
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
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Game)) details.Add(item.Game);
        if (!string.IsNullOrWhiteSpace(item.Location)) details.Add(item.Location);
        if (!string.IsNullOrWhiteSpace(item.Alias)) details.Add($"pour {item.Alias}");

        var subtitle = state == AsterState.Trap
            ? $"{item.Item} envoyé par {finder}… aïe."
            : $"{item.Item} — de {finder}";

        return new AsterNotification(
            state,
            AsterReactions.Label(state),
            subtitle,
            string.Join(" • ", details),
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
            {
                SetAsterOnly(AsterState.Idle);
                HideBubbleWhenIdle();
            }
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
        _bubble.IsVisible = true;
    }

    private void SetAsterOnly(AsterState state)
    {
        _aster.State = state;
        _aster.InvalidateVisual();
    }

    private void HideBubbleWhenIdle()
    {
        if (_wasConnected && !_processingReactions)
            _bubble.IsVisible = false;
    }

    private void SetConnectedStatus(int itemCount)
    {
        _statusText.Text = $"● Connecté • {itemCount} objet(s) • {DateTime.Now:HH:mm:ss}";
        if (!_processingReactions)
            SetAsterOnly(AsterState.Idle);
    }

    private void SetOffline(string message)
    {
        _statusText.Text = message;
        if (!_processingReactions)
            SetState(AsterState.Offline,
                "Petite pause…",
                "Je n'arrive pas à joindre AST pour le moment.",
                "Je réessaierai automatiquement.");
    }

    private void OnAsterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        Hide();
    }

    private void LoadInitialHistory(IReadOnlyList<CompanionItem> items, IReadOnlyList<CompanionHint> hints)
    {
        History.Clear();
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
        History.Insert(0, $"🎁 {alias}{item.Item}{finder}{location}");
        TrimHistory();
    }

    private void AddHintToHistory(CompanionHint hint)
    {
        History.Insert(0, $"💡 [{hint.Alias}] {hint.Item} • {hint.Location} • {hint.Game}");
        TrimHistory();
    }

    private void TrimHistory()
    {
        while (History.Count > 40)
            History.RemoveAt(History.Count - 1);
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

    private void DisposeRuntime()
    {
        _pollTimer.Stop();
        _animationTimer.Stop();
        _pollCts?.Cancel();
        _pollCts?.Dispose();
    }
}
