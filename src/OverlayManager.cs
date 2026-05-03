using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Threading;
using Gma.System.MouseKeyHook;
using Microsoft.Win32;

namespace BASpark
{
    public sealed class OverlayManager : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProcDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint GA_ROOT = 2;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int FullscreenTolerance = 2;
        private static readonly long SuppressionCacheDurationTicks = TimeSpan.FromMilliseconds(250).Ticks;
        private const long ClickIntervalTicks = 300000;

        private readonly Dictionary<string, MainWindow> _overlays = new(StringComparer.OrdinalIgnoreCase);
        private IKeyboardMouseEvents? _globalHook;
        private MainWindow? _activePointerOverlay;
        private long _lastMoveTicks;
        private long _lastClickTicks;
        private long _moveIntervalTicks = 250000;
        private bool _isPrimaryPointerDown;
        private bool _isTouchLikeInput;
        private bool _isSuppressedByEnvironment;
        private long _suppressionCacheValidUntilTicks;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private bool _disposed;

        private bool _screenshotCompatCaptureWired;
        private bool _screenshotCaptureSessionActive;
        private bool _winKeyDown;
        private bool _shiftKeyDown;
        private IntPtr _foregroundWinEventHook = IntPtr.Zero;
        private WinEventProcDelegate? _foregroundWinEventDelegate;
        private System.Threading.Timer? _screenshotFailsafeTimer;
        private DispatcherTimer? _screenshotEndDebounceTimer;

        private delegate void WinEventProcDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        private static readonly HashSet<string> ScreenshotForegroundHostExe = new(StringComparer.OrdinalIgnoreCase)
        {
            "SnippingTool.exe",
            "ScreenSketch.exe",
            "ScreenClippingHost.exe",
            "ms-screenclip.exe",
        };

        public void Start()
        {
            RebuildWindows(forceRebuild: true);
            SetupGlobalHooks();
            SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
        }

        public void UpdateColor(string color) => ForEachOverlay(w => w.UpdateColor(color));
        public void UpdateEffectSettings(double scale, double opacity, double speed) => ForEachOverlay(w => w.UpdateEffectSettings(scale, opacity, speed));
        public void UpdateTrailRefreshRate(int hz)
        {
            hz = Math.Clamp(hz, 10, 240);
            _moveIntervalTicks = TimeSpan.FromSeconds(1.0 / hz).Ticks;
            ForEachOverlay(w => w.UpdateTrailRefreshRate(hz));
        }
        public void UpdateTouchMode(bool enabled) => ForEachOverlay(w => w.UpdateTouchMode(enabled));
        public void UpdateScreenshotCompatibilityMode(bool enabled)
        {
            ForEachOverlay(w => w.UpdateScreenshotCompatibilityMode(enabled));
            SyncScreenshotCompatCaptureSurfaces();
        }
        public bool IsEffectSuppressedByEnvironment() => ShouldSuppressEffects();
        public void RefreshEnvironmentFilterState()
        {
            _suppressionCacheValidUntilTicks = 0;
            _lastForegroundWindow = IntPtr.Zero;
            ShouldSuppressEffects(forceRefresh: true);
        }
        public void RefreshScreenSelection()
        {
            RebuildWindows(forceRebuild: false);
        }

        private void SetupGlobalHooks()
        {
            TeardownScreenshotCompatCaptureSurfaces();
            _globalHook?.Dispose();
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseDownExt += OnMouseDownExt;
            _globalHook.MouseMoveExt += OnMouseMoveExt;
            _globalHook.MouseUpExt += OnMouseUpExt;
            SyncScreenshotCompatCaptureSurfaces();
        }

        private void SyncScreenshotCompatCaptureSurfaces()
        {
            if (!ConfigManager.ScreenshotCompatibilityMode)
            {
                if (_screenshotCompatCaptureWired)
                {
                    TeardownScreenshotCompatCaptureSurfaces();
                }

                EndScreenshotCaptureSession();
                return;
            }

            if (_globalHook == null)
            {
                return;
            }

            if (!_screenshotCompatCaptureWired)
            {
                _globalHook.KeyDown += OnScreenshotCompatKeyDown;
                _globalHook.KeyUp += OnScreenshotCompatKeyUp;
                InstallForegroundScreenshotHook();
                _screenshotCompatCaptureWired = true;
            }
        }

        private void TeardownScreenshotCompatCaptureSurfaces()
        {
            if (!_screenshotCompatCaptureWired)
            {
                return;
            }

            if (_globalHook != null)
            {
                _globalHook.KeyDown -= OnScreenshotCompatKeyDown;
                _globalHook.KeyUp -= OnScreenshotCompatKeyUp;
            }
            UninstallForegroundScreenshotHook();
            StopScreenshotEndDebounce();
            CancelScreenshotFailsafeTimer();
            _screenshotCompatCaptureWired = false;
        }

        private void InstallForegroundScreenshotHook()
        {
            if (_foregroundWinEventHook != IntPtr.Zero)
            {
                return;
            }

            _foregroundWinEventDelegate ??= ForegroundWinEventProc;
            _foregroundWinEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _foregroundWinEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }

        private void UninstallForegroundScreenshotHook()
        {
            if (_foregroundWinEventHook == IntPtr.Zero)
            {
                return;
            }

            UnhookWinEvent(_foregroundWinEventHook);
            _foregroundWinEventHook = IntPtr.Zero;
        }

        private void ForegroundWinEventProc(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            _ = hWinEventHook;
            _ = hwnd;
            _ = idObject;
            _ = idChild;
            _ = dwEventThread;
            _ = dwmsEventTime;
            if (eventType != EVENT_SYSTEM_FOREGROUND)
            {
                return;
            }

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(EvaluateScreenshotForegroundForCapture));
        }

        private void EvaluateScreenshotForegroundForCapture()
        {
            if (_disposed || !ConfigManager.ScreenshotCompatibilityMode)
            {
                return;
            }

            if (IsForegroundScreenshotHost())
            {
                StopScreenshotEndDebounce();
                BeginScreenshotCaptureSession();
                return;
            }

            if (_screenshotCaptureSessionActive)
            {
                ScheduleEndScreenshotCaptureSessionDebounced();
            }
        }

        private bool IsForegroundScreenshotHost()
        {
            return IsKnownScreenshotHostWindow(GetForegroundWindow());
        }

        private bool IsKnownScreenshotHostWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero)
            {
                root = hwnd;
            }

            if (IsOverlayWindow(root))
            {
                return false;
            }

            if (!IsWindow(root) || !IsWindowVisible(root))
            {
                return false;
            }

            GetWindowThreadProcessId(root, out uint pid);
            if (pid == 0 || pid == (uint)Environment.ProcessId)
            {
                return false;
            }

            string exe = GetProcessExecutableName(pid);
            return ScreenshotForegroundHostExe.Contains(exe);
        }

        private void OnScreenshotCompatKeyDown(object? sender, KeyEventArgs e)
        {
            if (_disposed || !ConfigManager.ScreenshotCompatibilityMode)
            {
                return;
            }

            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                _winKeyDown = true;
                return;
            }

            if (e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey || e.KeyCode == Keys.ShiftKey)
            {
                _shiftKeyDown = true;
                return;
            }

            if (e.KeyCode == Keys.S && _winKeyDown && (e.Shift || _shiftKeyDown))
            {
                BeginScreenshotCaptureSession();
                return;
            }

            if (e.KeyCode == Keys.A && e.Control && e.Alt)
            {
                BeginScreenshotCaptureSession();
                return;
            }

            if (e.KeyCode == Keys.Escape && _screenshotCaptureSessionActive)
            {
                ScheduleEndScreenshotCaptureSessionDebounced();
            }
        }

        private void OnScreenshotCompatKeyUp(object? sender, KeyEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                _winKeyDown = false;
            }

            if (e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey || e.KeyCode == Keys.ShiftKey)
            {
                _shiftKeyDown = false;
            }
        }

        private void BeginScreenshotCaptureSession()
        {
            if (_disposed || !ConfigManager.ScreenshotCompatibilityMode)
            {
                return;
            }

            if (_screenshotCaptureSessionActive)
            {
                ArmScreenshotFailsafeTimer();
                return;
            }

            ReleasePointerState();
            _screenshotCaptureSessionActive = true;
            ForEachOverlay(w => w.SetHiddenForExternalScreenshotCapture(true));
            ArmScreenshotFailsafeTimer();
        }

        private void EndScreenshotCaptureSession()
        {
            if (!_screenshotCaptureSessionActive)
            {
                return;
            }

            _screenshotCaptureSessionActive = false;
            StopScreenshotEndDebounce();
            CancelScreenshotFailsafeTimer();
            ForEachOverlay(w => w.SetHiddenForExternalScreenshotCapture(false));
        }

        private void ScheduleEndScreenshotCaptureSessionDebounced()
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(ScheduleEndOnDispatcherThread));
        }

        private void ScheduleEndOnDispatcherThread()
        {
            if (_disposed)
            {
                return;
            }

            StopScreenshotEndDebounce();
            _screenshotEndDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(360) };
            _screenshotEndDebounceTimer.Tick += OnScreenshotEndDebounceTick;
            _screenshotEndDebounceTimer.Start();
        }

        private void OnScreenshotEndDebounceTick(object? sender, EventArgs e)
        {
            StopScreenshotEndDebounce();
            if (_disposed || !ConfigManager.ScreenshotCompatibilityMode)
            {
                return;
            }

            if (!IsForegroundScreenshotHost())
            {
                EndScreenshotCaptureSession();
            }
        }

        private void StopScreenshotEndDebounce()
        {
            if (_screenshotEndDebounceTimer != null)
            {
                _screenshotEndDebounceTimer.Stop();
                _screenshotEndDebounceTimer.Tick -= OnScreenshotEndDebounceTick;
                _screenshotEndDebounceTimer = null;
            }
        }

        private void ArmScreenshotFailsafeTimer()
        {
            CancelScreenshotFailsafeTimer();
            _screenshotFailsafeTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        EndScreenshotCaptureSession();
                    }));
                }
                catch
                {
                }
            }, null, 90000, Timeout.Infinite);
        }

        private void CancelScreenshotFailsafeTimer()
        {
            _screenshotFailsafeTimer?.Dispose();
            _screenshotFailsafeTimer = null;
        }

        private void OnMouseDownExt(object? sender, MouseEventExtArgs e)
        {
            if (!CanRenderEffects()) return;

            bool isLeft = e.Button == MouseButtons.Left;
            bool isRight = e.Button == MouseButtons.Right;
            bool isMiddle = e.Button == MouseButtons.Middle;
            bool shouldPrimaryTrigger = ConfigManager.ClickTriggerType switch
            {
                1 => isRight,
                2 => isLeft || isRight,
                _ => isLeft
            };
            // 中键触发
            bool shouldTrigger = shouldPrimaryTrigger || (ConfigManager.EnableMiddleClickTrigger && isMiddle);
            if (!shouldTrigger) return;

            if (!TryGetPhysicalCursorPosition(out int cursorX, out int cursorY))
            {
                cursorX = e.X;
                cursorY = e.Y;
            }

            MainWindow? target = ResolveTargetOverlay(cursorX, cursorY);
            if (target == null) return;

            _isPrimaryPointerDown = true;
            _isTouchLikeInput = !CursorIsVisible();
            _activePointerOverlay = target;

            long currentTicks = DateTime.Now.Ticks;
            if (currentTicks - _lastClickTicks < ClickIntervalTicks) return;
            _lastClickTicks = currentTicks;

            ConfigManager.TotalClicks++;
            target.EmitDown(cursorX, cursorY);
        }

        private void OnMouseMoveExt(object? sender, MouseEventExtArgs e)
        {
            if (!CanRenderEffects()) return;

            bool cursorVisible = CursorIsVisible();
            if (!cursorVisible && !_isPrimaryPointerDown) return;

            long currentTicks = DateTime.Now.Ticks;
            if (currentTicks - _lastMoveTicks < _moveIntervalTicks) return;
            _lastMoveTicks = currentTicks;

            if (!TryGetPhysicalCursorPosition(out int cursorX, out int cursorY))
            {
                cursorX = e.X;
                cursorY = e.Y;
            }

            var target = _activePointerOverlay ?? ResolveTargetOverlay(cursorX, cursorY);
            target?.EmitMove(cursorX, cursorY, _isTouchLikeInput || !cursorVisible);
        }

        private void OnMouseUpExt(object? sender, MouseEventExtArgs e)
        {
            _ = e;
            if (!_isPrimaryPointerDown)
            {
                _isTouchLikeInput = false;
                return;
            }

            _activePointerOverlay?.EmitUp(_isTouchLikeInput);
            _isPrimaryPointerDown = false;
            _isTouchLikeInput = false;
            _activePointerOverlay = null;
        }

        private bool CanRenderEffects()
        {
            if (!ConfigManager.IsEffectEnabled || _overlays.Count == 0)
            {
                ReleasePointerState();
                return false;
            }

            if (ShouldSuppressEffects())
            {
                ReleasePointerState();
                return false;
            }

            if (!ConfigManager.IsTouchscreenMode && !CursorIsVisible())
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
                _activePointerOverlay = null;
                return;
            }

            _activePointerOverlay?.EmitUp(_isTouchLikeInput);
            _isPrimaryPointerDown = false;
            _isTouchLikeInput = false;
            _activePointerOverlay = null;
        }

        private MainWindow? ResolveTargetOverlay(int x, int y)
        {
            MainWindow? direct = _overlays.Values.FirstOrDefault(w => w.ContainsScreenPoint(x, y));
            if (direct != null) return direct;

            Screen nearest = Screen.FromPoint(new Point(x, y));
            if (_overlays.TryGetValue(nearest.DeviceName, out MainWindow? byDevice))
            {
                return byDevice;
            }

            return _overlays.Values.FirstOrDefault(w => w.ContainsScreenPoint(nearest.Bounds.Left, nearest.Bounds.Top));
        }

        private void RebuildWindows(bool forceRebuild)
        {
            var screenInfos = Screen.AllScreens
                .Select(screen => new { Screen = screen, Identity = ScreenIdentity.FromScreen(screen) })
                .ToList();
            var enabledIds = ConfigManager.ResolveEnabledScreenDeviceNames(screenInfos.Select(item => item.Identity));

            var targetScreens = screenInfos
                .Where(item => enabledIds.Contains(item.Screen.DeviceName))
                .Select(item => item.Screen)
                .ToDictionary(screen => screen.DeviceName, screen => screen, StringComparer.OrdinalIgnoreCase);

            if (forceRebuild)
            {
                CloseWindows();
            }

            foreach (string staleKey in _overlays.Keys.Except(targetScreens.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            {
                CloseOverlay(staleKey);
            }

            foreach (var pair in targetScreens)
            {
                if (_overlays.ContainsKey(pair.Key))
                {
                    continue;
                }

                var win = new MainWindow(pair.Value);
                _overlays[pair.Key] = win;
                win.Show();
            }
        }

        private void CloseWindows()
        {
            foreach (string deviceName in _overlays.Keys.ToList())
            {
                CloseOverlay(deviceName);
            }
        }

        private void CloseOverlay(string deviceName)
        {
            if (!_overlays.TryGetValue(deviceName, out MainWindow? overlay))
            {
                return;
            }

            try { overlay.Close(); } catch (Exception ex) { Debug.WriteLine(ex.Message); }
            _overlays.Remove(deviceName);
        }

        private bool ShouldSuppressEffects(bool forceRefresh = false)
        {
            if (!ConfigManager.EnableEnvironmentFilter)
            {
                _isSuppressedByEnvironment = false;
                _suppressionCacheValidUntilTicks = 0;
                return false;
            }

            GetCursorPos(out POINT pt);
            IntPtr cursorHwnd = WindowFromPoint(pt);
            IntPtr targetWindow = GetAncestor(cursorHwnd, GA_ROOT);

            if (targetWindow == IntPtr.Zero || IsOverlayWindow(targetWindow))
            {
                targetWindow = GetForegroundWindow();
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            if (targetWindow != _lastForegroundWindow)
            {
                forceRefresh = true;
                _lastForegroundWindow = targetWindow;
            }
            if (!forceRefresh && nowTicks < _suppressionCacheValidUntilTicks)
            {
                return _isSuppressedByEnvironment;
            }

            string className = GetWindowClassName(targetWindow);
            if (string.IsNullOrEmpty(className))
            {
                className = GetWindowClassName(GetForegroundWindow());
            }

            bool isDesktop = string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(className, "SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase);
            if (isDesktop)
            {
                UpdateSuppressionState(nowTicks, !ConfigManager.ShowEffectOnDesktop);
                return _isSuppressedByEnvironment;
            }

            if (!TryGetForegroundProcessName(targetWindow, out string processName))
            {
                if (!TryGetForegroundProcessName(GetForegroundWindow(), out processName))
                {
                    UpdateSuppressionState(nowTicks, false);
                    return false;
                }
            }

            IntPtr actualForeground = GetForegroundWindow();
            if (ConfigManager.HideInFullscreen && IsEffectiveFullscreenWindow(actualForeground))
            {
                UpdateSuppressionState(nowTicks, true);
                return true;
            }

            bool isSuppressedByProcessFilter = IsSuppressedByProcessFilter(processName);
            UpdateSuppressionState(nowTicks, isSuppressedByProcessFilter);
            return _isSuppressedByEnvironment;
        }

        private bool IsOverlayWindow(IntPtr hwnd)
        {
            return _overlays.Values.Any(o => o.Handle == hwnd);
        }

        private static bool IsSuppressedByProcessFilter(string processName)
        {
            var profile = ConfigManager.GetActiveProfile();
            if (profile == null || profile.Mode == ProcessFilterModeOption.Disabled)
            {
                return false;
            }

            bool isListed = profile.Processes.Contains(processName, StringComparer.OrdinalIgnoreCase);
            return profile.Mode switch
            {
                ProcessFilterModeOption.Blacklist => isListed,
                ProcessFilterModeOption.Whitelist => !isListed,
                _ => false
            };
        }

        private static void UpdateSuppressionState(long nowTicks, bool isSuppressed, ref bool suppressed, ref long cacheUntil)
        {
            suppressed = isSuppressed;
            cacheUntil = nowTicks + SuppressionCacheDurationTicks;
        }

        private void UpdateSuppressionState(long nowTicks, bool isSuppressed)
            => UpdateSuppressionState(nowTicks, isSuppressed, ref _isSuppressedByEnvironment, ref _suppressionCacheValidUntilTicks);

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
            if (hwnd == IntPtr.Zero || IsOverlayWindow(hwnd))
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

        private static string GetWindowClassName(IntPtr hwnd)
        {
            var classNameBuilder = new StringBuilder(256);
            return GetClassName(hwnd, classNameBuilder, classNameBuilder.Capacity) > 0
                ? classNameBuilder.ToString()
                : string.Empty;
        }

        private static string GetProcessExecutableName(uint processId)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProc == IntPtr.Zero) return string.Empty;

            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (!QueryFullProcessImageName(hProc, 0, sb, ref size))
            {
                CloseHandle(hProc);
                return string.Empty;
            }

            CloseHandle(hProc);
            string fileName = System.IO.Path.GetFileName(sb.ToString());
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".exe";
            }
            return fileName.ToLowerInvariant();
        }

        private static bool IsEffectiveFullscreenWindow(IntPtr hwnd)
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

            MONITORINFO monitorInfo = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            return Math.Abs(windowRect.Left - monitorInfo.rcMonitor.Left) <= FullscreenTolerance &&
                   Math.Abs(windowRect.Top - monitorInfo.rcMonitor.Top) <= FullscreenTolerance &&
                   Math.Abs(windowRect.Right - monitorInfo.rcMonitor.Right) <= FullscreenTolerance &&
                   Math.Abs(windowRect.Bottom - monitorInfo.rcMonitor.Bottom) <= FullscreenTolerance;
        }

        private static bool CursorIsVisible()
        {
            CURSORINFO pci = new() { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
            return GetCursorInfo(out pci) && (pci.flags & 0x00000001) != 0;
        }

        private static bool TryGetPhysicalCursorPosition(out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!GetCursorPos(out POINT pt))
            {
                return false;
            }

            x = pt.x;
            y = pt.y;
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        private void HandleDisplaySettingsChanged(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => RebuildWindows(forceRebuild: true)));
        }

        private void ForEachOverlay(Action<MainWindow> action)
        {
            foreach (var overlay in _overlays.Values.ToList())
            {
                action(overlay);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
            TeardownScreenshotCompatCaptureSurfaces();
            EndScreenshotCaptureSession();
            if (_globalHook != null)
            {
                _globalHook.MouseDownExt -= OnMouseDownExt;
                _globalHook.MouseMoveExt -= OnMouseMoveExt;
                _globalHook.MouseUpExt -= OnMouseUpExt;
                _globalHook.Dispose();
                _globalHook = null;
            }

            CloseWindows();
        }
    }
}
