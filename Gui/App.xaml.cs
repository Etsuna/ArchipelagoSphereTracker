using System.Windows;

namespace AST.GUI
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var app = Application.Current;
            var prev = app.ShutdownMode;
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
            MainWindow = mw; 
            mw.Show();

            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}