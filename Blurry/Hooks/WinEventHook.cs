using System.Runtime.InteropServices;

namespace Blurry.Hooks
{
    public static class WinEventHook
    {
        public delegate void WinEventDelegate(nint hWinEventHook, uint eventType,
            nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        [DllImport("user32.dll")]
        public static extern nint SetWinEventHook(
            uint eventMin, uint eventMax, nint hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(nint hWinEventHook);
    }
}
