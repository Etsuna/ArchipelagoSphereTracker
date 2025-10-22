using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;

namespace AST.GUI
{
    public partial class EnvSetupWindow : Window
    {
        public EnvSetupWindow()
        {
            InitializeComponent();
            TokenBox.Focus();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            btn!.IsEnabled = false;

            try
            {
                var token = TokenBox.Password?.Trim() ?? "";
                var lang = ((LangCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "en").Trim();

                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Token Discord requis.");
                    return;
                }

                var (ok, err) = await CheckDiscordBotTokenAsync(token, TimeSpan.FromSeconds(15));
                if (!ok)
                {
                    MessageBox.Show($"Connexion Discord échouée: {err}");
                    return;
                }

                EnvHelper.WriteEnv(token, lang);
                MessageBox.Show($".env enregistré dans: {Path.Combine(EnvHelper.BasePath, ".env")}");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}");
            }
            finally
            {
                btn!.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static async Task<(bool ok, string err)> CheckDiscordBotTokenAsync(string token, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            using var http = new HttpClient
            {
                BaseAddress = new Uri("https://discord.com/api/v10/"),
                Timeout = timeout
            };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token);

            try
            {
                using var resp = await http.GetAsync("users/@me", cts.Token);
                if ((int)resp.StatusCode == 401) return (false, "token invalide");
                if (!resp.IsSuccessStatusCode) return (false, $"HTTP {(int)resp.StatusCode}");
                return (true, "");
            }
            catch (TaskCanceledException)
            {
                return (false, "timeout");
            }
            catch (Exception ex)
            {
                return (false, ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
