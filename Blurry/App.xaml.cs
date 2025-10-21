using System.Windows;
using Blurry.Managers;
using Application = System.Windows.Application;

namespace Blurry
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private SettingsWindow? _settingsWindow;



        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            InitializeTrayIcon();
        }



        private void InitializeTrayIcon()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            string iconPath = System.IO.Path.Combine(exeDir, "Resources", "trayIcon.ico");

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

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ScreenManager.TogglePrimaryMonitor();
                }
            };
        }

        private void OpenSettings()
        {
            Current.Dispatcher.Invoke(() =>
            {
                if (_settingsWindow == null)
                {
                    _settingsWindow = new SettingsWindow();
                    _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                    _settingsWindow.Show();
                }
                else
                {
                    _settingsWindow.Activate();
                }
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
