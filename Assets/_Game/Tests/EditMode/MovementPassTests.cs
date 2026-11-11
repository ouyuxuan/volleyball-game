using System.Reflection;
using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>Sprint, air control, burnout dive, jump cancel and the serve fault checks (FakeBrain pattern from ReleaseHitTests).</summary>
    public class MovementPassTests
    {
        const float Dt = 1f / 60f;
        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static void Set(object o, string field, object v) => o.GetType().GetField(field, Any).SetValue(o, v);

        GameObject playerGo, ballGo;
        PlayerController player;
        FakeBrain brain;

        [SetUp]
        public void SetUp()
        {
            ballGo = new GameObject("ball");
            var ball = ballGo.AddComponent<BallController>();
            playerGo = new GameObject("player", typeof(CharacterController));
            player = playerGo.AddComponent<PlayerController>();
            Set(player, "ball", ball);
            typeof(PlayerController).GetMethod("Awake", Any).Invoke(player, null);
            brain = new FakeBrain();
            Set(player, "brain", brain);   // after Awake, which would look for a brain component
            player.TeleportTo(new Vector3(-6f, 0f, -5f));
            Set(player, "grounded", true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(ballGo);
        }

        void Frames(int n) { for (int i = 0; i < n; i++) player.Tick(Dt); }
        void Seconds(float s) => Frames(Mathf.RoundToInt(s / Dt));
        void SetStamina(float value) { player.Stamina.Drain(player.Stamina.Current - value); }
        void Press(ref ButtonState b) { b = new ButtonState(true, true, false); player.Tick(Dt); b = new ButtonState(false, true, false); }
        void Release(ref ButtonState b) { b = new ButtonState(false, false, true); player.Tick(Dt); b = default; }

        // ---------------- sprint ----------------

        [Test]
        public void Sprint_IsQuarterFaster()
        {
            brain.move = Vector2.right;
            Seconds(0.5f);
            Assert.AreEqual(6f, player.PlanarVelocity.magnitude, 0.01f);
            brain.sprint = true;
            Seconds(0.5f);
            Assert.IsTrue(player.IsSprinting);
            Assert.AreEqual(7.5f, player.PlanarVelocity.magnitude, 0.01f);
        }

        [Test]
        public void Sprint_DrainsTwentyTwoPerSecond_AndDoesNotRegen()
        {
            brain.move = Vector2.right;
            brain.sprint = true;
            Seconds(1f);
            Assert.AreEqual(78f, player.Stamina.Current, 0.5f);
            Seconds(0.5f);   // still sprinting: nothing refills
            Assert.AreEqual(67f, player.Stamina.Current, 0.5f);
        }

        [Test]
        public void Sprint_CantStartBelowFive_ButStartsAbove()
        {
            brain.move = Vector2.right;
            brain.sprint = true;
            SetStamina(4.9f);
            Frames(3);
            Assert.IsFalse(player.IsSprinting);
            player.Stamina.Refill();
            SetStamina(5.2f);
            Frames(1);
            Assert.IsTrue(player.IsSprinting);
        }

        [Test]
        public void Sprint_StopsAtZero()
        {
            brain.move = Vector2.right;
            brain.sprint = true;
            SetStamina(6f);
            Frames(1);
            Assert.IsTrue(player.IsSprinting);
            Seconds(0.33f);                       // 6 / 22 = 0.27 s of sprint
            Assert.IsFalse(player.IsSprinting);
            Assert.AreEqual(0f, player.Stamina.Current, 1e-3f);
        }

        [Test]
        public void Sprint_NeedsMovement()
        {
            brain.sprint = true;
            Frames(5);
            Assert.IsFalse(player.IsSprinting);
        }

        // ---------------- air control ----------------

        float PeakAirSpeed(bool running)
        {
            if (running) { brain.move = Vector2.right; Seconds(0.3f); }
            player.Jump(0f);
            brain.move = Vector2.right;
            float peak = 0f;
            for (int i = 0; i < 25; i++)   // ~0.4 s in the air
            {
                player.Tick(Dt);
                if (!player.IsGrounded) peak = Mathf.Max(peak, player.PlanarVelocity.magnitude);
            }
            return peak;
        }

        [Test]
        public void StandingJump_CapsHorizontalAirSpeed()
        {
            Assert.AreEqual(2.5f, PeakAirSpeed(false), 0.01f);
        }

        [Test]
        public void RunningJump_KeepsTakeoffSpeed()
        {
            Assert.AreEqual(6f, PeakAirSpeed(true), 0.1f);
        }

        // ---------------- dive and burnout ----------------

        [Test]
        public void Dive_WithEnoughStamina_NoBurnout()
        {
            Press(ref brain.dive);
            Assert.IsTrue(player.IsDiving);
            Assert.AreEqual(85f, player.Stamina.Current, 0.1f);   // 100 - 15
            Seconds(1.0f);
            Assert.IsFalse(player.IsBurnedOut);
        }

        [Test]
        public void Dive_AtThreeStamina_StillDives_ThenBurnsOutForTwoSeconds()
        {
            SetStamina(3f);
            Press(ref brain.dive);
            Assert.IsTrue(player.IsDiving);
            Assert.AreEqual(0f, player.Stamina.Current, 1e-3f);
            while (player.IsDiving || player.IsRecovering) { Assert.IsFalse(player.IsBurnedOut); player.Tick(Dt); }
            Assert.IsTrue(player.IsBurnedOut);
            Assert.AreEqual(1f, player.Burnout01, 0.05f);

            // slower, no regen
            brain.move = Vector2.right;
            Seconds(1.0f);
            Assert.AreEqual(0.5f, player.Burnout01, 0.05f);
            Assert.AreEqual(3.6f, player.PlanarVelocity.magnitude, 0.01f);   // 6 x 0.6
            Assert.AreEqual(0f, player.Stamina.Current, 1e-3f);

            Seconds(1.05f);
            Assert.IsFalse(player.IsBurnedOut);
            Seconds(0.5f);
            Assert.AreEqual(6f, player.PlanarVelocity.magnitude, 0.7f);   // normal speed again
        }

        [Test]
        public void Burnout_ChargeCantBuild_JumpIsAHop()
        {
            SetStamina(3f);
            Press(ref brain.dive);
            while (player.IsDiving || player.IsRecovering) player.Tick(Dt);
            Assert.IsTrue(player.IsBurnedOut);

            Press(ref brain.jump);
            Seconds(0.5f);
            Assert.IsTrue(player.JumpCharge.IsCharging);
            Assert.AreEqual(0f, player.JumpCharge.Value, 1e-4f);
            Release(ref brain.jump);
            Assert.Greater(player.VerticalVelocity, 0f);
            Assert.AreEqual(ContactTimingJump(0.9f), player.VerticalVelocity, 0.01f);   // the tap hop, not the full jump
        }

        [Test]
        public void Burnout_BlocksSprint()
        {
            SetStamina(3f);
            Press(ref brain.dive);
            while (player.IsDiving || player.IsRecovering) player.Tick(Dt);
            brain.move = Vector2.right;
            brain.sprint = true;
            Frames(10);
            Assert.IsFalse(player.IsSprinting);
        }

        static float ContactTimingJump(float height) => ContactTiming.JumpSpeed(25f, height);

        // ---------------- jump cancel ----------------

        [Test]
        public void JumpCancel_DiveDuringChargeDivesWithoutJumping()
        {
            Press(ref brain.jump);
            Seconds(0.2f);
            Assert.IsTrue(player.JumpCharge.IsCharging);
            Press(ref brain.dive);
            Assert.IsTrue(player.IsDiving);
            Assert.IsFalse(player.JumpCharge.IsCharging);
            Release(ref brain.jump);                                 // letting go afterwards does not jump
            Assert.AreEqual(85f, player.Stamina.Current, 0.1f);      // only the dive cost: no jump stamina
            Seconds(1.0f);
            Assert.LessOrEqual(player.VerticalVelocity, 0f);
            Assert.IsTrue(player.IsGrounded);
        }

        [Test]
        public void HitButton_DoesNotCancelAJumpCharge()
        {
            Press(ref brain.jump);
            Seconds(0.1f);
            Press(ref brain.spike);
            Seconds(0.1f);
            Assert.IsTrue(player.JumpCharge.IsCharging);
            Release(ref brain.jump);
            Assert.Greater(player.VerticalVelocity, 0f);             // the jump still happens
        }

        // ---------------- serve ----------------

        // Puts the server behind the baseline with the ball in hand, like the MatchManager does.
        BallController ServeSetup(float serverZ, out HitTuning tuning)
        {
            var ball = ballGo.GetComponent<BallController>();
            player.TeleportTo(new Vector3(0f, 0f, serverZ));
            playerGo.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            ball.PlaceAt(player.ContactPointAt(HitType.Serve) + Vector3.up * 0.15f);
            tuning = player.Tuning;
            return ball;
        }

        [Test]
        public void LegalServe_IsNeverAFault_EvenWhenTheNetAssistKicksIn()
        {
            int assisted = 0, count = 0;
            foreach (float serverZ in new[] { -9f, -6f, -3f })
            foreach (float charge in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            for (float x = -4f; x <= 4.001f; x += 1f)
            for (float z = 0.3f; z <= 8f; z += 0.9f)
            {
                BallController ball = ServeSetup(serverZ, out HitTuning t);
                brain.aim = new Vector3(x, 0f, z);
                HitResult serve = player.PredictServe(charge);
                ball.Launch(ball.Position, serve.target, serve.flightTime, true, HitType.Serve);

                // did the net assist change anything? (compare with the plain resolver arc)
                float raw = BallTrajectory.FlightTimeForApex(ball.Position.y, Mathf.Lerp(t.serveApexMin, t.serveApexMax, charge), BallTrajectory.BallRadius);
                if (serve.flightTime > raw + 0.01f) assisted++;
                count++;

                string what = $"serverZ={serverZ} charge={charge} aim=({x},{z})";
                Assert.IsFalse(ball.WillHitNet, "serve hits the net: " + what);
                var rules = new RulesEngine();
                rules.Serve(0);
                PointReason r = rules.BallLanded(ball.LandingPoint.x, ball.LandingPoint.z);
                Assert.AreEqual(PointReason.BallIn, r, "serve is a fault: " + what + " lands " + ball.LandingPoint);
                Assert.AreEqual(0, rules.LastPointWinner, what);
            }
            Debug.Log($"[ServeTest] {count} serves checked, net assist engaged on {assisted}");
        }

        [Test]
        public void Serve_IsNotAReceivingTeamTouch()
        {
            // The MatchManager never feeds the serve to RulesEngine.Touch, so the receivers start from zero.
            var rules = new RulesEngine();
            rules.Serve(0);
            Assert.AreEqual(0, rules.TouchesFor(1));
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1));   // receivers: touch 1
            Assert.AreEqual(PointReason.None, rules.Touch(3, 1));   // touch 2
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1));   // touch 3 is still legal
            Assert.AreEqual(3, rules.TouchesFor(1));
        }
    }
}
