using System;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{
    public class CoroutineSystem : MonoBehaviour
    {
        private static CoroutineSystem _instance;

        public static CoroutineSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CameraTimelapseMod.CoroutineRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineSystem>();
                }
                return _instance;
            }
        }
    }
}