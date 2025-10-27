using System.Runtime.InteropServices;
using System.Windows;
using Blurry.Managers;
using Application = System.Windows.Application;

namespace Blurry
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private SettingsWindow? _settingsWindow;



        [DllImport("Shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness awareness);

        private enum ProcessDpiAwareness
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Enable per-monitor DPI awareness
                SetProcessDpiAwareness(ProcessDpiAwareness.Process_Per_Monitor_DPI_Aware);
            }
            catch
            {
                // On systems before Windows 8.1, an error is given, but that's not a problem.
            }

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
