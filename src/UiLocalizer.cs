using System.Windows;
using System.Windows.Controls;

namespace BASpark
{
    internal static class UiLocalizer
    {
        public static void ApplyPrivacy(PrivacyWindow window)
        {
            window.Title = Localization.Get("Privacy_Title");
            if (window.TxtTagline != null)
                window.TxtTagline.Text = Localization.Get("Privacy_Tagline");
            if (window.TxtIntro != null)
                window.TxtIntro.Text = Localization.Get("Privacy_Intro");
            if (window.TxtOpenSourceTitle != null)
                window.TxtOpenSourceTitle.Text = Localization.Get("Privacy_OpenSource_Title");
            if (window.TxtOpenSourceBody != null)
                window.TxtOpenSourceBody.Text = Localization.Get("Privacy_OpenSource_Body");
            if (window.TxtSecurityTitle != null)
                window.TxtSecurityTitle.Text = Localization.Get("Privacy_Security_Title");
            if (window.TxtSecurityBody != null)
                window.TxtSecurityBody.Text = Localization.Get("Privacy_Security_Body");
            if (window.TxtPrivacyTitle != null)
                window.TxtPrivacyTitle.Text = Localization.Get("Privacy_Privacy_Title");
            if (window.TxtPrivacyBody != null)
                window.TxtPrivacyBody.Text = Localization.Get("Privacy_Privacy_Body");
            if (window.TxtTelemetry != null)
                window.TxtTelemetry.Text = Localization.Get("Privacy_Telemetry");
            if (window.BtnRefuse != null)
                window.BtnRefuse.Content = Localization.Get("Privacy_Refuse");
            if (window.BtnAgree != null)
                window.BtnAgree.Content = Localization.Get("Privacy_Agree");
        }

        public static void ApplyControlPanel(ControlPanelWindow w)
        {
            w.Title = Localization.Get("App_Title_ControlPanel");

            w.TabWelcome.Content = Localization.Get("Nav_Home");
            w.TabSettings.Content = Localization.Get("Nav_Settings");
            w.SubTabBasicLabel.Text = Localization.Get("Nav_Basic");
            w.SubTabVisualLabel.Text = Localization.Get("Nav_Visual");
            w.SubTabFilterLabel.Text = Localization.Get("Nav_Filter");
            w.SubTabMultiScreenLabel.Text = Localization.Get("Nav_MultiScreen");
            w.SubTabMoreLabel.Text = Localization.Get("Nav_More");
            w.TabLog.Content = Localization.Get("Nav_Log");
            w.TabAbout.Content = Localization.Get("Nav_About");
            w.TxtSidebarCopyright.Text = Localization.Get("Sidebar_Copyright");

            w.TxtWelcomeTitle.Text = Localization.Get("Welcome_Title");
            w.TxtWelcomeSubtitle.Text = Localization.Get("Welcome_Subtitle");
            w.TxtStatsTitle.Text = Localization.Get("Welcome_StatsTitle");
            w.TxtStatusLabel.Text = Localization.Get("Welcome_StatusLabel");
            w.TxtClicksLabel.Text = Localization.Get("Welcome_ClicksLabel");
            w.NoticeTitle.Text = Localization.Get("Welcome_NoticeLoading");

            w.TxtSettingsTitle.Text = Localization.Get("Settings_Title");
            w.TxtSettingsHint.Text = Localization.Get("Settings_ApplyHint");
            w.BtnApplySettings.Content = Localization.Get("Settings_Apply");

            w.TxtBasicTitle.Text = Localization.Get("Basic_Title");
            w.TxtBasicLanguage.Text = Localization.Get("Basic_Language");
            w.TxtBasicNetworkRegion.Text = Localization.Get("Basic_NetworkRegion");
            w.RadioNetworkRegionAuto.Content = Localization.Get("Basic_NetworkRegionAuto");
            w.RadioNetworkRegionChina.Content = Localization.Get("Basic_NetworkRegionChina");
            w.RadioNetworkRegionGlobal.Content = Localization.Get("Basic_NetworkRegionGlobal");
            w.TxtNetworkRegionHint.Text = Localization.Get("Basic_NetworkRegionHint");
            w.TxtScrollbarVisibility.Text = Localization.Get("Basic_ScrollbarVisibility");
            w.RadioScrollbarAlways.Content = Localization.Get("Basic_ScrollbarAlways");
            w.RadioScrollbarOnScroll.Content = Localization.Get("Basic_ScrollbarOnScroll");
            w.TxtScrollbarHint.Text = Localization.Get("Basic_ScrollbarHint");
            w.CheckMasterSwitch.Content = Localization.Get("Basic_MasterSwitch");
            w.CheckAlwaysTrailEffectSwitch.Content = Localization.Get("Basic_TrailSwitch");
            w.TxtClickType.Text = Localization.Get("Basic_ClickType");
            w.RadioLeftClick.Content = Localization.Get("Basic_LeftClick");
            w.RadioRightClick.Content = Localization.Get("Basic_RightClick");
            w.RadioBothClick.Content = Localization.Get("Basic_BothClick");
            w.CheckMiddleClickTrigger.Content = Localization.Get("Basic_MiddleClick");
            w.CheckScreenshotCompatibilityMode.Content = Localization.Get("Basic_ScreenshotMode");
            w.TxtScreenshotHint.Text = Localization.Get("Basic_ScreenshotHint");
            w.CheckAutoStart.Content = Localization.Get("Basic_AutoStart");
            w.CheckStartSilent.Content = Localization.Get("Basic_StartSilent");
            w.CheckRunAsAdmin.Content = Localization.Get("Basic_RunAsAdmin");
            w.TxtRunAsAdminHint.Text = Localization.Get("Basic_RunAsAdminHint");
            w.CheckTouchscreenMode.Content = Localization.Get("Basic_Touchscreen");
            w.TxtTouchscreenHint.Text = Localization.Get("Basic_TouchscreenHint");
            w.CheckTelemetry.Content = Localization.Get("Basic_Telemetry");

            w.TxtVisualTitle.Text = Localization.Get("Visual_Title");
            w.BtnVisualReset.Content = Localization.Get("Visual_ResetDefaults");
            w.TxtVisualInputHint.Text = Localization.Get("Visual_InputHint");
            w.TxtVisualScale.Text = Localization.Get("Visual_Scale");
            w.TxtVisualOpacity.Text = Localization.Get("Visual_Opacity");
            w.CheckLinkedAnimationSpeed.Content = Localization.Get("Visual_LinkedSpeed");
            w.TxtLinkedSpeedHint.Text = Localization.Get("Visual_LinkedSpeedHint");
            w.TxtAnimSpeed.Text = Localization.Get("Visual_AnimSpeed");
            w.TxtTrailAnimSpeed.Text = Localization.Get("Visual_TrailAnimSpeed");
            w.TxtClickAnimSpeed.Text = Localization.Get("Visual_ClickAnimSpeed");
            w.TxtTrailRefresh.Text = Localization.Get("Visual_TrailRefresh");
            w.TxtEffectColor.Text = Localization.Get("Visual_Color");
            w.BtnPickColor.Content = Localization.Get("Visual_ChangeColor");

            w.TxtFilterTitle.Text = Localization.Get("Filter_Title");
            w.CheckEnvironmentFilter.Content = Localization.Get("Filter_Enable");
            w.CheckHideInFullscreen.Content = Localization.Get("Filter_Fullscreen");
            w.CheckShowEffectOnDesktop.Content = Localization.Get("Filter_Desktop");
            w.TxtFilterProfiles.Text = Localization.Get("Filter_Profiles");
            w.BtnDeleteProfile.Content = Localization.Get("Filter_Delete");
            w.BtnRenameProfile.Content = Localization.Get("Filter_Rename");
            w.BtnAddProfile.Content = Localization.Get("Filter_New");
            w.TxtFilterMode.Text = Localization.Get("Filter_Mode");
            w.TxtProcessList.Text = Localization.Get("Filter_ProcessList");
            w.TxtAddProcess.Text = Localization.Get("Filter_AddProcess");
            w.BtnAddProcess.Content = Localization.Get("Filter_Add");
            w.BtnBrowseProcess.Content = Localization.Get("Filter_Browse");
            w.BtnSelectRunningProcess.Content = Localization.Get("Filter_SelectRunning");

            SetComboFilterModes(w);
            w.PopulateLanguageCombo();

            w.TxtMultiScreenTitle.Text = Localization.Get("MultiScreen_Title");
            w.BtnRefreshScreens.Content = Localization.Get("MultiScreen_Refresh");
            w.TxtMultiScreenHint.Text = Localization.Get("MultiScreen_Hint");

            w.TxtMoreTitle.Text = Localization.Get("More_Title");
            w.TxtSidebarBackground.Text = Localization.Get("More_SidebarBackground");
            w.BtnBrowseSidebarBackground.Content = Localization.Get("More_Browse");
            w.BtnClearSidebarBackground.Content = Localization.Get("More_Clear");
            w.TxtSidebarBackgroundHint.Text = Localization.Get("More_SidebarBackgroundHint");

            w.TxtLogTitle.Text = Localization.Get("Log_Title");
            w.TxtLogHint.Text = Localization.Get("Log_Hint");
            w.BtnClearLog.Content = Localization.Get("Log_Clear");

            w.TxtAboutTitle.Text = Localization.Get("About_Title");
            w.BtnCheckUpdate.Content = Localization.Get("About_CheckUpdate");
            w.TxtAboutDescription.Text = Localization.Get("About_Description");
            w.TxtSecurityWarning.Text = Localization.Get("About_SecurityWarning");
            w.TxtAboutSupport.Text = Localization.Get("About_Support");
            w.BtnOfficialSite.Content = Localization.Get("About_OfficialSite");
            w.BtnGithub.Content = Localization.Get("About_Github");
            w.BtnBilibili.Content = Localization.Get("About_Bilibili");
            w.BtnQQ.Content = Localization.Get("About_QQ");
            w.BtnSponsor.Content = Localization.Get("About_Sponsor");
            w.BtnDiscord.Content = Localization.Get("About_Discord");
            w.TxtDevOptions.Text = Localization.Get("About_DevOptions");
            w.BtnResetAll.Content = Localization.Get("About_ResetAll");

            w.TxtOverlayRunning.Text = Localization.Get("Overlay_RunningProcess");
            w.BtnOverlayCancel.Content = Localization.Get("Overlay_Cancel");
            w.BtnOverlayConfirmAdd.Content = Localization.Get("Overlay_ConfirmAdd");
            w.TxtOverlayVisualReset.Text = Localization.Get("Overlay_VisualReset");
            w.BtnOverlayVisualCancel.Content = Localization.Get("Overlay_Cancel");
            w.BtnOverlayVisualConfirm.Content = Localization.Get("Overlay_ConfirmReset");
            w.TxtOverlayRename.Text = Localization.Get("Overlay_RenameProfile");
            w.TxtOverlayRenamePrompt.Text = Localization.Get("Overlay_RenamePrompt");
            w.BtnOverlayRenameCancel.Content = Localization.Get("Overlay_Cancel");
            w.BtnOverlayRenameConfirm.Content = Localization.Get("Overlay_ConfirmRename");

            w.ApplyAboutLinkVisibility();
            w.RefreshScreenEnableLabels();
        }

        private static void SetComboFilterModes(ControlPanelWindow w)
        {
            if (w.ComboProcessFilterMode.Items.Count >= 3)
            {
                ((ComboBoxItem)w.ComboProcessFilterMode.Items[0]).Content = Localization.Get("Filter_Mode_Disabled");
                ((ComboBoxItem)w.ComboProcessFilterMode.Items[1]).Content = Localization.Get("Filter_Mode_Blacklist");
                ((ComboBoxItem)w.ComboProcessFilterMode.Items[2]).Content = Localization.Get("Filter_Mode_Whitelist");
            }
        }
    }
}
