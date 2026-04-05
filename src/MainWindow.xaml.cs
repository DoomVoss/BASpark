using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Gma.System.MouseKeyHook;

namespace BASpark
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private IKeyboardMouseEvents? _globalHook;
        private IntPtr _hwnd;

        private long _lastMoveTicks = 0;
        private long _lastClickTicks = 0;
        
        private const long MoveIntervalTicks = 250000; 
        private const long ClickIntervalTicks = 300000;

        public MainWindow()
        {
            InitializeComponent();
            webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            _ = InitWebView();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);

            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            SetupGlobalHooks();
        }

        public void UpdateColor(string color)
        {
            if (webView?.CoreWebView2 != null)
                _ = webView.CoreWebView2.ExecuteScriptAsync($"if(window.updateColor) window.updateColor('{color}');");
        }

        private async System.Threading.Tasks.Task InitWebView()
        {
            try 
            {
                var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions(
                    "--disable-background-timer-throttling --disable-features=CalculateNativeWinOcclusion --enable-begin-frame-scheduling"
                );

                string userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "BASpark_WebView2");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Web/index.html"));
                if (streamInfo != null)
                {
                    using var reader = new System.IO.StreamReader(streamInfo.Stream);
                    string htmlContent = reader.ReadToEnd();
                    webView.CoreWebView2.NavigateToString(htmlContent);
                    webView.CoreWebView2.NavigationCompleted += (s, e) => {
                        UpdateColor(ConfigManager.ParticleColor);
                    };
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("WebView2 初始化失败: " + ex.Message);
            }
        }

        private void SetupGlobalHooks()
        {
            _globalHook = Hook.GlobalEvents();

            _globalHook.MouseDownExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ConfigManager.TotalClicks++; 

                    long currentTicks = DateTime.Now.Ticks;
                    if (currentTicks - _lastClickTicks < ClickIntervalTicks) return;
                    _lastClickTicks = currentTicks;

                    _ = webView.CoreWebView2.ExecuteScriptAsync($"if(window.externalBoom) window.externalBoom({e.X}, {e.Y});");
                }
            };

            _globalHook.MouseMoveExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;
                
                long currentTicks = DateTime.Now.Ticks;
                if (currentTicks - _lastMoveTicks < MoveIntervalTicks) return;
                _lastMoveTicks = currentTicks;

                _ = webView.CoreWebView2.ExecuteScriptAsync($"if(window.externalMove) window.externalMove({e.X}, {e.Y});");
            };

            _globalHook.MouseUpExt += (s, e) => {
                if (!ConfigManager.IsEffectEnabled || webView?.CoreWebView2 == null) return;
                _ = webView.CoreWebView2.ExecuteScriptAsync($"if(window.externalUp) window.externalUp();");
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            _globalHook?.Dispose();
            ConfigManager.Save("TotalClicks", ConfigManager.TotalClicks);
            base.OnClosed(e);
        }
    }
}
