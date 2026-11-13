using System;
using System.Collections.Generic;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Audio
{
    /// <summary>
    /// Plays every sound. It creates itself when play starts (no scene edit), builds the clips from SoundSynth once,
    /// listens to the match, ball and player events, and plays through a small pool of 2D AudioSources.
    /// It only listens: it never changes gameplay. "The human" is player 0 (bots can stand in for it in tests).
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        // shortcut: two volume fields instead of an AudioMixer; move to a mixer with MusicVol / SFXVol when the M6 settings screen is built
        [Range(0f, 1f), Tooltip("Sound effects loudness, 0..1.")] public float sfxVolume = 0.8f;
        [Range(0f, 1f), Tooltip("Music loudness, 0..1.")] public float musicVolume = 0.25f;

        [SerializeField] bool logSounds;    // prints "[Sound] name vol=..." for every sound played

        /// <summary>Debug: most sources busy at once, and plays dropped because all of them were busy.</summary>
        public int MaxBusy { get; private set; }
        public int Dropped { get; private set; }
        public bool MusicPlaying => music != null && music.isPlaying;

        const int PoolSize = 8;
        const float Mix = 0.7f;         // scales every level, so the loudest one (a full spike, 0.36) is still within reach of a short, sharp recording

        AudioSource[] pool;
        AudioSource music;
        MatchManager match;
        BallController ball;
        RulesEngine seenRules;          // changes at every new match
        bool wasReady;                  // spike "hit now" cue was on last frame
        int lastTick;
        readonly List<(float at, Action act)> later = new List<(float, Action)>();

        readonly Dictionary<AudioClip, float> loudness = new Dictionary<AudioClip, float>();   // each clip's own loudness, measured once

        AudioClip countdown, whistleStart, whistlePoint, whistleBlast, tick, toss, bump, set, spike, charge, jump, landing, block, sand, net, crowd, crowdBig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<AudioDirector>() != null) return;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDirector>();
        }

        void Awake()
        {
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource();
            music = NewSource();
            music.loop = true;

            countdown = Clip("Countdown", SoundSynth.CountdownHit());
            whistleStart = Clip("WhistleStart", SoundSynth.Whistle(0.4f));
            whistlePoint = Clip("WhistlePoint", SoundSynth.Whistle(0.5f));
            whistleBlast = Clip("WhistleBlast", SoundSynth.Whistle(0.15f));
            tick = Clip("Tick", SoundSynth.Tick());
            toss = Clip("Toss", SoundSynth.Toss());
            bump = Clip("Bump", SoundSynth.Bump());
            set = Clip("Set", SoundSynth.Set());
            spike = Clip("Spike", SoundSynth.Spike());
            charge = Clip("SpikeCharge", SoundSynth.SpikeCharge());
            jump = Clip("Jump", SoundSynth.Jump());
            landing = Clip("Landing", SoundSynth.Landing());
            block = Clip("Block", SoundSynth.Block());
            sand = Clip("Sand", SoundSynth.Sand());
            net = Clip("Net", SoundSynth.Net());
            crowd = Clip("Crowd", SoundSynth.Crowd(false));
            crowdBig = Clip("CrowdBig", SoundSynth.Crowd(true));
            music.clip = Clip("Music", SoundSynth.Music());
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;      // 2D
            return s;
        }

        // A recorded file named Assets/_Game/Audio/Resources/Sounds/<name> wins; without one, the synthesized samples are used.
        AudioClip Clip(string name, float[] samples)
        {
            var c = Resources.Load<AudioClip>("Sounds/" + name);
            if (c == null)
            {
                c = AudioClip.Create(name, samples.Length, 1, SoundSynth.Rate, false);
                c.SetData(samples, 0);
            }
            else
            {
                samples = new float[c.samples * c.channels];
                c.GetData(samples, 0);
            }
            loudness[c] = Mathf.Max(0.01f, SoundSynth.Loudness(samples, c.frequency, c.channels));
            return c;
        }

        /// <summary>
        /// Plays a clip on a free source at the wanted loudness 'level' (0.05 = barely there, 0.2 = a normal hit, 0.35 = the loudest),
        /// whatever the file's own volume is: swapping a sound file never changes the mix. A clip quieter than 'level' plays at full.
        /// Pitch varies +-5% and loudness +-10%. Dropped (and counted) if all 8 sources are busy.
        /// </summary>
        public void Play(AudioClip clip, float level, float pitch = 1f)
        {
            int busy = 0;
            AudioSource free = null;
            foreach (AudioSource s in pool) { if (s.isPlaying) busy++; else if (free == null) free = s; }
            MaxBusy = Mathf.Max(MaxBusy, busy + (free != null ? 1 : 0));
            if (free == null) { Dropped++; return; }
            float volume = Mathf.Clamp01(level * Mix / loudness[clip] * sfxVolume / 0.8f * UnityEngine.Random.Range(0.9f, 1.1f));   // levels are tuned at the default sfxVolume 0.8
            free.clip = clip;
            free.volume = volume;
            free.pitch = pitch * UnityEngine.Random.Range(0.95f, 1.05f);
            free.Play();
            if (logSounds) Debug.Log($"[Sound] {clip.name} vol={volume:F2}");
        }

        void Later(float seconds, Action act) => later.Add((Time.time + seconds, act));

        // ---- finding the game and listening to it ----

        void Hook()
        {
            match = FindFirstObjectByType<MatchManager>();
            ball = FindFirstObjectByType<BallController>();
            if (match == null || ball == null) { match = null; return; }
            match.OnPoint += OnPoint;
            ball.OnGroundContact += _ => Play(sand, 0.14f);
            ball.OnNetTouch += _ => Play(net, 0.06f);
            foreach (PlayerController p in match.Players)
            {
                PlayerController who = p;
                who.OnHitAttempt += r => OnHit(r);
                who.OnBlocked += _ => Play(block, 0.28f);
                who.OnTossed += _ => Play(toss, 0.08f);
                who.OnJumped += _ => Play(jump, 0.06f);
                who.OnLanded += _ => Play(landing, 0.10f);
                who.OnDiveEnded += _ => Play(landing, 0.18f);     // a dive ends with a bigger thud
            }
            seenRules = null;
        }

        void OnHit(HitReport r)
        {
            if (!r.contact) return;
            switch (r.type)
            {
                case HitType.Bump: Play(bump, 0.20f); break;
                case HitType.Set: Play(set, 0.12f); break;
                case HitType.Spike:
                case HitType.Serve: Play(spike, Mathf.Lerp(0.22f, 0.36f, r.charge), 1f + 0.1f * r.charge); break;
            }
        }

        void OnPoint(int team, PointReason reason, bool viaNet)
        {
            bool fault = reason != PointReason.BallIn && reason != PointReason.BallOut;
            Later(0.4f, () =>
            {
                if (!fault) { Play(whistlePoint, 0.2f); return; }
                Play(whistleBlast, 0.2f);                                       // a fault is two short blasts
                Later(0.25f, () => Play(whistleBlast, 0.2f));
            });
            bool big = ball.LastHitType == HitType.Spike || ball.LastHitType == HitType.Block;
            Later(0.3f, () => Play(big ? crowdBig : crowd, big ? 0.16f : 0.10f));
        }

        // ---- per-frame: new match, music, serve clock, spike charge, delayed sounds ----

        void Update()
        {
            if (match == null || ball == null) { Hook(); if (match == null) return; }

            music.volume = musicVolume;

            if (match.Rules != seenRules)       // a new match began: countdown, whistle, music
            {
                seenRules = match.Rules;
                for (int i = 0; i < 3; i++) Later(i, () => Play(countdown, 0.3f));
                Later(2.5f, () => Play(whistleStart, 0.2f));
                music.Play();
            }

            bool over = match.State == MatchState.MatchOver;
            if (over && music.isPlaying) music.Pause();
            else if (!over && !music.isPlaying) music.UnPause();

            // serve clock: one tick for each of the last 5 seconds of the human's serve
            PlayerController human = match.Players[0];
            if (match.State == MatchState.Serve && match.Server == human && !human.Tossed)
            {
                int left = Mathf.CeilToInt(match.ServeClockLeft);
                if (left >= 1 && left <= 5 && left != lastTick) { lastTick = left; Play(tick, 0.05f); }
            }
            else lastTick = 0;

            // spike charge whoosh: the human holds a spike in the air and the ball comes into reach
            bool ready = !human.IsGrounded && human.HitReady(out HitType t) && t == HitType.Spike;
            if (ready && !wasReady) Play(charge, 0.09f);
            wasReady = ready;

            for (int i = later.Count - 1; i >= 0; i--)
                if (Time.time >= later[i].at) { var a = later[i].act; later.RemoveAt(i); a(); }
        }
    }
}
