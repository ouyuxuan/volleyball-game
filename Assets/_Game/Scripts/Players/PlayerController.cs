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
        public float apex;             // bump / set / serve peak height (m)
        public float speed;            // spike speed (m/s) incl. momentum and combo
        public float momentum;         // spike: m/s added by the run-up
        public float combo;            // spike: 1.0 / 1.1 / 1.2
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
        [SerializeField] float gravity = 30f;        // the player's gravity outside the hang band (fast launch and landing)
        [SerializeField] float hangSpeed = 2.2f;     // m/s: while the vertical speed is below this (near the top) gravity is weaker...
        [SerializeField, Range(0.1f, 1f)] float hangGravityScale = 0.35f;   // ...this fraction of 'gravity', so the jump hangs and there is time to hit in the air
        [SerializeField] float chargeSeconds = 0.4f; // holding jump this long on the ground gives a full charge

        [Header("Sprint (Left Ctrl / left-stick click)")]
        [SerializeField] float sprintMultiplier = 1.25f;
        [SerializeField] float sprintDrainPerSecond = 22f;
        [SerializeField] float sprintMinStamina = 5f;       // sprint can't start below this

        [Header("Air control")]
        [SerializeField] float airControl = 0.3f;            // in the air, acceleration is this fraction of the ground's
        [SerializeField] float standingJumpSpeed = 2f;       // a jump that takes off slower than this is a "standing" jump...
        [SerializeField] float standingJumpAirSpeed = 2.5f;  // ...and can't go faster than this sideways until it lands

        [Header("Burnout (a dive with too little stamina)")]
        [SerializeField] float burnoutSeconds = 2f;
        [SerializeField] float burnoutSpeedMultiplier = 0.6f;

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

        [Header("Serve (hold Spike to toss, then jump and hit)")]
        [SerializeField] float serveNetMargin = 0.2f;     // a serve is slowed (lobbed) until it clears the net by this much
        [SerializeField] float serveMaxExtraSeconds = 1.0f;
        [SerializeField] float servePushStep = 0.25f;     // a serve aimed too close to the net to clear it lands this much deeper per step
        [SerializeField] float serveLineDistance = 8.1f;  // before the toss the server stays at least this far from the net (0.1 m behind the baseline)
        [SerializeField] float tossChargeSeconds = 0.8f;  // holding Spike this long gives a full toss
        [SerializeField] float tossApexMin = 3.6f;        // world height of the toss peak at charge 0...
        [SerializeField] float tossApexMax = 6.0f;        // ...and at full charge
        [SerializeField] float tossDistanceMin = 0.8f;    // the toss lands this far in front of the server (toward the net) at charge 0...
        [SerializeField] float tossDistanceMax = 2.5f;    // ...and at full charge
        [SerializeField] float jumpServeMaxSpeed = 22f;   // a jump serve is flattened (shorter flight) but never faster than this sideways-plus-forward speed (m/s)

        [Header("Block (jump at the net with no hit button held)")]
        [SerializeField] BlockTuning block = new BlockTuning();

        [Header("Charge, stamina, hit numbers")]
        [SerializeField] Stamina stamina = new Stamina();
        [SerializeField] ChargeMeter jumpCharge = new ChargeMeter();
        [SerializeField] ChargeMeter spikeCharge = new ChargeMeter { secondsToFull = 0.5f };   // a spike/bump/set wind-up is full after 0.5 s
        [SerializeField] HitTuning hitTuning = new HitTuning();

        /// <summary>Raised whenever this player connects with the ball (serve, hit or dig). The MatchManager counts touches with it.</summary>
        public event Action<PlayerController> OnBallTouched;

        /// <summary>Raised when this player's block stops the ball (the MatchManager counts it as a touch, "isBlock").</summary>
        public event Action<PlayerController> OnBlocked;

        /// <summary>Raised after every hit attempt that was inside reach (including whiffs).</summary>
        public event Action<HitReport> OnHitAttempt;

        /// <summary>Touches this player's team already used on the ball (0, 1 or 2). The MatchManager fills it from the RulesEngine; it picks the spike combo multiplier.</summary>
        public Func<int> TeamTouchCount;

        CharacterController controller;
        IPlayerBrain brain;
        HitResolver resolver;
        readonly ChargeMeter tossMeter = new ChargeMeter();
        Vector3 planarVelocity;
        float verticalVelocity;
        bool grounded;
        float cooldownLeft;

        // dive state
        bool diving;
        float diveLeft;
        float recoverLeft;
        Vector3 diveDir;

        // sprint / burnout / air state
        bool sprinting;
        bool burnoutPending;     // the dive was started with too little stamina: burnout begins when its get-up ends
        float burnoutLeft;
        float airSpeedCap = float.PositiveInfinity;   // set by a standing jump, cleared on landing

        // block state: the shield's sideways slide, and the ball's last position (to see it cross the shield between frames)
        float blockSlide;
        Vector3 prevBall;
        int prevLaunch = -1;

        // numbers the AI needs to judge what it can reach
        public float MoveSpeed => moveSpeed;
        public float SprintMultiplier => sprintMultiplier;
        /// <summary>The jump arc (launch speed, hang time, height over time). The AI and the contact prediction use it too.</summary>
        public JumpArc Arc => new JumpArc(gravity, hangSpeed, hangGravityScale);
        public float TapJumpHeight => tapJumpHeight;
        public float DiveDistance => diveDistance;
        public float DigReach => reachRadius + digReachBonus;
        public BlockTuning Block => block;

        public bool IsSprinting => sprinting;
        public bool IsBurnedOut => burnoutLeft > 0f;
        /// <summary>1 at the start of burnout, falling to 0 as it ends.</summary>
        public float Burnout01 => burnoutSeconds > 0f ? Mathf.Clamp01(burnoutLeft / burnoutSeconds) : 0f;
        float ChargeCap => IsBurnedOut ? 0f : stamina.MaxChargeAllowed;   // burnout: charge can't build

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
        /// <summary>True while this player is the server and the serve is not hit yet: Spike tosses, then Spike hits the toss.</summary>
        public bool ServeMode { get; set; }
        /// <summary>True once this server has tossed the ball (until the serve is hit or the point ends).</summary>
        public bool Tossed { get; private set; }
        /// <summary>The toss meter (0..1 while Spike is held before the toss). No stamina cost.</summary>
        public ChargeMeter TossCharge => tossMeter;
        /// <summary>True while the server holds Spike to charge the toss (the toss preview shows then).</summary>
        public bool TossCharging => ServeMode && !Tossed && tossMeter.IsCharging;
        /// <summary>Raised when the ball is tossed. It is not a touch: the MatchManager only uses it to watch for a dropped toss.</summary>
        public event Action<PlayerController> OnTossed;
        public event Action<PlayerController> OnJumped;     // takeoff
        public event Action<PlayerController> OnLanded;     // touched down after a jump
        bool jumpedAndAirborne;
        public event Action<PlayerController> OnDiveEnded;  // the dive lunge finished
        public bool IsDiving => diving;
        public bool HasQueuedHit => pending.active;          // a released hit is waiting for the ball (the visual holds its pose)
        public HitType QueuedHitType => pending.type;
        public bool IsRecovering => recoverLeft > 0f;
        /// <summary>0 = upright, 1 = fully down (diving); eases back to 0 during the get-up. PlayerVisual uses it.</summary>
        public float DiveAmount => diving ? 1f : (diveRecoverySeconds > 0f ? Mathf.Clamp01(recoverLeft / diveRecoverySeconds) : 0f);
        public Vector3 DigPoint => transform.position + FlatForward() * digContactOffset.z + Vector3.up * digContactOffset.y;

        // ---------------- block state (read by the AI, the visual and the tests) ----------------

        bool HoldingHit => spikeCharge.IsCharging || brain.Spike.Held || brain.Bump.Held || brain.Set.Held;

        /// <summary>
        /// Blocking = in the air within block.netDistance of the net, not diving, no hit button held and no hit queued.
        /// (A server in serve mode is not blocking.)
        /// </summary>
        public bool IsBlocking => brain != null && !grounded && !diving && !ServeMode && !pending.active && !HoldingHit
                                  && Mathf.Abs(transform.position.z) <= block.netDistance;

        /// <summary>The shield faces the way the body faces (square to the net while blocking). Flat, points at the opponents.</summary>
        public Vector3 BlockNormal => FlatForward();

        /// <summary>The middle of the shield: hand height above the feet, a little toward the net, slid sideways by the strafe.</summary>
        public Vector3 BlockCenter
        {
            get
            {
                Vector3 n = BlockNormal;
                return transform.position + Vector3.up * block.handHeight + n * block.forward + Vector3.Cross(Vector3.up, n) * blockSlide;
            }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            brain = GetComponent<IPlayerBrain>();
            resolver = new HitResolver(hitTuning);
            jumpCharge.secondsToFull = chargeSeconds;
            tossMeter.secondsToFull = tossChargeSeconds;
        }

        void Update() { if (brain != null) Tick(Time.deltaTime); }

        /// <summary>One frame of player logic (Update calls it; tests call it with a fixed step).</summary>
        public void Tick(float dt)
        {
            if (brain == null) return;
            cooldownLeft -= dt;

            Vector2 move = brain.Move;
            bool busy = diving || recoverLeft > 0f;
            UpdateSprint(dt, move, busy);
            stamina.Tick(dt, move.sqrMagnitude > 0.01f && !busy, !sprinting && !IsBurnedOut && !burnoutPending);

            UpdateDive(dt, move);
            busy = diving || recoverLeft > 0f;
            Move(busy ? Vector2.zero : move, dt);
            if (!busy)
            {
                UpdateJump(dt);
                if (ServeMode && !Tossed) UpdateToss(dt); else UpdateHits(dt);   // after the toss the server hits with Spike like anyone
                UpdatePending();
                if (IsBlocking) FaceNet(dt); else FaceAim(dt);
            }
            UpdateBlock(dt, move);
        }

        // The shield slides with left/right input while blocking; and if the ball crosses it, it rebounds.
        void UpdateBlock(float dt, Vector2 move)
        {
            bool blocking = IsBlocking;
            Vector3 right = Vector3.Cross(Vector3.up, BlockNormal);
            float want = blocking ? Mathf.Clamp(Vector3.Dot(new Vector3(move.x, 0f, move.y), right), -1f, 1f) * block.strafe : 0f;
            blockSlide = Mathf.MoveTowards(blockSlide, want, block.strafeSpeed * dt);

            if (ball == null || !ball.IsInFlight) { prevLaunch = -1; return; }
            Vector3 cur = ball.Position;
            if (blocking && prevLaunch == ball.LaunchCount) TryBlock(prevBall, cur);
            prevBall = cur;
            prevLaunch = ball.LaunchCount;
        }

        /// <summary>
        /// Did the ball cross the shield between 'prev' and 'cur'? If so it rebounds (BlockShield.Rebound), the ball is launched
        /// again and OnBlocked fires. A serve (or a toss) can't be blocked. Returns true on a block.
        /// </summary>
        public bool TryBlock(Vector3 prev, Vector3 cur)
        {
            if (!IsBlocking || ball == null || !ball.IsInFlight || ball.LastHitType == HitType.Serve || ball.LastHitType == HitType.Toss) return false;
            Vector3 n = BlockNormal;
            Vector3 vel = ball.Velocity;
            if (Vector3.Dot(vel, n) >= 0f) return false;                // must be travelling toward this side
            // The block is a wall from the top of the net up to the fingertips: however high the jump, a ball that crosses
            // just over the net and below the hands is still stopped (a high jump used to lift the shield clear over the ball).
            Vector3 centre = BlockCenter;
            float top = centre.y + block.height * 0.5f;
            float bottom = Mathf.Min(centre.y - block.height * 0.5f, BallTrajectory.NetTopHeight);
            centre.y = (top + bottom) * 0.5f;
            if (!BlockShield.TryCross(prev, cur, centre, n, block.width, top - bottom, ball.Radius, out Vector3 hit, out float u))
            {
                if (BlockShield.TryCross(prev, cur, centre, n, 99f, 99f, 0f, out Vector3 near, out float nu))
                    Debug.Log($"[BlockMiss] p{PlayerId} ball passed the shield {nu:F2} m sideways, {near.y - centre.y:F2} m vertically off");
                return false;
            }

            BlockShield.Rebound(hit, vel, n, u, courtSide, block, ball.Gravity, ball.Radius, out Vector3 landing, out float flight, brain.AimPoint);
            ball.Launch(hit, landing, flight, true, HitType.Block);
            Debug.Log($"[Block] p{PlayerId} shield=({centre.x:F2},{centre.y:F2},{centre.z:F2}) hit=({hit.x:F2},{hit.y:F2},{hit.z:F2}) offset={u:F2} in={vel.magnitude:F1} m/s rebound lands=({landing.x:F1},{landing.z:F1}) flight={flight:F2}");
            OnBlocked?.Invoke(this);
            return true;
        }

        /// <summary>Clears any wind-up, charge, dive or queued hit (the MatchManager calls it at every new serve).</summary>
        public void ResetState()
        {
            pending.active = false;
            diving = false;
            recoverLeft = 0f;
            burnoutPending = false;
            burnoutLeft = 0f;
            sprinting = false;
            jumpCharge.Cancel();
            spikeCharge.Cancel();
            tossMeter.Cancel();
            Tossed = false;
            cooldownLeft = 0f;
            blockSlide = 0f;
            jumpedAndAirborne = false;
            prevLaunch = -1;
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

        // Sprint: held, moving on the ground, not busy, not charging a jump, not burned out, and enough stamina
        // (5 to start, anything above 0 to keep going). It drains stamina, which then does not regenerate.
        void UpdateSprint(float dt, Vector2 move, bool busy)
        {
            bool want = brain.Sprint && move.sqrMagnitude > 0.01f && grounded && !busy && !jumpCharge.IsCharging && !IsBurnedOut;
            float s = stamina.Current;
            bool was = sprinting;
            sprinting = want && (sprinting ? s > 0f : s >= sprintMinStamina);
            if (sprinting && !was) Debug.Log($"[Sprint] p{PlayerId}");
            if (sprinting) stamina.Drain(sprintDrainPerSecond * dt);
        }

        void Move(Vector2 input, float dt)
        {
            if (diving)
            {
                planarVelocity = diveDir * (diveDistance / diveSeconds);   // a fixed lunge: 3.5 m in 0.4 s
            }
            else
            {
                float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f) * (IsBurnedOut ? burnoutSpeedMultiplier : 1f);
                Vector3 wish = new Vector3(input.x, 0f, input.y) * speed;
                float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
                if (!grounded) rate *= airControl;                       // little steering in the air: run-ups carry, standing jumps don't drift
                planarVelocity = Vector3.MoveTowards(planarVelocity, wish, rate * dt);
                if (!grounded) planarVelocity = Vector3.ClampMagnitude(planarVelocity, Mathf.Max(airSpeedCap, 0f));
            }

            float dy;   // height gained this frame
            if (grounded && verticalVelocity < 0f) { verticalVelocity = -2f; dy = verticalVelocity * dt; }   // stay pressed onto the sand
            else dy = Arc.Advance(ref verticalVelocity, dt);                                                 // weaker gravity near the top of a jump

            Vector3 pos = transform.position;
            Vector3 next = pos + planarVelocity * dt + Vector3.up * dy;
            next = ClampToCourt(next);
            bool wasGrounded = grounded;
            controller.Move(next - pos);
            grounded = controller.isGrounded || transform.position.y <= 0.001f;
            if (grounded && !wasGrounded && jumpedAndAirborne) { jumpedAndAirborne = false; OnLanded?.Invoke(this); }   // only after a real jump (the ground check can flicker while running)
            if (grounded) airSpeedCap = float.PositiveInfinity;
        }

        // Keeps the player on their own half (can't cross the net) and inside the beach area.
        Vector3 ClampToCourt(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -sideLimit, sideLimit);
            float near = courtSide * (ServeMode && !Tossed ? serveLineDistance : netMargin);   // closest z to the net (before the toss: behind the baseline)
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
            TickRecoveryAndBurnout(dt);

            if (!diving && recoverLeft <= 0f && grounded && !ServeMode && brain.Dive.Down)
            {
                // Always allowed. Too little stamina still dives, drains to 0 and burns the player out after the get-up.
                burnoutPending = stamina.Current < stamina.diveCost;
                Debug.Log($"[Dive] p{PlayerId} burnout={burnoutPending}");
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
                OnDiveEnded?.Invoke(this);
                recoverLeft = diveRecoverySeconds;
                planarVelocity = Vector3.zero;     // the lunge ends where it ends (3.5 m)
            }
        }

        // The get-up counts down; when it ends a pending burnout starts, and a running burnout counts down.
        void TickRecoveryAndBurnout(float dt)
        {
            if (recoverLeft > 0f)
            {
                recoverLeft = Mathf.Max(0f, recoverLeft - dt);
                if (recoverLeft <= 0f && burnoutPending) { burnoutPending = false; burnoutLeft = burnoutSeconds; }
            }
            else if (!diving && burnoutLeft > 0f) burnoutLeft = Mathf.Max(0f, burnoutLeft - dt);
        }

        void TryDig()
        {
            if (ball == null || !ball.IsInFlight || cooldownLeft > 0f || TossIsNotMine) return;
            Vector3 p = DigPoint;
            if (!ContactTiming.DigInReach(ball.Position, p, reachRadius, digReachBonus)) return;
            Vector3 aim = brain.AimPoint;
            var input = new HitInput
            {
                type = HitType.Bump, charge = digCharge, timingError = 0f, closestDistance = 0f, reachRadius = reachRadius,
                contactHeight = p.y, aimPoint = aim, hitterPosition = transform.position, ballPosition = ball.Position,
                ballGravity = ball.Gravity, hitterVelocity = planarVelocity, teamTouchIndex = TeamTouchCount != null ? TeamTouchCount() : 0,
            };
            Finish(HitType.Bump, digCharge, resolver.Resolve(input), 0f, p, Time.time, Time.time, aim);
        }

        // ---------------- serve: toss, then jump and hit ----------------

        // Before the toss: hold Spike to fill the toss meter, let go to toss the ball (no ball needed in flight, no stamina).
        void UpdateToss(float dt)
        {
            ButtonState spike = brain.Spike;
            if (!tossMeter.IsCharging)
            {
                if (spike.Down) tossMeter.Begin();
                return;
            }
            tossMeter.Tick(dt, 1f);
            if (spike.Up || !spike.Held) DoToss(tossMeter.Release());
        }

        /// <summary>The toss a release would make now at this charge: peak height, landing spot (straight toward the net) and flight time. The preview uses it too.</summary>
        public HitResult PredictToss(float charge)
        {
            charge = Mathf.Clamp01(charge);
            Vector3 start = ball.Position;
            float apex = Mathf.Lerp(tossApexMin, tossApexMax, charge);
            float distance = Mathf.Lerp(tossDistanceMin, tossDistanceMax, charge);
            return new HitResult
            {
                contact = true, apex = apex,
                target = new Vector3(start.x, 0f, transform.position.z - courtSide * distance),
                flightTime = BallTrajectory.FlightTimeForApex(start.y, apex, ball.Radius, ball.Gravity),
            };
        }

        // Launches the toss. It is not a touch (no OnBallTouched): the rally starts when the server hits it.
        void DoToss(float charge)
        {
            if (ball == null || ball.IsInFlight) return;
            HitResult toss = PredictToss(charge);
            ball.Launch(ball.Position, toss.target, toss.flightTime, false, HitType.Toss);
            Tossed = true;
            Debug.Log($"[Toss] p{PlayerId} charge={charge:F2} apex={toss.apex:F2} lands=({toss.target.x:F1},{toss.target.z:F1}) flight={toss.flightTime:F2}");
            OnTossed?.Invoke(this);
        }

        /// <summary>
        /// The serve a hit at 'start' would make now: the real resolver call plus the net-clear assist, and for an airborne
        /// server (a jump serve) a flatter, faster flight blended in by the charge. The hit itself and the tests use it.
        /// </summary>
        public HitResult PredictServe(float charge, Vector3 start, bool airborne)
        {
            var input = new HitInput
            {
                type = HitType.Serve, charge = charge, timingError = 0f, closestDistance = 0f, reachRadius = reachRadius,
                contactHeight = start.y, aimPoint = brain.AimPoint, hitterPosition = transform.position, ballPosition = start,
                ballGravity = ball.Gravity,
            };
            return ServeFrom(input, airborne);
        }

        // A serve never scatters (timing error 0) so a legal aim is never turned into a fault.
        HitResult ServeFrom(HitInput input, bool airborne)
        {
            input.type = HitType.Serve;
            input.timingError = 0f;
            Vector3 start = input.ballPosition;
            HitResult result = resolver.Resolve(input);
            // Net-clear assist: lob the serve (longer flight) until it clears the net. A target so close to the net that no lob
            // can clear it is moved back, away from the net, a step at a time (same idea as the spike's push), never past the baseline.
            float side = result.target.z >= 0f ? 1f : -1f;
            float maxDepth = hitTuning.courtHalfLength - hitTuning.baselineMargin;
            float time = result.flightTime;
            while (!ServeClears(start, result.target, ref time) && Mathf.Abs(result.target.z) + servePushStep <= maxDepth)
            {
                result.target.z += side * servePushStep;
                time = result.flightTime;
            }
            result.flightTime = time;
            if (airborne) result.flightTime = FlattenedFlight(start, result.target, result.flightTime, input.charge);
            return result;
        }

        // Jump serve: the shortest flight time (0.05 s steps below the lob's) that still clears the net by serveNetMargin and keeps
        // the speed under jumpServeMaxSpeed. The charge blends from the lob (0) to that flat serve (1). Clearance only grows with
        // flight time, so every blend in between clears too.
        float FlattenedFlight(Vector3 start, Vector3 groundTarget, float lobTime, float charge)
        {
            float g = ball.Gravity;
            Vector3 centre = new Vector3(groundTarget.x, ball.Radius, groundTarget.z);
            float flat = new Vector2(centre.x - start.x, centre.z - start.z).magnitude;
            float best = lobTime;
            for (float t = lobTime - 0.05f; t >= 0.1f && flat / t <= jumpServeMaxSpeed; t -= 0.05f)
            {
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(start, centre, t, g);
                if (BallTrajectory.ClearsNet(start, v0, t, BallTrajectory.NetTopHeight + serveNetMargin, ball.Radius, 0f, g)) best = t;
            }
            return Mathf.Lerp(lobTime, best, Mathf.Clamp01(charge));
        }

        // Tries longer flight times (up to serveMaxExtraSeconds more) until the serve clears the net by serveNetMargin.
        // Returns false if none does (time is then the longest tried).
        bool ServeClears(Vector3 start, Vector3 groundTarget, ref float time)
        {
            float g = ball.Gravity;
            Vector3 centre = new Vector3(groundTarget.x, ball.Radius, groundTarget.z);
            float max = time + serveMaxExtraSeconds;
            for (; time < max; time += 0.05f)
            {
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(start, centre, time, g);
                if (BallTrajectory.ClearsNet(start, v0, time, BallTrajectory.NetTopHeight + serveNetMargin, ball.Radius, 0f, g)) return true;
            }
            return false;
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
            else if (!jump.Down) jumpCharge.Tick(dt, ChargeCap);   // charge builds after the press frame, so a tap stays a pure tap
        }

        /// <summary>Jumps with the given charge (0 = tap hop, 1 = full jump) and spends stamina in proportion.</summary>
        public void Jump(float charge)
        {
            float apex = Mathf.Lerp(tapJumpHeight, fullJumpHeight, Mathf.Clamp01(charge));
            verticalVelocity = Arc.LaunchSpeed(apex);
            grounded = false;
            // a jump from (almost) standing still keeps little sideways speed; a running jump keeps its run-up
            airSpeedCap = planarVelocity.magnitude < standingJumpSpeed ? standingJumpAirSpeed : float.PositiveInfinity;
            stamina.SpendJump(charge);
            jumpedAndAirborne = true;
            OnJumped?.Invoke(this);
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
                else if (!ServeMode && bump.Down) { spikeCharge.Begin(); chargingType = HitType.Bump; }   // a server only has Spike
                else if (!ServeMode && set.Down) { spikeCharge.Begin(); chargingType = HitType.Set; }
            }
            if (spikeCharge.IsCharging)
            {
                spikeCharge.Tick(dt, ChargeCap);
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
            float y = Arc.HeightFrom(p.y, verticalVelocity, Mathf.Max(0f, dt));
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
            if (ball == null || !ball.IsInFlight || cooldownLeft > 0f || TossIsNotMine) return false;
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
                hitterVelocity = planarVelocity,
                teamTouchIndex = TeamTouchCount != null ? TeamTouchCount() : 0,
            };
            HitResult result = ResolveHit(input);

            // Ball not at the contact point yet: hold the hit until it gets there.
            if (result.contact && offset > 0.02f && !timingOverride.HasValue)
            {
                pending = new PendingHit { active = true, fireAt = Time.time + offset, type = type, input = input, chargeOverride = charge, timingError = timingError, contactPoint = contactPoint, idealTime = Time.time + offset, pressTime = Time.time, launchCount = ball.LaunchCount };
                return true;
            }

            Finish(FinalType(type), input.charge, result, timingError, contactPoint, Time.time + offset, Time.time, aim);
            return true;
        }

        // Nobody but the server may touch the tossed ball.
        bool TossIsNotMine => !ServeMode && ball != null && ball.LastHitType == HitType.Toss;

        // While serving, a Spike swing at the toss IS the serve: same reach and wind-up, but the serve maths decide where it goes.
        bool IsServeSwing(HitType type) => ServeMode && Tossed && type == HitType.Spike;
        HitType FinalType(HitType type) => IsServeSwing(type) ? HitType.Serve : type;
        HitResult ResolveHit(HitInput input) => IsServeSwing(input.type) ? ServeFrom(input, !grounded) : resolver.Resolve(input);

        // Applies the result: spends stamina, launches the ball on a connect (a Miss leaves it alone), and reports it.
        void Finish(HitType type, float charge, HitResult result, float timingError, Vector3 contactPoint, float idealTime, float pressTime, Vector3 aim)
        {
            if (type == HitType.Spike || type == HitType.Serve) stamina.SpendSpike(charge); else stamina.SpendHit(charge);
            if (!spikeCharge.IsCharging) spikeCharge.Cancel();   // the swing is spent (a wind-up already started for the next hit is left alone)
            if (result.contact)
            {
                cooldownLeft = hitCooldown;
                LastHitTime = Time.time;
                ball.Launch(ball.Position, result.target, result.flightTime, true, type);
                OnBallTouched?.Invoke(this);
            }

            var report = new HitReport
            {
                type = type, grade = result.grade, contact = result.contact, timingError = timingError,
                charge = charge, quality = result.quality, aimPoint = aim, target = result.target, flightTime = result.flightTime,
                apex = result.apex, speed = result.speed, momentum = result.momentumBonus, combo = result.comboMultiplier,
                contactPoint = contactPoint, idealTime = idealTime, pressTime = pressTime,
            };
            LastReport = report;
            Debug.Log($"[Hit] {type} contact={result.contact} grade={result.grade} err={timingError:F2} charge={charge:F2} quality={result.quality:F2} flight={result.flightTime:F2} target={result.target} p{PlayerId} apex={result.apex:F2} speed={result.speed:F1} mom={result.momentumBonus:F1} combo={result.comboMultiplier:F1} from=({ball.Position.x:F1},{ball.Position.y:F2},{ball.Position.z:F1}) touch={(TeamTouchCount != null ? TeamTouchCount() : -1)}");
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
            input.hitterVelocity = planarVelocity;     // the run-up speed at the moment of contact
            input.teamTouchIndex = TeamTouchCount != null ? TeamTouchCount() : 0;
            Finish(FinalType(pending.type), input.charge, ResolveHit(input), pending.timingError, pending.contactPoint, pending.idealTime, pending.pressTime, input.aimPoint);
        }

        // ---------------- facing ----------------

        // While blocking in the air the body turns square to the net.
        void FaceNet(float dt)
        {
            Quaternion want = Quaternion.LookRotation(new Vector3(0f, 0f, -courtSide), Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }

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
