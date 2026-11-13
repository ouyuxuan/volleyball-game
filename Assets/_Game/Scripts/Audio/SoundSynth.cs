using System;

namespace Sandspike.Audio
{
    /// <summary>
    /// Every sound in the game, made from maths: pure functions that return mono samples at 44100 Hz.
    /// No Unity objects, so the tests can call them. Noise comes from a seeded Random, so each call gives the same samples.
    /// Each sound is a few parts (a tone, filtered noise, a decay) added together, then faded 5 ms in and out and scaled to a peak.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;
        public const float Bpm = 120f;
        public const int MusicBars = 8;

        // ---- building blocks ----

        static float[] Make(float seconds) => new float[(int)(seconds * Rate)];
        static float T(int i) => i / (float)Rate;
        static float Decay(float t, float tau) => (float)Math.Exp(-t / tau);   // 1 at t = 0, falling by 63% every tau seconds
        static float Sine(float hz, float t) => (float)Math.Sin(2 * Math.PI * hz * t);

        // White noise in -1..1.
        static float[] Noise(int n, int seed)
        {
            var r = new Random(seed);
            var s = new float[n];
            for (int i = 0; i < n; i++) s[i] = (float)(r.NextDouble() * 2 - 1);
            return s;
        }

        // One-pole low-pass: keeps what is below about 'hz'.
        static float[] Low(float[] x, float hz)
        {
            float a = 1f - (float)Math.Exp(-2 * Math.PI * hz / Rate), y = 0f;
            var o = new float[x.Length];
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); o[i] = y; }
            return o;
        }

        // High-pass = the signal minus its low-pass.
        static float[] High(float[] x, float hz)
        {
            float[] l = Low(x, hz);
            for (int i = 0; i < x.Length; i++) l[i] = x[i] - l[i];
            return l;
        }

        // 5 ms fade in and out (so nothing clicks), then scale so the loudest sample is exactly 'peak'.
        static float[] Finish(float[] s, float peak)
        {
            int f = Rate / 200;
            float max = 1e-6f;
            for (int i = 0; i < s.Length; i++)
            {
                float g = Math.Min(1f, Math.Min(i, s.Length - 1 - i) / (float)f);
                s[i] *= g;
                max = Math.Max(max, Math.Abs(s[i]));
            }
            for (int i = 0; i < s.Length; i++) s[i] *= peak / max;
            return s;
        }

        // ---- the sounds ----

        /// <summary>Low punchy thump (about 110 Hz, dropping a little) with a click on top; the loudest clip.</summary>
        public static float[] CountdownHit()
        {
            float[] s = Make(0.5f), click = High(Noise(s.Length, 1), 3000f);
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                ph += 2f * (float)Math.PI * 110f * (1f + 0.4f * Decay(t, 0.03f)) / Rate;
                s[i] = (float)Math.Sin(ph) * Decay(t, 0.12f) + click[i] * 0.7f * Decay(t, 0.003f);
            }
            return Finish(s, 0.9f);
        }

        /// <summary>A narrow 2.6 kHz tone with a fast 30 Hz warble. Pass 0.4 (start), 0.5 (point) or 0.15 (one fault blast).</summary>
        public static float[] Whistle(float seconds)
        {
            float[] s = Make(seconds);
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                ph += 2f * (float)Math.PI * (2600f + 90f * Sine(30f, t)) / Rate;
                s[i] = (float)Math.Sin(ph) * Math.Min(1f, t / 0.02f);
            }
            return Finish(s, 0.6f);
        }

        /// <summary>Very short quiet blip near 1.8 kHz (serve clock).</summary>
        public static float[] Tick()
        {
            float[] s = Make(0.03f);
            for (int i = 0; i < s.Length; i++) s[i] = Sine(1800f, T(i)) * Decay(T(i), 0.008f);
            return Finish(s, 0.3f);
        }

        /// <summary>Short soft upward blip.</summary>
        public static float[] Toss()
        {
            float[] s = Make(0.08f);
            float ph = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                ph += 2f * (float)Math.PI * (500f + 5000f * t) / Rate;     // 500 -> 900 Hz
                s[i] = (float)Math.Sin(ph) * Decay(t, 0.04f);
            }
            return Finish(s, 0.35f);
        }

        /// <summary>Dull thud: low-passed noise plus a 140 Hz tone.</summary>
        public static float[] Bump()
        {
            float[] s = Make(0.12f), n = Low(Noise(s.Length, 2), 400f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * 3f * Decay(T(i), 0.03f) + Sine(140f, T(i)) * Decay(T(i), 0.04f);
            return Finish(s, 0.7f);
        }

        /// <summary>Softer, shorter, higher than the bump.</summary>
        public static float[] Set()
        {
            float[] s = Make(0.07f), n = Low(Noise(s.Length, 3), 1200f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * 2f * Decay(T(i), 0.02f) + Sine(260f, T(i)) * Decay(T(i), 0.02f);
            return Finish(s, 0.45f);
        }

        /// <summary>Full-band crack: a bright noise burst over a 180 Hz punch.</summary>
        public static float[] Spike()
        {
            float[] s = Make(0.15f), n = Noise(s.Length, 4);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * Decay(T(i), 0.03f) + 0.6f * Sine(180f, T(i)) * Decay(T(i), 0.05f);
            return Finish(s, 0.85f);
        }

        /// <summary>Rising noise whoosh, brighter and louder toward the end (the last 0.25 s before contact).</summary>
        public static float[] SpikeCharge()
        {
            float[] s = Make(0.25f), n = Noise(s.Length, 5);
            float y = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i) / 0.25f;
                float a = 1f - (float)Math.Exp(-2 * Math.PI * (300f + 4700f * t) / Rate);   // the filter opens up
                y += a * (n[i] - y);
                s[i] = y * t * t;
            }
            return Finish(s, 0.4f);
        }

        /// <summary>Light upward whoosh.</summary>
        public static float[] Jump()
        {
            float[] s = Make(0.15f), n = Noise(s.Length, 6);
            float y = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i) / 0.15f;
                float a = 1f - (float)Math.Exp(-2 * Math.PI * (500f + 2000f * t) / Rate);
                y += a * (n[i] - y);
                s[i] = y * (float)Math.Pow(Math.Sin(Math.PI * t), 2);
            }
            return Finish(s, 0.3f);
        }

        /// <summary>Soft sand thud (the Play call makes it louder for a dive).</summary>
        public static float[] Landing()
        {
            float[] s = Make(0.15f), n = Low(Noise(s.Length, 7), 500f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * 3f * Decay(T(i), 0.04f) + 0.3f * Sine(80f, T(i)) * Decay(T(i), 0.05f);
            return Finish(s, 0.5f);
        }

        /// <summary>Hard slap, between the bump and the spike.</summary>
        public static float[] Block()
        {
            float[] s = Make(0.12f), n = Low(Noise(s.Length, 8), 2500f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * 1.5f * Decay(T(i), 0.02f) + 0.7f * Sine(200f, T(i)) * Decay(T(i), 0.04f);
            return Finish(s, 0.75f);
        }

        /// <summary>Ball on the sand: soft low thump.</summary>
        public static float[] Sand()
        {
            float[] s = Make(0.2f), n = Low(Noise(s.Length, 9), 250f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * 4f * Decay(T(i), 0.06f) + Sine(70f, T(i)) * Decay(T(i), 0.08f);
            return Finish(s, 0.45f);
        }

        /// <summary>Short rustle for the net.</summary>
        public static float[] Net()
        {
            float[] s = Make(0.15f), n = High(Noise(s.Length, 10), 2000f);
            for (int i = 0; i < s.Length; i++) s[i] = n[i] * Decay(T(i), 0.05f) * (0.6f + 0.4f * Sine(45f, T(i)));
            return Finish(s, 0.3f);
        }

        /// <summary>Band-passed noise that swells for about a second, then fades. 'big' lasts longer and is louder.</summary>
        public static float[] Crowd(bool big)
        {
            float[] s = Make(big ? 3f : 2f), n = Noise(s.Length, big ? 12 : 11);
            float[] hi = Low(n, 2500f), lo = Low(n, 400f);
            float fade = big ? 2f : 1f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float env = t < 1f ? (float)Math.Sin(t * Math.PI / 2) : Math.Max(0f, 1f - (t - 1f) / fade);
                s[i] = (hi[i] - lo[i]) * env;
            }
            return Finish(s, big ? 0.8f : 0.5f);
        }

        // ---- music ----

        /// <summary>Exact length of the music loop: 8 bars of 4 beats at 120 BPM (16 s).</summary>
        public static int MusicSamples => (int)Math.Round(MusicBars * 4 * 60.0 / Bpm * Rate);

        // Bass root and lead notes (MIDI numbers, 0 = rest) for each bar: C, Am, F, G, twice. Lead = 4 quarter notes a bar.
        static readonly int[] BassMidi = { 36, 33, 41, 43, 36, 33, 41, 43 };
        static readonly int[][] Lead =
        {
            new[] { 72, 76, 79, 76 }, new[] { 69, 72, 76, 72 }, new[] { 77, 72, 69, 72 }, new[] { 71, 74, 79, 74 },
            new[] { 79, 76, 72, 76 }, new[] { 76, 72, 69, 72 }, new[] { 77, 81, 77, 72 }, new[] { 74, 71, 67, 0 },
        };
        static float Hz(int midi) => 440f * (float)Math.Pow(2, (midi - 69) / 12.0);

        /// <summary>The loop: kick on every beat, hat on the off-beats, a two-note bass (root, then fifth) and a short major-key lead.</summary>
        public static float[] Music()
        {
            var s = new float[MusicSamples];
            float beat = 60f / Bpm;
            float[] hat = High(Noise(s.Length, 13), 6000f);
            for (int b = 0; b < MusicBars * 4; b++)
            {
                int bar = b / 4, q = b % 4;
                int start = (int)(b * beat * Rate);
                float bass = Hz(BassMidi[bar]);
                float bassHz = q % 2 == 0 ? bass : bass * 1.5f;           // root on 1 and 3, fifth on 2 and 4
                int lead = Lead[bar][q];
                for (int i = 0; i < (int)(beat * Rate) && start + i < s.Length; i++)
                {
                    float t = T(i), v = 0f;
                    v += 0.9f * (float)Math.Sin(2 * Math.PI * 50f * t * (1 + 1.2f * Decay(t, 0.03f)) ) * Decay(t, 0.1f);   // kick
                    v += 0.5f * Sine(bassHz, t) * Decay(t, 0.25f);                                                       // bass
                    if (lead > 0) v += 0.35f * Tri(Hz(lead), t) * Decay(t, 0.18f);                                           // lead
                    s[start + i] += v;
                }
                // off-beat hat: half a beat after each beat
                int hs = start + (int)(beat * 0.5f * Rate);
                for (int i = 0; i < (int)(0.05f * Rate) && hs + i < s.Length; i++) s[hs + i] += hat[hs + i] * 0.5f * Decay(T(i), 0.012f);
            }
            return Finish(s, 0.5f);
        }

        static float Tri(float hz, float t) => 2f * Math.Abs(2f * (hz * t - (float)Math.Floor(hz * t + 0.5f))) - 1f;
    }
}
