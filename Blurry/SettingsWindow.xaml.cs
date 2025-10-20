using System.Windows;

namespace Blurry
{
    public partial class SettingsWindow : Window
    {
        public static double BlurValue { get; private set; } = 25;

        public SettingsWindow()
        {
            InitializeComponent();
            BlurSlider.Value = BlurValue;
            BlurValueText.Text = ((int)BlurValue).ToString();

            BlurSlider.ValueChanged += (s, e) =>
            {
                BlurValue = BlurSlider.Value;
                BlurValueText.Text = ((int)BlurSlider.Value).ToString();
            };
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
