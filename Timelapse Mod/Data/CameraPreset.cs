using System;
using System.Collections.Generic;
using UnityEngine;

namespace CameraTimelapseMod.Data
{
    [Serializable]
    public class CameraPreset
    {
        public string Name = "Untitled";

        public float PivotX, PivotY, PivotZ;
        public float Zoom = 200f;
        public float RotationX, RotationY, RotationZ;

        public List<PhotoModeEntry> PhotoModeProperties = new List<PhotoModeEntry>();

        public Vector3 GetPivot() => new Vector3(PivotX, PivotY, PivotZ);
        public void SetPivot(Vector3 v) { PivotX = v.x; PivotY = v.y; PivotZ = v.z; }

        public Vector3 GetRotation() => new Vector3(RotationX, RotationY, RotationZ);
        public void SetRotation(Vector3 v) { RotationX = v.x; RotationY = v.y; RotationZ = v.z; }
    }

    [Serializable]
    public class PhotoModeEntry
    {
        public string Id;
        public float Value;
        public bool IsEnabled;
    }

    [Serializable]
    public class CameraPresetList
    {
        public List<CameraPreset> Items = new List<CameraPreset>();
    }
}