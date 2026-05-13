using CameraTimelapseMod.Systems;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;


namespace CameraTimelapseMod.Util
{
    internal class UITools
    {
        public static void ShowError(string message)
        {
            Mod.log.Warn($"User error: {message}");


            ShowMessage("Error", message);

        }

        public static void ShowMessage(string message)
        {
            ShowMessage("Auto Timelapse Mod", message);
        }
        public static void ShowMessage(string title, string message)
        {
            try
            {

                var dialog = new MessageDialog(
                    title: LocalizedString.Value(title),
                    message: LocalizedString.Value(message),
                    confirmAction: LocalizedString.IdWithFallback("Common.OK", "OK"),
                    otherActions: null);

                Game.SceneFlow.GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, _ => { });
            }
            catch (Exception ex)
            {
                LogsTools.Error($"ShowMessage failed: {ex}");
            }
        }
        public static void ShowConfirm(string title, string message, Action onConfirm)
        {
            try
            {

                var dialog = new ConfirmationDialog(
                    title: LocalizedString.Value(title),
                    message: LocalizedString.Value(message),
                    confirmAction: LocalizedString.IdWithFallback("Common.YES", "Yes"),
                    cancelAction: null,
                    otherActions: new[] { LocalizedString.IdWithFallback("Common.NO", "No") });

                Game.SceneFlow.GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, result =>
                {
                    if (result == 0)
                    {
                        try { onConfirm?.Invoke(); }
                        catch (Exception ex) { LogsTools.Error($"ShowConfirm.onConfirm failed: {ex}"); }
                    }
                });
            }
            catch (Exception ex)
            {
                LogsTools.Error($"ShowConfirm failed: {ex}");
            }
        }

        public static bool SetUIVisible(bool visible)
        {
            try
            {
                var view = GameManager.instance?.userInterface?.view;
                if (view == null) return false;

                bool wasVisible = view.enabled;
                if (!visible)
                    HighlightSuppressor.Suppress();
                else
                    HighlightSuppressor.Restore();
                view.enabled = visible;
                UnityEngine.Cursor.visible = visible;
                return wasVisible;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"SetUIVisible failed: {ex.Message}");
                return false;
            }
        }

        public static void CloseGameMenu()
        {
            try
            {
                var gameScreenSystem = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.UI.InGame.GameScreenUISystem>();

                if (gameScreenSystem == null)
                {
                    Mod.log.Warn("GameScreenUISystem not found, cannot close menu");
                    return;
                }

                if (!gameScreenSystem.isMenuActive)
                {
                    return;
                }

                gameScreenSystem.SetScreen(Game.UI.InGame.GameScreenUISystem.GameScreen.Main);
                LogsTools.Info("Closed pause menu via GameScreenUISystem.SetScreen(Main)");
            }
            catch (System.Exception ex)
            {
                Mod.log.Warn($"CloseGameMenu failed: {ex.Message}");
            }
        }
    }
}
