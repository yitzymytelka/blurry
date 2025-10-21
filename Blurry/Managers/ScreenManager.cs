namespace Blurry.Managers
{
    public static class ScreenManager
    {

        private static readonly Dictionary<string, BlurWindow> _activeWindows = [];



        public static bool MonitorIsActive(string DeviceName) => 
            _activeWindows.ContainsKey(DeviceName);



        public static void TogglePrimaryMonitor() => ToggleMonitor(Screen.PrimaryScreen);
        

        public static void ToggleMonitor(Screen screen)
        {
            if (_activeWindows.TryGetValue(screen.DeviceName, out BlurWindow? value))
            {
                value.Close();
                _activeWindows.Remove(screen.DeviceName);
            }
            else
            {
                var win = new BlurWindow(screen);
                win.Show();
                _activeWindows[screen.DeviceName] = win;
            }
        }

    }
}
