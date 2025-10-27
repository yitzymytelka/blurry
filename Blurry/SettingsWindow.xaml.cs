using System.Windows;
using Blurry.Managers;

namespace Blurry
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadMonitors();
        }

        private void LoadMonitors()
        {
            var allScreens = Screen.AllScreens;

            var items = allScreens.OrderByDescending(x => x.Primary).Select(screen => new
            {
                Name = $"{(screen.Primary ? "[Primary] " : "")}{screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})",
                ButtonText = ScreenManager.MonitorIsActive(screen.DeviceName) ? "Disable" : "Enable",
                Screen = screen,
                IsPrimary = screen.Primary
            }).ToList();

            MonitorItems.ItemsSource = items;
        }

        private void ToggleMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            if (btn.Tag is not Screen screen) return;

            ScreenManager.ToggleMonitor(screen);

            LoadMonitors();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
