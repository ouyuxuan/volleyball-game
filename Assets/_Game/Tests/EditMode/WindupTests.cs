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
        static readonly JumpArc Arc = new JumpArc(30f, 2.2f, 0.35f);
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
            return Arc.ApexHeight(v);
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
        public void JumpAndSpike_ChargeTogether_JumpReleaseKeepsTheSpikeCharge()
        {
            var go = new GameObject("p", typeof(CharacterController));
            var p = go.AddComponent<PlayerController>();
            var brain = new FakeBrain();
            var t = typeof(PlayerController);
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            t.GetMethod("Awake", Any).Invoke(p, null);
            t.GetField("brain", Any).SetValue(p, brain);
            t.GetField("grounded", Any).SetValue(p, true);
            var jumpUpdate = t.GetMethod("UpdateJump", Any);
            var hitsUpdate = t.GetMethod("UpdateHits", Any);
            void Frame() { jumpUpdate.Invoke(p, new object[] { Dt }); hitsUpdate.Invoke(p, new object[] { Dt }); }

            brain.jump = new ButtonState(true, true, false);
            brain.spike = new ButtonState(true, true, false);
            Frame();
            for (int i = 0; i < 19; i++) { brain.jump = new ButtonState(false, true, false); brain.spike = new ButtonState(false, true, false); Frame(); }
            Assert.IsTrue(p.JumpCharge.IsCharging);
            Assert.IsTrue(p.SpikeCharge.IsCharging);

            brain.jump = new ButtonState(false, false, true);   // release the jump: the spike keeps charging, in the air
            brain.spike = new ButtonState(false, true, false);
            Frame();
            Assert.Greater(p.VerticalVelocity, 1f);             // took off
            float before = p.SpikeCharge.Value;
            p.GetType().GetField("grounded", Any).SetValue(p, false);
            for (int i = 0; i < 10; i++) { brain.jump = default; brain.spike = new ButtonState(false, true, false); Frame(); }
            Assert.IsTrue(p.SpikeCharge.IsCharging);
            Assert.Greater(p.SpikeCharge.Value, before);
            Assert.AreEqual(0.5f, p.SpikeCharge.secondsToFull, 1e-4f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpikeReach_IsATallCylinder()
        {
            var hand = new Vector3(1f, 3f, 2f);
            Assert.IsTrue(ContactTiming.SpikeInReach(hand + new Vector3(0f, 0.7f, 0f), hand, 1f, 0.6f, 0.8f));
            Assert.IsTrue(ContactTiming.SpikeInReach(hand + new Vector3(0f, -0.5f, 0f), hand, 1f, 0.6f, 0.8f));
            Assert.IsFalse(ContactTiming.SpikeInReach(hand + new Vector3(0f, 0.9f, 0f), hand, 1f, 0.6f, 0.8f));
            Assert.IsFalse(ContactTiming.SpikeInReach(hand + new Vector3(0f, -0.7f, 0f), hand, 1f, 0.6f, 0.8f));
            Assert.IsFalse(ContactTiming.SpikeInReach(hand + new Vector3(1.1f, 0f, 0f), hand, 1f, 0.6f, 0.8f));   // too far sideways
            Assert.IsTrue(ContactTiming.SpikeInReach(hand + new Vector3(0.9f, 0.7f, 0f), hand, 1f, 0.6f, 0.8f)); // sphere would reject this
        }

        [Test]
        public void BumpAndSetReach_StayASphere()
        {
            var go = new GameObject("p", typeof(CharacterController));
            var p = go.AddComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(p, null);
            var hand = new Vector3(0f, 2f, 0f);
            foreach (var type in new[] { HitType.Bump, HitType.Set })
            {
                Assert.IsTrue(p.InReach(type, hand + new Vector3(0f, 0.9f, 0f), hand), type + " straight above, inside the sphere");
                Assert.IsFalse(p.InReach(type, hand + new Vector3(0.7f, 0.8f, 0f), hand), type + " diagonal, outside the sphere");
            }
            Object.DestroyImmediate(go);
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
