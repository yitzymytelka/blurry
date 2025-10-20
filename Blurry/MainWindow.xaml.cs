using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Blurry
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public MainWindow()
        {
            InitializeComponent();

            InitializeTrayIcon();

            this.Topmost = true;
            this.ShowInTaskbar = false;

            // Check active window from every frame
            CompositionTarget.Rendering += OnFrameUpdate;

            // exit with ESC
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    Application.Current.Shutdown();
            };
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

        private void OnFrameUpdate(object? sender, EventArgs e)
        {
            IntPtr active = GetForegroundWindow();
            if (active == IntPtr.Zero) return;

            GetWindowRect(active, out RECT rect);

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Adjust blur intensity from settings
            BlurFx.Radius = SettingsWindow.BlurValue;

          
            var fullRect = new RectangleGeometry(new Rect(0, 0, screenWidth, screenHeight));
            var hole = new RectangleGeometry(new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);

            RootGrid.Clip = combined;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
