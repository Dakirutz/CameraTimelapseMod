using System.Collections.Generic;
using Colossal;
using Game.Tools;

namespace CameraTimelapseMod
{
    public class LocaleEN : IDictionarySource
    {
        private readonly Setting _setting;
        public LocaleEN(Setting setting) { _setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // ===== Mod root =====
                { _setting.GetSettingsLocaleID(), "Auto Timelapse & Camera Presets" },

                // ===== Tabs =====
                { _setting.GetOptionTabLocaleID(Setting.kGeneralGroup), "General" },
                { _setting.GetOptionTabLocaleID(Setting.kSavesGroup), "Timelapse from saves" },
                { _setting.GetOptionTabLocaleID(Setting.kAutoGroup), "Auto Timelapse" },

                // ===== Sub-groups =====
                { _setting.GetOptionGroupLocaleID(Setting.kGeneralGeneralGroup), "General" },
                { _setting.GetOptionGroupLocaleID(Setting.kGeneralAboutGroup), "About" },
                { _setting.GetOptionGroupLocaleID(Setting.kSavesGeneralGroup), "General" },
                { _setting.GetOptionGroupLocaleID(Setting.kSavesFilterGroup), "Filter" },
                { _setting.GetOptionGroupLocaleID(Setting.kAutoGeneralGroup), "General" },
                { _setting.GetOptionGroupLocaleID(Setting.kAutoFilterGroup), "Filter" },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.kGeneralAboutComment)), Mod.sorryForThisCommentAhah },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.kGeneralAboutCommentCompany)), Mod.companyText },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.kGeneralAboutCommentCompanyLink)),
                  "Rate my skills" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.kGeneralAboutCommentCompanyFollowLink)),
                   "Follow my startup" },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.kGeneralAbout)),
                  "I hope you will enjoy this mod as much as I had fun doing it! Please if " +
                  "you use it, quote it's link or name in your work, so other can enjoy it too, " +
                  "otherwise I may just loose interest and not maintain it if I see people using " +
                  "it without credits :P It's also open source so feel free to see it's github if needed. \n \n" +
                  "No need for a coffee, but if you want to help me too, I would love you to contact me if you know anyone working in" +
                  "a public transit company in the administratif part, it's for a project :) Thanks ! " },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.kSavesInfo)),
                  "You have 100+ saves of your same city ? Then this mod is made for you, it will go automatically in each of your" +
                  "saves and make the screenshots, just configure it as needed. \n\n " +
                  "WARNING: this mod open all of your saves matching the filters, if you have other mods or very old saves it is not" +
                  " sure that it will not crash or that another mod will not corrupt your old save file when they are being opened" +
                  ", thus, please, copy all your saves before in another directory to prevent any problem. Thanks!" },



                { _setting.GetOptionLabelLocaleID(nameof(Setting.kAutoInfo)),
                  "This destructive mode creates a reverse timelapse of your city by progressively demolishing it in reverse build order, " +
                  "taking screenshots from all your camera presets at each step. The mod identifies the most recent roads/rails, destroys them" +
                  " along with adjacent buildings, optionally marks the next batch as construction sites, lets the simulation run for a few seconds" +
                  " (so cranes and traffic appear), then captures screenshots at every configured hour. The result is a sequence of images you can " +
                  "reverse to obtain a true historic timelapse of how your city was built. \n\n " +
                  "WARNING: this destroys your city in the current opened game save, make a separate save copy first and DO NOT save the game after running this. " +
                  "Just reload your save to continue playing normally." },



                { _setting.GetOptionLabelLocaleID(nameof(Setting.kVideoInfo)),
                  "This is optional and let you use OBS automatically to record short videos of your views " +
                  "while also doing the screenshots. It will try to tell OBS to put the videos in the screenshot folder of the mod." +
                  "You should download OBS first and configure it the way you want it" +
                  "to record your screen, then configure it for CS:2 here and, when you start a auto timelapse session or " +
                  "timelapse from saves session you will see that OBS will also record each views." +
                  "OBS is a free software made for recording your screen (check on google) \n\n" +
                  "WARNING: OBS record the screen and computer sound. If your computer shows notifications on top of the game " +
                  "it will also be recorded, put your computer in do not disturb mod to avoid this. Also disable OBS notifications" +
                  "otherwise it will show a socket login message everytime, but reactivate notification after the session. Also disable the radio/music " +
                  "in the game if you don't want random music while your record." },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.kCartoInfo)),
                  "When enabled, Carto's Export will be called automatically at each captured save (Saves mode) " +
                  "or each step (Auto mode), producing GeoJSON/Shapefile/GeoTIFF files in Carto's output folder.\n\n" +
                  "Note: Carto will use your Carto configuration and export in your Carto usual folder, also if you" +
                  "have a lot of saves maybe skip some of them to not wait 3 days, or with auto timelapse delete more" +
                  "than one road on each step. \n\n Note: Carto exports vector data — to generate PNG maps you'll need to render those files with " +
                  "QGIS or another GIS tool. See the Carto documentation for tutorials. \n\n Warning: in Carto config, remove confirm dialog and confirm sound, also personalize the file name to include the {Now} at least, right now the limitation is that carto will run max one time a minute." },




                // ===== Capture =====
                { _setting.GetOptionLabelLocaleID(nameof(Setting.Quality)),
                  "Screenshot quality" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.Quality)),
                  "Resolution at which screenshots are rendered. Higher = sharper images, larger files, more VRAM. " +
                  "QHD is recommended for most setups. 4K and 8K require a strong GPU (6+ GB VRAM)." },

                { _setting.GetEnumValueLocaleID(Setting.CaptureQuality.ScreenResolution),
                  "Screen resolution (current)" },
                { _setting.GetEnumValueLocaleID(Setting.CaptureQuality.FullHD_1920x1080),
                  "Full HD - 1920×1080" },
                { _setting.GetEnumValueLocaleID(Setting.CaptureQuality.QHD_2560x1440),
                  "QHD - 2560×1440 (recommended)" },
                { _setting.GetEnumValueLocaleID(Setting.CaptureQuality.UHD_4K_3840x2160),
                  "4K UHD - 3840×2160" },
                { _setting.GetEnumValueLocaleID(Setting.CaptureQuality.UHD_8K_7680x4320),
                  "8K UHD - 7680×4320 (extreme)" },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.CaptureTimes)),
                  "Capture times (hours, comma-separated)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CaptureTimes)),
                  "Comma-separated list of hours (0-24) at which to capture screenshots, applied to ALL presets. " +
                  "Examples: '12' for noon only, '0,12' for midnight and noon, " +
                  "'6,12,18,22' for sunrise/noon/sunset/night. Use decimal for partial hours like '18.5' for 6:30 PM.\n\n" +
                  "LEAVE EMPTY to use each preset's own saved Time of Day instead.\n\n" +
                  "Warning: if a preset was saved with photo/camera mode active, the photo mode's Time of Day " +
  "is used for that preset (the value above is ignored for it)." },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.ForceClearWeatherMode)),
                  "Force clear weather" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ForceClearWeatherMode)),
                    "Temporarily overrides the in-game weather to clear sky during captures " +
                    "for consistent timelapses, then restores it after the session.\n\n" +
                    "- 'Always force clear': overrides weather presets " +
                    "saved with or without photo mode active. (cinematics always keeps it own setting)\n" +
                    "- 'Force except camera mode': clear weather is forced for normal presets, " +
                    "but presets saved in photo mode and cinematics keep their own weather settings " +
                    "(useful if you tuned custom fog, clouds, etc).\n" +
                    "- 'Off': don't touch weather, the game weather stays as-is." },

                { _setting.GetEnumValueLocaleID(Setting.ClearWeatherMode.Off),
                  "Off (use game weather)" },
                { _setting.GetEnumValueLocaleID(Setting.ClearWeatherMode.AlwaysForce),
                  "Always force clear weather" },
                { _setting.GetEnumValueLocaleID(Setting.ClearWeatherMode.ForceExceptCamModes),
                  "Force clear except camera mode and cinematics" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.HideUIInScreenshots)), "Hide UI" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.HideUIInScreenshots)),
                    "Hide the game's user interface while screenshots are being captured. " +
                    "Restored automatically after each capture." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.CaptureCurrentAsPreset)), "Add current view as preset" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CaptureCurrentAsPreset)),
                    "Save the current camera position, zoom and rotation as a new preset. " +
                    "You must be in a city." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.OpenPresetPanel)), "Open preset manager" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.OpenPresetPanel)),
                    "Open the floating preset manager panel. " +
                    "Automatically closes the pause menu so you can see the panel, you must be in a city." +
                    " You can also use the photo mode to add the different settings of a photo mode to the preset." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.OpenScreenshotFolder)), "Open screenshot folder" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.OpenScreenshotFolder)),
                    "Open the folder where captured screenshots are saved in the system file explorer." },

                // ===== Save filter =====
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CityNameFilter)), "City name filter" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CityNameFilter)),
                    "Only create screenshot for cities with same name or prefix ('all saves' sessions). " +
                    "Use this to screenshot only a specific city where to load its saves" +
                    ", for exemple MyCity to load all MyCity saves. Screenshots are automatically ordered by save date." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SavePrefix)), "Save name prefix" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SavePrefix)),
                    "Only create screenshot for saves with same name or prefix ('all saves' sessions). " +
                    "Use this to screenshot only a specific city where the save always have the same prefix" +
                    ", for exemple MyCity1 to MyCity10 saves. Screenshots are automatically ordered by save date." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SkipBetweenSaves)),
                  "Skip N saves between each processed save" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SkipBetweenSaves)),
                  "Process only one save out of (N+1). Useful when you have too many saves " +
                  "and want to thin them out for a smoother or shorter timelapse.\n\n" +
                  "Examples:\n" +
                  "- 0 = process every save (default)\n" +
                  "- 1 = process every 2nd save (skip 1 between each)\n" +
                  "- 9 = process every 10th save (skip 9 between each)\n\n" +
                  "Applied after the filters reduced the list already (Prefix, CityNameFilter, etc)." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.MaxSaves)), "Max saves (0 = all)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxSaves)),
                    "Maximum number of saves to process. " +
                    "Set to 0 for no limit." },

                // ===== Session =====
                { _setting.GetOptionLabelLocaleID(nameof(Setting.RestartGameEveryNSaves)), "Restart game every N saves (0 = off)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.RestartGameEveryNSaves)),
                    "Restart Cities Skylines II after every N processed saves to free memory and avoid leaks " +
                    "during very long sessions. The session automatically resumes after restart. " +
                    "Set to 0 to disable. Usefull if you have a lot of mods or cannot load two saves without restarting inbetween." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoRestartOnCrash)),
                  "Auto-restart game on crash and auto continue" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoRestartOnCrash)),
                  "If the game crashes during an all-saves session, automatically restart it and resume from where it stopped. " +
                  "A small background process monitors the game and relaunches it if it dies unexpectedly. " +
                  "Useful for unattended overnight sessions on long save queues.\n\n" +
                  "Note: if your game restart but don't go back in full screen or so, try to put the game in window mod instead of full screen." +
                  "If you have a blocking error that make the game crash each time, it will stop trying after 3-4 times." },

                
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ReturnToMenuBetweenSaves)),
                    "Return to main menu between saves" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ReturnToMenuBetweenSaves)),
                    "Returns to the main menu between each save load. Slower (a few extra seconds per save) " +
                    "but ensures a fully clean World state. Enable this only if you experience crashes during " +
                    "save transitions." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.ReminderMinutes)), "Reminder interval, minutes (0 = off)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ReminderMinutes)),
                    "When no session is active, show a reminder every N minutes to remind you to " +
                    "capture screenshots of your current city. Set to 0 to disable. This does not take any screenshot!" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.StartSessionOnCurrentSave)), "Take screenshots (current game only)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.StartSessionOnCurrentSave)),
                    "Take screenshots from all camera presets in the currently loaded city. " +
                    "Requires at least one preset and an active city." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.StartSessionAllSaves)), "Start Screenshot Session (all saves)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.StartSessionAllSaves)),
                    "Load each save matching the filter and take screenshots from all camera presets in each. " +
                    "This may take a long time depending on the number of saves and presets.\n\nWarning: If you activate anti crash restart and for whatever reason your game crash and when restarting the game crash again, just try to restart 3-4 time, then the mod will stop without trying again or delete the session.json file in the mod folder." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.ShutdownAfterSession)), "When session ends" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ShutdownAfterSession)),
                    "Action to perform when an auto timelapse or timelapse from save session completes successfully. " +
                    "Useful when running a long session at night. " +
                    "Has no effect if a save failed to process or if the game crash." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.SaveSortOrder)), "Save processing order" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SaveSortOrder)), "Order in which saves are processed during all-saves session." },
                { _setting.GetEnumValueLocaleID(Setting.SortOrder.DescendingDate), "Most recent first" },
                { _setting.GetEnumValueLocaleID(Setting.SortOrder.AscendingDate), "Oldest first" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ResumeFromSaveName)), "Resume from save (optional)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ResumeFromSaveName)), "Name of the save where to restart the all-saves session. Leave empty to start from the beginning. Useful after a crash." },


                // ===== Enum: ShutdownMode =====
                { _setting.GetEnumValueLocaleID(Setting.ShutdownMode.None),             "Do nothing" },
                { _setting.GetEnumValueLocaleID(Setting.ShutdownMode.ExitGame),         "Exit game" },
                { _setting.GetEnumValueLocaleID(Setting.ShutdownMode.ShutdownComputer), "Shutdown computer" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.OpenForumButton)),
                  "Open forum topic" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.OpenForumButton)),
                  "Open the mod's forum topic in your browser." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SendFeedbackButton)),
                  "Send email" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SendFeedbackButton)),
                   "Send me an email if you know someone working in a public transport company in the marketing or such department, thank you! It's for a project :)" },



                { _setting.GetOptionLabelLocaleID(nameof(Setting.SavesModPreviewConstruction)),
                  "Mark buildings on recent roads as construction sites" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SavesModPreviewConstruction)),
                  "After loading a save, marks the buildings on recent roads " +
                  "as under-construction so they appear with cranes and scaffolding." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SavesModRoadsToDeletePerClick)),
                  "How many recent roads should be under-construction ?" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SavesModRoadsToDeletePerClick)),
                  "Number of recent road/track edges (1 to 5000) to put under-construction. Small " +
                  "cities can have a lower number, big cities may need 100 to 1000 to not have thousand " +
                  "of screenshots at then end. Accordingly: lower = more screenshots, higher = quicker session." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SavesModPlayWaitSeconds)),
                  "Wait seconds before screenshots" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SavesModPlayWaitSeconds)),
                  "After loading a save, run the simulation for this many seconds before " +
                  "taking screenshots. Lets the map animate slightly specially if you " +
                  "activated the marking of buildings on recent roads as construction sites." },








                { _setting.GetOptionLabelLocaleID(nameof(Setting.StartAutoTimelapseButton)),
                  "Start Auto Historic Timelapse from this city" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.StartAutoTimelapseButton)),
                  "DESTRUCTIVE MODE. Generates a reverse timelapse of your current city by " +
                  "gradually demolishing it in the same order you built it. Saves screenshots from all" +
                  "your camera presets at each step.\n\nMake a SEPARATE save copy first to be sure, and DO NOT " +
                  "save this destructed game afterwards." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoModPreviewConstruction)),
                  "Mark buildings on recent roads as construction sites" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoModPreviewConstruction)),
                  "Before each step, marks the buildings on recent roads " +
                  "as under-construction so they appear with cranes and scaffolding." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoModRoadsToDeletePerClick)),
                  "How many recent roads should be marked ?" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoModRoadsToDeletePerClick)),
                  "Number of recent road/track edges (1 to 5000) to select to mark adjacent buildings as " +
                  "under construction and to destroy on each step. Small cities can have a lower number, " +
                  "big cities may need 50 to 200 to not have thousand of screenshots at then end. " +
                  "Accordingly: lower = more screenshots, higher = quicker session." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoModRoadsToDeleteBeforeStarting)),
                  "Delete x roads now" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoModRoadsToDeleteBeforeStarting)),
                  "If the game crashed while doing the screenshots or for any other reason and you want" +
                  "to start the timelapse with some recents roads already destroyed, click on this button," +
                  "it will destroy the x more recents roads right now, and then you can start the timelapse" +
                  "again. (x=the number you put in the next field)" },



                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoModPlayWaitSeconds)),
                  "Wait seconds before screenshots" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoModPlayWaitSeconds)),
                  "After each step, run the simulation for this many seconds before " +
                  "taking screenshots. Lets construction sites animate slightly specially if you " +
                  "activated the marking of recent areas as construction site." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.AutoModDistrictFilter)),
                  "Restrict to district (Auto mode)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.AutoModDistrictFilter)),
                  "If set, the Auto Historic Timelapse will only destroy roads inside the named districts. " +
                  "Type districts names or leave empty for no filter.\n\n" +
                  "Use the Debug action 'Auto: list districts' to see the available district names in your city.\n\n" +
                  "This setting only applies to Auto Historic Timelapse mode, not to the saves screenshot mode.\n\n" +
                  "Example: dist1,dist2,dist3 => the mod will destroy roads from the 3 districts only and in the order, first dist1, etc." },

                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_ListDistricts),
                  "Auto: list districts in current city" },

                // ===== Video Tab + Groups =====
                { _setting.GetOptionTabLocaleID(Setting.kVideoGroup), "Video" },
                { _setting.GetOptionGroupLocaleID(Setting.kVideoGeneralGroup), "General" },
                { _setting.GetOptionGroupLocaleID(Setting.kVideoObsGroup), "OBS connection" },

                // ===== Video — General =====
                { _setting.GetOptionLabelLocaleID(nameof(Setting.VideoRecordingEnabled)),
                  "Record video clips with OBS" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.VideoRecordingEnabled)),
                  "Enable video recording in addition to screenshots. " +
                  "Requires OBS Studio (free) running with WebSocket Server enabled. " +
                  "At each step, the simulation will run for a few seconds while OBS records, " +
                  "creating a short video clip showing the city in motion (cars, pedestrians, etc.).\n\n" +
                  "Warning: OBS Studio 30.1 (from 2024) or later is required for the video clips to be saved in your screenshot folder ordered. With older versions, all videos go to OBS's default record folder." },



                { _setting.GetOptionLabelLocaleID(nameof(Setting.OpenVideosFolder)), "Open videos folder" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.OpenVideosFolder)),
                    "Open the folder where captured videos are saved in the system file explorer." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.VideoRecordSeconds)),
                  "Recording duration (seconds)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.VideoRecordSeconds)),
                  "How many seconds the simulation should run while recording each video clip. " +
                  "Recommended: 3-10 seconds. Longer = bigger files but more visible animation." +
                  "Put 0 if you only want to use cinematics." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.CinematicsToRecord)),
                  "Cinematics to record" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CinematicsToRecord)),
                  "Comma-separated list of cinematic sequence names that will be played and recorded after each save's screenshots are done and for each hours you put in general. " +
                  "Example: \"AerialShot, OverviewSlow, NightFlyby\". " +
                  "Leave empty to disable. " +
                  "Cinematics are played in the order you list them, with their camera config, including simulation speed, time and weather. If you don't set a time, it will take the last set time of the mod config. " +
                  "Go to Photo Mode → Cinematic Camera of the game to see and create cinematics (it's not from the mod). " +
                  "These recordings happen IN ADDITION to per-preset videos: if you set 'Recording duration (seconds)' above to a non-zero value, you get short clips for each preset×hour, AND if you fill this field, you also get the full cinematic sequences." },


                { _setting.GetOptionLabelLocaleID(nameof(Setting.VideoSimulationSpeed)),
                  "Simulation speed during recording" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.VideoSimulationSpeed)),
                  "How fast the simulation runs while the video is being recorded. " +
                  "x1 = normal, x2 = double speed (more visible action), x3 = triple speed (chaotic). This is for presets, cinematics use their own speed setting." },

                { _setting.GetEnumValueLocaleID(Setting.SimulationSpeed.Pause), "Paused (still)" },
                { _setting.GetEnumValueLocaleID(Setting.SimulationSpeed.Slow_x1), "Normal (x1)" },
                { _setting.GetEnumValueLocaleID(Setting.SimulationSpeed.Normal_x2), "Fast (x2)" },
                { _setting.GetEnumValueLocaleID(Setting.SimulationSpeed.Fast_x3), "Very fast (x3)" },

                // ===== Video — OBS =====
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ObsHost)),
                  "OBS WebSocket host" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ObsHost)),
                  "Host where OBS Studio is running. Use 'localhost' if OBS is on the same computer (most common)." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.ObsPort)),
                  "OBS WebSocket port" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ObsPort)),
                  "Port number configured in OBS Studio under Tools → WebSocket Server Settings. Default is 4455." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.ObsPassword)),
                  "OBS WebSocket password" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ObsPassword)),
                  "Password set in OBS Studio under Tools → WebSocket Server Settings. " +
                  "Leave empty if no password is set (not recommended for security)." },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.TestObsConnectionButton)),
                  "Test OBS connection (wait few seconds)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.TestObsConnectionButton)),
                  "Try to connect to OBS Studio with the settings above. " +
                  "Make sure OBS is running and WebSocket Server is enabled." },

                { _setting.GetOptionTabLocaleID(Setting.kCartoGroup),
                  "Carto" },
                { _setting.GetOptionGroupLocaleID(Setting.kCartoGeneralGroup),
                  "General" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.TriggerCartoExport)),
                  "Trigger Carto map export at each save / step" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.TriggerCartoExport)),
                  "Requires the Carto mod to be installed and configured. " },





                { _setting.GetEnumValueLocaleID(Setting.DebugAction.None), "(select an action)" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Obs_RecordTest5s), "OBS: record 5 seconds test" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Obs_SetTestRecordDir), "OBS: try set record directory" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Cin_ListAvailable), "Cinematics: list all available" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Cin_PlayFirstConfigured), "Cinematics: play first configured" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Cam_ApplyFirstPreset), "Camera: apply first preset" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Cam_DumpPhotoProperties), "Camera: dump photo properties to log" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Tw_SetTime12), "Time/Weather: set time to 12h" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Tw_SetTime22), "Time/Weather: set time to 22h" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Tw_ClearWeatherOnly), "Time/Weather: force clear weather (no time change)" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Tw_Restore), "Time/Weather: restore defaults" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_CountEdges), "Auto Mod: count visible edges" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_CountEdgesPerDistrict), "Count edge by districts" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_Destroy1Road), "Auto Mod: destroy 1 most recent road" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_Destroy5Roads), "Auto Mod: destroy 5 most recent roads" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_MarkConstruction10), "Auto: mark 10 recent edges as construction" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Saves_ListFiltered), "Saves: list filtered to log" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Carto_CheckAvailable), "Carto: check if installed" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Carto_TriggerExport), "Carto: trigger export now" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Life_RestartGame), "Lifecycle: restart game now" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Life_StartWatchdog), "Lifecycle: start crash watchdog" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Cap_ScreenshotNow), "Capture: take a screenshot now" },

                // Setting labels
                { _setting.GetOptionLabelLocaleID(nameof(Setting.DebugSelectedAction)), "Debug action" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.DebugSelectedAction)),
                  "Select a function to test. Click 'Run debug action' below to execute it. This can help you identify what doesnt works if you see any bug with the mod. Let me know if something is not normal from here." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.RunDebugAction)), "Run debug action" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.RunDebugAction)),
                  "Run the selected debug action. Used to test individual functions of the mod " +
                  "to identify which one is causing problems." },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Auto_MoveCameraToRecent),
                "Auto Mod: move camera to most recent road" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Life_StopWatchdog),
                  "Lifecycle: stop crash watchdog" },
                { _setting.GetEnumValueLocaleID(Setting.DebugAction.Life_QuitGame),
                  "Lifecycle: quit game now" },

                { _setting.GetOptionTabLocaleID(Setting.kDebugGroup), "Debug" },
                { _setting.GetOptionGroupLocaleID(Setting.kDebugGeneralGroup), "Debug actions" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.ScreenshotFolderOverride)), "Custom screenshot folder" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ScreenshotFolderOverride)),
                    "Override the default screenshot folder. If empty, screenshots go to the mod's default folder " +
                    "(AppData/.../CameraTimelapseMod/Screenshots). " +
                    "Make sure the folder you choose exists and is writable. " +
                    "The 'Open screenshot folder' button above will open this custom folder when set." },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.VideoFolderOverride)), "Custom video folder" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.VideoFolderOverride)),
                    "Override the default video folder for OBS recordings and cinematics. Specially if you have another hardware with more space as video take a lot of space. If empty, videos go to the mod's default folder " +
                    "(AppData/.../CameraTimelapseMod/Videos). " +
                    "Make sure the folder you choose exists and is writable. " +
                    "OBS Studio 30.1 or later is required for the mod to redirect recordings here automatically." },
            };
        }

        public void Unload() { }
    }
}