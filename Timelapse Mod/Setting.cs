using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using CameraTimelapseMod.Systems;
using CameraTimelapseMod.Util;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod
{
    [FileLocation(nameof(CameraTimelapseMod))]
    [SettingsUIGroupOrder(
        kGeneralGeneralGroup, kGeneralAboutGroup, kDebugGeneralGroup,
        kSavesGeneralGroup, kSavesFilterGroup,
        kAutoGeneralGroup, kAutoFilterGroup,
        kVideoGeneralGroup, kVideoObsGroup, kCartoGeneralGroup
    )]
        [SettingsUIShowGroupName(
        kGeneralGeneralGroup, kGeneralAboutGroup,
        kSavesGeneralGroup, kSavesFilterGroup,
        kAutoGeneralGroup,
        kVideoGeneralGroup, kVideoObsGroup
    )]
    [SettingsUITabOrder(kGeneralGroup, kAutoGroup, kSavesGroup, kVideoGroup, kCartoGroup)]
    public class Setting : ModSetting
    {

        public const string kGeneralGroup = "General";
        public const string kAutoGroup = "Auto Timelapse Mod";
        public const string kSavesGroup = "Timelapse from saves Mod";


        public const string kGeneralGeneralGroup = "GeneralGeneral";
        public const string kGeneralAboutGroup = "GeneralAbout";
        public const string kAutoGeneralGroup = "AutoGeneral";
        public const string kAutoFilterGroup = "AutoFilter";
        public const string kSavesGeneralGroup = "SavesGeneral";
        public const string kSavesFilterGroup = "SavesFilter";

        public const string kVideoGroup = "Video";
        public const string kVideoGeneralGroup = "VideoGeneral";
        public const string kVideoObsGroup = "VideoObs";

        public const string kCartoGroup = "Carto";

        public const string kDebugGroup = "Debug";
        public const string kDebugGeneralGroup = "Debug actions";

        public const string kCartoGeneralGroup = "CartoGeneral";

        public enum DebugAction
        {
            None = 0,
            Auto_ListDistricts,
            Obs_RecordTest5s,
            Obs_SetTestRecordDir,

            // Cinematics
            Cin_ListAvailable,
            Cin_PlayFirstConfigured,

            // Camera
            Cam_ApplyFirstPreset,
            Cam_DumpPhotoProperties,

            // Time / weather
            Tw_SetTime12,
            Tw_SetTime22,
            Tw_ClearWeatherOnly,
            Tw_Restore,

            // Auto timelapse mechanics
            Auto_CountEdges,
            Auto_CountEdgesPerDistrict,
            Auto_Destroy1Road,
            Auto_Destroy5Roads,
            Auto_MarkConstruction10,
            Auto_MoveCameraToRecent,

            // Saves
            Saves_ListFiltered,

            // Capture
            Cap_ScreenshotNow,

            // Carto
            Carto_CheckAvailable,
            Carto_TriggerExport,

            // Lifecycle
            Life_RestartGame,
            Life_StopWatchdog,
            Life_QuitGame,
            Life_StartWatchdog,

        }

        public enum ClearWeatherMode
        {
            Off = 0,
            AlwaysForce,
            ForceExceptCamModes,
        }


        public Setting(IMod mod) : base(mod) { }

        public enum ShutdownMode { None, ExitGame, ShutdownComputer }
        public enum SortOrder { DescendingDate, AscendingDate }

        public enum CaptureQuality
        {
            ScreenResolution,
            FullHD_1920x1080,
            QHD_2560x1440,
            UHD_4K_3840x2160,
            UHD_8K_7680x4320
        }
        public enum SimulationSpeed
        {
            Pause = 0,
            Slow_x1 = 1,
            Normal_x2 = 2,
            Fast_x3 = 3
        }

        // ----- GENERAL - GENERAL -----

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public bool StartSessionOnCurrentSave { set { SessionSystem.RequestStartCurrent(); } }

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public bool CaptureCurrentAsPreset { set { PresetsSystem.AddFromCurrentCamera(); } }

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public bool OpenPresetPanel { set { UISystem.RequestOpenPanel(); } }


        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public bool OpenScreenshotFolder { set { Tools.OpenScreenshotFolder(); } }

        [SettingsUIDirectoryPicker]
        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public string ScreenshotFolderOverride { get; set; } = "";



        [SettingsUIMultilineText]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public string kGeneralAbout => string.Empty;

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public bool OpenForumButton
        {
            set { Tools.OpenUrl(Mod.forumLink); }
        }




        [SettingsUIMultilineText]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public string kGeneralAboutComment => Mod.sorryForThisCommentAhah;

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public bool SendFeedbackButton
        {
            set { Tools.OpenUrl("mailto:"+Mod.getEmail()+ "?subject=Timelapse%20Mod%20Feedback"); }
        }

        [SettingsUIMultilineText]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public string kGeneralAboutCommentCompany => Mod.companyText;

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public bool kGeneralAboutCommentCompanyLink
        {
            set { Tools.OpenUrl(Mod.companyLink); }
        }

        [SettingsUIButton]
        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public bool kGeneralAboutCommentCompanyFollowLink
        {
            set { Tools.OpenUrl(Mod.companyFollowLink); }
        }


        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        public DebugAction DebugSelectedAction { get; set; } = DebugAction.None;

        [SettingsUISection(kGeneralGroup, kGeneralAboutGroup)]
        [SettingsUIButton]
        public bool RunDebugAction
        {
            set => DebugTools.Run(DebugSelectedAction);
        }




        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        [SettingsUISlider(min = 0, max = 120, step = 1, unit = Unit.kInteger)]
        public int ReminderMinutes { get; set; } = 0;

        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public CaptureQuality Quality { get; set; } = CaptureQuality.QHD_2560x1440;

        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        [SettingsUITextInput]
        public string CaptureTimes { get; set; } = "12.0";

        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public ClearWeatherMode ForceClearWeatherMode { get; set; } = ClearWeatherMode.AlwaysForce;

        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public bool HideUIInScreenshots { get; set; } = true;

        [SettingsUISection(kGeneralGroup, kGeneralGeneralGroup)]
        public ShutdownMode ShutdownAfterSession { get; set; } = ShutdownMode.None;


        // ----- All Saves - GENERAL -----

        [SettingsUIMultilineText]
        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public string kSavesInfo => string.Empty;



        [SettingsUIButton]
        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public bool StartSessionAllSaves { set { SessionSystem.RequestStartAll(); } }

        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public bool SavesModPreviewConstruction { get; set; } = true;

        [SettingsUITextInput]
        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public string SavesModRoadsToDeletePerClick { get; set; } = "1";

        [SettingsUISlider(min = 0, max = 300, step = 1, scalarMultiplier = 1, unit = "integer")]
        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public int SavesModPlayWaitSeconds { get; set; } = 0;

        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public bool AutoRestartOnCrash { get; set; } = false;

        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        public bool ReturnToMenuBetweenSaves { get; set; } = false;

        [SettingsUISection(kSavesGroup, kSavesGeneralGroup)]
        [SettingsUISlider(min = 0, max = 50, step = 1, unit = Unit.kInteger)]
        public int RestartGameEveryNSaves { get; set; } = 0;



        // ----- All Saves - Filter -----

        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        [SettingsUITextInput]
        public string SavePrefix { get; set; } = "";

        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        [SettingsUITextInput]
        public string CityNameFilter { get; set; } = "";

        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        public SortOrder SaveSortOrder { get; set; } = SortOrder.DescendingDate;

        [SettingsUITextInput]
        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        public string SkipBetweenSaves { get; set; } = "0";

        public int SkipBetweenSavesInt
        {
            get
            {
                if (int.TryParse(SkipBetweenSaves, out int v))
                    return Mathf.Clamp(v, 0, 10000);
                return 0;
            }
        }

        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        [SettingsUITextInput]
        public string ResumeFromSaveName { get; set; } = "";

        [SettingsUISection(kSavesGroup, kSavesFilterGroup)]
        [SettingsUISlider(min = 0, max = 500, step = 1, unit = Unit.kInteger)]
        public int MaxSaves { get; set; } = 0;


        // ----- Auto - GENERAL -----

        [SettingsUIMultilineText]
        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        public string kAutoInfo => string.Empty;

        [SettingsUIButton]
        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        public bool StartAutoTimelapseButton
        {
            set
            {
                Systems.AutoTimelapseSessionSystem.RequestStartFromSettings();
            }
        }

        [SettingsUITextInput]
        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        public string AutoModRoadsToDeletePerClick { get; set; } = "1";

        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        public bool AutoModPreviewConstruction { get; set; } = true;

        [SettingsUISlider(min = 0, max = 300, step = 1, scalarMultiplier = 1, unit = "integer")]
        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        public int AutoModPlayWaitSeconds { get; set; } = 3;

        [SettingsUISection(kAutoGroup, kAutoGeneralGroup)]
        [SettingsUITextInput]
        public string AutoModDistrictFilter { get; set; } = "";

        // ----- Auto - Filter -----

        [SettingsUIButton]
        [SettingsUISection(kAutoGroup, kAutoFilterGroup)]
        public bool AutoModRoadsToDeleteBeforeStarting
        {
            set
            {
                int n = AutoModRoadsToDeletePerClickInt;
                GameTools.deleteRecentRoadsNow(n);
            }
        }


        // ----- Video - GENERAL -----

        [SettingsUIMultilineText]
        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public string kVideoInfo => string.Empty;


        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public bool VideoRecordingEnabled { get; set; } = false;

        [SettingsUIButton]
        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public bool OpenVideosFolder { set { Tools.OpenVideosFolder(); } }

        [SettingsUIDirectoryPicker]
        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public string VideoFolderOverride { get; set; } = "";


        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public SimulationSpeed VideoSimulationSpeed { get; set; } = SimulationSpeed.Normal_x2;

        [SettingsUISlider(min = 0, max = 60, step = 1, unit = Unit.kInteger)]
        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public int VideoRecordSeconds { get; set; } = 5;

        [SettingsUITextInput]
        [SettingsUISection(kVideoGroup, kVideoGeneralGroup)]
        public string CinematicsToRecord { get; set; } = "";


        // ----- Video - OBS -----

        [SettingsUISection(kVideoGroup, kVideoObsGroup)]
        [SettingsUITextInput]
        public string ObsHost { get; set; } = "localhost";

        [SettingsUISection(kVideoGroup, kVideoObsGroup)]
        [SettingsUITextInput]
        public string ObsPort { get; set; } = "4455";

        [SettingsUISection(kVideoGroup, kVideoObsGroup)]
        [SettingsUITextInput]
        public string ObsPassword { get; set; } = "";

        [SettingsUIButton]
        [SettingsUISection(kVideoGroup, kVideoObsGroup)]
        public bool TestObsConnectionButton
        {
            set
            {
                CoroutineSystem.Instance.StartCoroutine(
                    TestObsConnectionCoroutine());
            }
        }

        private static System.Collections.IEnumerator TestObsConnectionCoroutine()
        {
            var task = Systems.ObsClientSystem.TestConnection();
            yield return new UnityEngine.WaitUntil(() => task.IsCompleted);
        }


        // ----- Carto - GENERAL -----

        [SettingsUIMultilineText]
        [SettingsUISection(kCartoGroup, kCartoGeneralGroup)]
        public string kCartoInfo => string.Empty;

        [SettingsUISection(kCartoGroup, kCartoGeneralGroup)]
        public bool TriggerCartoExport { get; set; } = false;




        public int ObsPortInt
        {
            get
            {
                if (int.TryParse(ObsPort, out int n) && n > 0 && n <= 65535)
                    return n;
                return 4455;
            }
        }


        public int SavesModRoadsToDeletePerClickInt
        {
            get
            {
                if (int.TryParse(SavesModRoadsToDeletePerClick, out int n) && n > 0 && n <= 5000)
                    return n;
                return 10;
            }
        }

        public int AutoModRoadsToDeletePerClickInt
        {
            get
            {
                if (int.TryParse(AutoModRoadsToDeletePerClick, out int n) && n > 0 && n <= 5000)
                    return n;
                return 10;
            }
        }



        public override void SetDefaults() { }
    }
}