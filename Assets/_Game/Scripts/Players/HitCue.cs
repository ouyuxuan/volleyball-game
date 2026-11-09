using Sandspike.Aim;
using Sandspike.Art;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// "Hit now" cue for the human player: while a hit button is held and the ball is inside the reach of that hit's
    /// contact point, the ball glows and a ring shows at the contact point. Releasing then hits. Off otherwise.
    /// </summary>
    public class HitCue : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float glowIntensity = 3f;     // emission strength on the ball (HDR)
        [SerializeField] float ballScaleWhenOn = 1.4f;  // the ball swells a little so the cue reads from far away
        [SerializeField] float ringThickness = 0.2f;   // fraction of the ring's radius

        public bool IsOn { get; private set; }

        Renderer ballRenderer;
        Material ballMaterial;
        Color baseEmission;
        Vector3 baseScale;
        GameObject ring;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
        }

        void Start()
        {
            if (player == null || player.Ball == null) return;
            ballRenderer = player.Ball.GetComponentInChildren<Renderer>();
            if (ballRenderer != null)
            {
                ballMaterial = ballRenderer.material;   // this ball's own copy, so only it glows
                ballMaterial.EnableKeyword("_EMISSION");
                baseEmission = ballMaterial.HasProperty("_EmissionColor") ? ballMaterial.GetColor("_EmissionColor") : Color.black;
                baseScale = ballRenderer.transform.localScale;
            }

            // A flat ring the size of the reach radius, built once and turned to face the camera.
            ring = new GameObject("HitCueRing", typeof(MeshFilter), typeof(MeshRenderer));
            ring.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(1f - ringThickness, 1f);
            var r = ring.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Palette.HitRing };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ring.transform.localScale = Vector3.one * player.ReachRadius;
            ring.SetActive(false);
        }

        void LateUpdate()
        {
            if (ring == null) return;
            bool on = player.HitReady(out HitType type);
            if (on)
            {
                Vector3 p = player.ContactPointAt(type);
                ring.transform.position = p;
                Camera cam = Camera.main;
                if (cam != null) ring.transform.up = (cam.transform.position - p).normalized;   // ring faces the viewer
            }
            if (on != IsOn)
            {
                IsOn = on;
                ring.SetActive(on);
                if (ballMaterial != null) ballMaterial.SetColor("_EmissionColor", on ? Palette.HitCue * glowIntensity : baseEmission);
                if (ballRenderer != null) ballRenderer.transform.localScale = baseScale * (on ? ballScaleWhenOn : 1f);
            }
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring);
            if (ballMaterial != null) Destroy(ballMaterial);
        }
    }
}
