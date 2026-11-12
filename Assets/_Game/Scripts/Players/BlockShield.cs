using System;
using Sandspike.Ball;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>All the block numbers in one place (shown on the PlayerController in the Inspector).</summary>
    [Serializable]
    public class BlockTuning
    {
        public float netDistance = 1.2f;     // a jump this close to the net (|z|) is a block
        public float width = 1.2f;           // the shield: a flat rectangle in front of the raised hands (the arms open in a V)
        public float height = 0.6f;
        public float handHeight = 2.45f;     // shield centre above the feet
        public float forward = 0.25f;        // shield centre this far toward the net
        public float strafe = 0.35f;         // in the air, left/right shifts the shield up to this far
        public float strafeSpeed = 3f;       // m/s the shield slides
        public float deflect = 3f;           // m/s of sideways kick for a hit at the very edge of the shield
        [Range(0f, 1f)] public float rebound = 0.55f;   // the ball keeps this much of its incoming (horizontal) speed
        public float downSpeed = 4f;         // the rebound is launched downward at this speed (less if the net would be hit)
        public float minPastNet = 1f;        // the rebound lands this far to 4 m past the net on the attacker's side
        public float maxPastNet = 4f;
        public float landHalfWidth = 3.8f;   // and inside the side lines
    }

    /// <summary>The block maths as plain functions (no scene objects), so they are easy to unit test.</summary>
    public static class BlockShield
    {
        /// <summary>
        /// Did the ball, moving from 'prev' to 'cur' this frame, cross the shield plane (front to back) inside the
        /// rectangle (made bigger by the ball radius)? 'normal' is flat and points at the opponents.
        /// hit = the crossing point, u = how far right of the shield centre it struck (m).
        /// </summary>
        public static bool TryCross(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 normal, float width, float height,
            float radius, out Vector3 hit, out float u)
        {
            hit = cur; u = 0f;
            float dPrev = Vector3.Dot(prev - centre, normal);
            float dCur = Vector3.Dot(cur - centre, normal);
            if (!(dPrev >= 0f && dCur < 0f)) return false;
            hit = Vector3.Lerp(prev, cur, dPrev / (dPrev - dCur));
            Vector3 right = Vector3.Cross(Vector3.up, normal);
            u = Vector3.Dot(hit - centre, right);
            return Mathf.Abs(u) <= width * 0.5f + radius && Mathf.Abs(hit.y - centre.y) <= height * 0.5f + radius;
        }

        /// <summary>
        /// The rebound. 'blockerSide' is the blocker's court side (-1 or +1). The horizontal velocity is reflected about the
        /// shield normal, kicked sideways by where it struck, and slowed to 'rebound' of the incoming speed; then it is launched
        /// downward so it lands minPastNet..maxPastNet beyond the net (inside the court sides). If the reflection still heads for
        /// the blocker's side (angled hands, a soft block) it is left alone. Output: where it lands and the flight time, ready
        /// for BallController.Launch. If 'aim' is given and lies on the attacker's side, the blocker steers the ball: it lands at
        /// the aim point (kept inside the same landing zone) instead of where the mirror would send it.
        /// </summary>
        public static void Rebound(Vector3 hit, Vector3 velocity, Vector3 normal, float u, float blockerSide, BlockTuning t,
            float gravity, float ballRadius, out Vector3 landing, out float flightTime, Vector3? aim = null)
        {
            Vector3 vh = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 right = Vector3.Cross(Vector3.up, normal);
            Vector3 v = vh - 2f * Vector3.Dot(vh, normal) * normal;                       // mirror about the shield
            v += right * (u / (t.width * 0.5f) * t.deflect);                              // outward kick: edge hits go wide
            v = v.sqrMagnitude > 1e-6f ? v.normalized * (vh.magnitude * t.rebound) : Vector3.zero;
            bool aimed = aim.HasValue && aim.Value.z * blockerSide < 0f;
            bool towardAttacker = aimed || v.z * blockerSide < 0f;

            landing = hit; flightTime = 0.1f;
            for (float down = t.downSpeed; down >= -4f; down -= 0.5f)   // if it cannot clear the net going down, launch it flatter, then slightly up
            {
                float vy = -down;
                float drop = Mathf.Max(hit.y - ballRadius, 0f);
                flightTime = Mathf.Max(0.05f, (vy + Mathf.Sqrt(vy * vy + 2f * gravity * drop)) / gravity);   // time to fall to the sand
                landing = aimed ? new Vector3(aim.Value.x, 0f, aim.Value.z) : new Vector3(hit.x + v.x * flightTime, 0f, hit.z + v.z * flightTime);
                if (!towardAttacker) return;
                float past = Mathf.Clamp(-blockerSide * landing.z, t.minPastNet, t.maxPastNet);   // keep it 1..4 m past the net
                landing = new Vector3(Mathf.Clamp(landing.x, -t.landHalfWidth, t.landHalfWidth), 0f, -blockerSide * past);
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(hit, new Vector3(landing.x, ballRadius, landing.z), flightTime, gravity);
                if (BallTrajectory.ClearsNet(hit, v0, flightTime, BallTrajectory.NetTopHeight, ballRadius, 0f, gravity)) return;
            }
        }
    }
}
