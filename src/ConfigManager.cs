using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BASpark
{
    public enum ProcessFilterModeOption
    {
        Disabled,
        Blacklist,
        Whitelist
    }

    public static class ConfigManager
    {
        private const string RegPath = @"Software\BASpark";

        public static string ParticleColor { get; set; } = "45,175,255";
        public static bool IsEffectEnabled { get; set; } = true;
        public static bool AutoStart { get; set; } = false;
        public static bool AgreedToPrivacy { get; set; } = false;
        public static bool EnableTelemetry { get; set; } = false;
        public static int TotalClicks { get; set; } = 0;
        public static string LastNoticeContent { get; set; } = "";
        public static bool EnableAlwaysTrailEffect { get; set; } = false;
        public static bool StartSilent { get; set; } = false;
        public static double EffectScale { get; set; } = 1.5;
        public static double EffectOpacity { get; set; } = 1.0;
        public static double EffectSpeed { get; set; } = 1.0;
        public static int TrailRefreshRate { get; set; } = 40;
        public static bool EnableEnvironmentFilter { get; set; } = false;
        public static bool HideInFullscreen { get; set; } = true;
        public static ProcessFilterModeOption ProcessFilterMode { get; set; } = ProcessFilterModeOption.Disabled;
        public static string ProcessFilterList { get; set; } = "";

        public static void Load()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null)
                    {
                        ParticleColor = key.GetValue("ParticleColor", "45,175,255")?.ToString() ?? "45,175,255";

                        IsEffectEnabled = Convert.ToBoolean(key.GetValue("IsEffectEnabled", true));
                        AutoStart = Convert.ToBoolean(key.GetValue("AutoStart", false));
                        AgreedToPrivacy = Convert.ToBoolean(key.GetValue("AgreedToPrivacy", false));
                        EnableTelemetry = Convert.ToBoolean(key.GetValue("EnableTelemetry", false));
                        TotalClicks = Convert.ToInt32(key.GetValue("TotalClicks", 0));
                        LastNoticeContent = key.GetValue("LastNoticeContent", "")?.ToString() ?? "";
                        EnableAlwaysTrailEffect = Convert.ToBoolean(key.GetValue("EnableAlwaysTrailEffect", false));
                        StartSilent = Convert.ToBoolean(key.GetValue("StartSilent", false));
                        EffectScale = Math.Clamp(Convert.ToDouble(key.GetValue("EffectScale", 1.5)), 0.5, 3.0);
                        EffectOpacity = Math.Clamp(Convert.ToDouble(key.GetValue("EffectOpacity", 1.0)), 0.1, 1.0);
                        EffectSpeed = Math.Clamp(Convert.ToDouble(key.GetValue("EffectSpeed", 1.0)), 0.2, 3.0);
                        TrailRefreshRate = Math.Clamp(Convert.ToInt32(key.GetValue("TrailRefreshRate", 40)), 10, 240);
                        EnableEnvironmentFilter = Convert.ToBoolean(key.GetValue("EnableEnvironmentFilter", false));
                        HideInFullscreen = Convert.ToBoolean(key.GetValue("HideInFullscreen", true));

                        string processFilterModeRaw = key.GetValue("ProcessFilterMode", ProcessFilterModeOption.Disabled.ToString())?.ToString()
                            ?? ProcessFilterModeOption.Disabled.ToString();
                        if (!Enum.TryParse(processFilterModeRaw, true, out ProcessFilterModeOption processFilterMode))
                        {
                            processFilterMode = ProcessFilterModeOption.Disabled;
                        }

                        ProcessFilterMode = processFilterMode;
                        ProcessFilterList = NormalizeProcessFilterList(key.GetValue("ProcessFilterList", "")?.ToString() ?? "");
                    }
                }
            }
            catch { }
        }

        public static void Save(string name, object value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (value is Enum enumValue)
                    {
                        key.SetValue(name, enumValue.ToString());
                    }
                    else
                    {
                        key.SetValue(name, value);
                    }

                    var prop = typeof(ConfigManager).GetProperty(name);
                    if (prop != null)
                    {
                        object propertyValue = value;
                        if (prop.PropertyType.IsEnum)
                        {
                            if (value is string stringValue)
                            {
                                propertyValue = Enum.Parse(prop.PropertyType, stringValue, ignoreCase: true);
                            }
                            else
                            {
                                propertyValue = Enum.ToObject(prop.PropertyType, value);
                            }
                        }

                        prop.SetValue(null, propertyValue);
                    }
                }
            }
            catch { }
        }

        public static string NormalizeProcessFilterList(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var normalizedLines = rawValue
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Trim().ToLowerInvariant())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return string.Join(Environment.NewLine, normalizedLines);
        }

        public static IReadOnlySet<string> GetProcessFilterEntries()
        {
            string normalized = NormalizeProcessFilterList(ProcessFilterList);
            if (string.IsNullOrEmpty(normalized))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return normalized
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static void ResetAndClear()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegPath, false);

                // 适配 264100 版本之前的配置存储逻辑
                string oldJson = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (System.IO.File.Exists(oldJson))
                {
                    System.IO.File.Delete(oldJson);
                }

                ParticleColor = "45,175,255";
                IsEffectEnabled = true;
                AutoStart = false;
                AgreedToPrivacy = false;
                EnableTelemetry = false;
                TotalClicks = 0;
                LastNoticeContent = "";
                EnableAlwaysTrailEffect = false;
                StartSilent = false;
                EffectScale = 1.5;
                EffectOpacity = 1.0;
                EffectSpeed = 1.0;
                TrailRefreshRate = 40;
                EnableEnvironmentFilter = false;
                HideInFullscreen = true;
                ProcessFilterMode = ProcessFilterModeOption.Disabled;
                ProcessFilterList = "";
            }
            catch { }
        }
    }
}
