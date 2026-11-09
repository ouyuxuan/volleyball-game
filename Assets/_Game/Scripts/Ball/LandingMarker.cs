using Sandspike.Aim;
using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// A ring on the sand where the ball will land. It shrinks as the ball gets closer.
    /// The mesh is built in code; the material (palette Accent) is set on the MeshRenderer in the scene.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LandingMarker : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] float startRadius = 1.4f;    // size when the ball has just been hit
        [SerializeField] float endRadius = 0.45f;     // size at touchdown
        [SerializeField] float height = 0.04f;        // just above the sand so it doesn't flicker
        [SerializeField] float ringThickness = 0.18f; // as a fraction of the radius

        MeshRenderer meshRenderer;

        void Awake()
        {
            // Unit ring (outer radius 1): we scale the transform to get the size we want.
            GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(1f - ringThickness, 1f);
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        void LateUpdate()
        {
            bool show = ball != null && ball.IsInFlight;
            meshRenderer.enabled = show;
            if (!show) return;

            Vector3 p = ball.LandingPoint;
            transform.position = new Vector3(p.x, height, p.z);
            float progress = ball.TotalFlightTime > 0f ? Mathf.Clamp01(ball.TimeToLanding / ball.TotalFlightTime) : 0f;
            float r = Mathf.Lerp(endRadius, startRadius, progress);
            transform.localScale = new Vector3(r, 1f, r);
        }
    }
}
