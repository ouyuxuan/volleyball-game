using Sandspike.Aim;
using Sandspike.Art;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using TMPro;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// The human's spike wind-up, drawn at the ball so the eyes never leave it: while Spike is held in the air a ring
    /// closes around the ball (1.6 times its size down to 1.0 as the charge fills), a soft aura glows when letting go would
    /// connect (the HitCue), and a small "x2" / "x3" badge shows beside the ball when the team's touch count raises the combo.
    /// "The human" is player 0, so an auto-play bot standing in for it shows the same thing.
    /// </summary>
    public class SpikeChargeFx : MonoBehaviour
    {
        [SerializeField] float ringStartScale = 1.6f;
        [SerializeField] float auraScale = 4f;           // aura diameter, in ball diameters

        MatchManager match;
        BallController ball;
        Renderer ballRenderer;
        PlayerController human;
        HitCue cue;
        Transform ring, aura;
        TextMeshPro badge;

        /// <summary>The badge text for a team that has already used 'teamTouches' touches: nothing on the 1st, "x2", "x3" after. Matches the combo multiplier steps in HitResolver.</summary>
        public static string ComboBadge(int teamTouches) => teamTouches <= 0 ? "" : "×" + (Mathf.Min(teamTouches, 2) + 1);

        void Start()
        {
            match = FindFirstObjectByType<MatchManager>();
            ball = FindFirstObjectByType<BallController>();
            if (match == null || ball == null) { enabled = false; return; }
            ballRenderer = ball.GetComponentInChildren<MeshRenderer>();

            ring = Make("ChargeRing", RingMesh.Create(0.86f, 1f), FeetRing.MakeMaterial(Palette.TeamA));

            // aura: a soft round glow quad that always faces the camera
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "HitAura";
            q.transform.SetParent(transform, false);
            var mat = FeelFx.ParticleMaterial();
            mat.SetColor("_BaseColor", Palette.WithAlpha(Palette.HitCue, 0.7f));
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            aura = q.transform;
            aura.gameObject.SetActive(false);

            var b = new GameObject("ComboBadge");
            b.transform.SetParent(transform, false);
            badge = b.AddComponent<TextMeshPro>();
            badge.alignment = TextAlignmentOptions.Center;
            badge.fontSize = 4.5f;
            badge.fontStyle = FontStyles.Bold;
            badge.color = Palette.Accent;
            badge.outlineWidth = 0.3f;
            badge.outlineColor = (Color32)Palette.Shadow;
            badge.rectTransform.sizeDelta = new Vector2(2f, 1f);
            b.SetActive(false);
        }

        Transform Make(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        void LateUpdate()
        {
            if (match == null) return;
            if (human == null)
            {
                if (match.Players == null || match.Players.Length == 0 || match.Players[0] == null) return;
                human = match.Players[0];
                cue = human.GetComponent<HitCue>();
            }
            Camera cam = Camera.main;
            if (cam == null) return;

            bool air = ball.IsInFlight && !human.IsGrounded;
            bool charging = air && human.SpikeCharge.IsCharging && human.WindUpType == HitType.Spike;
            bool auraOn = air && cue != null && cue.IsOn;
            string text = charging && match.Rules != null ? ComboBadge(match.Rules.TouchesFor(human.Team)) : "";

            Vector3 p = ball.Position;
            float radius = ballRenderer != null ? ballRenderer.bounds.extents.x : ball.Radius;   // follows the ball's own swelling

            ring.gameObject.SetActive(charging);
            if (charging)
            {
                ring.position = p;
                ring.up = (cam.transform.position - p).normalized;    // lies flat to the camera
                float s = Mathf.Lerp(ringStartScale, 1f, human.SpikeCharge.Value);
                ring.localScale = Vector3.one * radius * s;
            }

            aura.gameObject.SetActive(auraOn);
            if (auraOn)
            {
                aura.position = p;
                aura.rotation = cam.transform.rotation;
                aura.localScale = Vector3.one * radius * 2f * auraScale;
            }

            badge.gameObject.SetActive(text != "");
            if (text != "")
            {
                if (badge.text != text) badge.text = text;
                badge.transform.rotation = cam.transform.rotation;
                badge.transform.position = p + cam.transform.right * (radius * 2.2f) + cam.transform.up * (radius * 1.2f);
            }
        }
    }
}
