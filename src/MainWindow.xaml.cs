using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

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
        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_MONITOR = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        private readonly string _screenDeviceName;
        private readonly Rectangle _screenBounds;
        private IntPtr _hwnd;
        private string? _lastReportedInputMode;
        private bool? _lastReportedAlwaysTrail;
        private const string InputModeMouse = "mouse";
        private const string InputModeTouch = "touch";
        private const string PrimaryRendererResourcePath = "Web/index.html";
        private const string LegacyRendererResourcePath = "Web/index.legacy.html";
        private const string RendererVendorResourcePath = "Web/vendor/ba-click-fx.iife.js";
        private const string RendererAdapterResourcePath = "Web/fx-adapter.js";

        private System.Windows.Threading.DispatcherTimer? _topmostTimer;
        private System.Windows.Threading.DispatcherTimer? _rendererReadyTimeoutTimer;
        private EventHandler<CoreWebView2NavigationStartingEventArgs>? _navigationStartingHandler;
        private EventHandler<CoreWebView2NavigationCompletedEventArgs>? _navigationCompletedHandler;
        private EventHandler<CoreWebView2ProcessFailedEventArgs>? _processFailedHandler;
        private EventHandler<CoreWebView2WebMessageReceivedEventArgs>? _webMessageReceivedHandler;
        private CoreWebView2? _coreWebView;
        private ulong? _currentNavigationId;
        private string? _rendererGeneration;
        private WinEventDelegate? _winEventDelegate;
        private IntPtr _winEventHook = IntPtr.Zero;
        private long _lastEnsureTopmostTicks;
        private bool _isClosing;
        private bool _screenshotCompatibilityMode = ConfigManager.ScreenshotCompatibilityMode;
        private static readonly long EnsureTopmostDebounceTicks = TimeSpan.FromMilliseconds(80).Ticks;
        private bool _hiddenForExternalScreenshotCapture;
        private bool _hiddenByEnvironmentSuppression;
        private bool _overlayRuntimePaused;
        private bool _rendererReady;
        private bool _usingLegacyRenderer;
        private int _inputSamplingRate = ConfigManager.DefaultInputSamplingRate;
        private long _lastLegacyMoveTicks;
        private bool _legacyFallbackAttempted;
        private bool _processRecoveryPending;
        private readonly WebViewUnresponsiveTracker _unresponsiveTracker = new();

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
            UpdateInputSamplingRate(ConfigManager.InputSamplingRate);
            _ = InitWebView();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;

            int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
            ApplyScreenshotCompatibilityMode();

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
            if (_hwnd == IntPtr.Zero || !IsVisible || _overlayRuntimePaused) return;

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
            // JSON serialization keeps registry-backed color text from becoming executable script.
            string colorJson = JsonSerializer.Serialize(color);
            ExecuteScript($"if(window.updateColor) window.updateColor({colorJson});");
        }

        public void UpdateEffectSettings(double scale, double opacity, double trailSpeed, double clickSpeed)
        {
            string scaleStr = scale.ToString("F2", CultureInfo.InvariantCulture);
            string opacityStr = opacity.ToString("F2", CultureInfo.InvariantCulture);
            string trailStr = trailSpeed.ToString("F2", CultureInfo.InvariantCulture);
            string clickStr = clickSpeed.ToString("F2", CultureInfo.InvariantCulture);

            ExecuteScript($"if(window.updateEffectSettings) window.updateEffectSettings({scaleStr}, {opacityStr}, {trailStr}, {clickStr});");
        }

        public void UpdateInputSamplingRate(int rateHz)
        {
            _inputSamplingRate = ConfigManager.NormalizeInputSamplingRate(rateHz);
            _lastLegacyMoveTicks = 0;
            ExecuteScript(BuildInputSamplingRateScript(_inputSamplingRate));
        }

        internal static string BuildInputSamplingRateScript(int rateHz)
        {
            int normalizedRate = ConfigManager.NormalizeInputSamplingRate(rateHz);
            return
                "if(window.updateInputSamplingRate) " +
                $"window.updateInputSamplingRate({normalizedRate.ToString(CultureInfo.InvariantCulture)});";
        }

        public void SetEffectGlow(bool enabled)
        {
            ExecuteScript($"if(window.setEffectGlow) window.setEffectGlow({(enabled ? "true" : "false")});");
        }

        public void SetCurveDraw(bool enabled)
        {
            ExecuteScript($"window.ApplyCurveDraw = {(enabled ? "true" : "false")};");
        }

        public void UpdateTouchMode(bool enabled)
        {
            ConfigManager.IsTouchscreenMode = enabled;
        }

        public void UpdateScreenshotCompatibilityMode(bool enabled)
        {
            _screenshotCompatibilityMode = enabled;
            ApplyScreenshotCompatibilityMode();
        }

        /// 截图工具框选窗口期间暂时隐藏叠加层
        public void SetHiddenForExternalScreenshotCapture(bool hidden)
        {
            if (_hiddenForExternalScreenshotCapture == hidden)
            {
                return;
            }

            _hiddenForExternalScreenshotCapture = hidden;
            SyncOverlayPresentationState();
        }

        /// 环境过滤时隐藏叠加层并暂停 WebView 渲染
        public void SetEnvironmentSuppressed(bool suppressed)
        {
            if (_hiddenByEnvironmentSuppression == suppressed)
            {
                return;
            }

            _hiddenByEnvironmentSuppression = suppressed;
            SyncOverlayPresentationState();
        }

        private bool ShouldOverlayBeVisible =>
            !_hiddenForExternalScreenshotCapture && !_hiddenByEnvironmentSuppression;

        private void SyncOverlayPresentationState()
        {
            if (ShouldOverlayBeVisible)
            {
                if (!IsVisible)
                {
                    Show();
                    ApplyScreenshotCompatibilityMode();
                }

                ResumeOverlayRuntime();
            }
            else
            {
                PauseOverlayRuntime();
                if (IsVisible)
                {
                    Hide();
                }
            }
        }

        private void PauseOverlayRuntime()
        {
            if (_overlayRuntimePaused)
            {
                return;
            }

            _overlayRuntimePaused = true;
            PauseTopmostMonitoring();
            ExecuteScript("if(window.setRenderingPaused) window.setRenderingPaused(true);");
            _ = TrySuspendWebViewAsync();
        }

        private void ResumeOverlayRuntime()
        {
            if (!_overlayRuntimePaused)
            {
                return;
            }

            _overlayRuntimePaused = false;
            if (TryGetCoreWebView2(out CoreWebView2? coreWebView))
            {
                try
                {
                    coreWebView.Resume();
                }
                catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                {
                }
            }

            ExecuteScript("if(window.setRenderingPaused) window.setRenderingPaused(false);");
            ResumeTopmostMonitoring();
        }

        private async System.Threading.Tasks.Task TrySuspendWebViewAsync()
        {
            if (!TryGetCoreWebView2(out CoreWebView2? coreWebView))
            {
                return;
            }

            try
            {
                await coreWebView.TrySuspendAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
            {
            }
        }

        private void PauseTopmostMonitoring()
        {
            if (_topmostTimer != null)
            {
                _topmostTimer.Stop();
            }

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
        }

        private void ResumeTopmostMonitoring()
        {
            if (_hwnd == IntPtr.Zero || !IsVisible)
            {
                return;
            }

            if (_winEventHook == IntPtr.Zero)
            {
                InitRealtimeTopmostHook();
            }

            if (_topmostTimer == null)
            {
                InitTopmostSentinel();
            }
            else if (!_topmostTimer.IsEnabled)
            {
                _topmostTimer.Start();
            }

            SafeEnsureTopmost();
        }

        private void ApplyScreenshotCompatibilityMode()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            // 把特效窗口从系统捕获结果中排除
            uint affinity = _screenshotCompatibilityMode ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
            if (!SetWindowDisplayAffinity(_hwnd, affinity) && _screenshotCompatibilityMode)
            {
                SetWindowDisplayAffinity(_hwnd, WDA_MONITOR);
            }

            SafeEnsureTopmost();
        }

        public IntPtr Handle => _hwnd;

        private async System.Threading.Tasks.Task InitWebView()
        {
            try
            {
                var env = await WebView2EnvironmentHolder.GetOrCreateAsync().ConfigureAwait(true);
                if (_isClosing)
                {
                    return;
                }

                if (webView.CoreWebView2 == null)
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);
                    }
                    catch (ArgumentException ex) when (IsWebViewEnvironmentConflictException(ex))
                    {
                        AppLogger.Debug($"WebView2 environment conflict ignored: {ex.Message}");
                        if (webView.CoreWebView2 == null)
                        {
                            throw;
                        }
                    }
                }

                if (_isClosing || !TryGetCoreWebView2(out CoreWebView2? coreWebView))
                {
                    return;
                }

                DetachCoreWebViewEvents();
                _coreWebView = coreWebView;
                coreWebView.Settings.IsZoomControlEnabled = false;
                coreWebView.Settings.AreDefaultContextMenusEnabled = false;
                coreWebView.Settings.IsStatusBarEnabled = false;

                _processFailedHandler = OnWebViewProcessFailed;
                _navigationStartingHandler = OnNavigationStarting;
                _navigationCompletedHandler = OnNavigationCompleted;
                _webMessageReceivedHandler = OnWebMessageReceived;
                coreWebView.ProcessFailed += _processFailedHandler;
                coreWebView.NavigationStarting += _navigationStartingHandler;
                coreWebView.NavigationCompleted += _navigationCompletedHandler;
                coreWebView.WebMessageReceived += _webMessageReceivedHandler;

                NavigateCurrentRenderer(coreWebView);
            }
            catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
            {
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Localization.Format("WebView2_InitFailed", ex.Message));
            }
        }

        private void NavigateCurrentRenderer(CoreWebView2 coreWebView)
        {
            StopRendererReadyTimeout();
            _rendererReady = _usingLegacyRenderer;

            if (_usingLegacyRenderer)
            {
                _rendererGeneration = null;
                string legacyHtml = ReadResourceText(LegacyRendererResourcePath);
                NavigateHtml(coreWebView, legacyHtml);
                return;
            }

            _rendererGeneration = null;
            try
            {
                string rendererGeneration = Guid.NewGuid().ToString("N");
                string rendererHtml = BuildPrimaryRendererHtml(rendererGeneration);
                _rendererGeneration = rendererGeneration;
                NavigateHtml(coreWebView, rendererHtml);
            }
            catch (Exception ex) when (!IsExpectedWebViewShutdownException(ex))
            {
                AppLogger.Error(
                    $"Failed to prepare BA click renderer on '{_screenDeviceName}'.",
                    ex);
                FallbackToLegacyRenderer("primary renderer resource preparation failed");
                return;
            }

            // A renderer that cannot announce readiness must not leave an invisible overlay active.
            if (!_rendererReady)
            {
                StartRendererReadyTimeout();
            }
        }

        private static string BuildPrimaryRendererHtml(string rendererGeneration)
        {
            string generationJson = JsonSerializer.Serialize(rendererGeneration);
            string adapterScript =
                $"window.__basparkRendererGeneration = {generationJson};\n" +
                ReadResourceText(RendererAdapterResourcePath);

            return WebRendererDocumentBuilder.Build(
                ReadResourceText(PrimaryRendererResourcePath),
                ReadResourceText(RendererVendorResourcePath),
                adapterScript);
        }

        private static string ReadResourceText(string resourcePath)
        {
            string assemblyName = typeof(MainWindow).Assembly.GetName().Name ?? "BASpark";
            // Explicit component lookup also works when a diagnostic host references BASpark.
            var resourceUri = new Uri(
                $"pack://application:,,,/{assemblyName};component/{resourcePath}",
                UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);
            if (streamInfo == null)
            {
                throw new InvalidOperationException($"Embedded renderer resource '{resourcePath}' was not found.");
            }

            using var reader = new System.IO.StreamReader(streamInfo.Stream);
            return reader.ReadToEnd();
        }

        private void NavigateHtml(CoreWebView2 coreWebView, string html)
        {
            // NavigationStarting assigns the authoritative ID before any matching completion event.
            _currentNavigationId = null;
            coreWebView.NavigateToString(html);
        }

        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (_isClosing || !ReferenceEquals(sender, _coreWebView))
            {
                return;
            }

            _currentNavigationId = e.NavigationId;
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_isClosing ||
                !ReferenceEquals(sender, _coreWebView) ||
                _currentNavigationId != e.NavigationId)
            {
                return;
            }

            if (!e.IsSuccess)
            {
                if (!_usingLegacyRenderer)
                {
                    FallbackToLegacyRenderer($"navigation failed: {e.WebErrorStatus}");
                }
                else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    AppLogger.Error(
                        $"Legacy renderer navigation failed on '{_screenDeviceName}': {e.WebErrorStatus}.");
                }
                return;
            }

            // Navigation creates a new JS global object, so every page needs the complete host state.
            _lastReportedInputMode = null;
            _lastReportedAlwaysTrail = null;
            UpdateColor(ConfigManager.ParticleColor);
            ConfigManager.GetAnimationSpeedsForOverlay(out double trailSp, out double clickSp);
            UpdateEffectSettings(ConfigManager.EffectScale, ConfigManager.EffectOpacity, trailSp, clickSp);
            SetEffectGlow(ConfigManager.EnableEffectGlow);
            UpdateInputSamplingRate(ConfigManager.InputSamplingRate);
            SyncInputContext(InputModeMouse);
            if (_overlayRuntimePaused)
            {
                ExecuteScript("if(window.setRenderingPaused) window.setRenderingPaused(true);");
                _ = TrySuspendWebViewAsync();
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_isClosing || _usingLegacyRenderer || !ReferenceEquals(sender, _coreWebView))
            {
                return;
            }

            string messageJson;
            try
            {
                messageJson = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                // Accept object messages defensively while the documented adapter contract remains JSON text.
                messageJson = e.WebMessageAsJson;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(messageJson);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return;
                }
                if (!string.Equals(GetJsonString(root, "source"), "baspark-fx", StringComparison.Ordinal))
                {
                    return;
                }
                if (!string.Equals(
                    GetJsonString(root, "generation"),
                    _rendererGeneration,
                    StringComparison.Ordinal))
                {
                    return;
                }

                string? type = GetJsonString(root, "type");
                string requestedEffectBackend =
                    GetJsonString(root, "requestedEffectBackend") ?? "unknown";
                string resolvedEffectBackend =
                    GetJsonString(root, "resolvedEffectBackend") ?? "unknown";
                string requestedBloomBackend =
                    GetJsonString(root, "requestedBloomBackend") ?? "unknown";
                string resolvedBloomBackend =
                    GetJsonString(root, "resolvedBloomBackend") ?? "unknown";
                string requestedHostCompositing =
                    GetJsonString(root, "requestedHostCompositing") ?? "unknown";
                string resolvedHostCompositing =
                    GetJsonString(root, "resolvedHostCompositing") ?? "unknown";
                string hostCompositingSurface =
                    GetJsonString(root, "hostCompositingSurface") ?? "unknown";
                string? compositingWarning =
                    GetJsonString(root, "compositingWarning");
                string backend = GetJsonString(root, "backend") ??
                    (resolvedEffectBackend == "webgl2"
                        ? resolvedEffectBackend
                        : resolvedBloomBackend);

                if (string.Equals(type, "ready", StringComparison.Ordinal))
                {
                    bool firstReadyMessage = !_rendererReady;
                    _rendererReady = true;
                    _unresponsiveTracker.Reset();
                    StopRendererReadyTimeout();
                    if (firstReadyMessage)
                    {
                        AppLogger.Info(
                            $"BA click renderer ready on '{_screenDeviceName}' " +
                            $"(effective: {backend}, effect: {resolvedEffectBackend}, " +
                            $"bloom: {resolvedBloomBackend}, host: " +
                            $"{resolvedHostCompositing} on {hostCompositingSurface}" +
                            $"{FormatCompositingWarning(compositingWarning)}).");
                    }
                    return;
                }

                if (string.Equals(type, "backend", StringComparison.Ordinal))
                {
                    AppLogger.Info(
                        $"BA click renderer backend on '{_screenDeviceName}': " +
                        $"effective {backend}; effect {resolvedEffectBackend} " +
                        $"(requested {requestedEffectBackend}); bloom {resolvedBloomBackend} " +
                        $"(requested {requestedBloomBackend}); host " +
                        $"{resolvedHostCompositing} on {hostCompositingSurface} " +
                        $"(requested {requestedHostCompositing})" +
                        $"{FormatCompositingWarning(compositingWarning)}.");
                    return;
                }

                if (string.Equals(type, "error", StringComparison.Ordinal))
                {
                    string phase = GetJsonString(root, "phase") ?? "unknown";
                    string message = GetJsonString(root, "message") ?? "unspecified renderer error";
                    AppLogger.Error(
                        $"BA click renderer error on '{_screenDeviceName}' during '{phase}': {message}.");
                    FallbackToLegacyRenderer($"renderer error during '{phase}'");
                }
            }
            catch (JsonException ex)
            {
                AppLogger.Warn(
                    $"Ignored malformed renderer message on '{_screenDeviceName}': {ex.Message}");
            }
        }

        private static string? GetJsonString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private static string FormatCompositingWarning(string? warning)
        {
            return string.IsNullOrWhiteSpace(warning)
                ? string.Empty
                : $"; warning: {warning}";
        }

        private void StartRendererReadyTimeout()
        {
            StopRendererReadyTimeout();
            _rendererReadyTimeoutTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _rendererReadyTimeoutTimer.Tick += OnRendererReadyTimeout;
            _rendererReadyTimeoutTimer.Start();
        }

        private void OnRendererReadyTimeout(object? sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, _rendererReadyTimeoutTimer))
            {
                return;
            }

            StopRendererReadyTimeout();
            if (_isClosing || _rendererReady || _usingLegacyRenderer)
            {
                return;
            }

            AppLogger.Warn(
                $"BA click renderer ready timeout on '{_screenDeviceName}'; switching to legacy renderer.");
            FallbackToLegacyRenderer("ready timeout");
        }

        private void StopRendererReadyTimeout()
        {
            if (_rendererReadyTimeoutTimer == null)
            {
                return;
            }

            _rendererReadyTimeoutTimer.Stop();
            _rendererReadyTimeoutTimer.Tick -= OnRendererReadyTimeout;
            _rendererReadyTimeoutTimer = null;
        }

        private void FallbackToLegacyRenderer(string reason)
        {
            if (_isClosing || _usingLegacyRenderer || _legacyFallbackAttempted)
            {
                return;
            }

            if (!TryGetCoreWebView2(out CoreWebView2? coreWebView))
            {
                return;
            }

            // The one-way fallback prevents a broken primary/legacy pair from navigating forever.
            _legacyFallbackAttempted = true;
            _usingLegacyRenderer = true;
            _rendererReady = true;
            _rendererGeneration = null;
            StopRendererReadyTimeout();
            AppLogger.Warn(
                $"Falling back to legacy renderer on '{_screenDeviceName}' ({reason}).");

            try
            {
                string legacyHtml = ReadResourceText(LegacyRendererResourcePath);
                NavigateHtml(coreWebView, legacyHtml);
            }
            catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
            {
            }
            catch (Exception ex) when (!IsExpectedWebViewShutdownException(ex))
            {
                AppLogger.Error(
                    $"Failed to load legacy renderer on '{_screenDeviceName}'.",
                    ex);
            }
        }

        private static bool IsWebViewEnvironmentConflictException(Exception ex)
        {
            return ex is ArgumentException &&
                   ex.Message.Contains("CoreWebView2Environment", StringComparison.OrdinalIgnoreCase);
        }

        private void OnWebViewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            if (_isClosing ||
                sender is not CoreWebView2 failedCoreWebView ||
                !ReferenceEquals(failedCoreWebView, _coreWebView))
            {
                return;
            }

            CoreWebView2ProcessFailedKind failureKind = e.ProcessFailedKind;
            WebViewProcessRecoveryAction recoveryAction =
                WebViewProcessFailurePolicy.GetRecoveryAction(failureKind);
            if (failureKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {
                bool shouldRecover = _unresponsiveTracker.Register(
                    failedCoreWebView,
                    DateTime.UtcNow.Ticks);
                if (!shouldRecover)
                {
                    AppLogger.Warn(
                        $"WebView2 renderer unresponsive on '{_screenDeviceName}' " +
                        $"({_unresponsiveTracker.ConsecutiveReports}/3); waiting for runtime recovery.");
                    return;
                }

                recoveryAction = WebViewProcessRecoveryAction.RecreateWebViewControl;
            }
            if (recoveryAction == WebViewProcessRecoveryAction.None)
            {
                AppLogger.Warn(
                    $"WebView2 process event on '{_screenDeviceName}' ({failureKind}); runtime recovery is left intact.");
                return;
            }

            if (_processRecoveryPending)
            {
                return;
            }

            _processRecoveryPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_isClosing || !ReferenceEquals(failedCoreWebView, _coreWebView))
                    {
                        return;
                    }

                    AppLogger.Warn(
                        $"WebView2 process failed on '{_screenDeviceName}' ({failureKind}); recovering renderer.");
                    if (recoveryAction == WebViewProcessRecoveryAction.RecreateWebViewControl)
                    {
                        RecreateWebViewControl();
                    }
                    else
                    {
                        NavigateCurrentRenderer(failedCoreWebView);
                    }
                }
                catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                {
                    // Process-failure recovery races with shutdown and must never escape the UI dispatcher.
                    if (!_isClosing)
                    {
                        AppLogger.Warn(
                            $"WebView2 recovery was interrupted on '{_screenDeviceName}': {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(
                        $"WebView2 recovery failed on '{_screenDeviceName}'.",
                        ex);
                }
                finally
                {
                    _processRecoveryPending = false;
                }
            }));
        }

        private void RecreateWebViewControl()
        {
            StopRendererReadyTimeout();
            // The browser process is already disconnected; Dispose owns native event cleanup.
            ClearCoreWebViewEventState();
            _coreWebView = null;

            Microsoft.Web.WebView2.Wpf.WebView2 oldWebView = webView;
            webViewHost.Children.Remove(oldWebView);
            UnregisterName("webView");
            oldWebView.Dispose();

            var replacementWebView = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                Name = "webView",
                DefaultBackgroundColor = System.Drawing.Color.Transparent
            };

            RegisterName("webView", replacementWebView);
            webView = replacementWebView;
            webViewHost.Children.Add(replacementWebView);
            _ = InitWebView();
        }

        private void DetachCoreWebViewEvents()
        {
            CoreWebView2? coreWebView = _coreWebView;
            if (coreWebView != null)
            {
                if (_navigationCompletedHandler != null)
                {
                    try
                    {
                        coreWebView.NavigationCompleted -= _navigationCompletedHandler;
                    }
                    catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                    {
                    }
                }

                if (_navigationStartingHandler != null)
                {
                    try
                    {
                        coreWebView.NavigationStarting -= _navigationStartingHandler;
                    }
                    catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                    {
                    }
                }

                if (_processFailedHandler != null)
                {
                    try
                    {
                        coreWebView.ProcessFailed -= _processFailedHandler;
                    }
                    catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                    {
                    }
                }

                if (_webMessageReceivedHandler != null)
                {
                    try
                    {
                        coreWebView.WebMessageReceived -= _webMessageReceivedHandler;
                    }
                    catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
                    {
                    }
                }
            }

            ClearCoreWebViewEventState();
        }

        private void ClearCoreWebViewEventState()
        {
            _navigationStartingHandler = null;
            _navigationCompletedHandler = null;
            _processFailedHandler = null;
            _webMessageReceivedHandler = null;
            _currentNavigationId = null;
            _rendererGeneration = null;
            _unresponsiveTracker.Reset();
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
            if (!TryGetCoreWebView2(out CoreWebView2? coreWebView)) return;

            string script = BuildInputContextScript(inputMode);
            if (string.IsNullOrEmpty(script)) return;

            ExecuteScript(coreWebView, script);
        }

        private void ExecuteWithInputContext(string inputMode, string actionScript)
        {
            if (!TryGetCoreWebView2(out CoreWebView2? coreWebView)) return;

            string contextScript = BuildInputContextScript(inputMode);
            ExecuteScript(coreWebView, contextScript + actionScript);
        }

        // 统一 JS 脚本执行入口
        private void ExecuteScript(string script)
        {
            if (string.IsNullOrEmpty(script) || !TryGetCoreWebView2(out CoreWebView2? coreWebView))
            {
                return;
            }

            ExecuteScript(coreWebView, script);
        }

        private void ExecuteScript(CoreWebView2 coreWebView, string script)
        {
            if (string.IsNullOrEmpty(script))
            {
                return;
            }

            try
            {
                _ = coreWebView.ExecuteScriptAsync(script);
            }
            catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
            {
            }
        }

        private bool TryGetCoreWebView2([NotNullWhen(true)] out CoreWebView2? coreWebView)
        {
            coreWebView = null;
            if (_isClosing)
            {
                return false;
            }

            try
            {
                coreWebView = _coreWebView ?? webView?.CoreWebView2;
                return coreWebView != null;
            }
            catch (Exception ex) when (IsExpectedWebViewShutdownException(ex))
            {
                return false;
            }
        }

        private bool IsExpectedWebViewShutdownException(Exception ex)
        {
            return _isClosing ||
                   ex is ObjectDisposedException ||
                   (ex is InvalidOperationException &&
                    ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase));
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
            if (_usingLegacyRenderer && !ShouldDispatchLegacyMove()) return;
            if (!TryConvertScreenToOverlayPoint(x, y, out System.Windows.Point clientPoint)) return;
            string inputMode = touchLike ? InputModeTouch : InputModeMouse;
            string px = FormatCoordinate(clientPoint.X);
            string py = FormatCoordinate(clientPoint.Y);
            ExecuteWithInputContext(inputMode, $"if(window.externalMove) window.externalMove({px}, {py});");
        }

        private bool ShouldDispatchLegacyMove()
        {
            // 旧渲染器没有输入采样 API，仅在回退路径保留等价的宿主限频。
            if (_inputSamplingRate == 0)
            {
                return true;
            }

            long currentTicks = DateTime.UtcNow.Ticks;
            long intervalTicks = TimeSpan.FromSeconds(1.0 / _inputSamplingRate).Ticks;
            if (currentTicks - _lastLegacyMoveTicks < intervalTicks)
            {
                return false;
            }

            _lastLegacyMoveTicks = currentTicks;
            return true;
        }

        public void EmitUp(bool touchLike)
        {
            string inputMode = touchLike ? InputModeTouch : InputModeMouse;
            ExecuteWithInputContext(inputMode, "if(window.externalUp) window.externalUp();");
        }

        public void EmitCancel()
        {
            ExecuteScript(
                "if(window.externalCancel){window.externalCancel();}" +
                "else if(window.spark&&window.spark.clearEffects){window.spark.clearEffects();}" +
                "else if(window.externalUp){window.externalUp();}");
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            StopRendererReadyTimeout();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;

            if (_topmostTimer != null)
            {
                _topmostTimer.Stop();
                _topmostTimer = null;
            }

            StopRendererReadyTimeout();
            DetachCoreWebViewEvents();
            _coreWebView = null;

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _winEventDelegate = null;
            _hwnd = IntPtr.Zero;

            webView?.Dispose();
            base.OnClosed(e);
        }
    }
}
