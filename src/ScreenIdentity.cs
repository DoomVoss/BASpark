using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BASpark
{
    public sealed class ScreenIdentityInfo
    {
        public string DeviceName { get; init; } = string.Empty;
        public string IdentityKey { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    public static class ScreenIdentity
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        public static ScreenIdentityInfo FromScreen(Screen screen)
        {
            string displayName = screen.DeviceName;
            string identityKey = screen.DeviceName;

            var monitorDevice = CreateDisplayDevice();
            if (EnumDisplayDevices(screen.DeviceName, 0, ref monitorDevice, 0))
            {
                // 多屏记忆使用硬件/虚拟显示器 ID，避免 DISPLAY1/2 变化后丢失设置
                if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceID))
                {
                    identityKey = monitorDevice.DeviceID.Trim();
                }

                // 设置页优先解析 EDID 名称
                string edidName = TryReadEdidDisplayName(monitorDevice.DeviceID);
                if (!string.IsNullOrWhiteSpace(edidName))
                {
                    displayName = edidName;
                }
                else if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceString))
                {
                    displayName = monitorDevice.DeviceString.Trim();
                }
            }

            return new ScreenIdentityInfo
            {
                DeviceName = screen.DeviceName,
                IdentityKey = identityKey,
                DisplayName = displayName
            };
        }

        private static DISPLAY_DEVICE CreateDisplayDevice()
        {
            return new DISPLAY_DEVICE
            {
                cb = Marshal.SizeOf<DISPLAY_DEVICE>(),
                DeviceName = string.Empty,
                DeviceString = string.Empty,
                DeviceID = string.Empty,
                DeviceKey = string.Empty
            };
        }

        private static string TryReadEdidDisplayName(string deviceId)
        {
            string hardwareId = ExtractDisplayHardwareId(deviceId);
            if (string.IsNullOrWhiteSpace(hardwareId))
            {
                return string.Empty;
            }

            try
            {
                using RegistryKey? displayKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{hardwareId}");
                if (displayKey == null)
                {
                    return string.Empty;
                }

                foreach (string instanceName in displayKey.GetSubKeyNames())
                {
                    using RegistryKey? parametersKey = displayKey.OpenSubKey($@"{instanceName}\Device Parameters");
                    if (parametersKey?.GetValue("EDID") is byte[] edid)
                    {
                        string displayName = ParseEdidDisplayName(edid);
                        if (!string.IsNullOrWhiteSpace(displayName))
                        {
                            return displayName;
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string ExtractDisplayHardwareId(string deviceId)
        {
            string[] parts = deviceId.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2 ? parts[1] : string.Empty;
        }

        private static string ParseEdidDisplayName(byte[] edid)
        {
            if (edid.Length < 128)
            {
                return string.Empty;
            }

            for (int offset = 54; offset <= 108; offset += 18)
            {
                bool isMonitorNameDescriptor =
                    edid[offset] == 0x00 &&
                    edid[offset + 1] == 0x00 &&
                    edid[offset + 2] == 0x00 &&
                    edid[offset + 3] == 0xFC &&
                    edid[offset + 4] == 0x00;

                if (isMonitorNameDescriptor)
                {
                    return Encoding.ASCII
                        .GetString(edid, offset + 5, 13)
                        .Replace("\0", string.Empty)
                        .Trim();
                }
            }

            return string.Empty;
        }
    }
}
