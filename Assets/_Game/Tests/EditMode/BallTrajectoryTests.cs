using NUnit.Framework;
using Sandspike.Ball;
using UnityEngine;

namespace Sandspike.Tests
{
    public class BallTrajectoryTests
    {
        const float G = 9.81f;

        [Test]
        public void LaunchVelocity_LandsExactlyOnTargetAtFlightTime()
        {
            var start = new Vector3(1f, 2f, -5f);
            var target = new Vector3(-2f, 0.11f, 4f);
            float t = 1.5f;
            var v0 = BallTrajectory.SolveLaunchVelocity(start, target, t, G);
            var p = BallTrajectory.PositionAt(start, v0, t, G);
            Assert.AreEqual(0f, Vector3.Distance(p, target), 1e-3f);
        }

        [Test]
        public void LaunchVelocity_PositionAtZeroIsStart()
        {
            var start = new Vector3(0f, 1f, -3f);
            var v0 = BallTrajectory.SolveLaunchVelocity(start, new Vector3(1f, 0f, 3f), 1f, G);
            Assert.AreEqual(0f, Vector3.Distance(BallTrajectory.PositionAt(start, v0, 0f, G), start), 1e-6f);
        }

        [Test]
        public void ApexHeight_MatchesPhysics()
        {
            var v0 = new Vector3(0f, 9.81f, 0f); // rises for 1 s, apex = 0.5 * 9.81
            Assert.AreEqual(1f + 4.905f, BallTrajectory.ApexHeight(new Vector3(0f, 1f, 0f), v0, G), 1e-3f);
            Assert.AreEqual(1f, BallTrajectory.ApexTime(v0, G), 1e-4f);
        }

        [Test]
        public void HighArc_ClearsNet()
        {
            var start = new Vector3(0f, 1f, -5f);
            var target = new Vector3(0f, 0.11f, 4f);
            var v0 = BallTrajectory.SolveLaunchVelocity(start, target, 1.5f, G);
            Assert.IsTrue(BallTrajectory.ClearsNet(start, v0, 1.5f));
        }

        [Test]
        public void FlatFastArc_HitsNet()
        {
            // Low and fast: crosses the net plane well below 2.43 m.
            var start = new Vector3(0f, 1f, -5f);
            var target = new Vector3(0f, 0.11f, 5f);
            var v0 = BallTrajectory.SolveLaunchVelocity(start, target, 0.5f, G);
            Assert.IsFalse(BallTrajectory.ClearsNet(start, v0, 0.5f));
        }

        [Test]
        public void ArcStayingOnOneSide_NeverCrossesNet()
        {
            var start = new Vector3(0f, 1f, -6f);
            var v0 = BallTrajectory.SolveLaunchVelocity(start, new Vector3(2f, 0.11f, -2f), 1f, G);
            Assert.IsFalse(BallTrajectory.CrossesNetAt(start, v0, 1f, out _, out _));
            Assert.IsTrue(BallTrajectory.ClearsNet(start, v0, 1f));
        }

        [Test]
        public void NetClearance_BoundaryIsNetTopPlusBallRadius()
        {
            // Horizontal flight at exactly the limit height: just under fails, just over passes.
            float limit = BallTrajectory.NetTopHeight + BallTrajectory.BallRadius;
            var v0 = new Vector3(0f, 0f, 10f);
            var low = new Vector3(0f, limit - 0.01f + 0.5f * G * 0.25f * 0.25f, -2.5f); // compensates drop until z = 0 (t = 0.25 s)
            var high = new Vector3(0f, limit + 0.01f + 0.5f * G * 0.25f * 0.25f, -2.5f);
            Assert.IsFalse(BallTrajectory.ClearsNet(low, v0, 1f));
            Assert.IsTrue(BallTrajectory.ClearsNet(high, v0, 1f));
        }

        [Test]
        public void TimeToReachHeight_DescendingBranch()
        {
            // Thrown up at 9.81 m/s from y = 0: back at y = 0 after 2 s (descending), at 0 s (ascending).
            Assert.AreEqual(2f, BallTrajectory.TimeToReachHeight(0f, 9.81f, 0f, true, G), 1e-4f);
            Assert.AreEqual(0f, BallTrajectory.TimeToReachHeight(0f, 9.81f, 0f, false, G), 1e-4f);
        }

        [Test]
        public void TimeToReachHeight_PositionAtThatTimeHasThatHeight()
        {
            var start = new Vector3(0f, 2f, 0f);
            var v0 = new Vector3(1f, 6f, 2f);
            float h = 1.0f;
            float t = BallTrajectory.TimeToReachHeight(start.y, v0.y, h, true, G);
            Assert.AreEqual(h, BallTrajectory.PositionAt(start, v0, t, G).y, 1e-3f);
        }

        [Test]
        public void TimeToReachHeight_ReturnsNaNWhenNeverReached()
        {
            Assert.IsTrue(float.IsNaN(BallTrajectory.TimeToReachHeight(0f, 1f, 50f, true, G)));
        }

        [Test]
        public void ClosestApproach_FindsTheNearestMoment()
        {
            var start = new Vector3(0f, 1f, 0f);
            var v0 = new Vector3(0f, 0f, 4f); // flies along +z
            var point = new Vector3(0f, 1f - 0.5f * G * 1f, 4f); // where the ball is at t = 1 s
            float t = BallTrajectory.ClosestApproach(start, v0, point, 0f, 2f, out float d, G);
            Assert.AreEqual(1f, t, 0.01f);
            Assert.Less(d, 0.05f);
        }

        [Test]
        public void WithinReach_UsesSphere()
        {
            Assert.IsTrue(BallTrajectory.WithinReach(new Vector3(0f, 1f, 1.1f), new Vector3(0f, 1f, 0f), 1.2f));
            Assert.IsFalse(BallTrajectory.WithinReach(new Vector3(0f, 1f, 1.3f), new Vector3(0f, 1f, 0f), 1.2f));
        }
    }
}
