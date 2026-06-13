using System;
using System.Reflection;
using System.Windows;

namespace BASpark
{
    public partial class PrivacyWindow : Window
    {
        public PrivacyWindow() 
        { 
            InitializeComponent(); 
            LoadVersion();
        }

        private void LoadVersion()
        {
            try
            {
                Version? version = Assembly.GetExecutingAssembly().GetName().Version;
                
                if (version != null)
                {
                    VersionText.Text = $"{version.Major}.{version.Minor}.{version.Build}-release";
                }
            }
            catch
            {
                VersionText.Text = Localization.Get("Privacy_VersionFailed");
            }
        }

        private void BtnAgree_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.Save("AgreedToPrivacy", true);
            ConfigManager.Save("EnableTelemetry", CheckTelemetry.IsChecked ?? false);
            
            this.DialogResult = true;
            this.Close();
        }

        private void BtnRefuse_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}