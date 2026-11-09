using System;
using Sandspike.Ball;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>What happened on a hit attempt (the HUD shows it, tests log it).</summary>
    public struct HitReport
    {
        public HitType type;
        public HitGrade grade;
        public bool contact;
        public float timingError;
        public float charge;
        public float quality;
        public Vector3 aimPoint;
        public Vector3 target;
        public float flightTime;
        public Vector3 contactPoint;   // where the hands/forearms were at the ideal moment
        public float idealTime;        // Time.time of the ideal contact moment
        public float releaseTime;      // Time.time when the button was released (or pressed, for bump/set)
    }

    /// <summary>
    /// Moves a player with a CharacterController and turns the brain's button presses into
    /// charged jumps and bump / set / spike hits. It only talks to IPlayerBrain, so an AI can drive it too.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] BallController ball;
        [SerializeField] Transform headTarget;       // where the 1st person camera sits

        [Header("Movement")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float acceleration = 45f;   // m/s^2: reaches full speed in about 0.13 s
        [SerializeField] float deceleration = 55f;
        [SerializeField] float turnSpeed = 720f;     // degrees/second the body turns toward the aim

        [Header("Jump")]
        [SerializeField] float tapJumpHeight = 0.9f;
        [SerializeField] float fullJumpHeight = 1.6f;
        [SerializeField] float gravity = 25f;        // the player's gravity (snappier than the ball's)

        [Header("Court limits (this player's half)")]
        [SerializeField] float courtSide = -1f;      // -1: z below the net, +1: z above
        [SerializeField] float netMargin = 0.55f;    // how close to the net the body may get
        [SerializeField] float sideLimit = 9f;
        [SerializeField] float backLimit = 11f;

        [Header("Hitting")]
        [SerializeField] float reachRadius = 0.6f;          // ball must pass this close to the contact point
        [Tooltip("Contact points relative to the player (x right, y up from the feet, z forward). They move with the player and the jump.")]
        [SerializeField] Vector3 bumpContactOffset = new Vector3(0f, 0.9f, 0.35f);    // forearms at waist height
        [SerializeField] Vector3 setContactOffset = new Vector3(0f, 2.0f, 0.15f);     // hands just above the head
        [SerializeField] Vector3 spikeContactOffset = new Vector3(0f, 2.3f, 0.3f);    // hitting hand at full overhead reach (spike and serve)
        [SerializeField] float hitCooldown = 0.25f;
        [SerializeField] float timingLookBack = 0.4f;      // how far into the past / future we search for the ideal moment
        [SerializeField] float timingLookAhead = 1.0f;

        [Header("Charge, stamina, hit numbers")]
        [SerializeField] Stamina stamina = new Stamina();
        [SerializeField] ChargeMeter jumpCharge = new ChargeMeter();
        [SerializeField] ChargeMeter spikeCharge = new ChargeMeter();
        [SerializeField] HitTuning hitTuning = new HitTuning();

        /// <summary>Raised after every hit attempt that was inside reach (including whiffs).</summary>
        public event Action<HitReport> OnHitAttempt;

        CharacterController controller;
        IPlayerBrain brain;
        HitResolver resolver;
        Vector3 planarVelocity;
        float verticalVelocity;
        bool grounded;
        float cooldownLeft;

        public Stamina Stamina => stamina;
        public ChargeMeter JumpCharge => jumpCharge;
        public ChargeMeter SpikeCharge => spikeCharge;
        public bool IsGrounded => grounded;
        public float VerticalVelocity => verticalVelocity;
        public float Speed01 => Mathf.Clamp01(planarVelocity.magnitude / moveSpeed);
        public Vector3 PlanarVelocity => planarVelocity;
        public float ReachRadius => reachRadius;
        public Transform HeadTarget => headTarget;
        public HitReport LastReport { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            brain = GetComponent<IPlayerBrain>();
            resolver = new HitResolver(hitTuning);
        }

        void Update()
        {
            if (brain == null) return;
            float dt = Time.deltaTime;
            cooldownLeft -= dt;

            Vector2 move = brain.Move;
            stamina.Tick(dt, move.sqrMagnitude > 0.01f);

            Move(move, dt);
            UpdateJump(dt);
            UpdateHits(dt);
            UpdatePending();
            FaceAim(dt);
        }

        // ---------------- movement ----------------

        void Move(Vector2 input, float dt)
        {
            Vector3 wish = new Vector3(input.x, 0f, input.y) * moveSpeed;
            float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, rate * dt);

            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;   // stay pressed onto the sand
            else verticalVelocity -= gravity * dt;

            Vector3 pos = transform.position;
            Vector3 next = pos + (planarVelocity + Vector3.up * verticalVelocity) * dt;
            next = ClampToCourt(next);
            controller.Move(next - pos);
            grounded = controller.isGrounded || transform.position.y <= 0.001f;
        }

        // Keeps the player on their own half (can't cross the net) and inside the beach area.
        Vector3 ClampToCourt(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -sideLimit, sideLimit);
            float near = courtSide * netMargin;                     // closest z to the net
            float far = courtSide * backLimit;
            p.z = Mathf.Clamp(p.z, Mathf.Min(near, far), Mathf.Max(near, far));
            return p;
        }

        /// <summary>Moves the player straight to a spot (used by checks and the future serve positions).</summary>
        public void TeleportTo(Vector3 position)
        {
            controller.enabled = false;
            transform.position = ClampToCourt(position);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        // ---------------- jump ----------------

        void UpdateJump(float dt)
        {
            ButtonState jump = brain.Jump;
            if (jump.Down && grounded) jumpCharge.Begin();
            if (jumpCharge.IsCharging)
            {
                jumpCharge.Tick(dt, stamina.MaxChargeAllowed);
                if (jump.Up || !jump.Held)
                {
                    float charge = jumpCharge.Release();
                    if (grounded) Jump(charge);
                }
            }
        }

        /// <summary>Jumps with the given charge (0 = tap hop, 1 = full jump) and spends stamina.</summary>
        public void Jump(float charge)
        {
            float apex = Mathf.Lerp(tapJumpHeight, fullJumpHeight, Mathf.Clamp01(charge));
            verticalVelocity = Mathf.Sqrt(2f * gravity * apex);      // v = sqrt(2*g*h)
            grounded = false;
            stamina.SpendJump(charge);
        }

        // ---------------- hitting ----------------

        HitType chargingType;

        // Spike, bump and set are all hold-to-charge: one shared charge meter for whichever button is held.
        // Release (the "swing") resolves the hit against the contact-point timing windows.
        void UpdateHits(float dt)
        {
            ButtonState spike = brain.Spike, bump = brain.Bump, set = brain.Set;
            if (!spikeCharge.IsCharging)
            {
                if (spike.Down) { spikeCharge.Begin(); chargingType = HitType.Spike; }
                else if (bump.Down) { spikeCharge.Begin(); chargingType = HitType.Bump; }
                else if (set.Down) { spikeCharge.Begin(); chargingType = HitType.Set; }
            }
            if (spikeCharge.IsCharging)
            {
                spikeCharge.Tick(dt, stamina.MaxChargeAllowed);
                ButtonState b = chargingType == HitType.Spike ? spike : chargingType == HitType.Bump ? bump : set;
                if (b.Up || !b.Held)
                    AttemptHit(chargingType, spikeCharge.Release());
            }
        }

        /// <summary>The contact offset (player-relative) for this kind of hit.</summary>
        Vector3 ContactOffset(HitType type)
        {
            return type == HitType.Bump ? bumpContactOffset : type == HitType.Set ? setContactOffset : spikeContactOffset;
        }

        /// <summary>
        /// Where this hit's contact point (forearms / hands) is now, or 'dt' seconds from now.
        /// It rides on the player: it turns with the body and follows the predicted jump arc in height.
        /// </summary>
        public Vector3 ContactPointAt(HitType type, float dt = 0f)
        {
            Vector3 o = ContactOffset(type);
            Vector3 p = transform.position;
            float y = ContactTiming.PlayerHeightAt(p.y, verticalVelocity, gravity, dt);
            Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatFwd);
            return new Vector3(p.x, y, p.z) + flatRight * o.x + Vector3.up * o.y + flatFwd * o.z;
        }

        public Vector3 HitPoint(HitType type) => ContactPointAt(type, 0f);

        // A swing that is inside the timing window but early: held until the moment of contact.
        struct PendingHit { public bool active; public float fireAt; public HitType type; public HitResult result; public float timingError; public float charge; public Vector3 contactPoint; public float idealTime; public float releaseTime; public Vector3 aim; }
        PendingHit pending;

        /// <summary>
        /// A hit attempt (the button release for spike/serve, the press for bump/set).
        /// - ball out of reach of the contact point, or |release - ideal| beyond the Ok window: MISS, nothing is touched;
        /// - otherwise the hit connects, graded by the timing error. If the release was early, the hit is held and
        ///   executes at the moment of contact (closest approach).
        /// timingOverride (seconds, release minus ideal) replaces the real timing; for checks.
        /// Returns true if an attempt was registered (hit, held hit or miss).
        /// </summary>
        public bool AttemptHit(HitType type, float charge, float? timingOverride = null)
        {
            if (ball == null || !ball.IsInFlight || cooldownLeft > 0f || pending.active) return false;

            // Ideal contact = when the ball's arc passes closest to the (moving) contact point.
            if (!ball.TryFindContact(d => ContactPointAt(type, d), timingLookBack, timingLookAhead, out float offset, out float distance)) return false;

            Vector3 contactPoint = ContactPointAt(type, offset);
            float timingError = timingOverride ?? -offset;   // negative = early, positive = late
            Vector3 aim = brain != null ? brain.AimPoint : new Vector3(0f, 0f, -courtSide * 4f);

            HitResult result = resolver.Resolve(new HitInput
            {
                type = type,
                charge = charge,
                timingError = timingError,
                closestDistance = distance,
                reachRadius = reachRadius,
                contactHeight = contactPoint.y,
                aimPoint = aim,
                hitterPosition = transform.position,
                ballPosition = ball.Position,
            });

            if (type == HitType.Spike || type == HitType.Serve) stamina.SpendSpike(charge); else stamina.SpendHit(charge);
            cooldownLeft = hitCooldown;

            // Early release inside the window: hold it until the ball reaches the contact point.
            if (result.contact && offset > 0f && !timingOverride.HasValue)
            {
                pending = new PendingHit { active = true, fireAt = Time.time + offset, type = type, result = result, timingError = timingError, charge = charge, contactPoint = contactPoint, idealTime = Time.time + offset, releaseTime = Time.time, aim = aim };
                return true;
            }

            Finish(type, charge, result, timingError, contactPoint, Time.time + offset, Time.time, aim);
            return true;
        }

        // Applies the result: launches the ball on a connect, leaves it alone on a Miss, and reports it.
        void Finish(HitType type, float charge, HitResult result, float timingError, Vector3 contactPoint, float idealTime, float releaseTime, Vector3 aim)
        {
            if (result.contact)
                ball.Launch(ball.Position, result.target, result.flightTime, true);

            var report = new HitReport
            {
                type = type, grade = result.grade, contact = result.contact, timingError = timingError,
                charge = charge, quality = result.quality, aimPoint = aim, target = result.target, flightTime = result.flightTime,
                contactPoint = contactPoint, idealTime = idealTime, releaseTime = releaseTime,
            };
            LastReport = report;
            OnHitAttempt?.Invoke(report);
        }

        void UpdatePending()
        {
            if (!pending.active) return;
            if (ball == null || !ball.IsInFlight) { pending.active = false; return; }
            if (Time.time < pending.fireAt) return;
            pending.active = false;
            Finish(pending.type, pending.charge, pending.result, pending.timingError, pending.contactPoint, pending.idealTime, pending.releaseTime, pending.aim);
        }

        // ---------------- facing ----------------

        void FaceAim(float dt)
        {
            Vector3 to = brain.AimPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }
    }
}
