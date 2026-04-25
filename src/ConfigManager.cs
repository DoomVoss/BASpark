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

    public class FilterProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "新配置组";
        public ProcessFilterModeOption Mode { get; set; } = ProcessFilterModeOption.Blacklist;
        public List<string> Processes { get; set; } = new List<string>();
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
        public static bool RunAsAdmin { get; set; } = false;
        public static double EffectScale { get; set; } = 1.5;
        public static double EffectOpacity { get; set; } = 1.0;
        public static double EffectSpeed { get; set; } = 1.0;
        public static int TrailRefreshRate { get; set; } = 40;
        public static bool EnableEnvironmentFilter { get; set; } = false;
        public static bool HideInFullscreen { get; set; } = true;
        public static bool ShowEffectOnDesktop { get; set; } = true;
        public static string FilterProfiles { get; set; } = "";
        public static string ActiveProfileId { get; set; } = "";
        public static bool IsTouchscreenMode { get; set; } = false;
        public static bool EnableMultiTouch { get; set; } = false;
        public static int ClickTriggerType { get; set; } = 0; // 0:左, 1:右, 2:左右
        public static string EnabledScreenIds { get; set; } = "";

        private static List<FilterProfile> _profiles = new List<FilterProfile>();

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
                        RunAsAdmin = Convert.ToBoolean(key.GetValue("RunAsAdmin", false));
                        EffectScale = Math.Clamp(Convert.ToDouble(key.GetValue("EffectScale", 1.5)), 0.5, 3.0);
                        EffectOpacity = Math.Clamp(Convert.ToDouble(key.GetValue("EffectOpacity", 1.0)), 0.1, 1.0);
                        EffectSpeed = Math.Clamp(Convert.ToDouble(key.GetValue("EffectSpeed", 1.0)), 0.2, 3.0);
                        TrailRefreshRate = Math.Clamp(Convert.ToInt32(key.GetValue("TrailRefreshRate", 40)), 10, 240);
                        EnableEnvironmentFilter = Convert.ToBoolean(key.GetValue("EnableEnvironmentFilter", false));
                        HideInFullscreen = Convert.ToBoolean(key.GetValue("HideInFullscreen", true));
                        ShowEffectOnDesktop = Convert.ToBoolean(key.GetValue("ShowEffectOnDesktop", true));
                        IsTouchscreenMode = Convert.ToBoolean(key.GetValue("IsTouchscreenMode", false));
                        EnableMultiTouch = Convert.ToBoolean(key.GetValue("EnableMultiTouch", false));
                        ClickTriggerType = Convert.ToInt32(key.GetValue("ClickTriggerType", 0));
                        EnabledScreenIds = key.GetValue("EnabledScreenIds", "")?.ToString() ?? "";

                        FilterProfiles = key.GetValue("FilterProfiles", "")?.ToString() ?? "";
                        ActiveProfileId = key.GetValue("ActiveProfileId", "")?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(FilterProfiles))
                        {
                            try
                            {
                                _profiles = System.Text.Json.JsonSerializer.Deserialize<List<FilterProfile>>(FilterProfiles) ?? new List<FilterProfile>();
                            }
                            catch { _profiles = new List<FilterProfile>(); }
                        }

                        // 向后兼容处理
                        if (_profiles.Count == 0)
                        {
                            string processFilterModeRaw = key.GetValue("ProcessFilterMode", "Disabled")?.ToString() ?? "Disabled";
                            ProcessFilterModeOption oldMode;
                            if (!Enum.TryParse(processFilterModeRaw, true, out oldMode))
                            {
                                oldMode = ProcessFilterModeOption.Disabled;
                            }
                            string oldListRaw = key.GetValue("ProcessFilterList", "")?.ToString() ?? "";
                            var oldList = oldListRaw
                                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(s => s.ToLowerInvariant())
                                .Distinct()
                                .ToList();

                            var defaultProfile = new FilterProfile
                            {
                                Name = "默认配置",
                                Mode = oldMode == ProcessFilterModeOption.Disabled ? ProcessFilterModeOption.Blacklist : oldMode,
                                Processes = oldList
                            };
                            _profiles.Add(defaultProfile);
                            ActiveProfileId = defaultProfile.Id;
                        }

                        if (string.IsNullOrEmpty(ActiveProfileId) && _profiles.Count > 0)
                        {
                            ActiveProfileId = _profiles[0].Id;
                        }
                    }
                }
            }
            catch { }
        }

        public static List<FilterProfile> GetProfiles() => _profiles;

        public static FilterProfile? GetActiveProfile()
        {
            return _profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? _profiles.FirstOrDefault();
        }

        public static void SaveProfiles(List<FilterProfile> profiles, string activeId)
        {
            _profiles = profiles;
            ActiveProfileId = activeId;
            string json = System.Text.Json.JsonSerializer.Serialize(_profiles);
            Save("FilterProfiles", json);
            Save("ActiveProfileId", activeId);
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

        public static IReadOnlySet<string> GetProcessFilterEntries()
        {
            var profile = GetActiveProfile();
            if (profile == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return profile.Processes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static HashSet<string> GetEnabledScreenIds()
        {
            if (string.IsNullOrWhiteSpace(EnabledScreenIds))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(EnabledScreenIds) ?? new List<string>();
                return parsed
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void SaveEnabledScreenIds(IEnumerable<string> screenIds)
        {
            var normalized = screenIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string json = System.Text.Json.JsonSerializer.Serialize(normalized);
            Save("EnabledScreenIds", json);
        }

        public static void ResetAndClear()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegPath, false);

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
                RunAsAdmin = false;
                EffectScale = 1.5;
                EffectOpacity = 1.0;
                EffectSpeed = 1.0;
                TrailRefreshRate = 40;
                EnableEnvironmentFilter = false;
                HideInFullscreen = true;
                ShowEffectOnDesktop = true;
                FilterProfiles = "";
                ActiveProfileId = "";
                _profiles.Clear();
                IsTouchscreenMode = false;
                EnableMultiTouch = false;
                ClickTriggerType = 0;
                EnabledScreenIds = "";
            }
            catch { }
        }
    }
}
