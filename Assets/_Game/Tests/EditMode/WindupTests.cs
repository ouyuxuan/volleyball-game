using System.Reflection;
using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>Wind-up hits and the charged jump (the parts that do not need a ball in flight).</summary>
    public class WindupTests
    {
        const float G = 25f;
        const float Dt = 0.02f;

        // Drives the real PlayerController.UpdateJump: press, hold for 'holdSeconds' (frame by frame), release. Returns the jump's apex.
        static float JumpApex(float holdSeconds, Stamina stamina = null)
        {
            var go = new GameObject("p", typeof(CharacterController));
            var p = go.AddComponent<PlayerController>();
            var brain = new FakeBrain();
            var t = typeof(PlayerController);
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            t.GetMethod("Awake", Any).Invoke(p, null);
            t.GetField("brain", Any).SetValue(p, brain);
            t.GetField("grounded", Any).SetValue(p, true);
            if (stamina != null) t.GetField("stamina", Any).SetValue(p, stamina);
            var update = t.GetMethod("UpdateJump", Any);

            brain.jump = new ButtonState(true, true, false);   // press
            update.Invoke(p, new object[] { Dt });
            for (float s = Dt; s < holdSeconds - 1e-4f; s += Dt)
            {
                brain.jump = new ButtonState(false, true, false);   // hold
                update.Invoke(p, new object[] { Dt });
            }
            brain.jump = new ButtonState(false, false, true);   // release
            update.Invoke(p, new object[] { Dt });

            float v = p.VerticalVelocity;
            Object.DestroyImmediate(go);
            return v * v / (2f * G);
        }

        [Test]
        public void Jump_Tap_PeaksAtAbout09m() => Assert.AreEqual(0.9f, JumpApex(0f), 0.03f);

        [Test]
        public void Jump_FullCharge_PeaksAtAbout16m() => Assert.AreEqual(1.6f, JumpApex(0.42f), 0.03f);

        [Test]
        public void Jump_LongerHold_GoesHigherUntilFull()
        {
            Assert.Greater(JumpApex(0.2f), JumpApex(0.1f));
            Assert.Greater(JumpApex(0.42f), JumpApex(0.2f) + 0.2f);
            Assert.AreEqual(JumpApex(0.42f), JumpApex(1.0f), 0.02f);   // charge stops at full
        }

        [Test]
        public void Jump_StaminaCost_ScalesWithCharge()
        {
            var full = new Stamina(); JumpApex(0.42f, full);
            var half = new Stamina(); JumpApex(0.22f, half);
            var tap = new Stamina(); JumpApex(0f, tap);
            Assert.AreEqual(75f, full.Current, 1f);     // max 25
            Assert.AreEqual(87.5f, half.Current, 1.5f);
            Assert.AreEqual(100f, tap.Current, 0.5f);   // a tap is (almost) free
        }

        [Test]
        public void Jump_Charge_IsLimitedByTheStaminaCap()
        {
            var s = new Stamina();
            s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f);   // 0 stamina: cap is 0.5
            float capped = JumpApex(0.42f, s);
            Assert.AreEqual(Mathf.Lerp(0.9f, 1.6f, 0.5f), capped, 0.04f);
        }

        [Test]
        public void HitCue_InReach_IsADistanceCheck()
        {
            Assert.IsTrue(ContactTiming.InReach(new Vector3(0f, 1f, 0f), new Vector3(0f, 1.5f, 0f), 1f));
            Assert.IsTrue(ContactTiming.InReach(new Vector3(0f, 0f, 1f), Vector3.zero, 1f));      // exactly on the edge
            Assert.IsFalse(ContactTiming.InReach(new Vector3(0f, 2.5f, 0f), new Vector3(0f, 1.5f, 0.1f), 1f));
        }

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
