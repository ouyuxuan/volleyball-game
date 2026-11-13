using Sandspike.Art;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>
    /// While the human server holds the spike button to charge the toss, shows where the toss will go:
    /// a dotted arc from the ball to the landing point, and a ring on the sand there.
    /// It asks the player for the same prediction the toss will use (same charge), every frame.
    /// </summary>
    public class ServePreview : MonoBehaviour
    {
        [SerializeField] PlayerController player;      // the human player
        [SerializeField] int dotCount = 16;
        [SerializeField] float dotSize = 0.14f;
        [SerializeField] float ringRadius = 0.5f;      // landing ring (outer radius, metres)
        [SerializeField] float ringHeight = 0.045f;    // just above the sand
        [SerializeField, Range(0f, 1f)] float alpha = 0.85f;

        Transform[] dots;
        Transform ring;

        void Awake()
        {
            Material mat = FeetRing.MakeMaterial(Palette.WithAlpha(Palette.Accent, alpha), true);
            dots = new Transform[dotCount];
            for (int i = 0; i < dotCount; i++)
            {
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(d.GetComponent<Collider>());
                d.name = "Dot";
                d.transform.SetParent(transform, false);
                d.transform.localScale = Vector3.one * dotSize;
                SetLook(d.GetComponent<MeshRenderer>(), mat);
                dots[i] = d.transform;
            }

            var r = new GameObject("Landing Ring", typeof(MeshFilter), typeof(MeshRenderer));
            r.transform.SetParent(transform, false);
            r.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(ringRadius * 0.75f, ringRadius);
            SetLook(r.GetComponent<MeshRenderer>(), mat);
            ring = r.transform;
            Show(false);
        }

        static void SetLook(MeshRenderer m, Material mat)
        {
            m.sharedMaterial = mat;
            m.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.receiveShadows = false;
        }

        void Show(bool on)
        {
            foreach (Transform d in dots) d.gameObject.SetActive(on);
            ring.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            BallController ball = player != null ? player.Ball : null;
            bool show = ball != null && !ball.IsInFlight && player.Brain is HumanBrain && player.TossCharging;
            if (dots[0].gameObject.activeSelf != show) Show(show);
            if (!show) return;

            HitResult serve = player.PredictToss(player.TossCharge.Value);
            Vector3 start = ball.Position;
            Vector3 v0 = BallTrajectory.SolveLaunchVelocity(start, new Vector3(serve.target.x, ball.Radius, serve.target.z), serve.flightTime, ball.Gravity);
            for (int i = 0; i < dots.Length; i++)
                dots[i].position = BallTrajectory.PositionAt(start, v0, serve.flightTime * (i + 1) / dots.Length, ball.Gravity);
            ring.position = new Vector3(serve.target.x, ringHeight, serve.target.z);
        }
    }
}
