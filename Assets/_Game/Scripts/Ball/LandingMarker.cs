using Sandspike.Aim;
using Sandspike.Art;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// A ring on the sand where the ball will land. It shrinks as the ball gets closer, and its colour shifts
    /// white, yellow, orange, red over the last second before landing, so the ring alone says how long you have.
    /// A thin rim around it is in the colour of the team that touched the ball last.
    /// The meshes are built in code; the materials are made here (palette colours only).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LandingMarker : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] float startRadius = 1.4f;    // size when the ball has just been hit
        [SerializeField] float endRadius = 0.45f;     // size at touchdown
        [SerializeField] float height = 0.04f;        // just above the sand so it doesn't flicker
        [SerializeField] float ringThickness = 0.18f; // as a fraction of the radius
        [SerializeField] float urgencySeconds = 1f;   // the colour shift covers this last stretch of the flight

        MeshRenderer meshRenderer;
        MeshRenderer rimRenderer;
        Material ringMat, rimMat;
        MatchManager match;

        /// <summary>
        /// The ring colour with 'timeToLand' seconds left: white until the last 'seconds', then yellow, orange, red as it reaches 0.
        /// </summary>
        public static Color UrgencyColour(float timeToLand, float seconds = 1f)
        {
            float k = seconds > 0f ? 1f - Mathf.Clamp01(timeToLand / seconds) : 1f;   // 0 = not urgent, 1 = touching down
            Color[] steps = { Palette.CourtLines, Palette.Accent, Palette.Orange, Palette.TeamA };
            float x = k * (steps.Length - 1);
            int i = Mathf.Min((int)x, steps.Length - 2);
            return Color.Lerp(steps[i], steps[i + 1], x - i);
        }

        void Awake()
        {
            // Unit ring (outer radius 1): we scale the transform to get the size we want.
            GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(1f - ringThickness, 1f);
            meshRenderer = GetComponent<MeshRenderer>();
            ringMat = FeetRing.MakeMaterial(Palette.CourtLines);
            meshRenderer.sharedMaterial = ringMat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // the team-colour rim, just outside the ring (a child, so it scales with it)
            var rim = new GameObject("Team Rim", typeof(MeshFilter), typeof(MeshRenderer));
            rim.transform.SetParent(transform, false);
            rim.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(1.03f, 1.15f);
            rimRenderer = rim.GetComponent<MeshRenderer>();
            rimMat = FeetRing.MakeMaterial(Palette.TeamA);
            rimRenderer.sharedMaterial = rimMat;
            rimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rimRenderer.receiveShadows = false;
        }

        void LateUpdate()
        {
            bool show = ball != null && ball.IsInFlight;
            meshRenderer.enabled = show;
            rimRenderer.enabled = show;
            if (!show) return;

            Vector3 p = ball.LandingPoint;
            transform.position = new Vector3(p.x, height, p.z);
            float progress = ball.TotalFlightTime > 0f ? Mathf.Clamp01(ball.TimeToLanding / ball.TotalFlightTime) : 0f;
            float r = Mathf.Lerp(endRadius, startRadius, progress);
            transform.localScale = new Vector3(r, 1f, r);

            ringMat.color = UrgencyColour(ball.TimeToLanding, urgencySeconds);
            if (match == null) match = FindFirstObjectByType<MatchManager>();
            int team = match != null && match.Rules != null ? match.Rules.LastTouchTeam : -1;
            rimMat.color = team == 0 ? Palette.TeamA : team == 1 ? Palette.TeamB : Palette.CourtLines;
        }

        void OnDestroy()
        {
            if (ringMat != null) Destroy(ringMat);
            if (rimMat != null) Destroy(rimMat);
        }
    }
}
