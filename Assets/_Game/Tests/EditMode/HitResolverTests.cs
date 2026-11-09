using NUnit.Framework;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    public class HitResolverTests
    {
        static HitResolver Make(int seed = 1) => new HitResolver(new HitTuning(), new System.Random(seed));

        static HitInput Input(HitType type, float timingError, float charge = 1f, Vector3? aim = null, float contactHeight = 3.6f, float closest = 0f)
        {
            return new HitInput
            {
                type = type,
                charge = charge,
                timingError = timingError,
                closestDistance = closest,
                reachRadius = 0.6f,
                contactHeight = contactHeight,
                aimPoint = aim ?? new Vector3(0f, 0f, 4f),
                hitterPosition = new Vector3(0f, 0f, -4f),
                ballPosition = new Vector3(0f, 2f, -4f),
            };
        }

        // ---- timing grades: boundaries at 0.08 / 0.20 / 0.35 s ----
        [TestCase(0.0f, HitGrade.Perfect, 1.0f)]
        [TestCase(0.08f, HitGrade.Perfect, 1.0f)]
        [TestCase(-0.08f, HitGrade.Perfect, 1.0f)]
        [TestCase(0.081f, HitGrade.Good, 0.9f)]
        [TestCase(0.20f, HitGrade.Good, 0.9f)]
        [TestCase(-0.20f, HitGrade.Good, 0.9f)]
        [TestCase(0.201f, HitGrade.Ok, 0.8f)]
        [TestCase(0.35f, HitGrade.Ok, 0.8f)]
        [TestCase(-0.35f, HitGrade.Ok, 0.8f)]
        public void GradeBoundaries(float error, HitGrade grade, float multiplier)
        {
            var r = Make().Resolve(Input(HitType.Bump, error));
            Assert.AreEqual(grade, r.grade);
            Assert.AreEqual(multiplier, r.timingMultiplier, 1e-4f);
            Assert.IsTrue(r.contact);
        }

        [TestCase(HitType.Bump)]
        [TestCase(HitType.Set)]
        [TestCase(HitType.Spike)]
        [TestCase(HitType.Serve)]
        public void FarOffRelease_IsAMiss_NoContact(HitType type)
        {
            var r = Make().Resolve(Input(type, 0.36f));
            Assert.AreEqual(HitGrade.Miss, r.grade);
            Assert.IsFalse(r.contact);
            Assert.AreEqual(0f, r.flightTime);   // nothing is launched: the ball keeps its own arc
        }

        [Test]
        public void SlightlyOff_StillConnects_EvenWhenEarly()
        {
            var early = Make().Resolve(Input(HitType.Bump, -0.3f));   // early release inside the window
            Assert.IsTrue(early.contact);
            Assert.AreEqual(HitGrade.Ok, early.grade);
        }

        [Test]
        public void OutOfReach_IsAMiss_EvenWithPerfectTiming()
        {
            var r = Make().Resolve(Input(HitType.Bump, 0f, 1f, null, 3.6f, 0.61f));
            Assert.AreEqual(HitGrade.Miss, r.grade);
            Assert.IsFalse(r.contact);
            Assert.IsTrue(Make().Resolve(Input(HitType.Bump, 0f, 1f, null, 3.6f, 0.59f)).contact);
        }

        // ---- charge scaling ----
        [Test]
        public void Spike_QualityScalesWithCharge()
        {
            var none = Make().Resolve(Input(HitType.Spike, 0f, 0f));
            var full = Make().Resolve(Input(HitType.Spike, 0f, 1f));
            Assert.AreEqual(0.4f, none.quality, 1e-4f);
            Assert.AreEqual(1.0f, full.quality, 1e-4f);
            Assert.AreEqual(Mathf.Lerp(0.9f, 0.35f, 0.4f), none.flightTime, 1e-3f);
            Assert.AreEqual(0.35f, full.flightTime, 1e-3f);
        }

        [Test]
        public void Spike_QualityIsTimingTimesChargeFactor()
        {
            var r = Make().Resolve(Input(HitType.Spike, 0.1f, 0.5f)); // Good (0.9) x lerp(0.4,1,0.5)=0.7
            Assert.AreEqual(0.9f * 0.7f, r.quality, 1e-4f);
        }

        [Test]
        public void Charge_DominatesTiming_ForSpikePower()
        {
            var badTimingFullCharge = Make().Resolve(Input(HitType.Spike, 0.3f, 1f));   // Ok x 1.0
            var perfectNoCharge = Make().Resolve(Input(HitType.Spike, 0f, 0f));       // Perfect x 0.4
            Assert.Greater(badTimingFullCharge.quality, perfectNoCharge.quality);
        }

        [Test]
        public void Spike_LowHand_IsWeakerButStillConnects()
        {
            var high = Make().Resolve(Input(HitType.Spike, 0f, 1f, null, 3.6f));
            var low = Make().Resolve(Input(HitType.Spike, 0f, 1f, null, 2.3f));
            Assert.IsTrue(low.contact);
            Assert.Less(low.quality, high.quality);
            Assert.Greater(low.flightTime, high.flightTime);   // slower
            Assert.AreEqual(0.85f, low.quality, 1e-4f);
        }

        [Test]
        public void Serve_FlightTimeScalesWithQuality()
        {
            Assert.AreEqual(0.8f, Make().Resolve(Input(HitType.Serve, 0f, 1f)).flightTime, 1e-3f);
            Assert.AreEqual(1.6f - 0.4f * 0.8f, Make().Resolve(Input(HitType.Serve, 0f, 0f)).flightTime, 1e-3f);
        }

        [Test]
        public void Bump_LowCharge_LandsShorterAndSlowerThanFullCharge()
        {
            var aim = new Vector3(0f, 0f, 4f);   // hitter at z = -4, so 8 m away
            var soft = Make().Resolve(Input(HitType.Bump, 0f, 0f, aim));
            var hard = Make().Resolve(Input(HitType.Bump, 0f, 1f, aim));
            float softDist = Vector3.Distance(soft.target, new Vector3(0f, 0f, -4f));
            float hardDist = Vector3.Distance(hard.target, new Vector3(0f, 0f, -4f));
            Assert.AreEqual(8f * 0.6f, softDist, 1e-3f);
            Assert.AreEqual(8f * 1.2f, hardDist, 1e-3f);
            Assert.Less(softDist, hardDist);
            Assert.AreEqual(1.8f, soft.flightTime, 1e-4f);
            Assert.AreEqual(1.2f, hard.flightTime, 1e-4f);
        }

        [Test]
        public void Set_LowCharge_IsSofterThanFullCharge()
        {
            var aim = new Vector3(0f, 0f, -0.2f);
            var soft = Make().Resolve(Input(HitType.Set, 0f, 0f, aim));
            var hard = Make().Resolve(Input(HitType.Set, 0f, 1f, aim));
            Assert.AreEqual(1.5f, soft.flightTime, 1e-4f);
            Assert.AreEqual(1.1f, hard.flightTime, 1e-4f);
        }


        // ---- scatter: none for Perfect, up to 0.5 m for Good, up to 1.0 m for Ok ----
        [Test]
        public void PerfectHit_LandsExactlyOnAim()
        {
            var aim = new Vector3(1.5f, 0f, 5f);
            var r = Make().Resolve(Input(HitType.Spike, 0f, 1f, aim));
            Assert.AreEqual(0f, Vector3.Distance(r.target, aim), 1e-4f);
        }

        [Test]
        public void Scatter_StaysWithinRadius_ForGoodAndOk()
        {
            var aim = new Vector3(0f, 0f, 4f);
            var resolver = Make(1234);
            float goodFar = 0f, okFar = 0f;
            for (int i = 0; i < 500; i++)
            {
                float g = Vector3.Distance(resolver.Resolve(Input(HitType.Bump, 0.15f, 2f / 3f, aim)).target, aim);
                float o = Vector3.Distance(resolver.Resolve(Input(HitType.Bump, 0.3f, 2f / 3f, aim)).target, aim);
                Assert.LessOrEqual(g, 0.5f + 1e-3f);
                Assert.LessOrEqual(o, 1.0f + 1e-3f);
                goodFar = Mathf.Max(goodFar, g);
                okFar = Mathf.Max(okFar, o);
            }
            Assert.Greater(goodFar, 0.25f);   // it really scatters
            Assert.Greater(okFar, 0.5f);
        }

        [Test]
        public void Scatter_IsRepeatableWithSameSeed()
        {
            var a = Make(99).Resolve(Input(HitType.Bump, 0.3f));
            var b = Make(99).Resolve(Input(HitType.Bump, 0.3f));
            Assert.AreEqual(a.target, b.target);
        }

        // ---- set clamping ----
        [Test]
        public void Set_IsClampedToOwnHalf_WhenAimedAtOpponentSide()
        {
            var r = Make().Resolve(Input(HitType.Set, 0f, 1f, new Vector3(0f, 0f, 6f))); // hitter at z = -4
            Assert.Less(r.target.z, 0f);
            Assert.AreEqual(-3.5f, r.target.z, 1e-4f);
        }

        [Test]
        public void Set_IsKeptOffTheNet_AndInsideCourtWidth()
        {
            var r = Make().Resolve(Input(HitType.Set, 0f, 1f, new Vector3(9f, 0f, -0.2f)));
            Assert.AreEqual(-1.0f, r.target.z, 1e-4f);
            Assert.AreEqual(4.0f, r.target.x, 1e-4f);
            Assert.AreEqual(1.1f, r.flightTime, 1e-4f);
        }

        [Test]
        public void Set_ForHitterOnFarSide_StaysOnFarSide()
        {
            var input = Input(HitType.Set, 0f, 1f, new Vector3(0f, 0f, -6f));
            input.hitterPosition = new Vector3(0f, 0f, 5f);
            Assert.Greater(Make().Resolve(input).target.z, 0f);
        }
    }
}
