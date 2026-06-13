using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BASpark
{
    [Flags]
    public enum VisualAppearanceResetFlags
    {
        None = 0,
        EffectScale = 1 << 0,
        EffectOpacity = 1 << 1,
        UnifiedAnimationSpeed = 1 << 2,
        TrailRefreshRate = 1 << 3,
        ParticleColor = 1 << 4,
        TrailAnimationSpeed = 1 << 5,
        ClickAnimationSpeed = 1 << 6
    }

    public enum ProcessFilterModeOption
    {
        Disabled,
        Blacklist,
        Whitelist
    }

    public enum PanelScrollbarVisibility
    {
        Always,
        OnScroll
    }

    public enum NetworkRegionOption
    {
        Auto,
        China,
        Global
    }

    public class FilterProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public ProcessFilterModeOption Mode { get; set; } = ProcessFilterModeOption.Blacklist;
        public List<string> Processes { get; set; } = new List<string>();
    }

    // 新增：多屏记忆
    public class ScreenSelectionState
    {
        public string IdentityKey { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
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
        public static bool UseLinkedAnimationSpeed { get; set; } = true;
        public static double TrailAnimationSpeed { get; set; } = 1.0;
        public static double ClickAnimationSpeed { get; set; } = 1.0;
        public static int TrailRefreshRate { get; set; } = 40;
        public static bool EnableEnvironmentFilter { get; set; } = false;
        public static bool HideInFullscreen { get; set; } = true;
        public static bool ShowEffectOnDesktop { get; set; } = true;
        public static string FilterProfiles { get; set; } = "";
        public static string ActiveProfileId { get; set; } = "";
        public static bool IsTouchscreenMode { get; set; } = false;
        public static int ClickTriggerType { get; set; } = 0; // 0:左, 1:右, 2:左右
        public static bool EnableMiddleClickTrigger { get; set; } = false;
        public static bool ScreenshotCompatibilityMode { get; set; } = false;
        public static string EnabledScreenIds { get; set; } = "";
        public static string ScreenSelections { get; set; } = "";
        public static string UiLanguage { get; set; } = "";
        public static NetworkRegionOption NetworkRegion { get; set; } = NetworkRegionOption.Auto;
        public static PanelScrollbarVisibility ScrollbarVisibility { get; set; } = PanelScrollbarVisibility.OnScroll;
        public static string SidebarBackgroundImagePath { get; set; } = "";
        public static string TelemetryClientId { get; set; } = "";
        public static string LastTelemetrySentUtc { get; set; } = "";

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
                        UseLinkedAnimationSpeed = Convert.ToBoolean(key.GetValue("UseLinkedAnimationSpeed", true));
                        TrailAnimationSpeed = Math.Clamp(Convert.ToDouble(key.GetValue("TrailAnimationSpeed", EffectSpeed)), 0.2, 3.0);
                        ClickAnimationSpeed = Math.Clamp(Convert.ToDouble(key.GetValue("ClickAnimationSpeed", EffectSpeed)), 0.2, 3.0);
                        TrailRefreshRate = Math.Clamp(Convert.ToInt32(key.GetValue("TrailRefreshRate", 40)), 10, 240);
                        EnableEnvironmentFilter = Convert.ToBoolean(key.GetValue("EnableEnvironmentFilter", false));
                        HideInFullscreen = Convert.ToBoolean(key.GetValue("HideInFullscreen", true));
                        ShowEffectOnDesktop = Convert.ToBoolean(key.GetValue("ShowEffectOnDesktop", true));
                        IsTouchscreenMode = Convert.ToBoolean(key.GetValue("IsTouchscreenMode", false));
                        ClickTriggerType = Convert.ToInt32(key.GetValue("ClickTriggerType", 0));
                        EnableMiddleClickTrigger = Convert.ToBoolean(key.GetValue("EnableMiddleClickTrigger", false));
                        ScreenshotCompatibilityMode = Convert.ToBoolean(key.GetValue("ScreenshotCompatibilityMode", false));
                        EnabledScreenIds = key.GetValue("EnabledScreenIds", "")?.ToString() ?? "";
                        ScreenSelections = key.GetValue("ScreenSelections", "")?.ToString() ?? "";
                        UiLanguage = key.GetValue("UiLanguage", "")?.ToString() ?? "";
                        NetworkRegion = ParseNetworkRegion(key.GetValue("NetworkRegion", "Auto")?.ToString());
                        ScrollbarVisibility = ParseScrollbarVisibility(key.GetValue("ScrollbarVisibility", "OnScroll")?.ToString());
                        SidebarBackgroundImagePath = key.GetValue("SidebarBackgroundImagePath", "")?.ToString() ?? "";
                        TelemetryClientId = key.GetValue("TelemetryClientId", "")?.ToString() ?? "";
                        LastTelemetrySentUtc = key.GetValue("LastTelemetrySentUtc", "")?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(UiLanguage))
                        {
                            Localization.ApplyCulture(UiLanguage);
                        }

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
                                Name = Localization.Get("Profile_Default"),
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

        public static PanelScrollbarVisibility ParseScrollbarVisibility(string? raw)
        {
            if (string.Equals(raw, "Always", StringComparison.OrdinalIgnoreCase))
            {
                return PanelScrollbarVisibility.Always;
            }

            return PanelScrollbarVisibility.OnScroll;
        }

        public static NetworkRegionOption ParseNetworkRegion(string? raw)
        {
            if (string.Equals(raw, "China", StringComparison.OrdinalIgnoreCase))
            {
                return NetworkRegionOption.China;
            }

            if (string.Equals(raw, "Global", StringComparison.OrdinalIgnoreCase))
            {
                return NetworkRegionOption.Global;
            }

            return NetworkRegionOption.Auto;
        }

        public static void GetAnimationSpeedsForOverlay(out double trailSpeed, out double clickSpeed)
        {
            if (UseLinkedAnimationSpeed)
            {
                trailSpeed = EffectSpeed;
                clickSpeed = EffectSpeed;
            }
            else
            {
                trailSpeed = TrailAnimationSpeed;
                clickSpeed = ClickAnimationSpeed;
            }
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

        /// 将选中的视觉表现项恢复默认值并写入注册表
        public static void ApplyVisualAppearanceDefaults(VisualAppearanceResetFlags flags)
        {
            if (flags == VisualAppearanceResetFlags.None)
            {
                return;
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.EffectScale))
            {
                Save("EffectScale", 1.5);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.EffectOpacity))
            {
                Save("EffectOpacity", 1.0);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.UnifiedAnimationSpeed))
            {
                Save("UseLinkedAnimationSpeed", true);
                Save("EffectSpeed", 1.0);
                Save("TrailAnimationSpeed", 1.0);
                Save("ClickAnimationSpeed", 1.0);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.TrailAnimationSpeed))
            {
                Save("TrailAnimationSpeed", 1.0);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.ClickAnimationSpeed))
            {
                Save("ClickAnimationSpeed", 1.0);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.TrailRefreshRate))
            {
                Save("TrailRefreshRate", 40);
            }

            if (flags.HasFlag(VisualAppearanceResetFlags.ParticleColor))
            {
                Save("ParticleColor", "45,175,255");
            }
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

        public static List<ScreenSelectionState> GetScreenSelections()
        {
            if (string.IsNullOrWhiteSpace(ScreenSelections))
            {
                return new List<ScreenSelectionState>();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<ScreenSelectionState>>(ScreenSelections) ?? new List<ScreenSelectionState>();
            }
            catch
            {
                return new List<ScreenSelectionState>();
            }
        }

        public static HashSet<string> ResolveEnabledScreenDeviceNames(IEnumerable<ScreenIdentityInfo> currentScreens)
        {
            var screens = currentScreens.ToList();
            var selections = GetScreenSelections();
            var legacyEnabledIds = GetEnabledScreenIds();
            bool hasSavedPreference = selections.Count > 0 || legacyEnabledIds.Count > 0;

            // 没有已记忆的可用屏幕时自动启用现有屏幕，避免 Sunshine 虚拟屏场景丢失特效(Issue #104)
            var enabled = screens
                .Where(screen => IsScreenEnabledByPreference(screen, selections, legacyEnabledIds))
                .Select(screen => screen.DeviceName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (enabled.Count == 0 && hasSavedPreference)
            {
                enabled = screens.Select(screen => screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            return enabled;
        }

        public static void SaveScreenSelections(IEnumerable<ScreenSelectionState> screenSelections)
        {
            var incoming = screenSelections
                .Where(s => !string.IsNullOrWhiteSpace(s.IdentityKey) || !string.IsNullOrWhiteSpace(s.DeviceName))
                .Select(s => new ScreenSelectionState
                {
                    IdentityKey = NormalizeScreenValue(s.IdentityKey),
                    DeviceName = NormalizeScreenValue(s.DeviceName),
                    DisplayName = NormalizeScreenValue(s.DisplayName),
                    IsEnabled = s.IsEnabled
                })
                .ToList();

            var merged = GetScreenSelections();
            foreach (var item in incoming)
            {
                merged.RemoveAll(existing => IsSameSavedScreen(existing, item));
                merged.Add(item);
            }

            string json = System.Text.Json.JsonSerializer.Serialize(merged);
            Save("ScreenSelections", json);
            SaveEnabledScreenIds(incoming.Where(s => s.IsEnabled).Select(s => s.DeviceName));
        }

        private static bool IsScreenEnabledByPreference(
            ScreenIdentityInfo screen,
            List<ScreenSelectionState> selections,
            HashSet<string> legacyEnabledIds)
        {
            ScreenSelectionState? saved = selections.FirstOrDefault(selection => IsSameScreen(selection, screen));
            if (saved != null)
            {
                return saved.IsEnabled;
            }

            if (selections.Count > 0)
            {
                return true;
            }

            return legacyEnabledIds.Count == 0 || legacyEnabledIds.Contains(screen.DeviceName);
        }

        private static bool IsSameScreen(ScreenSelectionState selection, ScreenIdentityInfo screen)
        {
            return (!string.IsNullOrWhiteSpace(selection.IdentityKey) &&
                    string.Equals(selection.IdentityKey, screen.IdentityKey, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(selection.DeviceName) &&
                    string.Equals(selection.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSameSavedScreen(ScreenSelectionState left, ScreenSelectionState right)
        {
            return (!string.IsNullOrWhiteSpace(left.IdentityKey) &&
                    string.Equals(left.IdentityKey, right.IdentityKey, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(left.DeviceName) &&
                    string.Equals(left.DeviceName, right.DeviceName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeScreenValue(string value) => value.Trim();

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
                UseLinkedAnimationSpeed = true;
                TrailAnimationSpeed = 1.0;
                ClickAnimationSpeed = 1.0;
                TrailRefreshRate = 40;
                EnableEnvironmentFilter = false;
                HideInFullscreen = true;
                ShowEffectOnDesktop = true;
                FilterProfiles = "";
                ActiveProfileId = "";
                _profiles.Clear();
                IsTouchscreenMode = false;
                ClickTriggerType = 0;
                EnableMiddleClickTrigger = false;
                ScreenshotCompatibilityMode = false;
                EnabledScreenIds = "";
                ScreenSelections = "";
                UiLanguage = "";
                NetworkRegion = NetworkRegionOption.Auto;
                SidebarBackgroundImagePath = "";
                TelemetryClientId = "";
                LastTelemetrySentUtc = "";
            }
            catch { }
        }
    }
}
