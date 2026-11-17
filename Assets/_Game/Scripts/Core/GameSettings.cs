using System;
using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>Where settings are saved. The game uses PlayerPrefs; tests use a dictionary.</summary>
    public interface ISettingsStore
    {
        int Get(string key, int fallback);
        void Set(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
    }

    public class PlayerPrefsStore : ISettingsStore
    {
        public int Get(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void Set(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
    }

    /// <summary>The player's settings: two volumes (0 to 100 %, steps of 5), aim assist and fullscreen. Plain C#, saved through an ISettingsStore.</summary>
    public class GameSettings
    {
        public const int MusicDefault = 25, EffectsDefault = 80;
        public const int Step = 5;

        int music = MusicDefault, effects = EffectsDefault;
        public bool AimAssist { get; set; } = true;
        public bool Fullscreen { get; set; } = true;
        // Feedback toggles: what the ball draws for the players. Off = not drawn in any view.
        public bool LandingRing { get; set; } = true;
        public bool BallTrail { get; set; } = true;
        public bool InOutLabel { get; set; } = true;

        /// <summary>The settings the game is using now (the Shell loads and edits this one; LandingMarker and BallFx read it).</summary>
        public static GameSettings Current { get; } = new GameSettings();

        // Camera (3rd person). Height and distance move in half metres, the field of view in 5 degrees; shoulder is -1 Left, 0 Centre, +1 Right.
        public const float HeightMin = 3f, HeightMax = 7f, HeightDefault = 5f, DistanceMin = 5f, DistanceMax = 10f, DistanceDefault = 7.5f;
        public const float FovMin = 50f, FovMax = 75f, FovDefault = 60f;
        float camHeight = HeightDefault, camDistance = DistanceDefault, camFov = FovDefault;
        int camShoulder;
        static float SnapTo(float v, float min, float max, float step) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
        public float CameraHeight { get => camHeight; set => camHeight = SnapTo(value, HeightMin, HeightMax, 0.5f); }
        public float CameraDistance { get => camDistance; set => camDistance = SnapTo(value, DistanceMin, DistanceMax, 0.5f); }
        public float CameraFov { get => camFov; set => camFov = SnapTo(value, FovMin, FovMax, 5f); }
        public int CameraShoulder { get => camShoulder; set => camShoulder = Math.Max(-1, Math.Min(1, value)); }

        // Values snap to the nearest step and stay inside 0..100.
        static int Snap(int v) => Math.Max(0, Math.Min(100, (int)Math.Round(v / (double)Step) * Step));
        public int MusicPercent { get => music; set => music = Snap(value); }
        public int EffectsPercent { get => effects; set => effects = Snap(value); }

        public void ResetToDefaults()
        {
            music = MusicDefault; effects = EffectsDefault; AimAssist = true; Fullscreen = true;
            LandingRing = BallTrail = InOutLabel = true;
            ResetCamera();
        }

        public void ResetCamera() { camHeight = HeightDefault; camDistance = DistanceDefault; camFov = FovDefault; camShoulder = 0; }

        public void Save(ISettingsStore s)
        {
            s.Set("music", music); s.Set("effects", effects);
            s.Set("aimAssist", AimAssist ? 1 : 0); s.Set("fullscreen", Fullscreen ? 1 : 0);
            s.Set("landingRing", LandingRing ? 1 : 0); s.Set("ballTrail", BallTrail ? 1 : 0); s.Set("inOutLabel", InOutLabel ? 1 : 0);
            s.Set("camHeight", Mathf.RoundToInt(camHeight * 10f)); s.Set("camDistance", Mathf.RoundToInt(camDistance * 10f)); s.Set("camFov", (int)camFov); s.Set("camShoulder", camShoulder);
        }

        public void Load(ISettingsStore s)
        {
            MusicPercent = s.Get("music", MusicDefault);
            EffectsPercent = s.Get("effects", EffectsDefault);
            AimAssist = s.Get("aimAssist", 1) != 0;
            Fullscreen = s.Get("fullscreen", 1) != 0;
            LandingRing = s.Get("landingRing", 1) != 0; BallTrail = s.Get("ballTrail", 1) != 0; InOutLabel = s.Get("inOutLabel", 1) != 0;
            CameraHeight = s.Get("camHeight", 50) / 10f; CameraDistance = s.Get("camDistance", 75) / 10f; CameraFov = s.Get("camFov", 60); CameraShoulder = s.Get("camShoulder", 0);
        }
    }
}
