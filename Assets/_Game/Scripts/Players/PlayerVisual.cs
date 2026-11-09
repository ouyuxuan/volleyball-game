using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// Procedural motion for the primitive-built player (no animation clips): idle breathing,
    /// a walking/running swing of legs and arms, a lean into movement, stretch on jumps, squash on landing,
    /// and arms thrown up while charging a spike. Everything is a tunable number.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Parts (set by the prefab builder)")]
        [SerializeField] PlayerController player;
        [SerializeField] Transform body;       // everything above the hips
        [SerializeField] Transform legLeft;    // pivots at the hip
        [SerializeField] Transform legRight;
        [SerializeField] Transform armLeft;    // pivots at the shoulder
        [SerializeField] Transform armRight;

        [Header("Idle")]
        [SerializeField] float breatheAmount = 0.02f;   // metres
        [SerializeField] float breatheHz = 0.6f;

        [Header("Gait")]
        [SerializeField] float stepHz = 2.5f;
        [SerializeField] float bobAmount = 0.05f;
        [SerializeField] float legSwingDegrees = 38f;
        [SerializeField] float armSwingDegrees = 30f;
        [SerializeField] float leanDegrees = 10f;

        [Header("Jump")]
        [SerializeField] float stretchOnRise = 1.08f;
        [SerializeField] float squashOnLand = 0.86f;
        [SerializeField] float squashSeconds = 0.12f;
        [SerializeField] float armsUpDegrees = 160f;

        Vector3 bodyBase;
        Vector3 rootScaleBase;
        float gaitPhase;
        float gaitAmplitude;
        float squashTimer;
        bool wasGrounded = true;
        Renderer[] renderers;

        void Awake()
        {
            if (body != null) bodyBase = body.localPosition;
            rootScaleBase = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>In 1st person the camera is inside the head, so we only keep the shadow of the body.</summary>
        public void SetFirstPerson(bool firstPerson)
        {
            if (renderers == null) return;
            var mode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in renderers) r.shadowCastingMode = mode;
        }

        void Update()
        {
            if (player == null || body == null) return;
            float dt = Time.deltaTime;
            float speed = player.Speed01;

            // Gait amplitude eases toward the current speed, so stopping settles instead of freezing.
            gaitAmplitude = Mathf.MoveTowards(gaitAmplitude, speed, dt * 3f);
            gaitPhase += dt * stepHz * Mathf.Max(0.3f, speed) * Mathf.PI * 2f;
            float swing = Mathf.Sin(gaitPhase) * gaitAmplitude;

            // Body: bob on each step plus a slow breathing sway that never stops.
            float breathe = Mathf.Sin(Time.time * breatheHz * Mathf.PI * 2f) * breatheAmount;
            float bob = Mathf.Abs(Mathf.Sin(gaitPhase)) * bobAmount * gaitAmplitude;
            body.localPosition = bodyBase + Vector3.up * (breathe + bob);

            // Lean forward into movement (relative to where the body faces).
            Vector3 localVel = transform.parent != null ? transform.parent.InverseTransformDirection(player.PlanarVelocity) : player.PlanarVelocity;
            float lean = Mathf.Clamp(localVel.z / 6f, -1f, 1f) * leanDegrees;
            float roll = Mathf.Sin(gaitPhase * 0.5f) * 2f * gaitAmplitude;
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(lean, 0f, roll), dt * 10f);

            // Legs and arms swing in opposite phase.
            Swing(legLeft, swing * legSwingDegrees);
            Swing(legRight, -swing * legSwingDegrees);

            bool armsUp = player.SpikeCharge.IsCharging || !player.IsGrounded;
            float armTarget = armsUp ? -armsUpDegrees * Mathf.Max(0.4f, player.SpikeCharge.IsCharging ? 0.5f + player.SpikeCharge.Value * 0.5f : 0.6f) : 0f;
            SwingSmooth(armLeft, armsUp ? armTarget : -swing * armSwingDegrees, dt);
            SwingSmooth(armRight, armsUp ? armTarget : swing * armSwingDegrees, dt);

            // Stretch while rising, squash briefly on landing.
            bool grounded = player.IsGrounded;
            if (grounded && !wasGrounded) squashTimer = squashSeconds;
            wasGrounded = grounded;
            squashTimer = Mathf.Max(0f, squashTimer - dt);

            float sy = 1f;
            if (!grounded && player.VerticalVelocity > 0.5f) sy = stretchOnRise;
            if (squashTimer > 0f) sy = Mathf.Lerp(1f, squashOnLand, squashTimer / squashSeconds);
            float sxz = 1f / Mathf.Sqrt(sy);                   // keep the volume roughly constant
            Vector3 target = new Vector3(rootScaleBase.x * sxz, rootScaleBase.y * sy, rootScaleBase.z * sxz);
            transform.localScale = Vector3.Lerp(transform.localScale, target, dt * 18f);
        }

        static void Swing(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        static void SwingSmooth(Transform t, float degrees, float dt)
        {
            if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(degrees, 0f, 0f), dt * 14f);
        }
    }
}
