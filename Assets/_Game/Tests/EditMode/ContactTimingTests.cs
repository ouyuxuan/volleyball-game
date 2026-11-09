using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>Timing comes from the ball passing the player's contact point, never from landing time.</summary>
    public class ContactTimingTests
    {
        const float G = 9.81f;
        const float Reach = 0.6f;
        static readonly HitResolver Resolver = new HitResolver(new HitTuning(), new System.Random(1));

        // A ball whose arc is at 'where' exactly 'atTime' seconds from now.
        static System.Func<float, Vector3> BallThrough(Vector3 where, float atTime)
        {
            Vector3 start = new Vector3(where.x, 5f, where.z + 6f);
            Vector3 v0 = BallTrajectory.SolveLaunchVelocity(start, where, atTime, G);
            return t => BallTrajectory.PositionAt(start, v0, t, G);
        }

        static HitResult Grade(HitType type, float releaseTime, float idealTime, float handHeight = 3.6f)
        {
            return Resolver.Resolve(new HitInput { type = type, charge = 1f, timingError = releaseTime - idealTime, contactHeight = handHeight, aimPoint = new Vector3(0, 0, 4), hitterPosition = Vector3.zero });
        }

        static System.Func<float, Vector3> JumpingHand(float vy, float g, float takeOffAgo, float handOffset) =>
            t => new Vector3(0f, Mathf.Max(0f, vy * (takeOffAgo + t) - 0.5f * g * (takeOffAgo + t) * (takeOffAgo + t)) + handOffset, 0.3f);

        [Test]
        public void StationaryBump_BallThroughForearms_IsPerfectAtClosestApproach()
        {
            Vector3 forearms = new Vector3(0f, 0.9f, 0.35f);
            var ball = BallThrough(forearms, 0.8f);
            float ideal = ContactTiming.FindIdealTime(ball, t => forearms, -0.4f, 1.5f, out float dist);
            Assert.AreEqual(0.8f, ideal, 0.01f);
            Assert.Less(dist, 0.05f);
            Assert.AreEqual(HitGrade.Perfect, Grade(HitType.Bump, 0.8f, ideal).grade);
            Assert.AreEqual(HitGrade.Good, Grade(HitType.Bump, 0.9f, ideal).grade);
            Assert.AreEqual(HitGrade.Ok, Grade(HitType.Bump, 1.05f, ideal).grade);
            Assert.AreEqual(HitGrade.Miss, Grade(HitType.Bump, 1.2f, ideal).grade);
        }

        [Test]
        public void Spike_WellTimedJump_HandMeetsBall_PerfectAndStrong()
        {
            float g = 25f, vy = Mathf.Sqrt(2f * g * 1.6f);
            Vector3 handAtApex = new Vector3(0f, 1.6f + 2.3f, 0.3f);
            var ball = BallThrough(handAtApex, 0.5f);
            float takeOffAgo = vy / g - 0.5f;   // the apex arrives exactly when the ball does
            float ideal = ContactTiming.FindIdealTime(ball, JumpingHand(vy, g, takeOffAgo, 2.3f), -0.4f, 1.5f, out float dist);
            Assert.LessOrEqual(dist, Reach);
            var r = Grade(HitType.Spike, ideal, ideal, handAtApex.y);
            Assert.AreEqual(HitGrade.Perfect, r.grade);
            Assert.AreEqual(1f, r.quality, 1e-4f);
        }

        [Test]
        public void Spike_SlightlyEarlyJump_HandIsLower_ConnectsButWeaker()
        {
            float g = 25f, vy = Mathf.Sqrt(2f * g * 1.6f);
            float takeOffAgo = 0.2f;                       // jump started early: the hand is already falling
            float T = 0.45f;                               // when the ball arrives
            float handY = Mathf.Max(0f, vy * (takeOffAgo + T) - 0.5f * g * (takeOffAgo + T) * (takeOffAgo + T)) + 2.3f;
            var ball = BallThrough(new Vector3(0f, handY, 0.3f), T);
            float ideal = ContactTiming.FindIdealTime(ball, JumpingHand(vy, g, takeOffAgo, 2.3f), -0.4f, 1.5f, out float dist);
            Assert.LessOrEqual(dist, Reach);
            var low = Grade(HitType.Spike, ideal, ideal, handY);
            var best = Grade(HitType.Spike, ideal, ideal, 3.9f);
            Assert.IsTrue(low.contact);
            Assert.Less(handY, 3.6f);
            Assert.Less(low.quality, best.quality);
        }

        [Test]
        public void Spike_WayOffTiming_IsAMiss()
        {
            var r = Grade(HitType.Spike, 1.0f + 0.36f, 1.0f);
            Assert.AreEqual(HitGrade.Miss, r.grade);
            Assert.IsFalse(r.contact);
        }

        [Test]
        public void OutOfReach_IsAMiss()
        {
            Vector3 forearms = new Vector3(0f, 0.9f, 0.35f);
            var ball = BallThrough(forearms + new Vector3(2f, 0.5f, 0f), 0.8f);   // passes 2 m to the side
            ContactTiming.FindIdealTime(ball, t => forearms, -0.4f, 1.5f, out float dist);
            Assert.Greater(dist, Reach);
            var r = Resolver.Resolve(new HitInput { type = HitType.Bump, timingError = 0f, closestDistance = dist, reachRadius = Reach, aimPoint = new Vector3(0, 0, 4), hitterPosition = Vector3.zero });
            Assert.AreEqual(HitGrade.Miss, r.grade);
            Assert.IsFalse(r.contact);
        }

        [Test]
        public void EarlyRelease_InsideWindow_IsHeldUntilTheIdealMoment()
        {
            Vector3 forearms = new Vector3(0f, 0.9f, 0.35f);
            var ball = BallThrough(forearms, 0.5f);
            float ideal = ContactTiming.FindIdealTime(ball, t => forearms, -0.4f, 1.0f, out float dist);
            Assert.AreEqual(0.5f, ideal, 0.01f);          // the hit fires 0.5 s from now (the moment of contact)
            // released now: error = -0.5 (too early, Miss); released 0.3 s early: error -0.3 -> connects as Ok at the ideal time
            Assert.AreEqual(HitGrade.Miss, Grade(HitType.Bump, 0f, ideal).grade);
            var held = Grade(HitType.Bump, ideal - 0.3f, ideal);
            Assert.IsTrue(held.contact);
            Assert.AreEqual(HitGrade.Ok, held.grade);
        }

        [Test]
        public void ArcOfAMissedSwing_IsUnchanged()
        {
            // A Miss launches nothing, so the ball keeps following the same parabola and lands where it was going to.
            var go = new GameObject("TestBall");
            try
            {
                var ball = go.AddComponent<BallController>();
                ball.Launch(new Vector3(0f, 2f, 6f), new Vector3(1f, 0f, -4f), 2f, false);
                Vector3 before = ball.LandingPoint;
                var miss = Grade(HitType.Bump, 0.36f, 0f);       // what the controller gets for a swing that is too far off
                Assert.IsFalse(miss.contact);                     // the controller then does not call Launch
                Assert.AreEqual(before, ball.LandingPoint);
                Assert.AreEqual(1f, ball.LandingPoint.x, 1e-3f);
                Assert.AreEqual(-4f, ball.LandingPoint.z, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PlayerHeight_FollowsTheJumpArcAndStopsAtGround()
        {
            float g = 25f, vy = 8f;
            Assert.AreEqual(0f, ContactTiming.PlayerHeightAt(0f, 0f, g, 0.5f), 1e-5f);
            Assert.AreEqual(vy * 0.2f - 0.5f * g * 0.04f, ContactTiming.PlayerHeightAt(0f, vy, g, 0.2f), 1e-4f);
            Assert.AreEqual(0f, ContactTiming.PlayerHeightAt(0f, vy, g, 2f), 1e-5f);
        }
    }
}
