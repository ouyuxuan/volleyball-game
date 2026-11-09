using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>
    /// 1st person aiming: the crosshair is the centre of the screen, so the aim point is where the
    /// camera's forward ray meets the sand. If that is further than maxRayDistance (or the player looks up
    /// at the sky), we use the point fallbackDistance metres away along the look direction instead.
    /// </summary>
    public class CrosshairAim : MonoBehaviour, IAimSource
    {
        [SerializeField] Transform viewTransform;          // the 1st person camera
        [SerializeField] float maxRayDistance = 25f;
        [SerializeField] float fallbackDistance = 20f;

        public Vector3 AimPoint => Compute(viewTransform != null ? viewTransform : transform);

        public Vector3 Compute(Transform view)
        {
            return Compute(view.position, view.forward, maxRayDistance, fallbackDistance);
        }

        /// <summary>The pure maths, easy to check: origin + forward direction in, ground aim point out.</summary>
        public static Vector3 Compute(Vector3 origin, Vector3 forward, float maxRay, float fallback)
        {
            if (forward.y < -1e-4f)
            {
                float t = -origin.y / forward.y;
                if (t >= 0f && t <= maxRay)
                    return new Vector3(origin.x + forward.x * t, 0f, origin.z + forward.z * t);
            }
            // Too far away or looking up: pick a point 'fallback' metres away horizontally.
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            return new Vector3(origin.x + flat.x * fallback, 0f, origin.z + flat.z * fallback);
        }
    }
}
