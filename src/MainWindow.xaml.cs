using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace BASpark
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int CURSOR_SHOWING = 0x00000001;
        private const uint EVENT_OBJECT_REORDER = 0x8004;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        private readonly string _screenDeviceName;
        private readonly Rectangle _screenBounds;
        private IntPtr _hwnd;
        private string? _lastReportedInputMode;
        private bool? _lastReportedAlwaysTrail;
        private bool? _lastReportedMultiTouch;
        private const string InputModeMouse = "mouse";
        private const string InputModeTouch = "touch";

        private System.Windows.Threading.DispatcherTimer? _topmostTimer;
        private EventHandler<CoreWebView2NavigationCompletedEventArgs>? _navigationCompletedHandler;
        private WinEventDelegate? _winEventDelegate;
        private IntPtr _winEventHook = IntPtr.Zero;
        private long _lastEnsureTopmostTicks;
        private static readonly long EnsureTopmostDebounceTicks = TimeSpan.FromMilliseconds(80).Ticks;

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        public MainWindow(Screen screen)
        {
            _screenDeviceName = screen.DeviceName;
            _screenBounds = screen.Bounds;
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            InitializeComponent();
            webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            UpdateTrailRefreshRate(ConfigManager.TrailRefreshRate);
            _ = InitWebView();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;

            int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);

            UpdateOverlayBounds();
            InitRealtimeTopmostHook();

            InitTopmostSentinel();
        }

        private void InitRealtimeTopmostHook()
        {
            _winEventDelegate = WinEventProc;
            _winEventHook = SetWinEventHook(
                EVENT_OBJECT_REORDER,
                EVENT_OBJECT_REORDER,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            _ = hWinEventHook;
            _ = eventType;
            _ = hwnd;
            _ = idObject;
            _ = idChild;
            _ = dwEventThread;
            _ = dwmsEventTime;
            long nowTicks = DateTime.UtcNow.Ticks;
            if (nowTicks - _lastEnsureTopmostTicks < EnsureTopmostDebounceTicks)
            {
                return;
            }
            _lastEnsureTopmostTicks = nowTicks;
            Dispatcher.BeginInvoke(new Action(SafeEnsureTopmost));
        }

        private void InitTopmostSentinel()
        {
            SafeEnsureTopmost();

            _topmostTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _topmostTimer.Tick += (s, e) => SafeEnsureTopmost();
            _topmostTimer.Start();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            SafeEnsureTopmost();
        }

        private void SafeEnsureTopmost()
        {
            if (_hwnd == IntPtr.Zero) return;
            Rectangle bounds = GetScreenBounds();
            SetWindowPos(_hwnd, HWND_TOPMOST,
                bounds.Left,
                bounds.Top - 1,
                bounds.Width,
                bounds.Height,
                SWP_NOACTIVATE | SWP_NOSENDCHANGING);
        }

        public void UpdateColor(string color)
        {
            if (webView?.CoreWebView2 != null)
                _ = webView.CoreWebView2.ExecuteScriptAsync($"if(window.updateColor) window.updateColor('{color}');");
        }

        public void UpdateEffectSettings(double scale, double opacity, double speed)
        {
            if (webView?.CoreWebView2 == null) return;

            string scaleStr = scale.ToString("F2", CultureInfo.InvariantCulture);
            string opacityStr = opacity.ToString("F2", CultureInfo.InvariantCulture);
            string speedStr = speed.ToString("F2", CultureInfo.InvariantCulture);

            _ = webView.CoreWebView2.ExecuteScriptAsync(
                $"if(window.updateEffectSettings) window.updateEffectSettings({scaleStr}, {opacityStr}, {speedStr});");
        }

        public void UpdateTrailRefreshRate(int hz)
        {
            _ = hz;
        }

        public void UpdateTouchMode(bool enabled)
        {
            ConfigManager.IsTouchscreenMode = enabled;
        }

        public IntPtr Handle => _hwnd;

        private async System.Threading.Tasks.Task InitWebView()
        {
            try
            {
                var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions(
                    "--disable-background-timer-throttling --disable-features=CalculateNativeWinOcclusion --enable-begin-frame-scheduling"
                );

                string userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BASpark_WebView2");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Web/index.html"));
                if (streamInfo != null)
                {
                    using var reader = new System.IO.StreamReader(streamInfo.Stream);
                    string htmlContent = reader.ReadToEnd();
                    webView.CoreWebView2.NavigateToString(htmlContent);
                    _navigationCompletedHandler = (s, e) =>
                    {
                        _lastReportedInputMode = null;
                        _lastReportedAlwaysTrail = null;
                        UpdateColor(ConfigManager.ParticleColor);
                        UpdateEffectSettings(ConfigManager.EffectScale, ConfigManager.EffectOpacity, ConfigManager.EffectSpeed);
                        SyncInputContext(InputModeMouse);
                    };
                    webView.CoreWebView2.NavigationCompleted += _navigationCompletedHandler;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("WebView2 初始化失败: " + ex.Message);
            }
        }

        private static bool IsCursorVisible()
        {
            CURSORINFO pci = new CURSORINFO();
            pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (GetCursorInfo(out pci))
            {
                return (pci.flags & CURSOR_SHOWING) != 0;
            }
            return true;
        }

        private string BuildInputContextScript(string inputMode)
        {
            bool alwaysTrailEnabled = ConfigManager.EnableAlwaysTrailEffect;
            bool multiTouchEnabled = ConfigManager.EnableMultiTouch;

            if (_lastReportedInputMode == inputMode && 
                _lastReportedAlwaysTrail == alwaysTrailEnabled &&
                _lastReportedMultiTouch == multiTouchEnabled)
            {
                return string.Empty;
            }

            _lastReportedInputMode = inputMode;
            _lastReportedAlwaysTrail = alwaysTrailEnabled;
            _lastReportedMultiTouch = multiTouchEnabled;

            string alwaysTrailLiteral = alwaysTrailEnabled ? "true" : "false";
            string multiTouchLiteral = multiTouchEnabled ? "true" : "false";

            return $"if(window.setInputContext) window.setInputContext('{inputMode}', {alwaysTrailLiteral}, {multiTouchLiteral});";
        }

        private void SyncInputContext(string inputMode)
        {
            if (webView?.CoreWebView2 == null) return;

            string script = BuildInputContextScript(inputMode);
            if (string.IsNullOrEmpty(script)) return;

            _ = webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void ExecuteWithInputContext(string inputMode, string actionScript)
        {
            if (webView?.CoreWebView2 == null) return;

            string contextScript = BuildInputContextScript(inputMode);
            _ = webView.CoreWebView2.ExecuteScriptAsync(contextScript + actionScript);
        }

        public bool ContainsScreenPoint(int x, int y)
        {
            return GetScreenBounds().Contains(x, y);
        }

        public void EmitDown(int x, int y)
        {
            if (!TryConvertScreenToOverlayPoint(x, y, out System.Windows.Point clientPoint)) return;
            bool touchLike = !IsCursorVisible();
            string inputMode = touchLike ? InputModeTouch : InputModeMouse;
            string px = FormatCoordinate(clientPoint.X);
            string py = FormatCoordinate(clientPoint.Y);
            ExecuteWithInputContext(inputMode, $"if(window.externalBoom) window.externalBoom({px}, {py});");
        }

        public void EmitMove(int x, int y, bool touchLike)
        {
            if (!TryConvertScreenToOverlayPoint(x, y, out System.Windows.Point clientPoint)) return;
            string inputMode = touchLike ? InputModeTouch : InputModeMouse;
            string px = FormatCoordinate(clientPoint.X);
            string py = FormatCoordinate(clientPoint.Y);
            ExecuteWithInputContext(inputMode, $"if(window.externalMove) window.externalMove({px}, {py});");
        }

        public void EmitUp(bool touchLike)
        {
            string inputMode = touchLike ? InputModeTouch : InputModeMouse;
            ExecuteWithInputContext(inputMode, "if(window.externalUp) window.externalUp();");
        }

        private void UpdateOverlayBounds()
        {
            Rectangle bounds = GetScreenBounds();
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = bounds.Left / dpi.DpiScaleX;
            Top = (bounds.Top - 1) / dpi.DpiScaleY;
            Width = bounds.Width / dpi.DpiScaleX;
            Height = bounds.Height / dpi.DpiScaleY;

            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(_hwnd, HWND_TOPMOST, bounds.Left, bounds.Top - 1, bounds.Width, bounds.Height, SWP_NOACTIVATE);
        }

        public string ScreenDeviceName => _screenDeviceName;

        private Rectangle GetScreenBounds()
        {
            Screen? current = Screen.AllScreens.FirstOrDefault(s =>
                string.Equals(s.DeviceName, _screenDeviceName, StringComparison.OrdinalIgnoreCase));
            return current?.Bounds ?? _screenBounds;
        }

        private static string FormatCoordinate(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private bool TryConvertScreenToOverlayPoint(int screenX, int screenY, out System.Windows.Point percentPoint)
        {
            percentPoint = default;
            try
            {
                if (!GetWindowRect(_hwnd, out RECT rect)) return false;

                double physWidth = rect.Right - rect.Left;
                double physHeight = rect.Bottom - rect.Top;
                if (physWidth <= 0 || physHeight <= 0) return false;

                double percentX = (screenX - rect.Left) / physWidth;
                double percentY = (screenY - rect.Top) / physHeight;

                percentPoint = new System.Windows.Point(
                    Math.Clamp(percentX, 0.0, 1.0),
                    Math.Clamp(percentY, 0.0, 1.0)
                );
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_topmostTimer != null)
            {
                _topmostTimer.Stop();
                _topmostTimer = null;
            }

            if (webView?.CoreWebView2 != null && _navigationCompletedHandler != null)
            {
                webView.CoreWebView2.NavigationCompleted -= _navigationCompletedHandler;
                _navigationCompletedHandler = null;
            }

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _winEventDelegate = null;

            webView?.Dispose();
            base.OnClosed(e);
        }
    }
}