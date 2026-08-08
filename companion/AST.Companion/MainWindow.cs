using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.ObjectModel;

namespace AST.Companion;

public sealed class MainWindow : Window
{
    private readonly CompanionApiClient _api = new();
    private readonly ObservableCollection<string> _history = new();
    private readonly TextBox _portalUrl = new() { Watermark = "https://ast.example/portal/guild/channel/token/" };
    private readonly TextBlock _status = new() { Text = "Non configuré" };
    private readonly TextBlock _speech = new()
    {
        Text = "Donne-moi ton lien portail AST et je surveillerai tes objets.",
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        MaxWidth = 300
    };
    private readonly TextBlock _mascot = new()
    {
        Text = "(◕‿◕)",
        FontSize = 46,
        HorizontalAlignment = HorizontalAlignment.Center
    };
    private readonly Button _connectButton = new() { Content = "Connecter" };
    private readonly CheckBox _alwaysOnTop = new() { Content = "Toujours au-dessus", IsChecked = true };
    private readonly ListBox _historyList;
    private readonly DispatcherTimer _pollTimer;

    private CompanionSettings _settings = new();
    private CompanionConnection? _connection;
    private Dictionary<string, int>? _knownItemCounts;
    private CancellationTokenSource? _pollCts;
    private bool _polling;

    public MainWindow()
    {
        Title = "AST Companion";
        Width = 380;
        Height = 610;
        MinWidth = 340;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _historyList = new ListBox
        {
            ItemsSource = _history,
            Height = 210
        };

        _connectButton.Click += async (_, _) => await SaveAndConnectAsync();
        _alwaysOnTop.IsCheckedChanged += (_, _) =>
        {
            Topmost = _alwaysOnTop.IsChecked == true;
            _settings.AlwaysOnTop = Topmost;
            _ = _settings.SaveAsync();
        };

        Content = BuildLayout();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        Opened += async (_, _) => await InitializeAsync();
        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        };
    }

    private Control BuildLayout()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12
        };

        var mascotCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _mascot,
                    _speech
                }
            }
        };

        var settingsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };
        Grid.SetColumn(_portalUrl, 0);
        Grid.SetColumn(_connectButton, 1);
        settingsRow.Children.Add(_portalUrl);
        settingsRow.Children.Add(_connectButton);

        root.Children.Add(mascotCard);
        root.Children.Add(new TextBlock { Text = "Lien portail AST", FontWeight = FontWeight.SemiBold });
        root.Children.Add(settingsRow);
        root.Children.Add(_alwaysOnTop);
        root.Children.Add(_status);
        root.Children.Add(new Separator());
        root.Children.Add(new TextBlock { Text = "Objets détectés", FontWeight = FontWeight.SemiBold });
        root.Children.Add(_historyList);
        root.Children.Add(new TextBlock
        {
            Text = "MVP : le companion lit uniquement ton résumé utilisateur AST. Aucun accès Discord n'est nécessaire sur ce PC.",
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        return new ScrollViewer { Content = root };
    }

    private async Task InitializeAsync()
    {
        _settings = await CompanionSettings.LoadAsync();
        _portalUrl.Text = _settings.PortalUrl;
        _alwaysOnTop.IsChecked = _settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;

        var seconds = Math.Clamp(_settings.PollSeconds, 2, 60);
        _pollTimer.Interval = TimeSpan.FromSeconds(seconds);

        if (!string.IsNullOrWhiteSpace(_settings.PortalUrl))
            await ConnectAsync(_settings.PortalUrl, announceSuccess: false);
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
            SetDisconnected("Lien portail invalide.");
            return;
        }

        _connection = connection;
        _knownItemCounts = null;
        _history.Clear();
        _status.Text = "Connexion à AST…";
        _mascot.Text = "(•‿•)";

        var success = await PollAsync();
        if (!success)
            return;

        if (announceSuccess)
            _speech.Text = "Connecté ! Je te préviens dès qu'un nouvel objet arrive.";

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

            if (_knownItemCounts is null)
            {
                _knownItemCounts = currentCounts;
                LoadInitialHistory(snapshot.Items);
                _status.Text = $"Connecté • {snapshot.Items.Count} objet(s) suivi(s)";
                _mascot.Text = "(◕‿◕)";
                return true;
            }

            var newItems = FindNewItems(snapshot.Items, _knownItemCounts);
            _knownItemCounts = currentCounts;
            _status.Text = $"Connecté • mise à jour {DateTime.Now:HH:mm:ss}";
            _mascot.Text = "(◕‿◕)";

            foreach (var item in newItems)
                AddToHistory(item);

            if (newItems.Count > 0)
                Announce(newItems);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            SetDisconnected($"AST indisponible : {ex.Message}");
            return false;
        }
        finally
        {
            _polling = false;
        }
    }

    private void Announce(IReadOnlyList<CompanionItem> newItems)
    {
        var last = newItems[^1];
        _mascot.Text = last.Flag == "4" ? "(⊙﹏⊙)" : "(★‿★)";

        if (newItems.Count == 1)
        {
            var source = string.IsNullOrWhiteSpace(last.Finder) ? "quelqu'un" : last.Finder;
            _speech.Text = last.Flag == "4"
                ? $"Oh non… {last.Item} envoyé par {source} !"
                : $"Tu as reçu {last.Item} de {source} !";
        }
        else
        {
            _speech.Text = $"Tu viens de recevoir {newItems.Count} objets ! Le dernier est {last.Item}.";
        }
    }

    private void SetDisconnected(string message)
    {
        _status.Text = message;
        _mascot.Text = "(-_-) zZ";
        _speech.Text = "Je n'arrive pas à joindre AST pour le moment.";
    }

    private void LoadInitialHistory(IReadOnlyList<CompanionItem> items)
    {
        foreach (var item in items.TakeLast(20).Reverse())
            AddToHistory(item);
    }

    private void AddToHistory(CompanionItem item)
    {
        var location = string.IsNullOrWhiteSpace(item.Location) ? string.Empty : $" • {item.Location}";
        var finder = string.IsNullOrWhiteSpace(item.Finder) ? string.Empty : $" ← {item.Finder}";
        var alias = string.IsNullOrWhiteSpace(item.Alias) ? string.Empty : $"[{item.Alias}] ";
        _history.Insert(0, $"{alias}{item.Item}{finder}{location}");

        while (_history.Count > 30)
            _history.RemoveAt(_history.Count - 1);
    }

    private static Dictionary<string, int> BuildCounts(IEnumerable<CompanionItem> items)
        => items
            .GroupBy(x => x.Identity, StringComparer.Ordinal)
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
