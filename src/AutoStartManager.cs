using System;
using System.IO;

namespace BASpark
{
    public readonly record struct AutoStartPlan(bool RegistryRunEnabled, bool ScheduledTaskEnabled);

    public static class AutoStartManager
    {
        public const string RunValueName = "BASpark";
        public const string TaskName = "BASparkAutoStart";

        public static AutoStartPlan CreatePlan(bool autoStart, bool runAsAdmin)
        {
            if (!autoStart)
            {
                return new AutoStartPlan(false, false);
            }

            return runAsAdmin
                ? new AutoStartPlan(false, true)
                : new AutoStartPlan(true, false);
        }

        public static string BuildRunCommand(string exePath)
        {
            return $"\"{exePath}\" --autostart";
        }

        public static string? ResolveExecutablePath(
            string? processPath,
            string? mainModulePath,
            string? assemblyLocation,
            string baseDirectory)
        {
            foreach (string? candidate in new[] { processPath, mainModulePath, assemblyLocation })
            {
                if (IsExecutablePath(candidate))
                {
                    return candidate;
                }
            }

            return string.IsNullOrWhiteSpace(baseDirectory)
                ? null
                : Path.Combine(baseDirectory, "BASpark.exe");
        }

        private static bool IsExecutablePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
