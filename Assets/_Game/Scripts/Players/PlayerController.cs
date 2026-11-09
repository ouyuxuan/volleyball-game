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
        public float pressTime;        // Time.time when the button was pressed
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
        [SerializeField] float boostSeconds = 0.35f; // how long after takeoff holding the button adds height
        [SerializeField] float tapGrace = 0.08f;     // a press shorter than this is a plain tap (no boost); hold past it to go higher

        [Header("Court limits (this player's half)")]
        [SerializeField] float courtSide = -1f;      // -1: z below the net, +1: z above
        [SerializeField] float netMargin = 0.55f;    // how close to the net the body may get
        [SerializeField] float sideLimit = 9f;
        [SerializeField] float backLimit = 11f;

        [Header("Hitting")]
        [SerializeField] float reachRadius = 1.0f;          // ball must pass this close to the contact point
        [Tooltip("Contact points relative to the player (x right, y up from the feet, z forward). They move with the player and the jump.")]
        [SerializeField] Vector3 bumpContactOffset = new Vector3(0f, 0.9f, 0.35f);    // forearms at waist height
        [SerializeField] Vector3 setContactOffset = new Vector3(0f, 2.0f, 0.15f);     // hands just above the head
        [SerializeField] Vector3 spikeContactOffset = new Vector3(0f, 2.3f, 0.3f);    // hitting hand at full overhead reach (spike and serve)
        [SerializeField] float hitCooldown = 0.25f;
        [SerializeField] float timingLookBack = 0f;      // a ball that has already passed the player is a miss
        [SerializeField] float timingLookAhead = 6f;      // look all the way to the landing: a press queues the hit

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
        public float LastHitTime { get; private set; } = -99f;   // Time.time of the last connected hit (the visual swings on it)
        public bool WindingUp => spikeCharge.IsCharging || spikeCharge.Value > 0f;
        public HitType WindUpType => chargingType;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            brain = GetComponent<IPlayerBrain>();
            resolver = new HitResolver(hitTuning);
            jumpCharge.secondsToFull = boostSeconds;   // the jump meter measures the boost window
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

        // Jump fires the instant you press. Holding while rising adds a boost (stamina-limited) up to fullJumpHeight.
        // The jump meter shows the boost used (0..1 over boostSeconds).
        void UpdateJump(float dt)
        {
            ButtonState jump = brain.Jump;
            if (grounded) jumpCharge.Cancel();                       // landed: the meter resets
            if (jump.Down && grounded) { Jump(); jumpCharge.Begin(); }
            if (!jumpCharge.IsCharging) return;
            if (!jump.Held || verticalVelocity <= 0f) { jumpCharge.Lock(); return; }   // released early / past the apex: boost over

            float used = jumpCharge.TickDelayed(dt, stamina.MaxChargeAllowed, tapGrace);   // stamina caps it; none during the tap grace
            if (used <= 0f) return;
            verticalVelocity += ContactTiming.JumpBoostAccel(gravity, tapJumpHeight, fullJumpHeight, boostSeconds) * used * boostSeconds;
            stamina.SpendJump(used);                                 // cost is proportional to the boost used
        }

        /// <summary>Jumps straight away to the tap height (the hold boost is added later, in UpdateJump).</summary>
        public void Jump()
        {
            verticalVelocity = ContactTiming.JumpSpeed(gravity, tapJumpHeight);
            grounded = false;
        }

        // ---------------- hitting ----------------

        HitType chargingType;
        bool queued;     // this press has a hit waiting for the contact moment

        // Spike, bump and set are wind-ups. PRESS queues the hit at once (it fires when the ball reaches the fixed
        // contact point) and starts the charge. Holding builds charge until then; RELEASE only locks it.
        void UpdateHits(float dt)
        {
            ButtonState spike = brain.Spike, bump = brain.Bump, set = brain.Set;
            if (!spikeCharge.IsCharging)
            {
                if (spike.Down) { spikeCharge.Begin(); chargingType = HitType.Spike; queued = false; }
                else if (bump.Down) { spikeCharge.Begin(); chargingType = HitType.Bump; queued = false; }
                else if (set.Down) { spikeCharge.Begin(); chargingType = HitType.Set; queued = false; }
            }
            if (spikeCharge.IsCharging)
            {
                spikeCharge.Tick(dt, stamina.MaxChargeAllowed);
                if (!queued) queued = AttemptHit(chargingType);   // no reachable ball yet? keep looking while held
                ButtonState b = chargingType == HitType.Spike ? spike : chargingType == HitType.Bump ? bump : set;
                if (spikeCharge.IsCharging && (b.Up || !b.Held))
                {
                    if (queued) spikeCharge.Lock();    // keep the charge built so far; the queued hit fires with it
                    else spikeCharge.Cancel();         // nothing was reachable: the press does nothing
                }
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

        // A queued hit: waits for the moment of contact, then fires with the charge built by then.
        struct PendingHit { public bool active; public float fireAt; public HitType type; public HitInput input; public float? chargeOverride; public float timingError; public Vector3 contactPoint; public float idealTime; public float pressTime; public int launchCount; }
        PendingHit pending;

        /// <summary>
        /// A hit attempt (a button press). The contact point is fixed per hit type; charge never moves it.
        /// - ball never comes within reach (or already passed): MISS, nothing is touched;
        /// - otherwise the hit is queued and fires at the moment of contact (closest approach) with the charge built by then.
        /// Timing: a press earlier than the contact is Perfect; only a late one (timingOverride > 0, for checks) is nudged.
        /// charge (0..1) replaces the live charge meter; for checks. Returns true if an attempt was registered (hit, queued or miss).
        /// </summary>
        public bool AttemptHit(HitType type, float? charge = null, float? timingOverride = null)
        {
            if (ball == null || !ball.IsInFlight || cooldownLeft > 0f) return false;
            pending.active = false;   // a new press replaces a queued hit

            // Ideal contact = when the ball's arc passes closest to the (moving) contact point.
            if (!ball.TryFindContact(d => ContactPointAt(type, d), timingLookBack, timingLookAhead, out float offset, out float distance)) return false;

            Vector3 contactPoint = ContactPointAt(type, offset);
            float timingError = timingOverride ?? Mathf.Max(0f, -offset);   // early press = Perfect (0)
            Vector3 aim = brain != null ? brain.AimPoint : new Vector3(0f, 0f, -courtSide * 4f);

            var input = new HitInput
            {
                type = type,
                charge = charge ?? spikeCharge.Value,
                timingError = timingError,
                closestDistance = distance,
                reachRadius = reachRadius,
                contactHeight = contactPoint.y,
                aimPoint = aim,
                hitterPosition = transform.position,
                ballPosition = ball.Position,
            };
            HitResult result = resolver.Resolve(input);

            // Ball not at the contact point yet: hold the hit until it gets there.
            if (result.contact && offset > 0.02f && !timingOverride.HasValue)
            {
                pending = new PendingHit { active = true, fireAt = Time.time + offset, type = type, input = input, chargeOverride = charge, timingError = timingError, contactPoint = contactPoint, idealTime = Time.time + offset, pressTime = Time.time, launchCount = ball.LaunchCount };
                return true;
            }

            Finish(type, input.charge, result, timingError, contactPoint, Time.time + offset, Time.time, aim);
            return true;
        }

        // Applies the result: spends stamina, launches the ball on a connect (a Miss leaves it alone), and reports it.
        void Finish(HitType type, float charge, HitResult result, float timingError, Vector3 contactPoint, float idealTime, float pressTime, Vector3 aim)
        {
            if (type == HitType.Spike || type == HitType.Serve) stamina.SpendSpike(charge); else stamina.SpendHit(charge);
            spikeCharge.Cancel();     // the wind-up is spent; a new press starts a new one
            queued = false;
            if (result.contact)
            {
                cooldownLeft = hitCooldown;
                LastHitTime = Time.time;
                ball.Launch(ball.Position, result.target, result.flightTime, true);
            }

            var report = new HitReport
            {
                type = type, grade = result.grade, contact = result.contact, timingError = timingError,
                charge = charge, quality = result.quality, aimPoint = aim, target = result.target, flightTime = result.flightTime,
                contactPoint = contactPoint, idealTime = idealTime, pressTime = pressTime,
            };
            LastReport = report;
            Debug.Log($"[Hit] {type} contact={result.contact} grade={result.grade} err={timingError:F2} charge={charge:F2} quality={result.quality:F2} flight={result.flightTime:F2} target={result.target}");
            OnHitAttempt?.Invoke(report);
        }

        void UpdatePending()
        {
            if (!pending.active) return;
            if (ball == null || !ball.IsInFlight || ball.LaunchCount != pending.launchCount)
            {
                pending.active = false;   // the ball was hit by someone else / reset: this wind-up is void
                queued = false;
                if (!spikeCharge.IsCharging) spikeCharge.Cancel();
                return;
            }
            if (Time.time < pending.fireAt) return;
            pending.active = false;

            // Swing now with the charge built so far, aimed where the player looks at this moment.
            HitInput input = pending.input;
            input.charge = pending.chargeOverride ?? spikeCharge.Value;
            if (brain != null) input.aimPoint = brain.AimPoint;
            input.hitterPosition = transform.position;
            input.ballPosition = ball.Position;
            Finish(pending.type, input.charge, resolver.Resolve(input), pending.timingError, pending.contactPoint, pending.idealTime, pending.pressTime, input.aimPoint);
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
