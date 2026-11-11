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
        [SerializeField] float chargeSeconds = 0.4f; // holding jump this long on the ground gives a full charge

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
        [SerializeField] float timingLookAhead = 6f;      // look all the way to the landing: a release queues the hit

        [Header("Dive (Shift / B / Circle)")]
        [SerializeField] float diveDistance = 3.5f;       // metres thrown toward the move direction
        [SerializeField] float diveSeconds = 0.4f;
        [SerializeField] float diveRecoverySeconds = 0.5f; // get-up: no moving, jumping or hitting
        [SerializeField] Vector3 digContactOffset = new Vector3(0f, 0.35f, 0.7f);   // low and forward
        [SerializeField] float digReachBonus = 0.3f;      // the dig reaches this much farther than a normal hit
        [SerializeField] float digCharge = 0.3f;          // the dig is a soft auto-bump toward the aim point

        [Header("Serve")]
        [SerializeField] float serveNetMargin = 0.2f;     // a serve is slowed (lobbed) until it clears the net by this much
        [SerializeField] float serveMaxExtraSeconds = 1.0f;

        [Header("Charge, stamina, hit numbers")]
        [SerializeField] Stamina stamina = new Stamina();
        [SerializeField] ChargeMeter jumpCharge = new ChargeMeter();
        [SerializeField] ChargeMeter spikeCharge = new ChargeMeter { secondsToFull = 0.5f };   // a spike/bump/set wind-up is full after 0.5 s
        [SerializeField] HitTuning hitTuning = new HitTuning();

        /// <summary>Raised whenever this player connects with the ball (serve, hit or dig). The MatchManager counts touches with it.</summary>
        public event Action<PlayerController> OnBallTouched;

        /// <summary>Raised after every hit attempt that was inside reach (including whiffs).</summary>
        public event Action<HitReport> OnHitAttempt;

        CharacterController controller;
        IPlayerBrain brain;
        HitResolver resolver;
        Vector3 planarVelocity;
        float verticalVelocity;
        bool grounded;
        float cooldownLeft;

        // dive state
        bool diving;
        float diveLeft;
        float recoverLeft;
        Vector3 diveDir;

        public Stamina Stamina => stamina;
        public ChargeMeter JumpCharge => jumpCharge;
        public ChargeMeter SpikeCharge => spikeCharge;
        public bool IsGrounded => grounded;
        public float VerticalVelocity => verticalVelocity;
        public float Speed01 => Mathf.Clamp01(planarVelocity.magnitude / moveSpeed);
        public Vector3 PlanarVelocity => planarVelocity;
        public float ReachRadius => reachRadius;
        public BallController Ball => ball;
        public Transform HeadTarget => headTarget;
        public HitReport LastReport { get; private set; }
        public float LastHitTime { get; private set; } = -99f;   // Time.time of the last connected hit (the visual swings on it)
        public bool WindingUp => spikeCharge.IsCharging || spikeCharge.Value > 0f;
        public HitType WindUpType => chargingType;
        public int PlayerId { get; set; }                 // 0..3, set by the MatchManager
        public float CourtSide => courtSide;
        public int Team => courtSide < 0f ? 0 : 1;
        public HitTuning Tuning => hitTuning;
        /// <summary>True while this player is the server and the ball is held: a spike-button hold/release serves.</summary>
        public bool ServeMode { get; set; }
        public bool IsDiving => diving;
        public bool IsRecovering => recoverLeft > 0f;
        /// <summary>0 = upright, 1 = fully down (diving); eases back to 0 during the get-up. PlayerVisual uses it.</summary>
        public float DiveAmount => diving ? 1f : (diveRecoverySeconds > 0f ? Mathf.Clamp01(recoverLeft / diveRecoverySeconds) : 0f);
        public Vector3 DigPoint => transform.position + FlatForward() * digContactOffset.z + Vector3.up * digContactOffset.y;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            brain = GetComponent<IPlayerBrain>();
            resolver = new HitResolver(hitTuning);
            jumpCharge.secondsToFull = chargeSeconds;
        }

        void Update()
        {
            if (brain == null) return;
            float dt = Time.deltaTime;
            cooldownLeft -= dt;

            Vector2 move = brain.Move;
            bool busy = diving || recoverLeft > 0f;
            stamina.Tick(dt, move.sqrMagnitude > 0.01f && !busy);

            UpdateDive(dt, move);
            busy = diving || recoverLeft > 0f;
            Move(busy ? Vector2.zero : move, dt);
            if (!busy)
            {
                UpdateJump(dt);
                if (ServeMode) UpdateServe(dt); else UpdateHits(dt);
                UpdatePending();
                FaceAim(dt);
            }
        }

        /// <summary>Clears any wind-up, charge, dive or queued hit (the MatchManager calls it at every new serve).</summary>
        public void ResetState()
        {
            pending.active = false;
            diving = false;
            recoverLeft = 0f;
            jumpCharge.Cancel();
            spikeCharge.Cancel();
            cooldownLeft = 0f;
        }

        /// <summary>Swaps the brain (the MatchManager's auto-play switch uses it).</summary>
        public void SetBrain(IPlayerBrain newBrain) { brain = newBrain; }

        /// <summary>The brain driving this player right now (a HumanBrain for the person, a SimpleBotBrain for a bot).</summary>
        public IPlayerBrain Brain => brain;

        Vector3 FlatForward()
        {
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        // ---------------- movement ----------------

        void Move(Vector2 input, float dt)
        {
            if (diving)
            {
                planarVelocity = diveDir * (diveDistance / diveSeconds);   // a fixed lunge: 3.5 m in 0.4 s
            }
            else
            {
                Vector3 wish = new Vector3(input.x, 0f, input.y) * moveSpeed;
                float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
                planarVelocity = Vector3.MoveTowards(planarVelocity, wish, rate * dt);
            }

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

        // ---------------- dive ----------------

        // Press Dive on the ground: lunge toward the move direction (or where you face), then a get-up pause.
        // While diving, a ball near the low forward contact point is dug (auto-bumped) toward the aim point.
        void UpdateDive(float dt, Vector2 move)
        {
            if (recoverLeft > 0f) recoverLeft = Mathf.Max(0f, recoverLeft - dt);

            if (!diving && recoverLeft <= 0f && grounded && !ServeMode && brain.Dive.Down && stamina.CanDive)
            {
                stamina.SpendDive();
                diveDir = move.sqrMagnitude > 0.01f ? new Vector3(move.x, 0f, move.y).normalized : FlatForward();
                transform.rotation = Quaternion.LookRotation(diveDir, Vector3.up);
                diving = true;
                diveLeft = diveSeconds;
                pending.active = false;
                jumpCharge.Cancel();
                spikeCharge.Cancel();
            }
            if (!diving) return;

            TryDig();
            diveLeft -= dt;
            if (diveLeft <= 0f)
            {
                diving = false;
                recoverLeft = diveRecoverySeconds;
                planarVelocity = Vector3.zero;     // the lunge ends where it ends (3.5 m)
            }
        }

        void TryDig()
        {
            if (ball == null || !ball.IsInFlight || cooldownLeft > 0f) return;
            Vector3 p = DigPoint;
            if (!ContactTiming.DigInReach(ball.Position, p, reachRadius, digReachBonus)) return;
            Vector3 aim = brain.AimPoint;
            var input = new HitInput
            {
                type = HitType.Bump, charge = digCharge, timingError = 0f, closestDistance = 0f, reachRadius = reachRadius,
                contactHeight = p.y, aimPoint = aim, hitterPosition = transform.position, ballPosition = ball.Position,
            };
            Finish(HitType.Bump, digCharge, resolver.Resolve(input), 0f, p, Time.time, Time.time, aim);
        }

        // ---------------- serve ----------------

        // The server holds the spike button to charge and lets go to serve toward the aim point (perfect timing, no ball needed in flight).
        void UpdateServe(float dt)
        {
            ButtonState spike = brain.Spike;
            if (!spikeCharge.IsCharging)
            {
                if (spike.Down) { spikeCharge.Begin(); chargingType = HitType.Serve; }
                return;
            }
            spikeCharge.Tick(dt, stamina.MaxChargeAllowed);
            if (spike.Up || !spike.Held)
            {
                spikeCharge.Lock();
                DoServe(spikeCharge.Value);
            }
        }

        void DoServe(float charge)
        {
            if (ball == null || ball.IsInFlight) { spikeCharge.Cancel(); return; }
            Vector3 start = ball.Position;
            Vector3 aim = brain.AimPoint;
            var input = new HitInput
            {
                type = HitType.Serve, charge = charge, timingError = 0f, closestDistance = 0f, reachRadius = reachRadius,
                contactHeight = start.y, aimPoint = aim, hitterPosition = transform.position, ballPosition = start,
            };
            HitResult result = resolver.Resolve(input);
            result.flightTime = ServeFlightTime(start, result.target, result.flightTime);
            ServeMode = false;
            Finish(HitType.Serve, charge, result, 0f, start, Time.time, Time.time, aim);
        }

        // A serve that would hit the net is slowed down (a higher, softer arc) until it clears it.
        float ServeFlightTime(Vector3 start, Vector3 groundTarget, float time)
        {
            float g = ball.Gravity;
            Vector3 centre = new Vector3(groundTarget.x, ball.Radius, groundTarget.z);
            float max = time + serveMaxExtraSeconds;
            for (; time < max; time += 0.05f)
            {
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(start, centre, time, g);
                if (BallTrajectory.ClearsNet(start, v0, time, BallTrajectory.NetTopHeight + serveNetMargin, ball.Radius, 0f, g)) break;
            }
            return time;
        }

        // ---------------- jump ----------------

        // Hold jump on the ground to charge (the body crouches), release to jump: height = lerp(tap, full, charge).
        // A quick tap releases at once, so it still jumps right away at the tap height. Stamina caps the charge.
        void UpdateJump(float dt)
        {
            ButtonState jump = brain.Jump;
            if (jump.Down && grounded) jumpCharge.Begin();
            if (!jumpCharge.IsCharging) return;
            if (jump.Up || !jump.Held)
            {
                float charge = jumpCharge.Release();
                if (grounded) Jump(charge);
            }
            else if (!jump.Down) jumpCharge.Tick(dt, stamina.MaxChargeAllowed);   // charge builds after the press frame, so a tap stays a pure tap
        }

        /// <summary>Jumps with the given charge (0 = tap hop, 1 = full jump) and spends stamina in proportion.</summary>
        public void Jump(float charge)
        {
            float apex = Mathf.Lerp(tapJumpHeight, fullJumpHeight, Mathf.Clamp01(charge));
            verticalVelocity = ContactTiming.JumpSpeed(gravity, apex);
            grounded = false;
            stamina.SpendJump(charge);
        }

        // ---------------- hitting ----------------

        HitType chargingType;

        // Spike, bump and set are wind-ups. PRESS starts the charge and pulls the arm back; nothing is hit while the
        // button is held. RELEASE swings: a ball in reach is hit at once, one arriving later is queued for the contact
        // moment (a quick tap), and one that already passed (or never comes close) is left alone.
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
                {
                    spikeCharge.Lock();                                              // keep the charge built so far
                    if (!AttemptHit(chargingType, spikeCharge.Value)) spikeCharge.Cancel();   // nothing reachable: the swing does nothing
                }
            }
        }

        /// <summary>
        /// "Hit now" cue: true while a hit button is held and the ball is inside the reach of that hit's contact point.
        /// 'type' is which hit is being held (where the ring goes).
        /// </summary>
        public bool HitReady(out HitType type)
        {
            type = chargingType;
            if (!spikeCharge.IsCharging || ball == null || !ball.IsInFlight || cooldownLeft > 0f) return false;
            return InReach(type, ball.Position, ContactPointAt(type));
        }

        /// <summary>
        /// Is a ball at 'ballPos' inside the reach of this hit's contact point 'contact'? A spike uses a tall cylinder
        /// (see HitTuning.spikeReachBelow / Above); bump and set use a sphere. The bots and the cue use this too.
        /// </summary>
        public bool InReach(HitType type, Vector3 ballPos, Vector3 contact)
        {
            return type == HitType.Spike
                ? ContactTiming.SpikeInReach(ballPos, contact, reachRadius, hitTuning.spikeReachBelow, hitTuning.spikeReachAbove)
                : ContactTiming.InReach(ballPos, contact, reachRadius);
        }

        /// <summary>The contact offset (player-relative) for this kind of hit.</summary>
        public Vector3 ContactOffset(HitType type)
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
            // passed already, or never comes within reach (a sphere; a tall cylinder for a spike): the swing touches nothing
            if (!InReach(type, ball.PositionAtOffset(offset), contactPoint)) return false;
            distance = Mathf.Min(distance, reachRadius);   // inside the zone: never a Miss in the resolver
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
                ballGravity = ball.Gravity,
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
            if (!spikeCharge.IsCharging) spikeCharge.Cancel();   // the swing is spent (a wind-up already started for the next hit is left alone)
            if (result.contact)
            {
                cooldownLeft = hitCooldown;
                LastHitTime = Time.time;
                ball.Launch(ball.Position, result.target, result.flightTime, true);
                OnBallTouched?.Invoke(this);
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
            input.ballGravity = ball.Gravity;
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
