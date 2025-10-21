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



        // Default constructor — for primary screen and tray
        public MainWindow() : this(Screen.PrimaryScreen, true) { }

        public MainWindow(Screen targetScreen, bool showTray)
        {
            InitializeComponent();

            Left = targetScreen.Bounds.Left;
            Top = targetScreen.Bounds.Top;
            Width = targetScreen.Bounds.Width;
            Height = targetScreen.Bounds.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Tag = targetScreen;

            Loaded += OnLoaded;
            this.ShowInTaskbar = false;

            if (showTray)
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
            menu.Items.Add("Blur Settings", null, (s, e) => OpenSettingsWindow());
            menu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Click-through and ALT+TAB
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            if (Tag is Screen screen)
                ApplyFullScreenBlur(screen);

            SetupFocusHook();
        }

        private void ApplyFullScreenBlur(Screen screen)
        {
            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;

            // taskbar height by screen
            var taskbarHeight = screen.Bounds.Height - screen.WorkingArea.Height;
            RootGrid.Margin = new Thickness(0, 0, 0, taskbarHeight);

            BlurFx.Radius = SettingsWindow.BlurValue;
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

            // First send the blur window to the bottom
            SetWindowPos(hwndBlur, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            // Then drag it below the active window
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
