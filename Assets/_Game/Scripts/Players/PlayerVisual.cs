using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// Procedural motion for the primitive-built player (no animation clips): idle breathing,
    /// a walking/running swing of legs and arms, a lean into movement, stretch on jumps, squash on landing,
    /// and arms that wind back while charging a hit and swing through the contact point. Everything is a tunable number.
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
        [SerializeField] float crouchAtFullCharge = 0.8f;   // body height factor while a jump is fully charged
        [SerializeField] float armsUpDegrees = 160f;
        [SerializeField] float swingSeconds = 0.12f;   // length of the hit swing
        [SerializeField] float diveLeanDegrees = 78f;      // the whole body tips forward this far in a dive

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

            // Arms: a hit wind-up pulls them BACK by the charge (never above the contact point); the hit plays a quick swing
            // through the contact pose; otherwise they hang / swing with the gait, or sit mid-raised in the air.
            bool swinging = Time.time - player.LastHitTime < swingSeconds;
            if (swinging || player.WindingUp)
            {
                HitType type = swinging ? player.LastReport.type : player.WindUpType;
                float charge = swinging ? 0f : player.SpikeCharge.Value;
                ArmPose(type, charge, swinging, out float left, out float right);
                float rate = swinging ? 40f : 14f;
                SwingSmooth(armLeft, left, dt, rate);
                SwingSmooth(armRight, right, dt, rate);
            }
            else
            {
                bool armsUp = !player.IsGrounded || player.DiveAmount > 0.1f;     // -96 degrees: well below the spike contact point (arms reach out in a dive)
                float armTarget = -armsUpDegrees * 0.6f;
                SwingSmooth(armLeft, armsUp ? armTarget : -swing * armSwingDegrees, dt);
                SwingSmooth(armRight, armsUp ? armTarget : swing * armSwingDegrees, dt);
            }

            // Stretch while rising, squash briefly on landing.
            bool grounded = player.IsGrounded;
            if (grounded && !wasGrounded) squashTimer = squashSeconds;
            wasGrounded = grounded;
            squashTimer = Mathf.Max(0f, squashTimer - dt);

            float sy = 1f;
            if (!grounded && player.VerticalVelocity > 0.5f) sy = stretchOnRise;
            if (squashTimer > 0f) sy = Mathf.Lerp(1f, squashOnLand, squashTimer / squashSeconds);
            if (grounded && player.JumpCharge.IsCharging) sy = Mathf.Lerp(1f, crouchAtFullCharge, player.JumpCharge.Value);   // crouch while charging a jump
            float dive = player.DiveAmount;                      // lunging: tip the whole body forward, a little flatter
            sy = Mathf.Lerp(sy, 0.92f, dive);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(diveLeanDegrees * dive, 0f, 0f), dt * 22f);
            float sxz = 1f / Mathf.Sqrt(sy);                   // keep the volume roughly constant
            Vector3 target = new Vector3(rootScaleBase.x * sxz, rootScaleBase.y * sy, rootScaleBase.z * sxz);
            transform.localScale = Vector3.Lerp(transform.localScale, target, dt * 18f);
        }

        // Arm angles in degrees about the shoulder (0 = hanging down, negative = raised forward / up, -180 = straight up).
        // The hitter's arm cocks back as the charge grows; the swing pose is the contact pose (the highest the hand goes).
        static void ArmPose(HitType type, float charge, bool swing, out float left, out float right)
        {
            switch (type)
            {
                case HitType.Bump:   // arms low and together; pulled back as charge builds, swung up to the waist-high contact
                    left = right = swing ? -60f : Mathf.Lerp(-25f, 15f, charge);
                    break;
                case HitType.Set:    // hands at the forehead; elbows draw in a little with charge, swing to the overhead contact
                    left = right = swing ? -150f : Mathf.Lerp(-125f, -100f, charge);
                    break;
                default:             // spike / serve: off arm up for balance, hitting arm cocked back behind the head
                    left = -100f;
                    right = swing ? -175f : Mathf.Lerp(-140f, -225f, charge);
                    break;
            }
        }

        static void Swing(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        static void SwingSmooth(Transform t, float degrees, float dt, float rate = 14f)
        {
            if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(degrees, 0f, 0f), dt * rate);
        }
    }
}
