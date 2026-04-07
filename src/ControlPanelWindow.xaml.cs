using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BASpark
{
    public partial class ControlPanelWindow : Window
    {
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _noticeTimer;
        private bool _isCheckingUpdate = false;

        public ControlPanelWindow()
        {
            InitializeComponent();
            LoadVersion();
            LoadSettings();
            LoadRemoteNotice();
            
            // 启动时自动静默检查更新
            _ = CheckForUpdates(isManual: false);

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();

            _noticeTimer = new DispatcherTimer();
            _noticeTimer.Interval = TimeSpan.FromHours(3);
            _noticeTimer.Tick += (s, e) => LoadRemoteNotice();
            _noticeTimer.Start();
        }

        // 检查更新核心逻辑
        private async Task CheckForUpdates(bool isManual)
        {
            string updateUrl = "https://qq.catbotstudio.top/update.json"; 

            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BASparkClient/1.0");

                string json = await client.GetStringAsync(updateUrl);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string latestVersionStr = root.GetProperty("version").GetString() ?? "0.0.0.0";
                string downloadUrl = root.GetProperty("url").GetString() ?? "";
                string updateNotes = root.GetProperty("notes").GetString() ?? "无更新说明";

                Version latestVersion = new Version(latestVersionStr);
                Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (currentVersion != null && latestVersion > currentVersion)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"发现新版本: V{latestVersionStr}\n\n更新内容:\n{updateNotes}\n\n是否立即前往下载？",
                            "发现新版本",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                        {
                            Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                        }
                    });
                }
                else if (isManual)
                {
                    Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("当前已是最新版本，无需更新！", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                {
                    Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("检查更新失败: \n" + ex.Message, "网络错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                else
                {
                    Debug.WriteLine("自动检查更新失败: " + ex.Message);
                }
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isCheckingUpdate) return;

            var btn = sender as System.Windows.Controls.Button;
            try
            {
                _isCheckingUpdate = true;
                
                if (btn != null)
                {
                    btn.IsEnabled = false;
                    btn.Content = "正在检查..."; 
                }

                await CheckForUpdates(isManual: true);
            }
            finally
            {
                _isCheckingUpdate = false;
                
                if (btn != null)
                {
                    btn.IsEnabled = true;
                    btn.Content = "检查更新"; 
                }
            }
        }

        private async void LoadRemoteNotice()
        {
            string noticeUrl = "https://qq.catbotstudio.top/notice.json";

            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BASparkClient/1.0");

                string json = await client.GetStringAsync(noticeUrl);

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string title = root.GetProperty("title").GetString() ?? "官方公告";
                string content = root.GetProperty("content").GetString() ?? "";
                string date = root.GetProperty("date").GetString() ?? "";

                string lastContent = ConfigManager.LastNoticeContent;

                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(content))
                    {
                        NoticeTitle.Text = title;
                        NoticeContent.Text = content;
                        NoticeDate.Text = date;
                        NoticeBar.Visibility = Visibility.Visible;

                        if (content != lastContent)
                        {
                            ShowWindowsNotification(title, content);
                            ConfigManager.Save("LastNoticeContent", content);
                        }
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrEmpty(NoticeContent.Text) || NoticeContent.Text == "...")
                    {
                        NoticeBar.Visibility = Visibility.Collapsed;
                    }
                });
            }
        }

        private void ShowWindowsNotification(string title, string content)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(content)
                    .Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("通知推送失败: " + ex.Message);
            }
        }

        private void LoadVersion()
        {
            try
            {
                Version? version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null && VersionText != null)
                {
                    VersionText.Text = $"V{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                if (VersionText != null) VersionText.Text = "版本信息读取失败";
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (ClickCountText != null)
                ClickCountText.Text = $"{ConfigManager.TotalClicks} 次";

            if (StatusText != null)
            {
                if (ConfigManager.IsEffectEnabled)
                {
                    StatusText.Text = "工作中 (Active)";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    StatusText.Text = "已暂停 (Paused)";
                    StatusText.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
        }

        private void LoadSettings()
        {
            CheckMasterSwitch.IsChecked = ConfigManager.IsEffectEnabled;
            CheckAutoStart.IsChecked = ConfigManager.AutoStart;
            CheckStartSilent.IsChecked = ConfigManager.StartSilent;
            CheckTelemetry.IsChecked = ConfigManager.EnableTelemetry;
            CheckAlwaysTrailEffectSwitch.IsChecked = ConfigManager.EnableAlwaysTrailEffect;
            UpdateColorPreview(ConfigManager.ParticleColor);

            UpdateStartSilentInterlock();

            SliderScale.Value = ConfigManager.EffectScale;
            SliderOpacity.Value = ConfigManager.EffectOpacity;
            SliderSpeed.Value = ConfigManager.EffectSpeed;
            SliderTrailRefresh.Value = ConfigManager.TrailRefreshRate;
            UpdateEffectValueTexts();
        }

        private void CheckAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStartSilentInterlock();
        }

        private void UpdateStartSilentInterlock()
        {
            bool autoStartEnabled = CheckAutoStart.IsChecked == true;
            CheckStartSilent.IsEnabled = autoStartEnabled;
        }

        private void UpdateEffectValueTexts()
        {
            TextScaleValue.Text = $"{SliderScale.Value:F2}x";
            TextOpacityValue.Text = $"{SliderOpacity.Value:P0}";
            TextSpeedValue.Text = $"{SliderSpeed.Value:F2}x";
            TextTrailRefreshValue.Text = $"{Math.Round(SliderTrailRefresh.Value)}";
        }

        private void EffectSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            UpdateEffectValueTexts();
        }

        private void UpdateColorPreview(string rgbString)
        {
            try
            {
                var parts = rgbString.Split(',');
                if (parts.Length == 3)
                {
                    byte r = byte.Parse(parts[0].Trim());
                    byte g = byte.Parse(parts[1].Trim());
                    byte b = byte.Parse(parts[2].Trim());
                    ColorPreview.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(r, g, b));
                }
            }
            catch
            {
                ColorPreview.Background = System.Windows.Media.Brushes.Gray;
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (PageWelcome == null) return;
            PageWelcome.Visibility = Visibility.Collapsed;
            PageSettings.Visibility = Visibility.Collapsed;
            PageAbout.Visibility = Visibility.Collapsed;

            if (TabWelcome.IsChecked == true) PageWelcome.Visibility = Visibility.Visible;
            else if (TabSettings.IsChecked == true) PageSettings.Visibility = Visibility.Visible;
            else if (TabAbout.IsChecked == true) PageAbout.Visibility = Visibility.Visible;
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.ColorDialog();
            dialog.FullOpen = true;
            try
            {
                var parts = ConfigManager.ParticleColor.Split(',');
                dialog.Color = System.Drawing.Color.FromArgb(
                    byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]));
            }
            catch { }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string newColor = $"{dialog.Color.R},{dialog.Color.G},{dialog.Color.B}";
                ConfigManager.ParticleColor = newColor;
                UpdateColorPreview(newColor);
            }
        }

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("无法打开链接: " + ex.Message);
                }
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            double effectScale = Math.Round(SliderScale.Value, 2);
            double effectOpacity = Math.Round(SliderOpacity.Value, 2);
            double effectSpeed = Math.Round(SliderSpeed.Value, 2);
            int trailRefreshRate = (int)Math.Round(SliderTrailRefresh.Value);
            bool autoStartEnabled = CheckAutoStart.IsChecked ?? false;
            bool startSilentEnabled = CheckStartSilent.IsChecked ?? false;

            ConfigManager.Save("IsEffectEnabled", CheckMasterSwitch.IsChecked ?? true);
            ConfigManager.Save("AutoStart", autoStartEnabled);
            ConfigManager.Save("EnableTelemetry", CheckTelemetry.IsChecked ?? false);
            ConfigManager.Save("ParticleColor", ConfigManager.ParticleColor);
            ConfigManager.Save("EffectScale", effectScale);
            ConfigManager.Save("EffectOpacity", effectOpacity);
            ConfigManager.Save("EffectSpeed", effectSpeed);
            ConfigManager.Save("TrailRefreshRate", trailRefreshRate);
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            ConfigManager.Save("EnableAlwaysTrailEffect", CheckAlwaysTrailEffectSwitch.IsChecked ?? false);
            ConfigManager.Save("StartSilent", startSilentEnabled);

            App.SetAutoStart(ConfigManager.AutoStart);

            App.Overlay?.UpdateColor(ConfigManager.ParticleColor);
            App.Overlay?.UpdateEffectSettings(effectScale, effectOpacity, effectSpeed);
            App.Overlay?.UpdateTrailRefreshRate(trailRefreshRate);

            System.Windows.MessageBox.Show("配置已成功应用！", "BASpark", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            base.OnClosing(e);
        }

        private void ResetConfig_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "确定要重置所有配置吗？这将会清空所有配置，程序随后将关闭。",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ConfigManager.ResetAndClear();
                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("删除失败: " + ex.Message);
                }
            }
        }
    }
}