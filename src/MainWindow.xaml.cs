using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
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
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("user32.dll")]
        static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // 获取光标信息的 API
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const Int32 CURSOR_SHOWING = 0x00000001; // 光标可见状态码
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int FullscreenTolerance = 2;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSENDCHANGING = 0x0400;
        private static readonly long SuppressionCacheDurationTicks = TimeSpan.FromMilliseconds(250).Ticks;

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
        private long _suppressionCacheValidUntilTicks = 0;
        private bool _isSuppressedByEnvironment = false;

        private long _moveIntervalTicks = 250000;
        private const long ClickIntervalTicks = 300000;
        private const string InputModeMouse = "mouse";
        private const string InputModeTouch = "touch";

        // 新增：层级保活计时器
        private System.Windows.Threading.DispatcherTimer? _topmostTimer;

        public MainWindow()
        {
            // 强制 WPF 使用软件渲染，以避免在 WebView2 透明背景下 DWM 占用过高 GPU 的问题
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
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            UpdateOverlayBounds();
            SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
            SetupGlobalHooks();

            InitTopmostSentinel();
        }

        private void InitTopmostSentinel()
        {
            SafeEnsureTopmost();

            _topmostTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
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

            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
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

        public void RefreshEnvironmentFilterState()
        {
            _suppressionCacheValidUntilTicks = 0;
            ShouldSuppressEffects(forceRefresh: true);
        }

        public bool IsEffectSuppressedByEnvironment()
        {
            return ShouldSuppressEffects();
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
                    webView.CoreWebView2.NavigationCompleted += (s, e) =>
                    {
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

        private bool CanRenderEffects()
        {
            if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null)
            {
                ReleasePointerState();
                return false;
            }

            if (ShouldSuppressEffects())
            {
                ReleasePointerState();
                return false;
            }

            return true;
        }

        private void ReleasePointerState()
        {
            if (!_isPrimaryPointerDown)
            {
                _isTouchLikeInput = false;
                return;
            }

            string inputMode = _isTouchLikeInput ? InputModeTouch : InputModeMouse;
            ExecuteWithInputContext(inputMode, "if(window.externalUp) window.externalUp();");
            _isPrimaryPointerDown = false;
            _isTouchLikeInput = false;
        }

        private bool ShouldSuppressEffects(bool forceRefresh = false)
        {
            if (!ConfigManager.EnableEnvironmentFilter)
            {
                _isSuppressedByEnvironment = false;
                _suppressionCacheValidUntilTicks = 0;
                return false;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            if (!forceRefresh && nowTicks < _suppressionCacheValidUntilTicks)
            {
                return _isSuppressedByEnvironment;
            }

            IntPtr foregroundWindow = GetForegroundWindow();
            if (!TryGetForegroundProcessName(foregroundWindow, out string processName))
            {
                UpdateSuppressionState(nowTicks, false);
                return false;
            }

            if (ConfigManager.HideInFullscreen && IsEffectiveFullscreenWindow(foregroundWindow))
            {
                UpdateSuppressionState(nowTicks, true);
                return true;
            }

            bool isSuppressedByProcessFilter = IsSuppressedByProcessFilter(processName);
            UpdateSuppressionState(nowTicks, isSuppressedByProcessFilter);

            return _isSuppressedByEnvironment;
        }

        private void UpdateSuppressionState(long nowTicks, bool isSuppressed)
        {
            _isSuppressedByEnvironment = isSuppressed;
            _suppressionCacheValidUntilTicks = nowTicks + SuppressionCacheDurationTicks;
        }

        private bool IsSuppressedByProcessFilter(string processName)
        {
            ProcessFilterModeOption mode = ConfigManager.ProcessFilterMode;
            if (mode == ProcessFilterModeOption.Disabled)
            {
                return false;
            }

            var filterEntries = ConfigManager.GetProcessFilterEntries();
            if (filterEntries.Count == 0)
            {
                return false;
            }

            bool isListed = filterEntries.Contains(processName);
            return mode switch
            {
                ProcessFilterModeOption.Blacklist => isListed,
                ProcessFilterModeOption.Whitelist => !isListed,
                _ => false
            };
        }

        private bool TryGetForegroundProcessName(IntPtr hwnd, out string processName)
        {
            processName = string.Empty;
            if (!IsEligibleForegroundWindow(hwnd))
            {
                return false;
            }

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0 || processId == (uint)Environment.ProcessId)
            {
                return false;
            }

            processName = GetProcessExecutableName(processId);
            return !string.IsNullOrWhiteSpace(processName);
        }

        private bool IsEligibleForegroundWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == _hwnd)
            {
                return false;
            }

            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                return false;
            }

            if (hwnd == GetDesktopWindow() || hwnd == GetShellWindow())
            {
                return false;
            }

            string className = GetWindowClassName(hwnd);
            return !string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase);
        }

        private string GetWindowClassName(IntPtr hwnd)
        {
            var classNameBuilder = new StringBuilder(256);
            return GetClassName(hwnd, classNameBuilder, classNameBuilder.Capacity) > 0
                ? classNameBuilder.ToString()
                : string.Empty;
        }

        private string GetProcessExecutableName(uint processId)
        {
            try
            {
                using Process process = Process.GetProcessById(unchecked((int)processId));

                string fallbackName = process.ProcessName;
                if (!fallbackName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    fallbackName += ".exe";
                }

                return fallbackName.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool IsEffectiveFullscreenWindow(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out RECT windowRect))
            {
                return false;
            }

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            MONITORINFO monitorInfo = new MONITORINFO
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            return AreRectsClose(windowRect, monitorInfo.rcMonitor, FullscreenTolerance);
        }

        private static bool AreRectsClose(RECT left, RECT right, int tolerance)
        {
            return Math.Abs(left.Left - right.Left) <= tolerance &&
                   Math.Abs(left.Top - right.Top) <= tolerance &&
                   Math.Abs(left.Right - right.Right) <= tolerance &&
                   Math.Abs(left.Bottom - right.Bottom) <= tolerance;
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
                HWND_TOPMOST,
                _virtualScreenLeft,
                _virtualScreenTop,
                _virtualScreenWidth,
                _virtualScreenHeight,
                SWP_NOACTIVATE);
        }

        private bool TryConvertScreenToOverlayPoint(int screenX, int screenY, out System.Windows.Point percentPoint)
        {
            percentPoint = default;
            try
            {
                // 1. 获取窗口绝对的物理边界
                if (!GetWindowRect(_hwnd, out RECT rect)) return false;

                double physWidth = rect.Right - rect.Left;
                double physHeight = rect.Bottom - rect.Top;
                if (physWidth <= 0 || physHeight <= 0) return false;

                // 2. 计算出纯物理维度的百分比 (0.000 ~ 1.000)
                double percentX = (screenX - rect.Left) / physWidth;
                double percentY = (screenY - rect.Top) / physHeight;

                // 3. 将百分比包装在 Point 里传给下面，彻底抛弃 viewportWidth
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

        private void SetupGlobalHooks()
        {
            _globalHook = Hook.GlobalEvents();

            _globalHook.MouseDownExt += (s, e) =>
            {
                if (!CanRenderEffects()) return;
                if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

                _isPrimaryPointerDown = true;
                _isTouchLikeInput = !IsCursorVisible();

                string inputMode = _isTouchLikeInput ? InputModeTouch : InputModeMouse;
                SyncInputContext(inputMode);

                long currentTicks = DateTime.Now.Ticks;
                if (currentTicks - _lastClickTicks < ClickIntervalTicks) return;
                _lastClickTicks = currentTicks;

                if (!TryConvertScreenToOverlayPoint(e.X, e.Y, out System.Windows.Point clientPoint)) return;

                ConfigManager.TotalClicks++;

                string x = FormatCoordinate(clientPoint.X);
                string y = FormatCoordinate(clientPoint.Y);
                ExecuteWithInputContext(inputMode, $"if(window.externalBoom) window.externalBoom({x}, {y});");
            };

            _globalHook.MouseMoveExt += (s, e) =>
            {
                if (!CanRenderEffects()) return;

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

            _globalHook.MouseUpExt += (s, e) =>
            {
                if (!CanRenderEffects()) return;
                if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

                string inputMode = _isTouchLikeInput ? InputModeTouch : InputModeMouse;
                ExecuteWithInputContext(inputMode, "if(window.externalUp) window.externalUp();");
                _isPrimaryPointerDown = false;
                _isTouchLikeInput = false;
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_topmostTimer != null)
            {
                _topmostTimer.Stop();
                _topmostTimer = null;
            }

            SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
            _globalHook?.Dispose();
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            base.OnClosed(e);
        }
    }
}
