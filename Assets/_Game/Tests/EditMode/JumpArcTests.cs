using NUnit.Framework;
using Sandspike.Ball;

namespace Sandspike.Tests
{
    /// <summary>The floaty jump: same apex heights, quick launch, long hang near the top.</summary>
    public class JumpArcTests
    {
        static readonly JumpArc Arc = new JumpArc(30f, 2.2f, 0.35f);

        // Steps the arc the way PlayerController.Move does; returns apex and airtime.
        static void Simulate(float apex, out float peak, out float airTime, out float t80, out float launch)
        {
            launch = Arc.LaunchSpeed(apex);
            const float dt = 1f / 120f;
            float y = 0f, v = launch, t = 0f; peak = 0f; t80 = -1f;
            do
            {
                y += Arc.Advance(ref v, dt);   // the same step PlayerController.Move takes
                t += dt;
                if (y > peak) peak = y;
                if (t80 < 0f && y >= 0.8f * apex) t80 = t;
            } while (y > 0f);
            airTime = t;
        }

        [TestCase(0.9f, 0.72f)]
        [TestCase(1.6f, 0.90f)]
        public void Apex_AndAirtime_MatchTheTargets(float apex, float air)
        {
            Simulate(apex, out float peak, out float airTime, out _, out _);
            Assert.AreEqual(apex, peak, 0.02f);
            Assert.AreEqual(air, airTime, 0.05f);
            Assert.AreEqual(Arc.AirTime(apex), airTime, 0.03f);
        }

        [Test]
        public void SpendsLongerNearTheTop_ThanTheOldArc()
        {
            // time spent above 80% of the apex (the old arc: constant gravity 25)
            Simulate(0.9f, out _, out float air, out float t80, out _);
            float v = System.MathF.Sqrt(2f * 25f * 0.9f);
            float oldT80 = (v - System.MathF.Sqrt(v * v - 2f * 25f * 0.72f)) / 25f;
            float oldAbove = 2f * (v / 25f - oldT80);
            Assert.Greater(air - 2f * t80, oldAbove + 0.05f);
        }

        [TestCase(0.9f)]
        [TestCase(1.6f)]
        [TestCase(0.1f)]   // low enough to stay inside the hang band
        public void HeightAt_MatchesTheSteppedSimulation(float apex)
        {
            float v0 = Arc.LaunchSpeed(apex);
            const float dt = 1f / 5000f;   // a very fine plain Euler walk with GravityAt: independent of Advance
            float y = 0f, v = v0;
            for (int i = 1; i * dt < Arc.AirTime(apex) - 0.02f; i++)
            {
                v -= Arc.GravityAt(v) * dt;
                y += v * dt;
                if (i % 50 == 0) Assert.AreEqual(y, Arc.HeightAt(v0, i * dt), 0.02f, $"apex {apex} step {i}");
            }
            Assert.AreEqual(apex, Arc.ApexHeight(v0), 1e-3f);
            Assert.AreEqual(0f, Arc.HeightAt(v0, 5f), 1e-5f);
        }

        [Test]
        public void TimeToHeight_InvertsHeightAt()
        {
            foreach (float h in new[] { 0.2f, 0.5f, 0.9f })
            {
                float t = Arc.TimeToHeight(0.9f, h);
                Assert.AreEqual(h, Arc.HeightAt(Arc.LaunchSpeed(0.9f), t), 1e-3f);
            }
        }
    }
}
