using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AST.GUI
{
    public partial class MainWindow : Window
    {
        private Process? _proc;
        private bool _closeScheduled;

        public MainWindow()
        {
            InitializeComponent();

            ExePathBox.Text = Path.Combine(AppContext.BaseDirectory, "ArchipelagoSphereTracker.exe");
            DbPathBox.Text  = Path.Combine(AppContext.BaseDirectory, "AST.db");
            this.Closing += MainWindow_Closing;
            Application.Current.SessionEnding += Current_SessionEnding;
            AppDomain.CurrentDomain.ProcessExit += (_, __) => KillChildHard();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => KillChildHard();
            _ = LoadGuildsAsync();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_closeScheduled) return; 
            e.Cancel = true;
            _closeScheduled = true;
            _ = CloseAfterStopAsync();
        }

        private async Task CloseAfterStopAsync()
        {
            await StopChildAsync();
            await Dispatcher.InvokeAsync(() =>
            {
                this.Closing -= MainWindow_Closing; 
                Close();
            });
        }

        private void Current_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            KillChildHard();
        }

        private async Task StopChildAsync()
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    _proc.CloseMainWindow();
                    var exited = await Task.Run(() => _proc.WaitForExit(1000));
                    if (!exited)
                    {
                        KillChildHard();
                        await Task.Run(() => _proc.WaitForExit(2000));
                    }
                }
            }
            catch { /* mute */ }
        }

        private void KillChildHard()
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                    _proc.Kill(entireProcessTree: true);
            }
            catch { /* mute */ }
        }

        private void AppendLog(string line)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendLog(line));
                return;
            }
            LogBox.AppendText(line + Environment.NewLine);
            if (AutoScrollCheck.IsChecked == true)
                LogBox.ScrollToEnd();
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(ExePathBox.Text))
                {
                    MessageBox.Show("Binaire introuvable.");
                    return;
                }

                StartBtn.IsEnabled = false;
                StopBtn.IsEnabled  = true;
                StatusText.Text    = "Démarrage...";

                var mode = ((ComboBoxItem)ModeBox.SelectedItem).Content?.ToString() ?? "--NormalMode";

                var psi = new ProcessStartInfo
                {
                    FileName = ExePathBox.Text,
                    Arguments = mode,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ExePathBox.Text) ?? AppContext.BaseDirectory
                };

                _proc = new Process();
                _proc.StartInfo = psi;
                _proc.OutputDataReceived += (s, a) => { if (a.Data != null) AppendLog(a.Data); };
                _proc.ErrorDataReceived  += (s, a) => { if (a.Data != null) AppendLog(a.Data); };
                _proc.EnableRaisingEvents = true;
                _proc.Exited += (s, a) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog($"[process] terminé avec code {_proc?.ExitCode}");
                        StatusText.Text = "Arrêté";
                        StartBtn.IsEnabled = true;
                        StopBtn.IsEnabled = false;
                    });
                };

                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();

                StatusText.Text = "En cours";
                AppendLog($"[process] lancé: {psi.FileName} {psi.Arguments}");
            }
            catch (Exception ex)
            {
                AppendLog("[error] " + ex);
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                StatusText.Text = "Erreur";
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    AppendLog("[process] arrêt demandé");
                    _proc.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                AppendLog("[error] " + ex.Message);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|Tous les fichiers (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
                ExePathBox.Text = dlg.FileName;
        }

        private void BrowseDb_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "SQLite DB (*.db)|*.db|Tous les fichiers (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
                DbPathBox.Text = dlg.FileName;
        }

        private async void ReloadDb_Click(object sender, RoutedEventArgs e) => await LoadGuildsAsync();

        private async Task LoadGuildsAsync()
        {
            try
            {
                GuildList.SelectedItem   = null;
                ChannelList.SelectedItem = null;
                GuildList.ItemsSource = await DbExplorer.GetGuildsAsync(DbPathBox.Text);
                GridChannels.ItemsSource = null;
                GridAlias.ItemsSource = null;
                ChannelList.ItemsSource = null;
                ClearTables();
            }
            catch (Exception ex)
            {
                AppendLog("[db] " + ex.Message);
            }
        }

        private void GuildList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GuildList.SelectedItem is string guildId)
            {
                ChannelList.ItemsSource = DbExplorer.GetChannels(DbPathBox.Text, guildId);
                ClearTables();
            }
        }

        private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GuildList.SelectedItem is string guildId && ChannelList.SelectedItem is string channelId)
            {
                GridChannels.ItemsSource        = DbExplorer.LoadTable(DbPathBox.Text, "ChannelsAndUrlsTable", guildId, channelId).DefaultView;
                GridDisplayedItem.ItemsSource   = DbExplorer.LoadTable(DbPathBox.Text, "DisplayedItemTable", guildId, channelId).DefaultView;
                GridAlias.ItemsSource   = DbExplorer.LoadTable(DbPathBox.Text, "AliasChoicesTable", guildId, channelId).DefaultView;
            }
        }

        private void ClearTables()
        {
            GridChannels.ItemsSource = null;
            GridDisplayedItem.ItemsSource = null;
            GridAlias.ItemsSource = null;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();
    }
}
