using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DotNetEnv;

namespace AST.GUI
{
    public partial class MainWindow : Window
    {
        private Process? _proc;
        private bool _closeScheduled;
        public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException(Resource.EnvIsNull);

        public MainWindow()
        {
            InitializeComponent();
            Env.Load();

            Thread.CurrentThread.CurrentUICulture = new CultureInfo((Environment.GetEnvironmentVariable("LANGUAGE") ?? "en").ToLowerInvariant());

            ExePathBox.Text = Path.Combine(BasePath, "ArchipelagoSphereTracker.exe");
            DbPathBox.Text  = Path.Combine(BasePath, "AST.db");

            TabItemBdd.Header = Resource.DataBase;
            DbFile.Text = Resource.DbFile;
            BrowseDbFile.Content = Resource.Browse;
            ReloadDb.Content = Resource.Reload;
            BrowseExe.Content = Resource.Browse;
            Binary.Text = Resource.Binary;
            StatusText.Text = Resource.Inactive;
            StopBtn.Content = Resource.StopBtn;
            StartBtn.Content = Resource.StartBtn;
            ClearLogs.Content = Resource.Clear;
            AutoScrollCheck.Content = Resource.AutoScroll;


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

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(ExePathBox.Text))
                {
                    MessageBox.Show(Resource.BinaryNotFound);
                    return;
                }

                StartBtn.IsEnabled = false;
                StopBtn.IsEnabled = true;
                StatusText.Text = Resource.Starting;

                var mode = ((ComboBoxItem)ModeBox.SelectedItem).Content?.ToString() ?? "--NormalMode";

                var psi = new ProcessStartInfo
                {
                    FileName = ExePathBox.Text,
                    Arguments = mode,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ExePathBox.Text) ?? BasePath
                };

                _proc = new Process();
                _proc.StartInfo = psi;
                _proc.OutputDataReceived += (s, a) => { if (a.Data != null) AppendLog(a.Data); };
                _proc.ErrorDataReceived += (s, a) => { if (a.Data != null) AppendLog(a.Data); };
                _proc.EnableRaisingEvents = true;
                _proc.Exited += (s, a) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog($"{Resource.ProcessStop} {_proc?.ExitCode}");
                        StatusText.Text = Resource.Stop;
                        StartBtn.IsEnabled = true;
                        StopBtn.IsEnabled = false;
                    });
                };

                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();

                StatusText.Text = Resource.Processing;
                AppendLog($"{Resource.ProcessLaunched}: {psi.FileName} {psi.Arguments}");
            }
            catch (Exception ex)
            {
                AppendLog($"[{Resource.Error}] " + ex);
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                StatusText.Text = Resource.Error;
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    AppendLog(Resource.StopRequested);
                    _proc.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{Resource.Error}] " + ex.Message);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Resource.ExeFilter };
            if (dlg.ShowDialog() == true)
                ExePathBox.Text = dlg.FileName;
        }

        private void BrowseDb_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Resource.BddFilter };
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
