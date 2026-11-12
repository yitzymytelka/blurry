using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using Blurry.Hooks;
using Blurry.Managers;

namespace Blurry
{
    public partial class BlurWindow : Window
    {
        private nint _hook;
        private WinEventHook.WinEventDelegate? _callback;
        private HwndSource? _source;
        private bool _applying;
        private bool _showingBlur;

        // Default constructor — for primary screen
        public BlurWindow() : this(Screen.PrimaryScreen!) { }

        public BlurWindow(Screen targetScreen)
        {
            InitializeComponent();

            // Accent blur is ignored, and the client stays black, when the glass
            // frame is extended into the window. A zero-thickness chrome keeps it off.
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                GlassFrameThickness = new Thickness(0),
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            WindowStartupLocation = WindowStartupLocation.Manual;
            Tag = targetScreen;
            FitToScreen(targetScreen);

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            ShowInTaskbar = false;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _source = (HwndSource)PresentationSource.FromVisual(this)!;
            _source.AddHook(WndProc);
            ApplyPresentation();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Tag is Screen screen)
                FitToScreen(screen);

            // WPF can rewrite extended styles and z-order while the HWND is shown.
            ApplyPresentation();
            SinkBelowForeground();
            SetupFocusHook();
            Dispatcher.BeginInvoke(new Action(SinkBelowForeground), DispatcherPriority.Render);
        }

        private void FitToScreen(Screen screen)
        {
            var rect = DpiManager.ToWpfRect(screen);

            Left = rect.Left;
            Top = rect.Top;
            Width = rect.Width;
            Height = rect.Height;
        }

        private void ApplyPresentation()
        {
            if (_applying)
                return;

            _applying = true;
            try
            {
                _showingBlur = BlurEffectManager.Apply(this);
                ApplyClickThrough();
                if (_showingBlur)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    DefWindowProc(hwnd, WM_NCACTIVATE, 1, 0);
                }
            }
            finally
            {
                _applying = false;
            }
        }

        private void ApplyClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == 0)
                return;

            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

            // A layered window cannot show the accent blur; DWM paints it black.
            // Dim mode uses Window.Opacity, which needs the layered bit left on.
            if (Opacity >= 1)
            {
                exStyle &= ~WS_EX_LAYERED;
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
                return;
            }

            SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
            var alpha = (byte)Math.Clamp((int)Math.Round(Opacity * 255), 1, 255);
            SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
        }

        private const int WM_NCACTIVATE = 0x0086;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_POWERBROADCAST = 0x0218;

            // Paint the blur as active without becoming the foreground window.
            // Otherwise DWM swaps background acrylic for the opaque tint, which
            // is solid black, while the user's window is the one that stays active.
            if (msg == WM_NCACTIVATE && _showingBlur)
            {
                handled = true;
                return DefWindowProc(hwnd, msg, new IntPtr(1), lParam);
            }

            if (msg == WM_SETTINGCHANGE || msg == WM_POWERBROADCAST)
            {
                // Transparency effects and battery saver turn acrylic into an
                // opaque copy of the tint. Swap to the dim instead of waiting
                // for the next toggle.
                if (IsLoaded && _showingBlur != BlurEffectManager.CanProduceBlur())
                {
                    ApplyPresentation();
                    SinkBelowForeground();
                }
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }

            if (_hook != 0)
                WinEventHook.UnhookWinEvent(_hook);
        }

        #region Win32 / SetWinEventHook

        private void SetupFocusHook()
        {
            _callback = OnForegroundWindowChanged;
            _hook = WinEventHook.SetWinEventHook(
                WinEventHook.EVENT_SYSTEM_FOREGROUND,
                WinEventHook.EVENT_SYSTEM_FOREGROUND,
                0,
                _callback,
                0, 0,
                WinEventHook.WINEVENT_OUTOFCONTEXT | WinEventHook.WINEVENT_SKIPOWNPROCESS
            );
        }

        private void OnForegroundWindowChanged(nint hWinEventHook, uint eventType,
            nint hwndActive, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(SinkBelowForeground));
                return;
            }

            SinkBelowForeground();
        }

        private void SinkBelowForeground()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == 0)
                return;

            // Topmost would keep this sheet above the active window no matter
            // what insert-after handle we pass.
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            var foreground = GetForegroundWindow();
            if (foreground == 0 || foreground == hwnd || IsTopmost(foreground))
                return;

            SetWindowPos(hwnd, foreground, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private static bool IsTopmost(nint hwnd)
        {
            return (GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        private const int GWL_EXSTYLE = -20;
        private const nint WS_EX_LAYERED = 0x00080000;
        private const nint WS_EX_TRANSPARENT = 0x00000020;
        private const nint WS_EX_TOOLWINDOW = 0x00000080;
        private const nint WS_EX_NOACTIVATE = 0x08000000;
        private const nint WS_EX_TOPMOST = 0x00000008;
        private const uint LWA_ALPHA = 0x2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly nint HWND_NOTOPMOST = new(-2);

        // GetWindowLongPtr is a macro. The PtrW export exists only in 64-bit user32.
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern nint GetWindowLong32(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern nint SetWindowLong32(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

        private static nint GetWindowLongPtr(nint hWnd, int nIndex) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

        private static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong) =>
            IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern nint DefWindowProc(nint hwnd, int msg, nint wParam, nint lParam);

        #endregion
    }
}
