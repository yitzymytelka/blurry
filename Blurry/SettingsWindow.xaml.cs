using System.Windows;
using Blurry.Managers;

namespace Blurry
{
    public partial class SettingsWindow : Window
    {
        public static double BlurValue { get; private set; } = 25;

        public SettingsWindow()
        {
            InitializeComponent();

            // Blur slider
            BlurSlider.Value = BlurValue;
            BlurValueText.Text = ((int)BlurValue).ToString();
            BlurSlider.ValueChanged += (s, e) =>
            {
                BlurValue = BlurSlider.Value;
                BlurValueText.Text = ((int)BlurSlider.Value).ToString();
            };

            LoadMonitors();
        }

        private void LoadMonitors()
        {
            var allScreens = Screen.AllScreens;

            var items = allScreens.Select(screen => new
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
