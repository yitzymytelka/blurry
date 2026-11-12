using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Blurry.Managers
{
    public static class BlurEffectManager
    {
        // ABGR. Alpha must be non-zero: acrylic treats 0 as "no effect" and DWM
        // fills the window with opaque black. 0.2 matches the previous tint.
        private const float BlurTintIntensity = 0.2f;
        private const byte DimAlpha = 128;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMSBT_NONE = 1;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

        // Windows 11 22H2 removed ACCENT_ENABLE_BLURBEHIND (state 3). Using it
        // paints the client area with the tint and drops the alpha, which is
        // solid black. Acrylic (state 4) still blurs, including while this
        // overlay is unfocused. The system-backdrop attribute does not: it
        // replaces background acrylic with an opaque color when the window
        // deactivates, and this overlay is never the active window.
        private const int Win11_22H2 = 22621;
        private const int AccentAcrylicFlagsModern = 2; // ACCENT_ENABLE_MODERN_ACRYLIC_RECIPE

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int Flags;
            public uint GradientColor;
            public int AnimationId;
        }

        // cbData is SIZE_T. An int here makes the x64 marshaller too short and
        // the call reads past the struct.
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public nint Data;
            public nuint SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        // hRgn (0,0,-1,-1) is the DWM "whole window" blur region.
        private const uint DwmBbEnable = 0x1;
        private const uint DwmBbBlurRegion = 0x2;
        private const uint DwmBbTransitionOnMaximized = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        private struct DwmBlurBehind
        {
            public uint dwFlags;
            public int fEnable;
            public nint hRgnBlur;
            public int fTransitionOnMaximized;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OSVERSIONINFOW
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(nint hwnd, ref DwmBlurBehind blurBehind);

        [DllImport("gdi32.dll")]
        private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(nint hObject);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOW version);

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        private static readonly int WindowsBuild = ReadWindowsBuild();

        /// <summary>
        /// Applies a desktop blur, or a semi-transparent dim when this machine
        /// cannot produce one. Returns true when the blur was applied.
        /// </summary>
        public static bool Apply(BlurWindow window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == 0)
                return false;

            if (CanProduceBlur() && TryEnableBlur(window, hwnd))
                return true;

            EnableDim(window, hwnd);
            return false;
        }

        public static bool CanProduceBlur()
        {
            if (SystemParameters.HighContrast)
                return false;
            if (IsBatterySaverOn())
                return false;
            if (!IsTransparencyEnabled())
                return false;
            return true;
        }

        private static bool TryEnableBlur(BlurWindow window, nint hwnd)
        {
            window.Opacity = 1;
            window.Background = System.Windows.Media.Brushes.Transparent;

            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget == null)
                return false;

            // Without this, WPF clears the client to opaque black and the
            // backdrop never shows through.
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

            // Accent blur is ignored (and the window stays black) unless the
            // glass frame is NOT extended into the client area.
            var margins = new Margins();
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            EnableWholeWindowBlurRegion(hwnd);

            // System backdrop acrylic is replaced with an opaque color while
            // the window is inactive. This overlay is never the foreground
            // window, so leave that attribute off.
            int backdropNone = DWMSBT_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropNone, sizeof(int));

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                Flags = WindowsBuild >= Win11_22H2 ? AccentAcrylicFlagsModern : 0,
                GradientColor = BlackTint(BlurTintIntensity)
            };

            if (!SetAccent(hwnd, accent))
            {
                // Pre-acrylic Windows 10: the legacy blur still exists there.
                // Do not try it on 22H2+, where that state is solid black.
                if (WindowsBuild >= Win11_22H2)
                    return false;

                accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                accent.Flags = 2; // ACCENT_ENABLE_GRADIENT_COLOR
                if (!SetAccent(hwnd, accent))
                    return false;
            }

            int corner = DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            int border = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            return true;
        }

        private static void EnableDim(BlurWindow window, nint hwnd)
        {
            SetAccent(hwnd, new AccentPolicy { AccentState = AccentState.ACCENT_DISABLED });
            DisableBlurRegion(hwnd);

            int backdropNone = DWMSBT_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropNone, sizeof(int));

            // Window.Opacity blends the whole window. A black window at 50%
            // is a real dim and does not depend on Transparency effects,
            // which is what turns the accent tint into solid black.
            window.Background = System.Windows.Media.Brushes.Black;
            window.Opacity = DimAlpha / 255.0;

            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = Colors.Black;
        }

        private static void DisableBlurRegion(nint hwnd)
        {
            var blur = new DwmBlurBehind { dwFlags = DwmBbEnable, fEnable = 0 };
            DwmEnableBlurBehindWindow(hwnd, ref blur);
        }

        private static void EnableWholeWindowBlurRegion(nint hwnd)
        {
            var region = CreateRectRgn(0, 0, -1, -1);
            try
            {
                var blur = new DwmBlurBehind
                {
                    dwFlags = region != 0
                        ? DwmBbEnable | DwmBbBlurRegion | DwmBbTransitionOnMaximized
                        : DwmBbEnable,
                    fEnable = 1,
                    hRgnBlur = region,
                    fTransitionOnMaximized = 1
                };
                DwmEnableBlurBehindWindow(hwnd, ref blur);
            }
            finally
            {
                if (region != 0)
                    DeleteObject(region);
            }
        }

        private static bool SetAccent(nint hwnd, AccentPolicy accent)
        {
            var size = Marshal.SizeOf<AccentPolicy>();
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = (nuint)size,
                Data = Marshal.AllocHGlobal(size)
            };

            try
            {
                Marshal.StructureToPtr(accent, data.Data, false);
                return SetWindowCompositionAttribute(hwnd, ref data) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(data.Data);
            }
        }

        private static uint BlackTint(float intensity)
        {
            var alpha = (byte)Math.Clamp((int)(intensity * 255), 1, 255);
            return (uint)(alpha << 24);
        }

        private static bool IsTransparencyEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                // Missing value means the OS default, which is on.
                return key?.GetValue("EnableTransparency") is not int value || value != 0;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsBatterySaverOn()
        {
            if (!GetSystemPowerStatus(out var status))
                return false;
            return status.SystemStatusFlag == 1;
        }

        private static int ReadWindowsBuild()
        {
            try
            {
                var info = new OSVERSIONINFOW
                {
                    dwOSVersionInfoSize = Marshal.SizeOf<OSVERSIONINFOW>(),
                    szCSDVersion = ""
                };
                return RtlGetVersion(ref info) == 0 ? info.dwBuildNumber : Win11_22H2;
            }
            catch
            {
                return Win11_22H2;
            }
        }
    }
}
