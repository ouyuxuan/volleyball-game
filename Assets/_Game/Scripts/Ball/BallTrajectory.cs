using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// Pure maths for the ball's flight (no MonoBehaviour, easy to unit test).
    /// The ball never uses Rigidbody physics: every hit picks a landing target and a flight time,
    /// and the ball follows the parabola  p(t) = start + v0*t + 0.5*g*t^2  exactly.
    /// Gravity is a parameter everywhere (default 9.81) so it can be tuned in the Inspector.
    /// </summary>
    public static class BallTrajectory
    {
        public const float DefaultGravity = 9.81f;
        public const float BallRadius = 0.11f;
        public const float NetTopHeight = 2.43f;

        /// <summary>The launch velocity that makes the ball reach 'target' exactly 'flightTime' seconds after leaving 'start'.</summary>
        public static Vector3 SolveLaunchVelocity(Vector3 start, Vector3 target, float flightTime, float gravity = DefaultGravity)
        {
            var g = new Vector3(0f, -gravity, 0f);
            return (target - start - 0.5f * g * flightTime * flightTime) / flightTime;
        }

        /// <summary>Where the ball is 't' seconds after launch.</summary>
        public static Vector3 PositionAt(Vector3 start, Vector3 v0, float t, float gravity = DefaultGravity)
        {
            var g = new Vector3(0f, -gravity, 0f);
            return start + v0 * t + 0.5f * g * t * t;
        }

        /// <summary>The ball's velocity 't' seconds after launch.</summary>
        public static Vector3 VelocityAt(Vector3 v0, float t, float gravity = DefaultGravity)
        {
            return v0 + new Vector3(0f, -gravity * t, 0f);
        }

        /// <summary>Highest point of the arc (equals the start height if the ball is launched downward).</summary>
        public static float ApexHeight(Vector3 start, Vector3 v0, float gravity = DefaultGravity)
        {
            return v0.y <= 0f ? start.y : start.y + v0.y * v0.y / (2f * gravity);
        }

        /// <summary>Seconds after launch when the ball is at its highest (0 if it is launched downward).</summary>
        public static float ApexTime(Vector3 v0, float gravity = DefaultGravity)
        {
            return v0.y <= 0f ? 0f : v0.y / gravity;
        }

        /// <summary>
        /// Time (seconds after launch) at which the ball is at height 'height'.
        /// descending = true gives the later crossing (the ball on its way down), false the earlier one (on its way up).
        /// Returns NaN if the arc never reaches that height.
        /// Solves  startY + vy*t - 0.5*g*t^2 = height.
        /// </summary>
        public static float TimeToReachHeight(float startY, float vy, float height, bool descending = true, float gravity = DefaultGravity)
        {
            float disc = vy * vy - 2f * gravity * (height - startY);
            if (disc < 0f) return float.NaN;
            float root = Mathf.Sqrt(disc);
            float tUp = (vy - root) / gravity;   // smaller root
            float tDown = (vy + root) / gravity; // larger root
            return descending ? tDown : tUp;
        }

        /// <summary>
        /// Does the arc cross the net plane (z = netZ) at some time during the flight? If so, returns when and how high.
        /// An arc that stays on one side of the net never crosses it.
        /// </summary>
        public static bool CrossesNetAt(Vector3 start, Vector3 v0, float flightTime, out float timeAtNet, out float heightAtNet, float netZ = 0f, float gravity = DefaultGravity)
        {
            timeAtNet = 0f;
            heightAtNet = 0f;
            if (Mathf.Abs(v0.z) < 1e-5f) return false; // moving parallel to the net
            float t = (netZ - start.z) / v0.z;
            if (t < 0f || t > flightTime) return false;
            timeAtNet = t;
            heightAtNet = PositionAt(start, v0, t, gravity).y;
            return true;
        }

        /// <summary>
        /// Net-clearance check: true if the ball passes ABOVE (net top + ball radius) when it reaches z = netZ,
        /// or never reaches the net at all.
        /// </summary>
        public static bool ClearsNet(Vector3 start, Vector3 v0, float flightTime, float netTop = NetTopHeight, float ballRadius = BallRadius, float netZ = 0f, float gravity = DefaultGravity)
        {
            if (!CrossesNetAt(start, v0, flightTime, out _, out float h, netZ, gravity)) return true;
            return h > netTop + ballRadius;
        }

        /// <summary>
        /// Finds when (within [tMin, tMax] seconds after launch) the ball is closest to 'point'.
        /// Used for hit timing: the "ideal contact moment" is the closest approach to the player's hit point.
        /// Samples the arc, then refines around the best sample.
        /// </summary>
        public static float ClosestApproach(Vector3 start, Vector3 v0, Vector3 point, float tMin, float tMax, out float distance, float gravity = DefaultGravity)
        {
            if (tMax < tMin) tMax = tMin;
            const int samples = 60;
            float best = tMin;
            float bestSqr = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(tMin, tMax, i / (float)samples);
                float d = (PositionAt(start, v0, t, gravity) - point).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            // Refine: ternary search in the neighbourhood of the best sample.
            float step = (tMax - tMin) / samples;
            float lo = Mathf.Max(tMin, best - step);
            float hi = Mathf.Min(tMax, best + step);
            for (int i = 0; i < 20; i++)
            {
                float m1 = lo + (hi - lo) / 3f;
                float m2 = hi - (hi - lo) / 3f;
                float d1 = (PositionAt(start, v0, m1, gravity) - point).sqrMagnitude;
                float d2 = (PositionAt(start, v0, m2, gravity) - point).sqrMagnitude;
                if (d1 < d2) hi = m2; else lo = m1;
            }
            best = (lo + hi) * 0.5f;
            distance = (PositionAt(start, v0, best, gravity) - point).magnitude;
            return best;
        }

        /// <summary>Reach check: is the ball centre inside the player's reach sphere?</summary>
        public static bool WithinReach(Vector3 ballCentre, Vector3 hitPoint, float reachRadius)
        {
            return (ballCentre - hitPoint).sqrMagnitude <= reachRadius * reachRadius;
        }
    }
}
