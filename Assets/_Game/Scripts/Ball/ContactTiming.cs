using System;
using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// Pure maths for hit timing. The ideal contact moment is the time when the ball's predicted arc passes
    /// closest to the player's CONTACT POINT (forearms, hands...). The contact point can move with time
    /// (a jumping player's hand rises and falls). Landing time is never used for timing.
    /// All times here are seconds relative to "now" (negative = in the past).
    /// </summary>
    public static class ContactTiming
    {
        /// <summary>The player's height 'dt' seconds from now, following the jump arc (never below the ground at 0).</summary>
        public static float PlayerHeightAt(float y0, float verticalVelocity, float gravity, float dt)
        {
            if (dt <= 0f) return y0;
            float y = y0 + verticalVelocity * dt - 0.5f * gravity * dt * dt;
            return Mathf.Max(0f, y);
        }


        /// <summary>Takeoff speed for a jump that peaks at 'apex' metres: v = sqrt(2*g*h).</summary>
        public static float JumpSpeed(float gravity, float apex) => Mathf.Sqrt(2f * gravity * apex);

        /// <summary>
        /// Extra upward acceleration (m/s^2) that, applied for 'boostSeconds' while rising, lifts a tap jump
        /// (apex 'tapApex') to 'fullApex'. Solves apex(a) = fullApex for the physical (smaller net-gravity) root.
        /// </summary>
        public static float JumpBoostAccel(float gravity, float tapApex, float fullApex, float boostSeconds)
        {
            float v0 = JumpSpeed(gravity, tapApex), t = boostSeconds;
            // apex = v0*t - n*t^2/2 + (v0 - n*t)^2/(2g), with n = g - a the net gravity during the boost.
            float qa = t * t / (2f * gravity);
            float qb = -(t * t / 2f + v0 * t / gravity);
            float qc = v0 * t - (fullApex - tapApex);
            float disc = Mathf.Max(0f, qb * qb - 4f * qa * qc);
            float n = (-qb - Mathf.Sqrt(disc)) / (2f * qa);
            return gravity - n;
        }

        /// <summary>
        /// Finds the time in [tMin, tMax] when ballAt(t) is closest to contactAt(t).
        /// Returns that time and the distance (metres) at that moment.
        /// </summary>
        public static float FindIdealTime(Func<float, Vector3> ballAt, Func<float, Vector3> contactAt, float tMin, float tMax, out float distance)
        {
            if (tMax < tMin) tMax = tMin;
            const int samples = 120;
            float best = tMin, bestSqr = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(tMin, tMax, i / (float)samples);
                float d = (ballAt(t) - contactAt(t)).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            // Refine around the best sample.
            float step = (tMax - tMin) / samples;
            float lo = Mathf.Max(tMin, best - step), hi = Mathf.Min(tMax, best + step);
            for (int i = 0; i < 24; i++)
            {
                float m1 = lo + (hi - lo) / 3f, m2 = hi - (hi - lo) / 3f;
                float d1 = (ballAt(m1) - contactAt(m1)).sqrMagnitude;
                float d2 = (ballAt(m2) - contactAt(m2)).sqrMagnitude;
                if (d1 < d2) hi = m2; else lo = m1;
            }
            best = (lo + hi) * 0.5f;
            distance = (ballAt(best) - contactAt(best)).magnitude;
            return best;
        }
    }
}
