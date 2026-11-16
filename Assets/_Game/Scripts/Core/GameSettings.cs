using System;
using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>Where settings are saved. The game uses PlayerPrefs; tests use a dictionary.</summary>
    public interface ISettingsStore
    {
        int Get(string key, int fallback);
        void Set(string key, int value);
    }

    public class PlayerPrefsStore : ISettingsStore
    {
        public int Get(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void Set(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
    }

    /// <summary>The player's settings: two volumes (0 to 100 %, steps of 5), aim assist and fullscreen. Plain C#, saved through an ISettingsStore.</summary>
    public class GameSettings
    {
        public const int MusicDefault = 25, EffectsDefault = 80;
        public const int Step = 5;

        int music = MusicDefault, effects = EffectsDefault;
        public bool AimAssist { get; set; } = true;
        public bool Fullscreen { get; set; } = true;

        // Values snap to the nearest step and stay inside 0..100.
        static int Snap(int v) => Math.Max(0, Math.Min(100, (int)Math.Round(v / (double)Step) * Step));
        public int MusicPercent { get => music; set => music = Snap(value); }
        public int EffectsPercent { get => effects; set => effects = Snap(value); }

        public void ResetToDefaults()
        {
            music = MusicDefault; effects = EffectsDefault; AimAssist = true; Fullscreen = true;
        }

        public void Save(ISettingsStore s)
        {
            s.Set("music", music); s.Set("effects", effects);
            s.Set("aimAssist", AimAssist ? 1 : 0); s.Set("fullscreen", Fullscreen ? 1 : 0);
        }

        public void Load(ISettingsStore s)
        {
            MusicPercent = s.Get("music", MusicDefault);
            EffectsPercent = s.Get("effects", EffectsDefault);
            AimAssist = s.Get("aimAssist", 1) != 0;
            Fullscreen = s.Get("fullscreen", 1) != 0;
        }
    }
}
