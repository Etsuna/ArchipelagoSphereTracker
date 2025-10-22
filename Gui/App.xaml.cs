using System.Windows;

namespace AST.GUI
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!EnvHelper.EnvExists())
            {
                var dlg = new EnvSetupWindow();
                var ok = dlg.ShowDialog() == true;
                if (!ok)
                {
                    Shutdown();
                    return;
                }
            }

            var mw = new MainWindow();
            mw.Show();
        }
    }
}