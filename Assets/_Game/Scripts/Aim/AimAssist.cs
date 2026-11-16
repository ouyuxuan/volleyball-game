using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>The hit a player is about to make, as far as aim assist cares. Spike covers the serve; Bump counts only as a first touch.</summary>
    public enum AssistHit { None, Spike, Set, Bump }

    /// <summary>The bits of court state the assist needs. side = the player's court side (-1 or +1); the others are world positions.</summary>
    public struct AssistCourt
    {
        public float halfWidth, halfLength, side;
        public Vector3 mate, opponentA, opponentB;
    }

    /// <summary>
    /// Gamepad aim assist (pure maths): when the raw aim point is within Radius of a sensible target it is pulled Pull of the way to it.
    /// Spike or serve: the two deep corners and the middle of the two opponents. Set or first-touch bump: the spot near the net at the
    /// teammate's x. Nothing while blocking or with no ball in play (the caller passes AssistHit.None).
    /// </summary>
    public static class AimAssist
    {
        public const float Radius = 1.5f, Pull = 0.6f, CornerInset = 1f, NetDistance = 2f;

        public static Vector3 Apply(Vector3 raw, AssistHit hit, in AssistCourt c, bool gamepadAim, bool assistOn)
        {
            if (!gamepadAim || !assistOn || hit == AssistHit.None) return raw;

            bool found = false;
            Vector3 best = default;
            float bestDist = Radius;
            int count = hit == AssistHit.Spike ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 cand = Candidate(hit, c, i);
                float d = Vector2.Distance(new Vector2(raw.x, raw.z), new Vector2(cand.x, cand.z));
                if (d <= bestDist) { bestDist = d; best = cand; found = true; }   // the nearest candidate in range wins
            }
            if (!found) return raw;

            Vector3 r = Vector3.Lerp(raw, best, Pull);
            r.x = Mathf.Clamp(r.x, -c.halfWidth, c.halfWidth);       // never leaves the court
            r.z = Mathf.Clamp(r.z, -c.halfLength, c.halfLength);
            r.y = 0f;
            return r;
        }

        /// <summary>Candidate i of this hit (a spike has three: the two deep corners, then the opponents' midpoint).</summary>
        public static Vector3 Candidate(AssistHit hit, in AssistCourt c, int i)
        {
            if (hit != AssistHit.Spike) return new Vector3(c.mate.x, 0f, c.side * NetDistance);   // set / bump: near the net at the teammate's x
            float far = -c.side * (c.halfLength - CornerInset);                                   // the opponents' end
            if (i == 0) return new Vector3(-(c.halfWidth - CornerInset), 0f, far);
            if (i == 1) return new Vector3(c.halfWidth - CornerInset, 0f, far);
            return new Vector3((c.opponentA.x + c.opponentB.x) * 0.5f, 0f, (c.opponentA.z + c.opponentB.z) * 0.5f);
        }
    }
}
