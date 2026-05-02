using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BASpark
{
    /// <summary>
    /// 使用 Raw Input (WM_INPUT + RIDEV_INPUTSINK) 全局捕获多点触控输入。
    /// 不需要 UIAccess 权限，可在普通用户模式下工作。
    /// </summary>
    internal sealed class TouchInputCapture : IDisposable
    {
        public event Action<uint, int, int>? TouchDown;   // contactId, screenX, screenY
        public event Action<uint, int, int>? TouchMove;   // contactId, screenX, screenY
        public event Action<uint>? TouchUp;               // contactId

        private RawInputReceiver? _receiver;
        private bool _disposed;

        public void Start()
        {
            _receiver = new RawInputReceiver(this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _receiver?.ReleaseHandle();
            _receiver = null;
        }

        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll")]
        private static extern int HidP_GetValueCaps(ushort ReportType, [Out] HIDP_VALUE_CAPS[] ValueCaps, ref ushort ValueCapsLength, IntPtr PreparsedData);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsageValue(ushort ReportType, ushort UsagePage, ushort LinkCollection, ushort Usage, out uint UsageValue, IntPtr PreparsedData, byte[] Report, uint ReportLength);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsages(ushort ReportType, ushort UsagePage, ushort LinkCollection, [Out] ushort[] UsageList, ref uint UsageLength, IntPtr PreparsedData, byte[] Report, uint ReportLength);

        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEHID = 2;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        private const int WM_INPUT = 0x00FF;
        private const ushort HidP_Input = 0;

        // HID Usage 常量
        private const ushort USAGE_PAGE_DIGITIZER = 0x0D;
        private const ushort USAGE_PAGE_GENERIC = 0x01;
        private const ushort USAGE_TOUCH_SCREEN = 0x04;
        private const ushort USAGE_CONTACT_ID = 0x51;
        private const ushort USAGE_TIP_SWITCH = 0x42;
        private const ushort USAGE_X = 0x30;
        private const ushort USAGE_Y = 0x31;

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
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)] public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)] public bool IsRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsAbsolute;
            [MarshalAs(UnmanagedType.U1)] public bool HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ushort[] Reserved2;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            // Union: Range / NotRange — 只取 NotRange.Usage
            public ushort UsageMin_or_Usage;
            public ushort UsageMax_or_Reserved;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        #endregion

        #region Device Info Cache

        private class TouchDeviceInfo
        {
            public IntPtr PreparsedData;
            public int LogicalMaxX;
            public int LogicalMaxY;
            public ushort LinkCollectionForContacts; // link collection containing X/Y/ContactID
            public bool IsValid;
        }

        private readonly Dictionary<IntPtr, TouchDeviceInfo> _deviceCache = new();

        private TouchDeviceInfo? GetOrCreateDeviceInfo(IntPtr hDevice)
        {
            if (_deviceCache.TryGetValue(hDevice, out var cached))
                return cached.IsValid ? cached : null;

            var info = BuildDeviceInfo(hDevice);
            _deviceCache[hDevice] = info;
            return info.IsValid ? info : null;
        }

        private TouchDeviceInfo BuildDeviceInfo(IntPtr hDevice)
        {
            var result = new TouchDeviceInfo();
            uint size = 0;

            // 获取 PreparsedData 大小
            GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
            if (size == 0) return result;

            IntPtr preparsedData = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, preparsedData, ref size) == unchecked((uint)-1))
                    return result;

                if (HidP_GetCaps(preparsedData, out HIDP_CAPS caps) != 0x00110000) // HIDP_STATUS_SUCCESS
                    return result;

                // 获取 Value Caps 以找出 X/Y 的逻辑范围和 LinkCollection
                ushort numValueCaps = caps.NumberInputValueCaps;
                if (numValueCaps == 0) return result;

                var valueCaps = new HIDP_VALUE_CAPS[numValueCaps];
                if (HidP_GetValueCaps(HidP_Input, valueCaps, ref numValueCaps, preparsedData) != 0x00110000)
                    return result;

                int logMaxX = 0, logMaxY = 0;
                ushort linkCol = 0;
                bool foundX = false, foundY = false;

                foreach (var vc in valueCaps)
                {
                    ushort usage = vc.IsRange ? vc.UsageMin_or_Usage : vc.UsageMin_or_Usage;

                    if (vc.UsagePage == USAGE_PAGE_GENERIC && usage == USAGE_X)
                    {
                        logMaxX = vc.LogicalMax;
                        linkCol = vc.LinkCollection;
                        foundX = true;
                    }
                    else if (vc.UsagePage == USAGE_PAGE_GENERIC && usage == USAGE_Y)
                    {
                        logMaxY = vc.LogicalMax;
                        foundY = true;
                    }
                }

                if (!foundX || !foundY || logMaxX <= 0 || logMaxY <= 0)
                    return result;

                // 不释放 preparsedData，缓存它（后续解析需要）
                result.PreparsedData = preparsedData;
                result.LogicalMaxX = logMaxX;
                result.LogicalMaxY = logMaxY;
                result.LinkCollectionForContacts = linkCol;
                result.IsValid = true;

                preparsedData = IntPtr.Zero; // 防止 finally 释放
                return result;
            }
            finally
            {
                if (preparsedData != IntPtr.Zero)
                    Marshal.FreeHGlobal(preparsedData);
            }
        }

        #endregion

        #region Contact State Tracking

        private readonly Dictionary<uint, bool> _contactStates = new(); // contactId -> wasDown

        #endregion

        #region Raw Input Processing

        private void ProcessRawInput(IntPtr hRawInput)
        {
            uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
            uint size = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0) return;

            IntPtr pData = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, pData, ref size, headerSize) == unchecked((uint)-1))
                    return;

                var header = Marshal.PtrToStructure<RAWINPUTHEADER>(pData);
                if (header.dwType != RIM_TYPEHID)
                    return;

                var deviceInfo = GetOrCreateDeviceInfo(header.hDevice);
                if (deviceInfo == null) return;

                // 读取 HID 报文：header 之后是 { dwSizeHid, dwCount, bRawData[] }
                int hidOffset = (int)headerSize;
                // 对齐到 IntPtr 边界
                if (IntPtr.Size == 8)
                    hidOffset = (hidOffset + 7) & ~7;

                uint dwSizeHid = (uint)Marshal.ReadInt32(pData, hidOffset);
                uint dwCount = (uint)Marshal.ReadInt32(pData, hidOffset + 4);
                int rawDataOffset = hidOffset + 8;

                if (dwSizeHid == 0 || dwCount == 0) return;

                // 每个 HID report 处理一个触控点
                for (uint i = 0; i < dwCount; i++)
                {
                    byte[] report = new byte[dwSizeHid];
                    Marshal.Copy(pData + rawDataOffset + (int)(i * dwSizeHid), report, 0, (int)dwSizeHid);

                    ParseSingleContact(report, dwSizeHid, deviceInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TouchInputCapture] ProcessRawInput error: {ex.Message}");
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }
        }

        private void ParseSingleContact(byte[] report, uint reportLength, TouchDeviceInfo device)
        {
            // 检查 TipSwitch（是否触摸中）
            ushort[] usages = new ushort[16];
            uint usageCount = (uint)usages.Length;
            bool isTouching = false;

            int statusBtn = HidP_GetUsages(HidP_Input, USAGE_PAGE_DIGITIZER, device.LinkCollectionForContacts,
                usages, ref usageCount, device.PreparsedData, report, reportLength);

            if (statusBtn == 0x00110000) // HIDP_STATUS_SUCCESS
            {
                for (int j = 0; j < usageCount; j++)
                {
                    if (usages[j] == USAGE_TIP_SWITCH)
                    {
                        isTouching = true;
                        break;
                    }
                }
            }

            // 获取 ContactID
            int statusCid = HidP_GetUsageValue(HidP_Input, USAGE_PAGE_DIGITIZER, device.LinkCollectionForContacts,
                USAGE_CONTACT_ID, out uint contactId, device.PreparsedData, report, reportLength);
            if (statusCid != 0x00110000) return;

            // 获取 X, Y（逻辑坐标）
            int statusX = HidP_GetUsageValue(HidP_Input, USAGE_PAGE_GENERIC, device.LinkCollectionForContacts,
                USAGE_X, out uint logX, device.PreparsedData, report, reportLength);
            int statusY = HidP_GetUsageValue(HidP_Input, USAGE_PAGE_GENERIC, device.LinkCollectionForContacts,
                USAGE_Y, out uint logY, device.PreparsedData, report, reportLength);

            if (statusX != 0x00110000 || statusY != 0x00110000) return;

            // 逻辑坐标 → 屏幕像素坐标
            // 使用虚拟屏幕（所有显示器的合并区域）
            int virtualLeft = SystemInformation.VirtualScreen.Left;
            int virtualTop = SystemInformation.VirtualScreen.Top;
            int virtualWidth = SystemInformation.VirtualScreen.Width;
            int virtualHeight = SystemInformation.VirtualScreen.Height;

            int screenX = virtualLeft + (int)((long)logX * virtualWidth / device.LogicalMaxX);
            int screenY = virtualTop + (int)((long)logY * virtualHeight / device.LogicalMaxY);

            // 状态跟踪并发射事件
            bool wasDown = _contactStates.TryGetValue(contactId, out bool prev) && prev;

            if (isTouching)
            {
                if (!wasDown)
                {
                    _contactStates[contactId] = true;
                    TouchDown?.Invoke(contactId, screenX, screenY);
                }
                else
                {
                    TouchMove?.Invoke(contactId, screenX, screenY);
                }
            }
            else
            {
                if (wasDown)
                {
                    _contactStates[contactId] = false;
                    TouchUp?.Invoke(contactId);
                }
            }
        }

        #endregion

        #region NativeWindow Receiver

        private class RawInputReceiver : NativeWindow
        {
            private readonly TouchInputCapture _owner;

            public RawInputReceiver(TouchInputCapture owner)
            {
                _owner = owner;
                CreateHandle(new CreateParams());

                // 注册接收触摸数字化器的原始输入（RIDEV_INPUTSINK 允许后台接收）
                var rid = new RAWINPUTDEVICE[]
                {
                    new RAWINPUTDEVICE
                    {
                        usUsagePage = USAGE_PAGE_DIGITIZER,
                        usUsage = USAGE_TOUCH_SCREEN,
                        dwFlags = RIDEV_INPUTSINK,
                        hwndTarget = Handle
                    }
                };

                bool ok = RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
                Debug.WriteLine($"[TouchInputCapture] RegisterRawInputDevices: {ok}");
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_INPUT)
                {
                    _owner.ProcessRawInput(m.LParam);
                }
                base.WndProc(ref m);
            }
        }

        #endregion
    }
}
