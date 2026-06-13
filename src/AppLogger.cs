using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BASpark
{
    public sealed class AppLoggerTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                AppLogger.Debug(message);
            }
        }
        public override void WriteLine(string? message) => Write(message + Environment.NewLine);
    }

    public static class AppLogger
    {
        private const int MaxEntries = 800;
        private static readonly object LockObj = new();
        private static readonly List<string> Entries = new();
        private static bool _initialized;

        [ThreadStatic]
        private static bool _isLogging;

        public static event Action<string>? EntryAdded;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Trace.Listeners.Add(new AppLoggerTraceListener());
            Info("AppLogger initialized.");
        }

        public static IReadOnlyList<string> GetEntries()
        {
            lock (LockObj)
            {
                return Entries.ToList();
            }
        }

        public static void Clear()
        {
            lock (LockObj)
            {
                Entries.Clear();
            }
        }

        public static void Debug(string message) => Log("DEBUG", message);

        public static void Info(string message) => Log("INFO", message);

        public static void Warn(string message) => Log("WARN", message);

        public static void Error(string message, Exception? ex = null)
        {
            if (ex == null)
            {
                Log("ERROR", message);
                return;
            }

            Log("ERROR", $"{message} | {ex.GetType().Name}: {ex.Message}");
        }

        private static void Log(string level, string message)
        {
            if (_isLogging)
            {
                return;
            }

            try
            {
                _isLogging = true;

                string sanitized = Sanitize(message);
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    return;
                }

                string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {sanitized}";
                lock (LockObj)
                {
                    Entries.Add(line);
                    if (Entries.Count > MaxEntries)
                    {
                        Entries.RemoveRange(0, Entries.Count - MaxEntries);
                    }
                }

                System.Diagnostics.Debug.WriteLine(line);
                EntryAdded?.Invoke(line);
            }
            finally
            {
                _isLogging = false;
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(Math.Min(value.Length, 2000));
            foreach (char ch in value)
            {
                if (char.IsControl(ch) && ch != '\t')
                {
                    continue;
                }

                builder.Append(ch);
                if (builder.Length >= 2000)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }
    }
}