using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>Anything that can say "the player is aiming at this spot on the sand".</summary>
    public interface IAimSource
    {
        /// <summary>A point on the ground plane (y = 0).</summary>
        Vector3 AimPoint { get; }
    }
}
