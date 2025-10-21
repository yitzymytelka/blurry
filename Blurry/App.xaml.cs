using System.Windows;
using Application = System.Windows.Application;

namespace Blurry
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;



        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            InitializeTrayIcon();
        }



        private void InitializeTrayIcon()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            string iconPath = System.IO.Path.Combine(exeDir, "Resources", "mainIcon.ico");

            _trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconPath),
                Visible = true,
                Text = "Blurry"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Settings", null, (s, e) => OpenSettings());
            menu.Items.Add("Exit", null, (s, e) => Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;

        }

        private void OpenSettings()
        {
            Current.Dispatcher.Invoke(() =>
            {
                var settings = new SettingsWindow();
                settings.Show();
                settings.Activate();
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnExit(e);
        }
    }

}
