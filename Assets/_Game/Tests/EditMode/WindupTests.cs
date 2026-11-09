using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>Wind-up hits and the instant variable jump (the parts that do not need a ball in flight).</summary>
    public class WindupTests
    {
        const float G = 25f, Tap = 0.9f, Full = 1.6f, Boost = 0.35f;

        // Mirrors PlayerController.UpdateJump/Move: takeoff at the tap speed, then extra accel while held. Returns the apex.
        static float SimulateJump(float holdSeconds, Stamina stamina = null)
        {
            float a = ContactTiming.JumpBoostAccel(G, Tap, Full, Boost);
            var meter = new ChargeMeter { secondsToFull = Boost };
            meter.Begin();
            float y = 0f, v = ContactTiming.JumpSpeed(G, Tap), apex = 0f, t = 0f;
            const float dt = 0.001f;
            while (v > 0f || y > 0f)
            {
                if (meter.IsCharging && t < holdSeconds && v > 0f)
                {
                    float before = meter.Value;
                    meter.Tick(dt, stamina != null ? stamina.MaxChargeAllowed : 1f);
                    float used = meter.Value - before;
                    v += a * used * Boost;
                    stamina?.SpendJump(used);
                }
                v -= G * dt;
                y += v * dt;
                t += dt;
                if (y > apex) apex = y;
                if (y <= 0f && v < 0f) break;
            }
            return apex;
        }

        [Test]
        public void Jump_Tap_PeaksAtAbout09m() => Assert.AreEqual(0.9f, SimulateJump(0f), 0.02f);

        [Test]
        public void Jump_FullHold_PeaksAtAbout16m() => Assert.AreEqual(1.6f, SimulateJump(1f), 0.03f);

        [Test]
        public void Jump_ShortHold_IsBetweenTapAndFull()
        {
            float h = SimulateJump(0.15f);
            Assert.Greater(h, 0.95f);
            Assert.Less(h, 1.55f);
        }

        [Test]
        public void Jump_StaminaCost_ScalesWithBoostUsed()
        {
            var full = new Stamina(); SimulateJump(1f, full);
            var half = new Stamina(); SimulateJump(0.175f, half);
            var tap = new Stamina(); SimulateJump(0f, tap);
            Assert.AreEqual(75f, full.Current, 0.5f);      // max 25
            Assert.AreEqual(87.5f, half.Current, 0.8f);
            Assert.AreEqual(100f, tap.Current, 1e-4f);     // a tap is free
        }

        [Test]
        public void Jump_Boost_IsLimitedByTheStaminaCap()
        {
            var s = new Stamina();
            s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f);   // 0 stamina: cap is 0.5
            Assert.Less(SimulateJump(1f, s), SimulateJump(1f));
        }

        [Test]
        public void ChargeMeter_Lock_KeepsTheChargeAndStopsGrowing()
        {
            var m = new ChargeMeter { secondsToFull = 1f };
            m.Begin();
            m.Tick(0.4f, 1f);
            m.Lock();                       // button released early
            m.Tick(0.4f, 1f);
            Assert.IsFalse(m.IsCharging);
            Assert.AreEqual(0.4f, m.Value, 1e-4f);
            m.Cancel();
            Assert.AreEqual(0f, m.Value);
        }

        [Test]
        public void ChargeMeter_WhileHeld_KeepsBuilding_UpToTheCap()
        {
            var m = new ChargeMeter { secondsToFull = 1f };
            m.Begin();
            m.Tick(0.3f, 1f); m.Tick(0.3f, 1f);
            Assert.AreEqual(0.6f, m.Value, 1e-4f);
            m.Tick(1f, 0.7f);
            Assert.AreEqual(0.7f, m.Value, 1e-4f);
        }

        [Test]
        public void ContactPoint_IsTheSameWhateverTheCharge()
        {
            var go = new GameObject("p", typeof(CharacterController));
            var p = go.AddComponent<PlayerController>();
            go.transform.position = new Vector3(1f, 0f, -3f);
            foreach (HitType type in new[] { HitType.Bump, HitType.Set, HitType.Spike })
            {
                Vector3 before = p.ContactPointAt(type, 0.2f);
                p.SpikeCharge.Begin(); p.SpikeCharge.Tick(0.8f, 1f);       // fully wound up
                Assert.AreEqual(1f, p.SpikeCharge.Value, 1e-4f);
                Assert.AreEqual(before, p.ContactPointAt(type, 0.2f));
                p.SpikeCharge.Cancel();
            }
            Object.DestroyImmediate(go);
        }
    }
}
