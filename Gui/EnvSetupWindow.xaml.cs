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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var token = TokenBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("DISCORD_TOKEN est requis.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var lang = ((ComboBoxItem)LangCombo.SelectedItem)?.Content?.ToString() ?? "en";

            try
            {
                EnvHelper.WriteEnv(token, lang);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible d'écrire .env:\n" + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
