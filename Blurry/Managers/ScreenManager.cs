namespace Blurry.Managers
{
    public static class ScreenManager
    {

        private static readonly Dictionary<string, BlurWindow> _activeWindows = [];



        public static bool MonitorIsActive(string DeviceName) =>
            _activeWindows.ContainsKey(DeviceName);



        public static void ToggleAllMonitors()
        {
            var allScreens = Screen.AllScreens;
            foreach (var screen in allScreens)
            {
                if(!_activeWindows.ContainsKey(screen.DeviceName))
                    CreateAndShowBlurWindow(screen);
            }
        }

        public static void DisableAllMonitors()
        {
            foreach (var item in _activeWindows)
            {
                item.Value.Close();
            }

           _activeWindows.Clear();
        }


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
                CreateAndShowBlurWindow(screen);
            }
        }

        private static void CreateAndShowBlurWindow(Screen screen)
        {
            var win = new BlurWindow(screen);
            win.Show();
            _activeWindows[screen.DeviceName] = win;
        }

    }
}
