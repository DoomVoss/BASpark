using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Security.Principal;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace BASpark
{
    public class ProcessItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class ScreenOptionItem
    {
        public int DisplayIndex { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ResolutionText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public partial class ControlPanelWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _noticeTimer;
        private bool _isCheckingUpdate = false;

        public ObservableCollection<FilterProfile> Profiles { get; set; } = new ObservableCollection<FilterProfile>();
        public ObservableCollection<string> CurrentProfileProcesses { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ProcessItem> RunningProcessList { get; set; } = new ObservableCollection<ProcessItem>();
        public ObservableCollection<ScreenOptionItem> ScreenOptions { get; set; } = new ObservableCollection<ScreenOptionItem>();

        public ControlPanelWindow()
        {
            InitializeComponent();
            
            ComboProfiles.ItemsSource = Profiles;
            ListConfiguredProcesses.ItemsSource = CurrentProfileProcesses;
            ListRunningProcesses.ItemsSource = RunningProcessList;
            ListScreenOptions.ItemsSource = ScreenOptions;

            LoadVersion();
            LoadSettings();
            LoadScreenOptions();
            CheckAdminStatus();
            LoadRemoteNotice();
            
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

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            bool isInvalid = regex.IsMatch(e.Text);

            if (!isInvalid && e.Text == ".")
            {
                var textBox = sender as System.Windows.Controls.TextBox;
                if (textBox != null && textBox.Text.Contains("."))
                {
                    isInvalid = true;
                }
            }

            e.Handled = isInvalid;
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var textBox = sender as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    System.Windows.Input.Keyboard.ClearFocus();
                    var binding = System.Windows.Data.BindingOperations.GetBindingExpression(textBox, System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
                e.Handled = true;
            }
        }

        private void CheckAdminStatus()
        {
            if (CheckRunAsAdmin != null)
            {
                CheckRunAsAdmin.IsChecked = ConfigManager.RunAsAdmin;
            }
        }

        private async Task CheckForUpdates(bool isManual)
        {
            string updateUrl = "https://api.catbotstudio.cn/baspark/update.json"; 
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
            string noticeUrl = "https://api.catbotstudio.cn/baspark/notice.json";
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
                new ToastContentBuilder().AddText(title).AddText(content).Show();
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
                bool suppressedByEnvironment = ConfigManager.IsEffectEnabled &&
                    App.Overlay?.IsEffectSuppressedByEnvironment() == true;

                if (!ConfigManager.IsEffectEnabled)
                {
                    StatusText.Text = "已暂停 (Paused)";
                    StatusText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else if (suppressedByEnvironment)
                {
                    StatusText.Text = "环境过滤中 (Auto Hidden)";
                    StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06));
                }
                else
                {
                    StatusText.Text = "工作中 (Active)";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
        }

        private void RefreshRunningProcessList()
        {
            RunningProcessList.Clear();
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                    .OrderBy(p => p.ProcessName);

                foreach (var p in processes)
                {
                    string pName = p.ProcessName + ".exe";
                    if (RunningProcessList.Any(item => item.ProcessName.Equals(pName, StringComparison.OrdinalIgnoreCase))) continue;

                    string dName = pName;
                    try 
                    { 
                        string? desc = p.MainModule?.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrWhiteSpace(desc)) dName = desc;
                    } 
                    catch { }

                    RunningProcessList.Add(new ProcessItem
                    {
                        DisplayName = dName,
                        ProcessName = pName,
                        IsSelected = false
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void SearchRunningProcess_TextChanged(object sender, TextChangedEventArgs e)
        {
            string? filter = (sender as System.Windows.Controls.TextBox)?.Text;
            ICollectionView view = CollectionViewSource.GetDefaultView(RunningProcessList);
            if (view == null) return;

            if (string.IsNullOrWhiteSpace(filter))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = obj =>
                {
                    if (obj is ProcessItem item)
                    {
                        return item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                               item.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                };
            }
        }

        private void LoadSettings()
        {
            CheckMasterSwitch.IsChecked = ConfigManager.IsEffectEnabled;
            CheckAutoStart.IsChecked = ConfigManager.AutoStart;
            CheckStartSilent.IsChecked = ConfigManager.StartSilent;
            CheckTelemetry.IsChecked = ConfigManager.EnableTelemetry;
            CheckAlwaysTrailEffectSwitch.IsChecked = ConfigManager.EnableAlwaysTrailEffect;
            CheckEnvironmentFilter.IsChecked = ConfigManager.EnableEnvironmentFilter;
            CheckHideInFullscreen.IsChecked = ConfigManager.HideInFullscreen;
            CheckShowEffectOnDesktop.IsChecked = ConfigManager.ShowEffectOnDesktop;
            CheckRunAsAdmin.IsChecked = ConfigManager.RunAsAdmin; 
            CheckTouchscreenMode.IsChecked = ConfigManager.IsTouchscreenMode;

            int mode = ConfigManager.ClickTriggerType;
            if (mode == 1) RadioRightClick.IsChecked = true;
            else if (mode == 2) RadioBothClick.IsChecked = true;
            else RadioLeftClick.IsChecked = true;

            Profiles.Clear();
            foreach (var p in ConfigManager.GetProfiles()) Profiles.Add(p);

            var active = ConfigManager.GetActiveProfile();
            ComboProfiles.SelectedItem = active;

            UpdateColorPreview(ConfigManager.ParticleColor);
            UpdateStartSilentInterlock();
            UpdateEnvironmentFilterInterlock();

            SliderScale.Value = ConfigManager.EffectScale;
            SliderOpacity.Value = ConfigManager.EffectOpacity * 100;
            SliderSpeed.Value = ConfigManager.EffectSpeed;
            SliderTrailRefresh.Value = ConfigManager.TrailRefreshRate;
        }

        private void LoadScreenOptions()
        {
            var selectedIds = ConfigManager.GetEnabledScreenIds();

            ScreenOptions.Clear();
            var screens = Screen.AllScreens.OrderBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top).ToList();

            for (int i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                bool enabled = selectedIds.Count == 0 || selectedIds.Contains(screen.DeviceName);
                string title = $"显示器 {i + 1}" + (screen.Primary ? " (主显示器)" : string.Empty);
                string resolution = $"{screen.Bounds.Width} x {screen.Bounds.Height}";
                string detail = $"{GetScaleText(screen)}  ·  位置 ({screen.Bounds.Left}, {screen.Bounds.Top})";

                ScreenOptions.Add(new ScreenOptionItem
                {
                    DisplayIndex = i + 1,
                    Title = title,
                    ResolutionText = resolution,
                    DetailText = detail,
                    DeviceName = screen.DeviceName,
                    IsEnabled = enabled
                });
            }
        }

        private static string GetScaleText(Screen screen)
        {
            try
            {
                var center = new POINT
                {
                    X = screen.Bounds.Left + (screen.Bounds.Width / 2),
                    Y = screen.Bounds.Top + (screen.Bounds.Height / 2)
                };
                IntPtr monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
                if (monitor == IntPtr.Zero)
                {
                    return "缩放 100%";
                }

                int hr = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
                if (hr != 0 || dpiX == 0)
                {
                    return "缩放 100%";
                }

                int scale = (int)Math.Round(dpiX / 96.0 * 100);
                return $"缩放 {scale}%";
            }
            catch
            {
                return "缩放 100%";
            }
        }

        private void CheckAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStartSilentInterlock();
        }

        private void CheckRunAsAdmin_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (CheckRunAsAdmin.IsChecked == true)
            {
                StatusText.Text = "管理员模式将在保存并重启后生效";
                StatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void UpdateStartSilentInterlock()
        {
            bool autoStartEnabled = CheckAutoStart.IsChecked == true;
            CheckStartSilent.IsEnabled = autoStartEnabled;
        }

        private void EnvironmentFilterSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateEnvironmentFilterInterlock();
        }

        private void ProcessFilterMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (ComboProfiles.SelectedItem is FilterProfile active)
            {
                active.Mode = GetSelectedProcessFilterMode();
            }
            UpdateEnvironmentFilterInterlock();
        }

        private void UpdateEnvironmentFilterInterlock()
        {
            bool environmentFilterEnabled = CheckEnvironmentFilter.IsChecked == true;
            ProcessFilterModeOption selectedMode = GetSelectedProcessFilterMode();
            bool processFilterEnabled = environmentFilterEnabled && selectedMode != ProcessFilterModeOption.Disabled;

            CheckHideInFullscreen.IsEnabled = environmentFilterEnabled;
            CheckShowEffectOnDesktop.IsEnabled = environmentFilterEnabled;
            ComboProfiles.IsEnabled = environmentFilterEnabled;
            ComboProcessFilterMode.IsEnabled = environmentFilterEnabled;
            
            if (ListConfiguredProcesses != null)
            {
                ListConfiguredProcesses.IsEnabled = processFilterEnabled;
                ListConfiguredProcesses.Opacity = processFilterEnabled ? 1.0 : 0.65;
            }
            ManualProcessInput.IsEnabled = processFilterEnabled;
        }

        private void SelectProcessFilterMode(ProcessFilterModeOption mode)
        {
            ComboBoxItem? selectedItem = ComboProcessFilterMode.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), mode.ToString(), StringComparison.OrdinalIgnoreCase));

            ComboProcessFilterMode.SelectedItem = selectedItem ?? ComboProcessFilterMode.Items[0];
        }

        private ProcessFilterModeOption GetSelectedProcessFilterMode()
        {
            if (ComboProcessFilterMode.SelectedItem is ComboBoxItem item &&
                Enum.TryParse(item.Tag?.ToString(), true, out ProcessFilterModeOption mode))
            {
                return mode;
            }
            return ProcessFilterModeOption.Disabled;
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

        // --- 配置组管理 ---
        private void ComboProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboProfiles.SelectedItem is FilterProfile selected)
            {
                CurrentProfileProcesses.Clear();
                foreach (var p in selected.Processes) CurrentProfileProcesses.Add(p);
                SelectProcessFilterMode(selected.Mode);
                UpdateEnvironmentFilterInterlock();
            }
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var newProfile = new FilterProfile { Name = "新配置组 " + (Profiles.Count + 1) };
            Profiles.Add(newProfile);
            ComboProfiles.SelectedItem = newProfile;
        }

        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ComboProfiles.SelectedItem is FilterProfile active)
            {
                // 使用自定义输入框
                NewProfileNameInput.Text = active.Name;
                RenameProfileOverlay.Visibility = Visibility.Visible;
                NewProfileNameInput.Focus();
                NewProfileNameInput.SelectAll();
            }
        }

        private void ConfirmRenameProfile_Click(object sender, RoutedEventArgs e)
        {
            string newName = NewProfileNameInput.Text.Trim();
            
            if (!string.IsNullOrWhiteSpace(newName) && ComboProfiles.SelectedItem is FilterProfile active)
            {
                active.Name = newName;
                // 刷新 ComboBox 显示
                int index = Profiles.IndexOf(active);
                if (index != -1)
                {
                    Profiles.RemoveAt(index);
                    Profiles.Insert(index, active);
                    ComboProfiles.SelectedItem = active;
                }
            }
            RenameProfileOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseRenameOverlay_Click(object sender, RoutedEventArgs e)
        {
            RenameProfileOverlay.Visibility = Visibility.Collapsed;
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (Profiles.Count <= 1)
            {
                System.Windows.MessageBox.Show("至少需要保留一个配置组。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ComboProfiles.SelectedItem is FilterProfile active)
            {
                if (System.Windows.MessageBox.Show($"确定要删除 '{active.Name}' 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Profiles.Remove(active);
                    ComboProfiles.SelectedIndex = 0;
                }
            }
        }

        // --- 进程管理 ---
        private void RemoveProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string processName)
            {
                CurrentProfileProcesses.Remove(processName);
                if (ComboProfiles.SelectedItem is FilterProfile active)
                {
                    active.Processes.Remove(processName);
                }
            }
        }

        private void AddManualProcess_Click(object sender, RoutedEventArgs e)
        {
            string input = ManualProcessInput.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(input)) return;
            if (!input.EndsWith(".exe")) input += ".exe";

            AddProcessToActiveProfile(input);
            ManualProcessInput.Clear();
        }

        private void BrowseProcess_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe",
                Title = "选择应用进程"
            };

            if (dialog.ShowDialog() == true)
            {
                string fileName = System.IO.Path.GetFileName(dialog.FileName).ToLowerInvariant();
                AddProcessToActiveProfile(fileName);
            }
        }

        private void SelectRunningProcess_Click(object sender, RoutedEventArgs e)
        {
            RefreshRunningProcessList();
            RunningProcessOverlay.Visibility = Visibility.Visible;
        }

        private void CloseRunningProcessOverlay_Click(object sender, RoutedEventArgs e)
        {
            RunningProcessOverlay.Visibility = Visibility.Collapsed;
        }

        private void ConfirmAddRunningProcesses_Click(object sender, RoutedEventArgs e)
        {
            var selected = RunningProcessList.Where(p => p.IsSelected).Select(p => p.ProcessName).ToList();
            foreach (var p in selected)
            {
                AddProcessToActiveProfile(p);
            }
            RunningProcessOverlay.Visibility = Visibility.Collapsed;
        }

        private void AddProcessToActiveProfile(string processName)
        {
            if (ComboProfiles.SelectedItem is FilterProfile active)
            {
                if (!active.Processes.Contains(processName, StringComparer.OrdinalIgnoreCase))
                {
                    active.Processes.Add(processName);
                    CurrentProfileProcesses.Add(processName);
                }
                else
                {
                    // 已存在，不重复添加
                }
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            double effectScale = Math.Round(SliderScale.Value, 2);
            double effectOpacity = Math.Round(SliderOpacity.Value / 100.0, 2);
            double effectSpeed = Math.Round(SliderSpeed.Value, 2);
            int trailRefreshRate = (int)Math.Round(SliderTrailRefresh.Value);
            bool autoStartEnabled = CheckAutoStart.IsChecked ?? false;
            bool startSilentEnabled = CheckStartSilent.IsChecked ?? false;
            bool runAsAdminEnabled = CheckRunAsAdmin.IsChecked ?? false;
            bool isTouchscreenEnabled = CheckTouchscreenMode?.IsChecked ?? false;

            int clickType = 0;
            if (RadioRightClick.IsChecked == true) clickType = 1;
            else if (RadioBothClick.IsChecked == true) clickType = 2;
            
            // 保存配置组
            string activeId = (ComboProfiles.SelectedItem as FilterProfile)?.Id ?? "";
            ConfigManager.SaveProfiles(Profiles.ToList(), activeId);

            ConfigManager.Save("RunAsAdmin", runAsAdminEnabled);
            ConfigManager.Save("IsTouchscreenMode", isTouchscreenEnabled);
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
            ConfigManager.Save("EnableEnvironmentFilter", CheckEnvironmentFilter.IsChecked ?? false);
            ConfigManager.Save("HideInFullscreen", CheckHideInFullscreen.IsChecked ?? true);
            ConfigManager.Save("ShowEffectOnDesktop", CheckShowEffectOnDesktop.IsChecked ?? true);
            ConfigManager.Save("ClickTriggerType", clickType);

            var enabledScreenIds = ConfigManager.GetEnabledScreenIds();
            var selectedIds = ScreenOptions
                .Where(s => s.IsEnabled)
                .Select(s => s.DeviceName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedIds.Count == 0)
            {
                System.Windows.MessageBox.Show(this, "至少需要启用一个屏幕。", "多屏控制", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConfigManager.SaveEnabledScreenIds(selectedIds);

            App.SetAutoStart(ConfigManager.AutoStart);
            ApplyAutoStartSettings();

            App.Overlay?.UpdateColor(ConfigManager.ParticleColor);
            App.Overlay?.UpdateEffectSettings(effectScale, effectOpacity, effectSpeed);
            App.Overlay?.UpdateTrailRefreshRate(trailRefreshRate);
            App.Overlay?.RefreshEnvironmentFilterState();
            App.Overlay?.UpdateTouchMode(isTouchscreenEnabled);
            if (!enabledScreenIds.SetEquals(selectedIds))
            {
                App.Overlay?.RefreshScreenSelection();
            }

            bool isCurrentAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (runAsAdminEnabled && !isCurrentAdmin)
            {
                var res = System.Windows.MessageBox.Show(this,
                    "需要以管理员权限重启应用以应用设置，是否立即重启？", 
                    "需要权限", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (res == MessageBoxResult.Yes)
                {
                    RestartAsAdmin();
                    return;
                }
            }

            System.Windows.MessageBox.Show(this, "配置已成功应用！", "BASpark", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshScreenOptions_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            _ = e;
            var selectedIds = ScreenOptions.Where(s => s.IsEnabled).Select(s => s.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            LoadScreenOptions();
            foreach (var item in ScreenOptions)
            {
                item.IsEnabled = selectedIds.Count == 0 || selectedIds.Contains(item.DeviceName);
            }
            ListScreenOptions.Items.Refresh();
        }

        private void ApplyAutoStartSettings()
        {
            bool autoStart = CheckAutoStart.IsChecked == true;
            bool runAsAdmin = CheckRunAsAdmin.IsChecked == true;
            
            string? exePath = AutoStartManager.ResolveExecutablePath(
                Environment.ProcessPath,
                Process.GetCurrentProcess().MainModule?.FileName,
                Assembly.GetExecutingAssembly().Location,
                AppDomain.CurrentDomain.BaseDirectory);

            if (string.IsNullOrEmpty(exePath)) return;

            string regKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            AutoStartPlan plan = AutoStartManager.CreatePlan(autoStart, runAsAdmin);
            
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(regKeyPath, true))
                {
                    if (plan.RegistryRunEnabled)
                    {
                        key?.SetValue(AutoStartManager.RunValueName, AutoStartManager.BuildRunCommand(exePath));
                    }
                    else
                    {
                        key?.DeleteValue(AutoStartManager.RunValueName, false);
                    }

                    ManageTaskScheduler(AutoStartManager.TaskName, exePath, plan.ScheduledTaskEnabled);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("应用高权限自启失败: " + ex.Message);
            }
        }

        private void ManageTaskScheduler(string taskName, string exePath, bool create)
        {
            try
            {
                string arguments = create 
                    ? $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\" --autostart\" /sc onlogon /rl highest /f" 
                    : $"/delete /tn \"{taskName}\" /f";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) ? "" : "runas"
                };
                
                using (Process? process = Process.Start(startInfo))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("任务计划程序配置失败: " + ex.Message);
            }
        }

        private void RestartAsAdmin()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BASpark.exe");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = ConfigManager.StartSilent ? "/silent" : ""
                };
                
                Process.Start(startInfo);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("提权重启失败，请尝试手动右键以管理员身份运行。\n错误信息: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            base.OnClosing(e);
        }

        private void EffectSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
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
