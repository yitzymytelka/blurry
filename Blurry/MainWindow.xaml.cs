using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace Blurry
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;
        private IntPtr _hook;
        private WinEventHook.WinEventDelegate _callback;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();

            Loaded += OnLoaded;

            this.ShowInTaskbar = false;

            // ESC -> çıkış
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    Application.Current.Shutdown();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Click-through ve ALT+TAB görünmez
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            ApplyFullScreenBlur();

            // SetWinEventHook ile aktif pencere değişimini yakala
            SetupFocusHook();
        }

        private void ApplyFullScreenBlur()
        {
            Left = 0;
            Top = 0;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // Görev çubuğu yüksekliği
            var taskbarHeight = Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
            RootGrid.Margin = new Thickness(0, 0, 0, taskbarHeight);

            BlurFx.Radius = SettingsWindow.BlurValue;
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
            menu.Items.Add("Blur Settings", null, (s, e) => OpenSettingsWindow());
            menu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;
        }

        private void OpenSettingsWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var settings = new SettingsWindow();
                settings.Show();
                settings.Activate();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_hook != IntPtr.Zero)
            {
                WinEventHook.UnhookWinEvent(_hook);
            }
        }

        #region Win32 / SetWinEventHook

        private void SetupFocusHook()
        {
            _callback = new WinEventHook.WinEventDelegate(OnForegroundWindowChanged);
            _hook = WinEventHook.SetWinEventHook(
                WinEventHook.EVENT_SYSTEM_FOREGROUND,
                WinEventHook.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _callback,
                0, 0,
                WinEventHook.WINEVENT_OUTOFCONTEXT | WinEventHook.WINEVENT_SKIPOWNPROCESS
            );
        }

        private void OnForegroundWindowChanged(IntPtr hWinEventHook, uint eventType,
            IntPtr hwndActive, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            var hwndBlur = new WindowInteropHelper(this).Handle;

            // Önce blur penceresini en alta gönder
            SetWindowPos(hwndBlur, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // Sonra aktif pencerenin altına çek
            SetWindowPos(hwndBlur, hwndActive, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_BOTTOM = new(1);

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        #endregion
    }
}
