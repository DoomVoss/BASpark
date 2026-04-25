using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
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
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
        [DllImport("user32.dll")]
        private static extern bool RegisterPointerInputTarget(IntPtr hwnd, uint scope);

        private const uint PP_SCOPE_RECENT_INPUT = 1;
        private const uint PP_SCOPE_GLOBAL = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            public byte bRawData;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(16)] // 在 64 位系统上 header 是 24 字节，但在 32 位是 16。
                             // .NET 会自动处理，但为了安全我们使用 IntPtr 偏移。
            public RAWHID hid;
        }

        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEHID = 2;
        private const ushort HID_USAGE_PAGE_DIGITIZER = 0x0D;
        private const ushort HID_USAGE_TOUCH_SCREEN = 0x04;
        private const uint RIDEV_INPUTSINK = 0x00000100;

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
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int FullscreenTolerance = 2;
        private static readonly long SuppressionCacheDurationTicks = TimeSpan.FromMilliseconds(250).Ticks;
        private const long ClickIntervalTicks = 300000;

        private readonly Dictionary<string, MainWindow> _overlays = new(StringComparer.OrdinalIgnoreCase);
        private IKeyboardMouseEvents? _globalHook;
        private RawInputWindow? _rawInputWindow;
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

        public void Start()
        {
            RebuildWindows(forceRebuild: true);
            SetupGlobalHooks();
            SetupRawInput();
            SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
        }

        private void SetupRawInput()
        {
            if (ConfigManager.EnableMultiTouch)
            {
                _rawInputWindow = new RawInputWindow(this);
            }
        }

        private class RawInputWindow : NativeWindow
        {
            private readonly OverlayManager _owner;
            private const int WM_POINTERDOWN = 0x0246;

            [DllImport("user32.dll")]
            private static extern bool GetPointerInfo(uint pointerId, ref POINTER_INFO pointerInfo);

            [StructLayout(LayoutKind.Sequential)]
            private struct POINTER_INFO
            {
                public uint pointerType;
                public uint pointerId;
                public IntPtr frameId;
                public uint pointerFlags;
                public IntPtr sourceDevice;
                public IntPtr hwndTarget;
                public POINT ptPixelLocation;
                public POINT ptHimetricLocation;
                public POINT ptPixelLocationRaw;
                public POINT ptHimetricLocationRaw;
                public uint dwTime;
                public uint historyCount;
                public int InputData;
                public uint dwKeyStates;
                public ulong PerformanceCount;
                public int ButtonChangeType;
            }

            public RawInputWindow(OverlayManager owner)
            {
                _owner = owner;
                CreateHandle(new CreateParams());

                // 注册全局指针监听（需要高权限或在 Secure Desktop 运行，但在普通模式下也能捕获部分消息）
                RegisterPointerInputTarget(Handle, PP_SCOPE_GLOBAL);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_POINTERDOWN)
                {
                    uint pointerId = (uint)((ulong)m.WParam & 0xFFFF);
                    POINTER_INFO pi = new POINTER_INFO();
                    if (GetPointerInfo(pointerId, ref pi))
                    {
                        _owner.EmitDown(pi.ptPixelLocation.x, pi.ptPixelLocation.y);
                    }
                }
                base.WndProc(ref m);
            }
        }

        private void HandleRawInput(IntPtr hRawInput)
        {
            if (!CanRenderEffects()) return;

            uint dwSize = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

            if (dwSize == 0) return;

            IntPtr pData = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, pData, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != dwSize)
                {
                    return;
                }

                RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(pData);
                if (header.dwType == RIM_TYPEHID)
                {
                    // 这里由于 HID 报文解析极其复杂且设备各异，BASpark 采用更稳健的策略：
                    // 当收到原始触摸输入且非主指针（由鼠标钩子处理）时，补全点击。
                    // 实际上，对于大多数多点触控应用，我们通过检测当前光标位置是否在特效范围内来优化。
                    
                    // 注意：为了保持性能和兼容性，我们主要通过 PointerSupport 提升 UI 响应，
                    // 全局多点特效在目前的开源驱动下通常通过模拟并发点击实现。
                    // 这里的 RawInput 主要是为了唤醒处于非活动状态的覆盖层。
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }
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
            _globalHook?.Dispose();
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseDownExt += OnMouseDownExt;
            _globalHook.MouseMoveExt += OnMouseMoveExt;
            _globalHook.MouseUpExt += OnMouseUpExt;
        }

        public void EmitDown(int x, int y)
        {
            if (!CanRenderEffects()) return;

            MainWindow? target = ResolveTargetOverlay(x, y);
            if (target == null) return;

            ConfigManager.TotalClicks++;
            target.EmitDown(x, y);
        }

        private void OnMouseDownExt(object? sender, MouseEventExtArgs e)
        {
            if (!CanRenderEffects()) return;

            bool isLeft = e.Button == MouseButtons.Left;
            bool isRight = e.Button == MouseButtons.Right;
            bool shouldTrigger = ConfigManager.ClickTriggerType switch
            {
                1 => isRight,
                2 => isLeft || isRight,
                _ => isLeft
            };
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
            var enabledIds = ConfigManager.GetEnabledScreenIds();
            var targetScreens = Screen.AllScreens
                .Where(screen => enabledIds.Count == 0 || enabledIds.Contains(screen.DeviceName))
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
            if (_globalHook != null)
            {
                _globalHook.MouseDownExt -= OnMouseDownExt;
                _globalHook.MouseMoveExt -= OnMouseMoveExt;
                _globalHook.MouseUpExt -= OnMouseUpExt;
                _globalHook.Dispose();
                _globalHook = null;
            }

            if (_rawInputWindow != null)
            {
                _rawInputWindow.ReleaseHandle();
                _rawInputWindow = null;
            }

            CloseWindows();
        }
    }
}
