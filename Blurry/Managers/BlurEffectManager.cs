using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Blurry.Managers
{
    public static class BlurEffectManager
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);


        #region Structs
        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_BLURBEHIND = 3
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

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public nint Data;
            public int SizeOfData;
        }

        #endregion Structs



        public static void EnableBlur(BlurWindow window, float intensity = 0.2f)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                GradientColor = (uint)((byte)(intensity * 255) << 24)
            };

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = Marshal.SizeOf(accent),
                Data = Marshal.AllocHGlobal(Marshal.SizeOf(accent))
            };

            Marshal.StructureToPtr(accent, data.Data, false);
            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(data.Data);
        }

    }
}
