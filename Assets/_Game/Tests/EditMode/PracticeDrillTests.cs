using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Core;
using UnityEngine;

namespace Sandspike.Tests
{
    public class PracticeDrillTests
    {
        static readonly Drill[] Feeding = { Drill.Receive, Drill.Set, Drill.Spike, Drill.Block };

        // every drill, a spread of player spots on the near half, and the extreme nudges
        static System.Collections.Generic.IEnumerable<(Drill d, Vector3 p, float jx, float jz)> Cases()
        {
            foreach (Drill d in Feeding)
                for (float x = -4f; x <= 4f; x += 2f)
                    for (float z = -8f; z <= -0.5f; z += 2.5f)
                        foreach (float jx in new[] { -1f, 0f, 1f })
                            foreach (float jz in new[] { -1f, 1f })
                                yield return (d, new Vector3(x, 0f, z), jx, jz);
        }

        static Vector3 LaunchVelocity(DrillBall b) =>
            BallTrajectory.SolveLaunchVelocity(b.start, new Vector3(b.target.x, BallTrajectory.BallRadius, b.target.z), b.flightTime);

        [Test]
        public void EveryFedBall_LandsOnThePlayersHalf_InsideTheLines()
        {
            foreach (var c in Cases())
            {
                DrillBall b = PracticeDrills.BallFor(c.d, c.p, c.jx, c.jz);
                Assert.Less(b.target.z, 0f, $"{c.d} must land on the near half");
                Assert.LessOrEqual(Mathf.Abs(b.target.x), 4f, $"{c.d} must land inside the side lines");
                Assert.GreaterOrEqual(b.target.z, -8f, $"{c.d} must land inside the end line");
            }
        }

        [Test]
        public void EveryFedBall_ClearsTheNet_OrNeverReachesIt()
        {
            foreach (var c in Cases())
            {
                DrillBall b = PracticeDrills.BallFor(c.d, c.p, c.jx, c.jz);
                Assert.IsTrue(BallTrajectory.ClearsNet(b.start, LaunchVelocity(b), b.flightTime), $"{c.d} from {c.p} must get over the net");
            }
        }

        [Test]
        public void ReceiveAndSetCrossTheNet_SpikeSetStaysOnTheNearSide()
        {
            Vector3 p = new Vector3(0f, 0f, -4f);
            Assert.Greater(PracticeDrills.BallFor(Drill.Receive, p, 0f, 0f).start.z, 0f, "Receive comes from the far side");
            Assert.Greater(PracticeDrills.BallFor(Drill.Set, p, 0f, 0f).start.z, 0f);
            DrillBall s = PracticeDrills.BallFor(Drill.Spike, p, 0f, 0f);
            Assert.Less(s.start.z, 0f, "the spike drill's set comes from behind the player");
            Assert.Greater(s.target.z, -2.5f, "and lands near the net");
            Assert.GreaterOrEqual(BallTrajectory.ApexHeight(s.start, LaunchVelocity(s)), 4f, "a high set");
        }

        [Test]
        public void SetBallIsSlowerThanTheReceiveBall()
        {
            Vector3 p = new Vector3(0f, 0f, -4f);
            Assert.Greater(PracticeDrills.BallFor(Drill.Set, p, 0f, 0f).flightTime, PracticeDrills.BallFor(Drill.Receive, p, 0f, 0f).flightTime);
        }

        [Test]
        public void BlockBall_CrossesTheNetAboveItAtSpikeSpeed_WherePlayerStands()
        {
            Vector3 p = new Vector3(2f, 0f, -1.5f);
            DrillBall b = PracticeDrills.BallFor(Drill.Block, p, 0f, 0f);
            Assert.IsTrue(BallTrajectory.CrossesNetAt(b.start, LaunchVelocity(b), b.flightTime, out _, out float h), "it crosses the net");
            Assert.GreaterOrEqual(h, BallTrajectory.NetTopHeight + BallTrajectory.BallRadius);
            Assert.LessOrEqual(h, 3.4f, "low enough for a jumping block to reach");
            Assert.AreEqual(2f, b.start.x, 0.01f, "it crosses where the player is");
            Assert.AreEqual(PracticeDrills.BlockBallSpeed, Vector3.Distance(b.start, b.target) / b.flightTime, 0.1f);
        }

        [Test]
        public void ServeHasNoFeeder_AndTheDrillPickerWraps()
        {
            Assert.IsFalse(PracticeDrills.Feeds(Drill.Serve));
            foreach (Drill d in Feeding) Assert.IsTrue(PracticeDrills.Feeds(d));
            Assert.AreEqual(Drill.Serve, PracticeDrills.Next(Drill.Receive, -1));
            Assert.AreEqual(Drill.Receive, PracticeDrills.Next(Drill.Serve, 1));
            Assert.AreEqual(5, PracticeDrills.Names.Length);
        }

        [Test]
        public void DrillStartSpots_AreOnThePlayersHalf()
        {
            foreach (Drill d in System.Enum.GetValues(typeof(Drill))) Assert.Less(PracticeDrills.StartSpot(d).z, 0f);
        }
    }
}
