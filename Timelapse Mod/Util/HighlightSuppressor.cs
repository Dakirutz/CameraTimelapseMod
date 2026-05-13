using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CameraTimelapseMod.Util
{
    public static class HighlightSuppressor
    {
        private static int _savedLayerMask = 0;
        private static bool _savedValid = false;
        private static int _suppressDepth = 0;

        public static void Suppress()
        {
            _suppressDepth++;
            if (_suppressDepth > 1) return;

            try
            {
                CustomPassVolume[] volumes = UnityEngine.Object.FindObjectsOfType<CustomPassVolume>();
                for (int i = 0; i < volumes.Length; i++)
                {
                    var v = volumes[i];
                    if (v == null || v.customPasses == null) continue;

                    for (int j = 0; j < v.customPasses.Count; j++)
                    {
                        var pass = v.customPasses[j];
                        var outlinePass = pass as Game.Rendering.OutlinesWorldUIPass;
                        if (outlinePass != null)
                        {
                            if (!_savedValid)
                            {
                                _savedLayerMask = outlinePass.m_OutlineLayer.value;
                                _savedValid = true;
                            }
                            outlinePass.m_OutlineLayer = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"HighlightSuppressor.Suppress failed: {ex.Message}");
            }
        }

        public static void Restore()
        {
            if (_suppressDepth > 0) _suppressDepth--;
            if (_suppressDepth > 0) return;
            if (!_savedValid) return;

            DoRestore();
        }

        public static void ForceRestore()
        {
            _suppressDepth = 0;
            if (!_savedValid) return;
            DoRestore();
        }

        private static void DoRestore()
        {
            try
            {
                CustomPassVolume[] volumes = UnityEngine.Object.FindObjectsOfType<CustomPassVolume>();
                for (int i = 0; i < volumes.Length; i++)
                {
                    var v = volumes[i];
                    if (v == null || v.customPasses == null) continue;

                    for (int j = 0; j < v.customPasses.Count; j++)
                    {
                        var pass = v.customPasses[j];
                        var outlinePass = pass as Game.Rendering.OutlinesWorldUIPass;
                        if (outlinePass != null)
                        {
                            outlinePass.m_OutlineLayer = _savedLayerMask;
                        }
                    }
                }
                _savedValid = false;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"HighlightSuppressor.Restore failed: {ex.Message}");
            }
        }
    }
}