using System;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using System.Diagnostics;
using System.Security.Principal;
using System.Linq;

namespace BASpark
{
    public partial class App : System.Windows.Application
    {
        public static OverlayManager? Overlay { get; private set; }
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private ControlPanelWindow? _controlPanel;

        private static Mutex? _mutex;
        private int _isExiting = 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = @"Global\BASpark_SingleInstance_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                ConfigManager.Load();
                if (!string.IsNullOrWhiteSpace(ConfigManager.UiLanguage))
                {
                    Localization.ApplyCulture(ConfigManager.UiLanguage);
                }

                System.Windows.MessageBox.Show(
                    Localization.Get("App_AlreadyRunning"),
                    Localization.Get("App_AlreadyRunning_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Windows.Application.Current.Shutdown();
                return;
            }

            ConfigManager.Load();
            AppLogger.Initialize();

            if (string.IsNullOrWhiteSpace(ConfigManager.UiLanguage))
            {
                if (!ConfigManager.AgreedToPrivacy)
                {
                    var languageWin = new LanguageSelectWindow();
                    bool? langResult = languageWin.ShowDialog();
                    if (langResult != true)
                    {
                        ExitApplication();
                        return;
                    }
                }
                else
                {
                    string detected = Localization.DetectCultureFromSystem();
                    Localization.ApplyCulture(detected);
                    ConfigManager.Save("UiLanguage", detected);
                }
            }
            else
            {
                Localization.ApplyCulture(ConfigManager.UiLanguage);
            }

            if (ConfigManager.RunAsAdmin && !IsRunningAsAdmin())
            {
                try
                {
                    RestartWithAdminPrivileges(e.Args);
                    return;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Restart with admin privileges failed.", ex);
                    Debug.WriteLine("自动请求管理员权限被拒绝或失败: " + ex.Message);
                }
            }

            SystemEvents.SessionEnding += OnSessionEnding;

            base.OnStartup(e);

            bool launchedFromAutoStart = IsAutoStartLaunch(e.Args);

            if (!ConfigManager.AgreedToPrivacy)
            {
                var privacyWin = new PrivacyWindow();
                UiLocalizer.ApplyPrivacy(privacyWin);
                bool? result = privacyWin.ShowDialog();
                if (result == true)
                {
                    ConfigManager.Save("AgreedToPrivacy", true);
                }
                else
                {
                    ExitApplication();
                    return;
                }
            }

            TelemetryHelper.SendStartupData();

            InitTrayIcon();

            Overlay = new OverlayManager();
            Overlay.Start();

            if (!(launchedFromAutoStart && ConfigManager.StartSilent))
            {
                ShowControlPanel();
            }
        }

        private bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void RestartWithAdminPrivileges(string[] args)
        {
            string exePath = System.Environment.ProcessPath ??
                             System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", args.Select(a => $"\"{a}\""))
            };

            try
            {
                Process.Start(startInfo);
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                    _mutex = null;
                }
                System.Windows.Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new Exception("用户拒绝了管理员授权。");
            }
        }

        private static bool IsAutoStartLaunch(string[] args)
        {
            return args.Any(arg => string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase));
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            ExitApplication();
        }

        private void InitTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            try
            {
                var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
                if (streamInfo != null)
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            RefreshTrayLocalization();
            _notifyIcon.DoubleClick += (s, e) => ShowControlPanel();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add(Localization.Get("Tray_OpenPanel"), null, (s, e) => ShowControlPanel());
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add(Localization.Get("Tray_Restart"), null, (s, e) => RestartApplication());
            contextMenu.Items.Add(Localization.Get("Tray_Exit"), null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void RefreshTrayLocalization()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = Localization.Get("Tray_Text");
            }
        }

        public void ShowControlPanel()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (_controlPanel == null || !_controlPanel.IsLoaded)
                {
                    _controlPanel = new ControlPanelWindow();
                    _controlPanel.Show();
                }
                else
                {
                    if (_controlPanel.WindowState == WindowState.Minimized)
                    {
                        _controlPanel.WindowState = WindowState.Normal;
                    }
                    _controlPanel.Activate();
                }
            });
        }

        public void RestartApplicationFromPanel()
        {
            RestartApplication();
        }

        private void RestartApplication()
        {
            try
            {
                string exePath = System.Environment.ProcessPath ??
                                 System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

                ProcessStartInfo startInfo = new ProcessStartInfo(exePath) { UseShellExecute = true };
                if (ConfigManager.RunAsAdmin)
                {
                    startInfo.Verb = "runas";
                }

                System.Diagnostics.Process.Start(startInfo);
                ExitApplication();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    Localization.Format("Tray_RestartFailed", ex.Message));
            }
        }

        private void ExitApplication()
        {
            if (Interlocked.Exchange(ref _isExiting, 1) == 1) return;

            SystemEvents.SessionEnding -= OnSessionEnding;

            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.ContextMenuStrip?.Dispose();
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            this.Dispatcher.Invoke(() =>
            {
                try { _controlPanel?.Close(); } catch { /* ignore: best-effort cleanup during shutdown */ }
                try { Overlay?.Dispose(); } catch { /* ignore: best-effort cleanup during shutdown */ }
            });

            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { /* ignore: mutex disposal during shutdown */ }
                _mutex.Dispose();
                _mutex = null;
            }
            System.Windows.Application.Current.Shutdown();
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true)!;
                string exePath = System.Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

                if (enable)
                    key.SetValue("BASpark", $"\"{exePath}\" --autostart");
                else
                    key.DeleteValue("BASpark", false);

                ConfigManager.Save("AutoStart", enable);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("自启设置失败: " + ex.Message);
            }
        }
    }
}
