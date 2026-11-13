using System;
using NUnit.Framework;
using Sandspike.Audio;

namespace Sandspike.Tests
{
    /// <summary>The synthesized sounds: right length, no NaN, sensible peak, no clicks, repeatable, and the right "colour".</summary>
    public class SoundSynthTests
    {
        static readonly (string name, Func<float[]> make, float seconds)[] All =
        {
            ("CountdownHit", SoundSynth.CountdownHit, 0.5f), ("Whistle0.4", () => SoundSynth.Whistle(0.4f), 0.4f),
            ("Whistle0.5", () => SoundSynth.Whistle(0.5f), 0.5f), ("Tick", SoundSynth.Tick, 0.03f), ("Toss", SoundSynth.Toss, 0.08f),
            ("Bump", SoundSynth.Bump, 0.12f), ("Set", SoundSynth.Set, 0.07f), ("Spike", SoundSynth.Spike, 0.15f),
            ("SpikeCharge", SoundSynth.SpikeCharge, 0.25f), ("Jump", SoundSynth.Jump, 0.15f), ("Landing", SoundSynth.Landing, 0.15f),
            ("Block", SoundSynth.Block, 0.12f), ("Sand", SoundSynth.Sand, 0.2f), ("Net", SoundSynth.Net, 0.15f),
            ("Crowd", () => SoundSynth.Crowd(false), 2f), ("CrowdBig", () => SoundSynth.Crowd(true), 3f),
            ("Music", SoundSynth.Music, 16f),
        };

        [Test]
        public void EveryClip_HasLengthPeakAndSoftEdges()
        {
            foreach (var (name, make, seconds) in All)
            {
                float[] s = make();
                Assert.That(s.Length, Is.EqualTo(seconds * SoundSynth.Rate).Within(seconds * SoundSynth.Rate * 0.05f), name + " length");
                float peak = 0f;
                foreach (float v in s) { Assert.IsFalse(float.IsNaN(v), name + " NaN"); peak = Math.Max(peak, Math.Abs(v)); }
                Assert.That(peak, Is.InRange(0.2f, 0.9f), name + " peak");
                Assert.That(Math.Abs(s[0]), Is.LessThan(0.01f), name + " start");
                Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(0.01f), name + " end");
            }
        }

        [Test]
        public void SameCallTwice_GivesSameSamples()
        {
            foreach (var (name, make, _) in All) CollectionAssert.AreEqual(make(), make(), name);
        }

        [Test]
        public void Whistle_StrongestFrequencyIsNear2600()
        {
            float[] s = SoundSynth.Whistle(0.4f);
            float best = 0f, bestHz = 0f;
            for (float hz = 500f; hz <= 8000f; hz += 50f)
            {
                float p = Power(s, hz);
                if (p > best) { best = p; bestHz = hz; }
            }
            Assert.That(bestHz, Is.EqualTo(2600f).Within(150f));
        }

        [Test]
        public void Bump_IsLowAndSpike_IsBright()
        {
            float[] bump = SoundSynth.Bump(), spike = SoundSynth.Spike();
            Assert.Greater(Band(bump, 50f, 500f), Band(bump, 2000f, 10000f));
            Assert.GreaterOrEqual(Band(spike, 2000f, 10000f), Band(spike, 50f, 10000f) / 3f);
        }

        [Test]
        public void Loudness_IsTheLoudest50ms_AndIgnoresSilenceAroundTheSound()
        {
            var tone = new float[SoundSynth.Rate / 2];                       // half a second of a 440 Hz sine at amplitude 0.5
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 440 * i / SoundSynth.Rate);
            var padded = new float[tone.Length * 4];
            tone.CopyTo(padded, tone.Length);
            Assert.That(SoundSynth.Loudness(tone, SoundSynth.Rate, 1), Is.EqualTo(0.5f / (float)Math.Sqrt(2)).Within(0.01f));
            Assert.That(SoundSynth.Loudness(padded, SoundSynth.Rate, 1), Is.EqualTo(SoundSynth.Loudness(tone, SoundSynth.Rate, 1)).Within(0.001f));
        }

        [Test]
        public void Music_LoopsCleanlyAndIsEightBars()
        {
            float[] m = SoundSynth.Music();
            Assert.AreEqual(8 * 4 * 60 * SoundSynth.Rate / (int)SoundSynth.Bpm, m.Length);
            Assert.Less(Math.Abs(m[0]), 0.02f);
            Assert.Less(Math.Abs(m[m.Length - 1]), 0.02f);
        }

        // Signal power at one frequency (a single DFT bin), and the sum over a band in 50 Hz steps.
        static float Power(float[] s, float hz)
        {
            double re = 0, im = 0, w = 2 * Math.PI * hz / SoundSynth.Rate;
            for (int i = 0; i < s.Length; i++) { re += s[i] * Math.Cos(w * i); im += s[i] * Math.Sin(w * i); }
            return (float)(re * re + im * im);
        }

        static float Band(float[] s, float from, float to)
        {
            float sum = 0f;
            for (float hz = from; hz <= to; hz += 50f) sum += Power(s, hz);
            return sum;
        }
    }
}
