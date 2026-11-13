using Sandspike.Art;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using TMPro;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// What follows the ball: a trail in the colour of the team that touched it last (longer the faster the ball flies),
    /// and a small "IN" / "OUT" label on the sand where a rally ball lands, for one second.
    /// </summary>
    public class BallFx : MonoBehaviour
    {
        [SerializeField] float trailMinSeconds = 0.05f;   // trail length (seconds of travel) at rest ...
        [SerializeField] float trailMaxSeconds = 0.4f;    // ... and at fullSpeed
        [SerializeField] float fullSpeed = 25f;           // m/s
        [SerializeField] float trailWidth = 0.16f;
        [SerializeField] float labelSeconds = 1f;

        BallController ball;
        MatchManager match;
        TrailRenderer trail;
        GameObject trailObject;
        TextMeshPro label;
        float labelLeft;
        int lastLaunch = -1;

        void Start()
        {
            ball = FindFirstObjectByType<BallController>();
            match = FindFirstObjectByType<MatchManager>();
            if (ball == null) { enabled = false; return; }

            trailObject = new GameObject("BallTrail");   // its own object, not on the ball: HitCue looks for the ball's own renderer
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FeelFx.ParticleMaterial();
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidth), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            var go = new GameObject("InOutLabel");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);    // lies on the sand, reads from the near baseline
            label = go.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 10f;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.25f;
            label.outlineColor = (Color32)Palette.CourtLines;
            label.rectTransform.sizeDelta = new Vector2(4f, 2f);
            go.SetActive(false);

            ball.OnGroundContact += OnLanded;
        }

        void OnDestroy() { if (ball != null) ball.OnGroundContact -= OnLanded; if (trailObject != null) Destroy(trailObject); }

        void Update()
        {
            if (ball == null) return;

            // trail: only while flying; cleared at each new flight so it never draws a line across the court
            if (ball.LaunchCount != lastLaunch) { lastLaunch = ball.LaunchCount; trail.Clear(); }
            trailObject.transform.position = ball.Position;
            trail.emitting = ball.IsInFlight;
            trail.time = Mathf.Lerp(trailMinSeconds, trailMaxSeconds, Mathf.Clamp01(ball.Velocity.magnitude / fullSpeed));
            int team = match != null && match.Rules != null ? match.Rules.LastTouchTeam : -1;
            Color c = team == 0 ? Palette.TeamA : team == 1 ? Palette.TeamB : Palette.CourtLines;
            trail.startColor = Palette.WithAlpha(c, 0.9f);
            trail.endColor = Palette.WithAlpha(c, 0f);

            if (labelLeft > 0f)
            {
                labelLeft -= Time.deltaTime;
                label.color = Palette.WithAlpha(label.color, Mathf.Clamp01(labelLeft / 0.3f));    // fades over the last 0.3 s
                if (labelLeft <= 0f) label.gameObject.SetActive(false);
            }
        }

        // Rally balls only: a dropped toss is a serve fault, not a landing in play.
        void OnLanded(Vector3 spot)
        {
            if (match == null || match.Rules == null || ball.LastHitType == HitType.Toss) return;
            bool inside = match.Rules.IsInside(spot.x, spot.z);
            label.text = inside ? "IN" : "OUT";
            label.color = inside ? Palette.Sea : Palette.TeamA;
            label.transform.position = new Vector3(spot.x, 0.06f, spot.z);
            label.gameObject.SetActive(true);
            labelLeft = labelSeconds;
        }
    }
}
