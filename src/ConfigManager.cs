using Microsoft.Win32;
using System;

namespace BASpark
{
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
                    key.SetValue(name, value);
                    
                    var prop = typeof(ConfigManager).GetProperty(name);
                    if (prop != null) prop.SetValue(null, value);
                }
            }
            catch { }
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
            }
            catch { }
        }
    }
}