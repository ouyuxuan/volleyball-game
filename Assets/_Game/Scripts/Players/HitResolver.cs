using System;
using Sandspike.Ball;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>All the numbers that shape a hit. Shown in the Inspector on the PlayerController.</summary>
    [Serializable]
    public class HitTuning
    {
        [Header("Timing windows (seconds, absolute error)")]
        public float perfectWindow = 0.08f;
        public float goodWindow = 0.20f;
        public float okWindow = 0.35f;        // beyond this is still a hit (graded Ok); timing never causes a Miss

        [Header("Timing multipliers (power)")]
        public float perfectMultiplier = 1.0f;
        public float goodMultiplier = 0.93f;
        public float okMultiplier = 0.85f;

        [Header("Scatter radius (metres around the aim point)")]
        public float perfectScatter = 0f;
        public float goodScatter = 0.25f;
        public float okScatter = 0.5f;


        [Header("Spike / serve power")]
        public float minChargeFactor = 0.65f;   // quality factor at zero charge (1.0 at full charge)

        [Header("Spike contact height (a low hand = flatter, weaker spike)")]
        public float lowHandHeight = 2.3f;     // hand height when standing
        public float bestHandHeight = 3.6f;    // hand height near the top of a good jump
        public float lowHandQualityFactor = 0.85f;

        [Header("Flight times (seconds)")]
        public float bumpDistanceScaleSoft = 0.85f;  // charge 0: lands at 60% of the distance to the aim point
        public float bumpDistanceScaleHard = 1.2f;   // charge 1: lands 120% of the way (past the aim)
        public float bumpTimeSoft = 1.6f;
        public float bumpTimeHard = 1.2f;
        public float setApexMin = 4.0f;       // a set's peak above the sand at charge 0 (quick, low set, about 1.5 s)
        public float setApexMax = 7.2f;       // ...and at full charge (about 2.25 s; a set for a human takes 1.9 s+ to fall to spike height): charge picks the height, not the distance
        public float spikeSlowTime = 0.9f;     // quality 0
        public float spikeFastTime = 0.35f;    // quality 1
        public float serveSlowTime = 1.6f;
        public float serveFastTime = 0.8f;

        [Header("Set clamp (never at or over the net)")]
        public float setMinDistanceFromNet = 1.5f;   // an aim closer to the net than this (or across it) moves back to this distance, on the setter's side

        [Header("Spike reach zone (a tall cylinder around the hand, not a sphere)")]
        public float spikeReachBelow = 0.6f;   // the ball may be this far below the hand
        public float spikeReachAbove = 0.8f;   // ...or this far above it
    }

    /// <summary>
    /// Pure C# (no Unity components): turns "what kind of hit, how charged, how well timed, aimed where"
    /// into "land the ball here, in this many seconds". Give it a seeded System.Random in tests.
    /// Slightly-off timing still connects (weaker or less accurate); a swing far off the ideal moment, or out of reach, is a Miss.
    /// </summary>
    public class HitResolver
    {
        readonly HitTuning t;
        readonly System.Random rng;

        public HitResolver(HitTuning tuning, System.Random random = null)
        {
            t = tuning ?? new HitTuning();
            rng = random ?? new System.Random();
        }

        /// <summary>Grade, power multiplier and scatter radius for an absolute timing error.</summary>
        public void GradeTiming(float absError, out HitGrade grade, out float multiplier, out float scatter)
        {
            if (absError <= t.perfectWindow) { grade = HitGrade.Perfect; multiplier = t.perfectMultiplier; scatter = t.perfectScatter; }
            else if (absError <= t.goodWindow) { grade = HitGrade.Good; multiplier = t.goodMultiplier; scatter = t.goodScatter; }
            else if (absError <= t.okWindow) { grade = HitGrade.Ok; multiplier = t.okMultiplier; scatter = t.okScatter; }
            else { grade = HitGrade.Ok; multiplier = t.okMultiplier; scatter = t.okScatter; }   // timing never misses
        }

        public HitResult Resolve(HitInput input)
        {
            var result = new HitResult { contact = true };
            GradeTiming(Mathf.Abs(input.timingError), out result.grade, out result.timingMultiplier, out float scatter);

            // Out of reach is the only Miss. The ball is not touched.
            if (input.closestDistance > input.reachRadius)
            {
                result.grade = HitGrade.Miss;
                result.contact = false;
                result.timingMultiplier = 0f;
                return result;
            }

            bool powered = input.type == HitType.Spike || input.type == HitType.Serve;
            float charge = Mathf.Clamp01(input.charge);
            result.quality = result.timingMultiplier * (powered ? Mathf.Lerp(t.minChargeFactor, 1f, charge) : 1f);

            // A low hand (badly timed jump) makes the spike a little flatter and weaker, but it still connects.
            if (input.type == HitType.Spike)
            {
                float h = Mathf.InverseLerp(t.lowHandHeight, t.bestHandHeight, input.contactHeight);
                result.quality *= Mathf.Lerp(t.lowHandQualityFactor, 1f, Mathf.Clamp01(h));
            }

            // Scatter: a random point inside a disc around the aim point.
            // Bump power: charge scales how far the ball travels toward the aim point (soft = lands short).
            // A set ignores this: it lands at the aim point, and charge only picks its height.
            Vector3 aimPoint = input.aimPoint;
            if (input.type == HitType.Bump)
            {
                float scale = Mathf.Lerp(t.bumpDistanceScaleSoft, t.bumpDistanceScaleHard, charge);
                Vector3 from = input.hitterPosition;
                aimPoint = new Vector3(from.x + (aimPoint.x - from.x) * scale, 0f, from.z + (aimPoint.z - from.z) * scale);
            }
            // Scatter: a random point inside a disc around the aim point.
            Vector3 target = aimPoint + RandomInDisc(scatter);
            target.y = 0f;

            switch (input.type)
            {
                case HitType.Bump:
                    result.flightTime = Mathf.Lerp(t.bumpTimeSoft, t.bumpTimeHard, charge);
                    break;
                case HitType.Set:
                    target = ClampSetTarget(target, input.hitterPosition);
                    // Apex-driven: charge picks the peak (setApexMin..Max); the flight time follows from the apex.
                    float g = input.ballGravity > 0f ? input.ballGravity : BallTrajectory.DefaultGravity;
                    float startY = input.ballPosition.y;
                    float apex = Mathf.Lerp(t.setApexMin, t.setApexMax, charge);
                    if (startY > apex - 1f) apex = startY + 1f;
                    result.flightTime = BallTrajectory.FlightTimeForApex(startY, apex, BallTrajectory.BallRadius, g);
                    break;
                case HitType.Spike:
                    result.flightTime = Mathf.Lerp(t.spikeSlowTime, t.spikeFastTime, result.quality);
                    break;
                default: // Serve
                    result.flightTime = Mathf.Lerp(t.serveSlowTime, t.serveFastTime, result.quality);
                    break;
            }
            result.target = target;
            return result;
        }

        /// <summary>
        /// The aim point that makes a hit LAND on 'target': undoes the charge distance scale of a bump
        /// (the bots use it). Sets, spikes and serves already aim straight at the target.
        /// </summary>
        public Vector3 AimFor(HitType type, float charge, Vector3 from, Vector3 target)
        {
            charge = Mathf.Clamp01(charge);
            float scale = type == HitType.Bump ? Mathf.Lerp(t.bumpDistanceScaleSoft, t.bumpDistanceScaleHard, charge) : 1f;
            return new Vector3(from.x + (target.x - from.x) / scale, 0f, from.z + (target.z - from.z) / scale);
        }

        /// <summary>The only set clamp: a target nearer the net than 1.5 m, or across it, moves back to 1.5 m on the setter's side (same x).</summary>
        Vector3 ClampSetTarget(Vector3 target, Vector3 hitterPosition)
        {
            float side = hitterPosition.z >= 0f ? 1f : -1f;
            if (target.z * side < t.setMinDistanceFromNet) target.z = side * t.setMinDistanceFromNet;
            return target;
        }

        Vector3 RandomInDisc(float radius)
        {
            if (radius <= 0f) return Vector3.zero;
            float r = radius * Mathf.Sqrt((float)rng.NextDouble());   // sqrt = evenly spread over the disc
            float a = (float)(rng.NextDouble() * Math.PI * 2.0);
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    }
}
