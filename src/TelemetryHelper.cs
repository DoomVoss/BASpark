using System;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace BASpark
{
    public static class TelemetryHelper
    {
        private const int MaxPayloadBytes = 4096;
        private const int MinSendIntervalHours = 24;
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static int _sendInProgress;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            [FieldOffset(36)]
            public short dmSize;

            [FieldOffset(40)]
            public int dmFields;

            [FieldOffset(108)]
            public int dmPelsWidth;

            [FieldOffset(112)]
            public int dmPelsHeight;

            [FieldOffset(120)]
            public int dmDisplayFrequency;
        }

        public static void SendStartupData()
        {
            if (!ConfigManager.EnableTelemetry) return;
            _ = SendTelemetryAsync("startup");
        }

        public static async Task SendTelemetryAsync(string trigger)
        {
            if (!ConfigManager.EnableTelemetry) return;

            if (Interlocked.CompareExchange(ref _sendInProgress, 1, 0) != 0) return;

            try
            {
                if (!ShouldSendNow()) return;

                string clientId = EnsureClientId();
                var payload = BuildPayload(clientId, trigger);
                string json = JsonSerializer.Serialize(payload);

                if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
                {
                    AppLogger.Warn("Telemetry payload exceeded size limit; skipped.");
                    return;
                }

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await HttpClient.PostAsync(Localization.GetTelemetryUrl(), content).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    ConfigManager.Save("LastTelemetrySentUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    AppLogger.Info("Telemetry sent successfully.");
                }
                else
                {
                    AppLogger.Warn($"Telemetry rejected: HTTP {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Telemetry send failed.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _sendInProgress, 0);
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BASparkClient/1.0");
            return client;
        }

        private static bool ShouldSendNow()
        {
            if (string.IsNullOrWhiteSpace(ConfigManager.LastTelemetrySentUtc)) return true;
            if (!DateTime.TryParse(ConfigManager.LastTelemetrySentUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime lastSent)) return true;
            return DateTime.UtcNow - lastSent.ToUniversalTime() >= TimeSpan.FromHours(MinSendIntervalHours);
        }

        private static string EnsureClientId()
        {
            if (!string.IsNullOrWhiteSpace(ConfigManager.TelemetryClientId) && Guid.TryParse(ConfigManager.TelemetryClientId, out _))
            {
                return ConfigManager.TelemetryClientId;
            }
            string clientId = Guid.NewGuid().ToString("D");
            ConfigManager.Save("TelemetryClientId", clientId);
            return clientId;
        }

        private static object BuildPayload(string clientId, string trigger)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionText = version == null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";

            var screensInfo = new List<string>();
            try
            {
                foreach (var screen in Screen.AllScreens)
                {
                    var dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    if (EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmDisplayFrequency > 1)
                    {
                        screensInfo.Add($"{dm.dmPelsWidth}x{dm.dmPelsHeight}@{dm.dmDisplayFrequency}Hz");
                    }
                    else
                    {
                        int fallbackRate = GetRefreshRateViaWmiFallback();
                        screensInfo.Add($"{screen.Bounds.Width}x{screen.Bounds.Height}@{fallbackRate}Hz");
                    }
                }
            }
            catch
            {
                screensInfo.Add($"{Screen.PrimaryScreen?.Bounds.Width ?? 1920}x{Screen.PrimaryScreen?.Bounds.Height ?? 1080}@60Hz");
            }

            return new
            {
                clientId = Sanitize(clientId, 36),
                trigger = Sanitize(trigger, 32),
                app = "BASpark",
                appVersion = Sanitize(versionText, 32),
                osVersion = Sanitize(GetOsVersion(), 64),
                osArchitecture = RuntimeInformation.OSArchitecture.ToString().ToUpper(),
                dotNetVersion = Sanitize(GetCleanDotNetVersion(), 32),
                uiLanguage = Sanitize(ConfigManager.UiLanguage, 16),
                networkRegion = ConfigManager.NetworkRegion.ToString(),
                isEffectEnabled = ConfigManager.IsEffectEnabled,
                autoStart = ConfigManager.AutoStart,
                isSilentStart = ConfigManager.StartSilent,
                runAsAdmin = ConfigManager.RunAsAdmin,
                screenCount = Math.Clamp(Screen.AllScreens.Length, 0, 32),
                screens = screensInfo,
                cpuModel = Sanitize(GetCpuName(), 64),
                gpuModel = Sanitize(GetGpuName(), 64),
                ramSize = GetTotalMemoryGb(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private static string GetCleanDotNetVersion()
        {
            try
            {
                string desc = RuntimeInformation.FrameworkDescription;
                if (!string.IsNullOrEmpty(desc))
                {
                    return desc.Replace(".NET Core", "Core").Replace(".NET", "").Trim();
                }
            }
            catch { /* ignore: fallback to Environment.Version below */ }

            try
            {
                var version = Environment.Version;
                if (version.Major == 4)
                {
                    return "Framework 4.x";
                }
                return version.ToString();
            }
            catch
            {
                /* ignore: cannot determine .NET version */
                return "Unknown";
            }
        }

        private static int GetRefreshRateViaWmiFallback()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT CurrentRefreshRate FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    if (obj["CurrentRefreshRate"] != null)
                    {
                        int rate = Convert.ToInt32(obj["CurrentRefreshRate"]);
                        if (rate > 1) return rate;
                    }
                }
            }
            catch { /* ignore: WMI unavailable, fallback to 60Hz */ }
            return 60;
        }

        private static string GetOsVersion()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Sanitize(RuntimeInformation.OSDescription, 64);
            return "unknown";
        }

        private static string GetCpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get()) return obj["Name"]?.ToString() ?? "Unknown CPU";
            }
            catch { /* ignore: WMI unavailable, fallback to 'Unknown CPU' */ }
            return "Unknown CPU";
        }

        private static string GetGpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_VideoController");
                var gpuList = searcher.Get().Cast<ManagementBaseObject>()
                    .Select(obj => obj["Name"]?.ToString() ?? "")
                    .Where(name => !string.IsNullOrEmpty(name)).ToList();

                if (gpuList.Count == 0) return "Unknown GPU";

                string[] physicalKeywords = { "NVIDIA", "GeForce", "AMD", "Radeon", "Intel", "ARC" };
                foreach (var keyword in physicalKeywords)
                {
                    var physicalGpu = gpuList.FirstOrDefault(g => g.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                    if (physicalGpu != null)
                    {
                        if (physicalGpu.Contains("Intel", StringComparison.OrdinalIgnoreCase) && gpuList.Any(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || g.Contains("AMD", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                        return SanitizeGpuName(physicalGpu);
                    }
                }
                return SanitizeGpuName(gpuList[0]);
            }
            catch { /* ignore: WMI unavailable, fallback to 'Unknown GPU' */ }
            return "Unknown GPU";
        }

        private static string SanitizeGpuName(string name)
        {
            if (name.Contains("(Microsoft Corporation"))
            {
                int index = name.IndexOf("(Microsoft");
                if (index > 0) return name.Substring(0, index).Trim();
            }
            return name;
        }

        private static string GetTotalMemoryGb()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes))
                    {
                        return $"{Math.Round((double)bytes / 1024 / 1024 / 1024)} GB";
                    }
                }
            }
            catch { /* ignore: WMI unavailable, fallback to 'Unknown RAM' */ }
            return "Unknown RAM";
        }

        private static string Sanitize(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(Math.Min(value.Length, maxLength));
            foreach (char ch in value.Trim())
            {
                if (char.IsControl(ch)) continue;
                builder.Append(ch);
                if (builder.Length >= maxLength) break;
            }
            return builder.ToString();
        }
    }
}