using System;
using System.Collections.Generic;

namespace CameraTimelapseMod.Data
{
    public enum SessionPhase
    {
        Idle,
        LoadingSave,
        WaitingForWorldSettle,
        PreviewConstructionAndWait,
        SetupTimeWeather,
        ApplyView,
        CaptureFrame,
        AdvanceView,
        AdvanceMode,
        PlayCinematicsForHour,
        AdvanceSave,
        Done
    }



    [Serializable]
    public class SessionState
    {
        public SessionPhase Phase = SessionPhase.Idle;
        public int SaveIdx;
        public int ViewIdx;
        public int ModeIdx;
        public string CurrentSaveName = "";
        public int SavesProcessedSinceRestart;
        public int ResumeAttempts;
        public int SuccessfulSaves;
        public int TotalExpectedSaves;
        public bool IsAllSavesMode;
        public string SessionStartDate = "";
        public bool CurrentSaveProcessed = false;
        public string CaptureTimesQueue = "";
        public bool IsPaused = false;
        public float ElapsedSeconds = 0f;
        public int CompletedScreenshots = 0;
    }
}