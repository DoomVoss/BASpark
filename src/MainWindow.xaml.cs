using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Gma.System.MouseKeyHook;
using Microsoft.Win32;

namespace BASpark
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        // 新增：引入获取光标信息的 API
        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080; 

        private const Int32 CURSOR_SHOWING = 0x00000001; // 新增：光标可见状态码
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private IKeyboardMouseEvents? _globalHook;
        private IntPtr _hwnd;
        private int _virtualScreenLeft;
        private int _virtualScreenTop;
        private int _virtualScreenWidth;
        private int _virtualScreenHeight;

        private long _lastMoveTicks = 0;
        private long _lastClickTicks = 0;
        private bool _isPrimaryPointerDown = false;
        private bool _isTouchLikeInput = false;
        private string? _lastReportedInputMode;
        private bool? _lastReportedAlwaysTrail;

        private long _moveIntervalTicks = 250000;
        private const long ClickIntervalTicks = 300000;
        private const string InputModeMouse = "mouse";
        private const string InputModeTouch = "touch";

        public MainWindow()
        {
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
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            UpdateOverlayBounds();
            SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
            SetupGlobalHooks();
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
            hz = Math.Clamp(hz, 10, 240);
            _moveIntervalTicks = TimeSpan.FromSeconds(1.0 / hz).Ticks;
        }

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
                    webView.CoreWebView2.NavigationCompleted += (s, e) => {
                        _lastReportedInputMode = null;
                        _lastReportedAlwaysTrail = null;
                        UpdateColor(ConfigManager.ParticleColor);
                        UpdateEffectSettings(ConfigManager.EffectScale, ConfigManager.EffectOpacity, ConfigManager.EffectSpeed);
                        SyncInputContext(InputModeMouse);
                    };
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("WebView2 初始化失败: " + ex.Message);
            }
        }

        // 判断光标是否可见
        private bool IsCursorVisible()
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
            if (_lastReportedInputMode == inputMode && _lastReportedAlwaysTrail == alwaysTrailEnabled)
            {
                return string.Empty;
            }

            _lastReportedInputMode = inputMode;
            _lastReportedAlwaysTrail = alwaysTrailEnabled;
            string alwaysTrailLiteral = alwaysTrailEnabled ? "true" : "false";
            return $"if(window.setInputContext) window.setInputContext('{inputMode}', {alwaysTrailLiteral});";
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

        private static string FormatCoordinate(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private void HandleDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdateOverlayBounds);
        }

        private void UpdateOverlayBounds()
        {
            _virtualScreenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
            _virtualScreenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
            _virtualScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            _virtualScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (_hwnd == IntPtr.Zero || _virtualScreenWidth <= 0 || _virtualScreenHeight <= 0)
            {
                return;
            }

            SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                _virtualScreenLeft,
                _virtualScreenTop,
                _virtualScreenWidth,
                _virtualScreenHeight,
                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private bool TryConvertScreenToOverlayPoint(int screenX, int screenY, out System.Windows.Point clientPoint)
        {
            clientPoint = default;

            double viewportWidth = webView.ActualWidth > 0 ? webView.ActualWidth : ActualWidth;
            double viewportHeight = webView.ActualHeight > 0 ? webView.ActualHeight : ActualHeight;
            if (_virtualScreenWidth <= 0 || _virtualScreenHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            {
                return false;
            }

            double normalizedX = (screenX - _virtualScreenLeft) / (double)_virtualScreenWidth;
            double normalizedY = (screenY - _virtualScreenTop) / (double)_virtualScreenHeight;
            double maxX = Math.Max(0, viewportWidth - 1);
            double maxY = Math.Max(0, viewportHeight - 1);

            clientPoint = new System.Windows.Point(
                Math.Clamp(normalizedX * viewportWidth, 0, maxX),
                Math.Clamp(normalizedY * viewportHeight, 0, maxY));
            return true;
        }

        private void SetupGlobalHooks()
        {
            _globalHook = Hook.GlobalEvents();

            _globalHook.MouseDownExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;

                if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

                _isPrimaryPointerDown = true;
                _isTouchLikeInput = !IsCursorVisible();

                string inputMode = _isTouchLikeInput ? InputModeTouch : InputModeMouse;
                SyncInputContext(inputMode);

                ConfigManager.TotalClicks++;

                long currentTicks = DateTime.Now.Ticks;
                if (currentTicks - _lastClickTicks < ClickIntervalTicks) return;
                _lastClickTicks = currentTicks;

                if (!TryConvertScreenToOverlayPoint(e.X, e.Y, out System.Windows.Point clientPoint)) return;
                string x = FormatCoordinate(clientPoint.X);
                string y = FormatCoordinate(clientPoint.Y);
                ExecuteWithInputContext(inputMode, $"if(window.externalBoom) window.externalBoom({x}, {y});");
            };

            _globalHook.MouseMoveExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;

                bool cursorVisible = IsCursorVisible();
                if (!cursorVisible && !_isPrimaryPointerDown) return;

                string inputMode = (_isTouchLikeInput || !cursorVisible) ? InputModeTouch : InputModeMouse;
                SyncInputContext(inputMode);

                long currentTicks = DateTime.Now.Ticks;
                if (currentTicks - _lastMoveTicks < _moveIntervalTicks) return;
                _lastMoveTicks = currentTicks;

                if (!TryConvertScreenToOverlayPoint(e.X, e.Y, out System.Windows.Point clientPoint)) return;
                string x = FormatCoordinate(clientPoint.X);
                string y = FormatCoordinate(clientPoint.Y);
                ExecuteWithInputContext(inputMode, $"if(window.externalMove) window.externalMove({x}, {y});");
            };

            _globalHook.MouseUpExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;
                if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

                string inputMode = _isTouchLikeInput ? InputModeTouch : InputModeMouse;
                ExecuteWithInputContext(inputMode, "if(window.externalUp) window.externalUp();");
                _isPrimaryPointerDown = false;
                _isTouchLikeInput = false;
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
            _globalHook?.Dispose();
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            base.OnClosed(e);
        }
    }
}
