using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Blurry.Managers
{
    public static class HotkeyManager
    {
        private static HwndSource? _source;
        private static int _currentId = 0;

        // Hotkey -> Action mapping
        private static readonly Dictionary<int, Action> _actions = new();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [Flags]
        public enum Modifiers : uint
        {
            None = 0x0000,
            Alt = 0x0001,
            Ctrl = 0x0002,
            Shift = 0x0004,
            Win = 0x0008
        }

        public static void Initialize()
        {
            if (_source != null)
                return;

            var parameters = new HwndSourceParameters("HotkeyMessageWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = unchecked((int)0x80000000) // WS_POPUP
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
        }

        public static bool RegisterHotkey(Modifiers modifiers, uint key, Action action)
        {
            if (_source == null)
                throw new InvalidOperationException("HotkeyManager.Initialize() was not called.");

            _currentId++;
            bool result = RegisterHotKey(_source.Handle, _currentId, (uint)modifiers, key);

            if (result)
                _actions[_currentId] = action;
            else
                _currentId--;

            return result;
        }

        public static void UnregisterAll()
        {
            if (_source == null)
                return;

            for (int i = _currentId; i > 0; i--)
                UnregisterHotKey(_source.Handle, i);

            _actions.Clear();
            _currentId = 0;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                if (_actions.TryGetValue(id, out var action))
                {
                    action.Invoke();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public static void Dispose()
        {
            UnregisterAll();

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source.Dispose();
                _source = null;
            }
        }
    }
}
