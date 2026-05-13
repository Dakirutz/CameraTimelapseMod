using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using CameraTimelapseMod.Systems;
using CameraTimelapseMod.Util;
using System.IO;
using Unity.Entities;
using Unity.Burst;

//readme and mod description
//youtube link and make mod public

//il faut build puis npm build et pas inverse sinon .mjs n'est pas mis dans mods file du jeu de cameratmelapsemod
//https://github.com/JadHajjar/RoadBuilder-CSII/blob/main/RoadBuilder/UI/src/vanillacomponentresolver.tsx
//https://github.com/ps1ke/Cities-Skylines-2-Modding-Guide/tree/origin/gh-pages/Colossal
//https://www.nuget.org/packages/CS2-ModdingTools/ 
//https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/

namespace CameraTimelapseMod
{
    public class Mod : IMod
    {
        public static string sorryForThisCommentAhah = "Please, if you share content made with the mod, share the mod name/link too, otherwise I may loose motivation to maintain it :/ Thanks! For any feedback or bug or so, please go on the forum topic. If you know anyone working in the marketing department or something similar in a public transport company (buses, tram, etc), write me an email that would be nice, thanks !";
        public static string companyText = "This mod was done as a side project while running my own startup, if you like it, it would be nice to rate my startup on Google regarding our coding and project skills :) Thanks ! You may also follow us on instagram, we do tech related to public transport. :) \nIf you are part of the development game team and use this mod, I would love to know. \nI also want to thank everyone on the modding discord for guiding me and being so nice with new commers, thanks!";
        public static string companyLink = "https://nexswiss.ch/rate-us.php?source=modcs";
        public static string companyFollowLink = "https://nexswiss.ch/follow-us.php?source=modcs";
        public static string email = "microscraft AT gmail";
        public static string forumLink = "https://forum.paradoxplaza.com/forum/threads/timelapse-mod-auto-screenshots-all-your-saves-historymod.1919365/";

        public static string getEmail()
        {
            return email.Replace(" AT gmail", "@gmail.com"); //preventing robots to read github and know my email :P
        }

        public static readonly ILog log = LogManager
            .GetLogger($"{nameof(CameraTimelapseMod)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        public static Setting Setting { get; private set; }

        public static string DataDir => Path.Combine(UnityEngine.Application.persistentDataPath, "CameraTimelapseMod");
        public static string SessionStatePath => Path.Combine(DataDir, "session.json");

        public void OnLoad(UpdateSystem updateSystem)
        {
            Util.LogsTools.Init();
            LogsTools.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                LogsTools.Info($"Mod asset path: {asset.path}");

            Directory.CreateDirectory(DataDir);

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));
            AssetDatabase.global.LoadSettings(nameof(CameraTimelapseMod), Setting, new Setting(this));

            PresetsSystem.Load();

            updateSystem.UpdateAt<SessionSystem>(SystemUpdatePhase.UIUpdate); //Modification4 doesnt make the key space/esc works 
            updateSystem.UpdateAt<UISystem>(SystemUpdatePhase.UIUpdate);

            LogsTools.Info("CameraTimelapseMod loaded");
        }



        public static bool IsInGame
        {
            get
            {
                try
                {
                    var manager = GameManager.instance;
                    if (manager == null) return false;
                    if (manager.gameMode != GameMode.Game) return false;

                    if (World.DefaultGameObjectInjectionWorld == null) return false;

                    return true;
                }
                catch { return false; }
            }
        }

        public void OnDispose()
        {
            LogsTools.Info(nameof(OnDispose));
            Util.LogsTools.Shutdown();
            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
        }
    }
}
