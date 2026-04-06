using System;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace BASpark
{
    public partial class App : System.Windows.Application
    {
        public static MainWindow? Overlay { get; private set; }
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private ControlPanelWindow? _controlPanel;

        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = @"Global\BASpark_SingleInstance_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);
            bool launchedFromAutoStart = IsAutoStartLaunch(e.Args);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show("BASpark 已经在运行中，请检查系统托盘。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            ConfigManager.Load();

            if (!ConfigManager.AgreedToPrivacy)
            {
                var privacyWin = new PrivacyWindow();
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

            Overlay = new MainWindow();
            Overlay.Show();

            // 仅手动启动/开机非静默启动时显示控制面板
            if (!(launchedFromAutoStart && ConfigManager.StartSilent))
            {
                ShowControlPanel();
            }
        }

        private static bool IsAutoStartLaunch(string[] args)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
            _notifyIcon.Text = "BASpark - 点击特效";
            _notifyIcon.DoubleClick += (s, e) => ShowControlPanel();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("打开控制面板", null, (s, e) => ShowControlPanel());
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("彻底退出", null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;
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

        private void ExitApplication()
        {
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);

            _notifyIcon?.Dispose();
            Overlay?.Close();

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }

            System.Windows.Application.Current.Shutdown();
        }
    }
}