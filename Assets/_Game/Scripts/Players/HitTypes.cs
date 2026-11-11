using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>The four kinds of hit.</summary>
    public enum HitType { Bump, Set, Spike, Serve }

    /// <summary>How good a hit was (shown in the HUD pop).</summary>
    public enum HitGrade { None, Perfect, Good, Ok, Miss }

    /// <summary>Everything the resolver needs to know about one hit attempt.</summary>
    public struct HitInput
    {
        public HitType type;
        public float charge;        // 0..1 (only spike/serve use it)
        public float timingError;   // seconds: release time minus ideal contact time (negative = early)
        public float closestDistance; // how close the ball gets to the contact point (m)
        public float reachRadius;     // beyond this the hit is a Miss
        public float contactHeight; // height (m above the ground) of the contact point at the ideal moment; a low hand weakens spikes
        public Vector3 aimPoint;    // where the player aimed on the sand
        public Vector3 hitterPosition;
        public Vector3 ballPosition;
        public float ballGravity;     // the ball's gravity (0 = the default 9.81); a set's flight time is derived from it
    }

    /// <summary>What the resolver decided.</summary>
    public struct HitResult
    {
        public bool contact;            // false = Miss: the ball is not touched and keeps its arc
        public HitGrade grade;
        public float timingMultiplier;  // 1.0 / 0.9 / 0.8 (0 on a Miss)
        public float quality;           // timing multiplier x charge factor
        public Vector3 target;          // where the ball should land (on the sand, y = 0)
        public float flightTime;        // seconds in the air
    }
}
