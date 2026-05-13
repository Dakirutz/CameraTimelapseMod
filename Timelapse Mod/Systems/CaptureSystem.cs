using Game.SceneFlow;
using CameraTimelapseMod.Util;
using System;
using System.Collections;
using System.IO;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{
    public static class CaptureSystem
    {
        private const int CAPTURE_MSAA = 4;

        public static event Action OnCaptureFinished;


        public static void CaptureNow(string fullPath)
        {
            CoroutineSystem.Instance.StartCoroutine(CaptureCoroutine(fullPath));
        }

        public static System.Collections.IEnumerator CaptureNowAndWait(string fullPath)
        {
            bool finished = false;
            Action handler = () => { finished = true; };
            OnCaptureFinished += handler;

            CoroutineSystem.Instance.StartCoroutine(CaptureCoroutine(fullPath));

            float timeout = 30f;
            while (!finished && timeout > 0f)
            {
                timeout -= UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            OnCaptureFinished -= handler;

            if (!finished)
                LogsTools.Warn($"CaptureNowAndWait: timeout for {fullPath}");
        }


        private static IEnumerator CaptureCoroutine(string path)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); }
            catch (Exception ex) { LogsTools.Warn($"mkdir failed: {ex.Message}"); }

            bool wasUiHidden = false;
            Game.Rendering.RenderingSystem renderingSys = null;
            bool prevHideOverlay = false;
            bool prevMarkersVisible = true;
            bool restoreNeeded = false;

            try
            {
                if (Mod.Setting != null && Mod.Setting.HideUIInScreenshots)
                    wasUiHidden = UITools.SetUIVisible(false);

                renderingSys = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.Rendering.RenderingSystem>();
                if (renderingSys != null)
                {
                    prevHideOverlay = renderingSys.hideOverlay;
                    prevMarkersVisible = renderingSys.markersVisible;
                    renderingSys.hideOverlay = true;
                    renderingSys.markersVisible = false;
                    restoreNeeded = true;
                }



                yield return null;
                yield return new WaitForEndOfFrame();

                var (w, h, useScreenCapture) = SettingsTools.GetScreenshotQualityTarget();
                if (useScreenCapture)
                {
                    try
                    {
                        ScreenCapture.CaptureScreenshot(path);
                        LogsTools.Info($"Captured (screen {w}x{h}): {path}");
                    }
                    catch (Exception ex)
                    {
                        LogsTools.Error($"ScreenCapture failed: {ex}");
                    }
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    yield return CameraTools.CaptureToRenderTexture(path, w, h, CAPTURE_MSAA);
                }

                yield return null;
                yield return new WaitForEndOfFrame();
            }
            finally
            {
                if (restoreNeeded && renderingSys != null)
                {
                    try
                    {
                        renderingSys.hideOverlay = prevHideOverlay;
                        renderingSys.markersVisible = prevMarkersVisible;
                    }
                    catch { }
                }
                if (wasUiHidden)
                {
                    try { UITools.SetUIVisible(true); } catch { }
                }
                try { OnCaptureFinished?.Invoke(); } catch (Exception ex) { LogsTools.Warn($"OnCaptureFinished handler threw: {ex.Message}"); }
            }
        }
    }
}