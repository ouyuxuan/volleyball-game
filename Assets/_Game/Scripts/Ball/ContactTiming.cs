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

        /// <summary>True when the ball is within 'reach' metres of the contact point (drives the "hit now" cue).</summary>
        public static bool InReach(Vector3 ball, Vector3 contact, float reach) => (ball - contact).sqrMagnitude <= reach * reach;

        /// <summary>
        /// The spike's reach zone: a tall cylinder. The ball counts when it is within 'reach' horizontally of the hand
        /// AND between (hand - below) and (hand + above) in height. One function, used wherever a spike's reach is checked.
        /// </summary>
        public static bool SpikeInReach(Vector3 ball, Vector3 hand, float reach, float below, float above)
        {
            float dy = ball.y - hand.y;
            if (dy < -below || dy > above) return false;
            float dx = ball.x - hand.x, dz = ball.z - hand.z;
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>The dive's dig: the low forward contact point reaches a little farther than a normal hit (reach + bonus).</summary>
        public static bool DigInReach(Vector3 ball, Vector3 digPoint, float reach, float bonus) => InReach(ball, digPoint, reach + bonus);

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

    /// <summary>
    /// The player's jump arc (pure maths). Gravity is 'gravity' while the vertical speed is at least 'hangSpeed' (either way),
    /// and only 'gravity * hangScale' below it, so the player launches fast and hangs near the top, with the same apex heights.
    /// </summary>
    public readonly struct JumpArc
    {
        public readonly float gravity, hangSpeed, hangScale;

        public JumpArc(float gravity, float hangSpeed, float hangScale)
        {
            this.gravity = gravity; this.hangSpeed = hangSpeed; this.hangScale = hangScale;
        }

        float HangGravity => gravity * hangScale;
        float HangHeight => hangSpeed * hangSpeed / (2f * HangGravity);   // height gained while slowing from hangSpeed to 0

        /// <summary>The gravity to apply at this vertical speed.</summary>
        public float GravityAt(float verticalSpeed) => Mathf.Abs(verticalSpeed) < hangSpeed ? HangGravity : gravity;

        /// <summary>Takeoff speed for a jump whose feet peak 'apex' metres above the start.</summary>
        public float LaunchSpeed(float apex)
        {
            if (apex <= HangHeight) return Mathf.Sqrt(2f * HangGravity * apex);   // the whole jump is inside the hang band
            return Mathf.Sqrt(2f * gravity * (apex - HangHeight) + hangSpeed * hangSpeed);
        }

        /// <summary>The peak height of a jump that takes off at 'launchSpeed' (the inverse of LaunchSpeed).</summary>
        public float ApexHeight(float launchSpeed)
        {
            if (launchSpeed <= hangSpeed) return launchSpeed * launchSpeed / (2f * HangGravity);
            return (launchSpeed * launchSpeed - hangSpeed * hangSpeed) / (2f * gravity) + HangHeight;
        }

        /// <summary>Seconds from takeoff to the peak.</summary>
        public float TimeToApex(float apex)
        {
            float v = LaunchSpeed(apex);
            return v <= hangSpeed ? v / HangGravity : (v - hangSpeed) / gravity + hangSpeed / HangGravity;
        }

        /// <summary>Seconds after takeoff (on the way up) when the feet of a jump peaking at 'apex' first reach height 'h' (clamped to the apex).</summary>
        public float TimeToHeight(float apex, float h)
        {
            h = Mathf.Clamp(h, 0f, apex);
            float v = LaunchSpeed(apex);
            float fastHeight = v <= hangSpeed ? 0f : apex - HangHeight;   // height gained before the speed drops to hangSpeed
            if (v > hangSpeed && h <= fastHeight) return (v - Mathf.Sqrt(Mathf.Max(0f, v * v - 2f * gravity * h))) / gravity;
            float t0 = v > hangSpeed ? (v - hangSpeed) / gravity : 0f;
            float vh = Mathf.Min(v, hangSpeed), rest = h - fastHeight;
            return t0 + (vh - Mathf.Sqrt(Mathf.Max(0f, vh * vh - 2f * HangGravity * rest))) / HangGravity;
        }

        /// <summary>Seconds from takeoff to landing (the arc is symmetric).</summary>
        public float AirTime(float apex) => 2f * TimeToApex(apex);

        /// <summary>Feet height 't' seconds after a takeoff at 'launchSpeed' (never below the sand).</summary>
        public float HeightAt(float launchSpeed, float t) => HeightFrom(0f, launchSpeed, t);

        /// <summary>Height 't' seconds from now for a body at height y0 moving vertically at v0 (never below the sand).</summary>
        public float HeightFrom(float y0, float v0, float t)
        {
            float v = v0;
            return Mathf.Max(0f, y0 + Advance(ref v, t));
        }

        /// <summary>
        /// Moves the vertical speed 'v' forward by 't' seconds and returns the height gained. It steps exactly through the three
        /// gravity bands (fast up, hang, fast down), so any frame length gives the same arc. PlayerController.Move uses it.
        /// </summary>
        public float Advance(ref float v, float t)
        {
            float dy = 0f;
            for (int i = 0; i < 8 && t > 1e-6f; i++)   // at most 3 bands; the cap guards against float edge cases
            {
                float g, span;   // this band's gravity, and how long until the speed leaves it
                if (v > hangSpeed) { g = gravity; span = (v - hangSpeed) / g; }
                else if (v > -hangSpeed) { g = HangGravity; span = (v + hangSpeed) / g; }
                else { g = gravity; span = float.PositiveInfinity; }
                float step = Mathf.Min(t, span);
                dy += v * step - 0.5f * g * step * step;
                v -= g * step;
                t -= step;
            }
            return dy;
        }
    }
}
