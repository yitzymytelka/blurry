using System.Runtime.InteropServices;
using System.Windows;

namespace Blurry.Managers
{
    public static class DpiManager
    {
        [DllImport("User32.dll")]
        private static extern nint MonitorFromPoint([In] POINT pt, [In] uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(nint hmonitor, MonitorDpiType dpiType,
            out uint dpiX, out uint dpiY);

        private enum MonitorDpiType
        {
            EffectiveDpi = 0,
            AngularDpi = 1,
            RawDpi = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        public static double GetScaleFactor(Screen screen)
        {
            var center = new POINT(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
            var hMonitor = MonitorFromPoint(center, 2);

            if (GetDpiForMonitor(hMonitor, MonitorDpiType.EffectiveDpi, out uint dpiX, out uint dpiY) != 0)
                return 1.0; // default 100% on error

            return dpiX / 96.0; // exm: 120 dpi => 1.25x
        }

        public static Rect ToWpfRect(Screen screen)
        {
            double scale = GetScaleFactor(screen);

            return new Rect(
                screen.Bounds.Left / scale,
                screen.Bounds.Top / scale,
                screen.Bounds.Width / scale,
                screen.Bounds.Height / scale
            );
        }
    }
}
