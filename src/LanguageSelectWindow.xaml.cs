using System.Windows;

namespace BASpark
{
    public partial class LanguageSelectWindow : Window
    {
        private readonly string _displayCulture;

        public LanguageSelectWindow()
        {
            _displayCulture = Localization.DetectCultureFromSystem();
            InitializeComponent();
            ApplyDisplayLanguage();
            SelectDefaultRadio();
        }

        private void ApplyDisplayLanguage()
        {
            Title = Localization.Get("LangSelect_Title", _displayCulture);
            TxtTitle.Text = Localization.Get("LangSelect_Title", _displayCulture);
            TxtSubtitle.Text = Localization.Get("LangSelect_Subtitle", _displayCulture);
            RadioChinese.Content = Localization.Get("LangSelect_Chinese", _displayCulture);
            RadioEnglish.Content = Localization.Get("LangSelect_English", _displayCulture);
            RadioJapanese.Content = Localization.Get("LangSelect_Japanese", _displayCulture);
            BtnContinue.Content = Localization.Get("LangSelect_Continue", _displayCulture);
        }

        private void SelectDefaultRadio()
        {
            switch (_displayCulture)
            {
                case Localization.CultureJa:
                    RadioJapanese.IsChecked = true;
                    break;
                case Localization.CultureEn:
                    RadioEnglish.IsChecked = true;
                    break;
                default:
                    RadioChinese.IsChecked = true;
                    break;
            }
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            string selected = Localization.CultureZhCn;
            if (RadioEnglish.IsChecked == true)
            {
                selected = Localization.CultureEn;
            }
            else if (RadioJapanese.IsChecked == true)
            {
                selected = Localization.CultureJa;
            }

            ConfigManager.Save("UiLanguage", selected);
            Localization.ApplyCulture(selected);
            DialogResult = true;
            Close();
        }
    }
}
