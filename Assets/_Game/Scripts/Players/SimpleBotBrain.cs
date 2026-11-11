using Sandspike.Ball;
using Sandspike.Core;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>What a bot decides to do with the ball.</summary>
    public enum BotHit { Bump, Set, Over }

    /// <summary>The bot's pure decisions (no scene objects), so they are easy to unit test.</summary>
    public static class BotPlanner
    {
        /// <summary>
        /// Which kind of hit by how many touches this team has used: 0 = bump to the teammate, 1 = set,
        /// 2 = send it over. A high ball near the net is sent over at once, whatever the count.
        /// </summary>
        public static BotHit ChooseHit(int teamTouches, bool ballHighNearNet)
        {
            if (teamTouches >= 2 || ballHighNearNet) return BotHit.Over;
            return teamTouches == 0 ? BotHit.Bump : BotHit.Set;
        }

        /// <summary>Is the ball close enough to the net, and high enough, for a jump spike?</summary>
        public static bool IsHighNearNet(float ballZ, float ballHeight, float maxNetDistance, float minHeight)
        {
            return Mathf.Abs(ballZ) <= maxNetDistance && ballHeight >= minHeight;
        }

        /// <summary>
        /// Where a set goes and how hard to charge it. For a human teammate: full charge, straight to the human's x
        /// (kept 'inset' inside the court), 'humanDistance' from the net. For a bot teammate: the old charge and target.
        /// </summary>
        public static void PlanSet(bool mateIsHuman, float mateX, float halfWidth, float inset, float side,
            float botCharge, float botDistance, float humanCharge, float humanDistance, out float charge, out Vector3 target)
        {
            float x = Mathf.Clamp(mateX, -(halfWidth - inset), halfWidth - inset);
            charge = mateIsHuman ? humanCharge : botCharge;
            target = new Vector3(x, 0f, side * (mateIsHuman ? humanDistance : botDistance));
        }
    }

    /// <summary>
    /// A small bot brain (the seed of M3's AIBrain). It presses the same virtual buttons a human does:
    /// it predicts where the ball lands, the closest player of the team runs to meet it, and it bumps, sets or
    /// sends the ball over depending on how many touches the team has used. Every number is a tunable.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // run before PlayerController, so it reads this frame's buttons
    public class SimpleBotBrain : MonoBehaviour, IPlayerBrain
    {
        [Header("Timing (seconds before the ball reaches the contact point)")]
        [SerializeField] float pressLead = 0.35f;      // press the hit button this early (the arm winds up)
        [SerializeField] float releaseLead = 0.08f;    // let go this early (the hit fires on release)
        [SerializeField] float serveDelay = 1.2f;      // a bot server waits this long
        [SerializeField] Vector2 serveChargeRange = new Vector2(0.4f, 0.9f);

        [Header("Mistakes")]
        [Range(0f, 1f)] [SerializeField] float errorChance = 0.1f;
        [SerializeField] float lateReleaseSeconds = 0.25f;   // a "late" mistake releases this much too late
        [SerializeField] float aimErrorMetres = 1.5f;        // an "aim" mistake lands this far from the target

        [Header("Targets (metres)")]
        [SerializeField] float targetInset = 1f;        // over-the-net targets stay this far inside the lines
        [SerializeField] float bumpDistanceFromNet = 2f;
        [SerializeField] float setDistanceFromNet = 1.5f;
        [SerializeField] float setDistanceFromNetForHuman = 2f;   // a set for a human lands this far from the net, at the human's x
        [SerializeField, Range(0f, 1f)] float setChargeForHuman = 1f;   // sets for a human are full charge (highest, longest)
        [SerializeField] float ignoreOutBy = 0.3f;      // a ball landing farther out than this is left alone

        [Header("Spike")]
        [SerializeField] float spikeHandHeight = 3.2f;       // hand height at the top of a tap jump (2.3 + 0.9)
        [SerializeField] float spikeMaxNetDistance = 3.5f;   // only spike a ball this close to the net
        [SerializeField] float jumpRiseSeconds = 0.27f;      // a tap jump takes this long to reach the top
        [SerializeField] float jumpMarginSeconds = 0.05f;
        [SerializeField] float netClearMargin = 0.15f;       // the bot checks its own shot clears the net by this much
        [SerializeField] int targetTries = 8;

        // A button the brain "holds": Step() turns the held flag into this frame's Down / Held / Up.
        class Btn
        {
            public bool want, held;
            public ButtonState state;
            public void Step() { state = new ButtonState(want && !held, want, !want && held); held = want; }
        }

        PlayerController me;
        BallController ball;
        MatchManager match;
        HitResolver preview;       // the same maths the real hit will use, to check a shot before taking it
        readonly Btn jump = new Btn(), spike = new Btn(), bump = new Btn(), set = new Btn();
        Vector2 move;
        Vector3 aim;

        // serve
        float lastServeStart = -1f, serveHoldUntil;
        bool serveHolding;
        Vector3 serveTarget;
        float serveCharge;

        // the plan for the ball that is flying now
        int planLaunch = -1;
        bool planValid;
        HitType planType;
        bool planJump, jumped, pressed, released, lateRelease;
        Vector3 planStand, planAim;
        float planContactAt, setPressLead, setCharge;
        bool setTracksHuman;     // a set for a human: the aim follows the human until the button is released

        public Vector2 Move => move;
        public Vector3 AimPoint => aim;
        public ButtonState Jump => jump.state;
        public ButtonState Spike => spike.state;
        public ButtonState Bump => bump.state;
        public ButtonState Set => set.state;
        public ButtonState Dive => default;      // bots do not dive in M2

        void Awake() { me = GetComponent<PlayerController>(); }

        /// <summary>Called by the MatchManager once everything exists.</summary>
        public void Setup(MatchManager m, PlayerController player, BallController b)
        {
            match = m; me = player; ball = b;
            preview = new HitResolver(player.Tuning);
        }

        void Update()
        {
            if (match == null || me == null || ball == null || match.Rules == null) return;

            if (jump.want) jump.want = false;          // a jump is a one-frame tap
            switch (match.State)
            {
                case MatchState.Serve: ServeLogic(); break;
                case MatchState.Rally: RallyLogic(); break;
                default: GoHome(); ClearPlan(); break;
            }
            jump.Step(); spike.Step(); bump.Step(); set.Step();
        }

        // ---------------- serve ----------------

        void ServeLogic()
        {
            ClearPlan();
            if (!me.ServeMode) { GoHome(); serveHolding = false; return; }

            move = Vector2.zero;
            if (!Mathf.Approximately(lastServeStart, match.ServeStartTime))
            {
                // a new serve: pick a deep spot in the opponent's half and how hard to hit it
                lastServeStart = match.ServeStartTime;
                float side = -me.CourtSide;
                serveTarget = new Vector3(Random.Range(-(match.HalfWidth - targetInset), match.HalfWidth - targetInset), 0f, side * Random.Range(match.HalfLength * 0.6f, match.HalfLength - targetInset));
                serveCharge = Random.Range(serveChargeRange.x, serveChargeRange.y);
                serveHolding = false;
            }
            aim = serveTarget;

            if (!serveHolding && Time.time - match.ServeStartTime >= serveDelay)
            {
                spike.want = true;
                serveHolding = true;
                serveHoldUntil = Time.time + serveCharge * me.SpikeCharge.secondsToFull;
            }
            else if (serveHolding && Time.time >= serveHoldUntil) spike.want = false;
        }

        // ---------------- rally ----------------

        void RallyLogic()
        {
            if (!ball.IsInFlight) { GoHome(); ClearPlan(); return; }
            if (planLaunch != ball.LaunchCount && planLaunch >= 0) ClearPlan();   // someone hit the ball: start over

            if (!pressed && !IsReceiver()) { GoHome(); planValid = false; planLaunch = -1; return; }
            if (!planValid) MakePlan();
            if (!planValid) { GoHome(); return; }
            Execute();
        }

        // I am the receiver if the ball lands on my side (and in play), I may touch it, and I am the closest player on my team who may.
        bool IsReceiver()
        {
            RulesEngine rules = match.Rules;
            Vector3 land = ball.LandingPoint;
            if ((land.z < 0f ? -1f : 1f) != me.CourtSide) return false;
            if (Mathf.Abs(land.x) > match.HalfWidth + ignoreOutBy || Mathf.Abs(land.z) > match.HalfLength + ignoreOutBy) return false;

            int touches = rules.TouchesFor(me.Team);
            if (touches >= rules.maxTouches) return false;

            float best = float.MaxValue;
            int bestId = -1;
            foreach (PlayerController p in match.Players)
            {
                if (p.Team != me.Team) continue;
                if (touches > 0 && rules.LastToucherId == p.PlayerId) continue;   // not twice in a row
                Vector3 d = p.transform.position - land;
                d.y = 0f;
                if (d.magnitude < best - 0.001f) { best = d.magnitude; bestId = p.PlayerId; }
            }
            return bestId == me.PlayerId;
        }

        // Time (seconds from now) when the ball, on its way down, is at 'height'. NaN if it never gets there.
        float TimeAtHeight(float height)
        {
            float t = BallTrajectory.TimeToReachHeight(ball.Position.y, ball.Velocity.y, height, true, ball.Gravity);
            return (float.IsNaN(t) || t < 0f || t > ball.TimeToLanding) ? float.NaN : t;
        }

        Vector3 BallAt(float t) => BallTrajectory.PositionAt(ball.Position, ball.Velocity, t, ball.Gravity);

        // Decides what to do with this ball: which hit, where to stand, when to hit, where to send it. Done once per ball.
        void MakePlan()
        {
            planLaunch = ball.LaunchCount;
            planValid = true;
            jumped = pressed = released = false;
            int touches = match.Rules.TouchesFor(me.Team);
            float side = me.CourtSide;
            PlayerController mate = match.Teammate(me);
            float mateX = mate != null ? Mathf.Clamp(mate.transform.position.x, -(match.HalfWidth - targetInset), match.HalfWidth - targetInset) : 0f;

            // Is there a good jump-spike chance (ball high and near the net)?
            float tSpike = TimeAtHeight(spikeHandHeight);
            bool spikeChance = false;
            if (!float.IsNaN(tSpike) && tSpike >= jumpRiseSeconds + jumpMarginSeconds + 0.03f)
            {
                Vector3 pos = BallAt(tSpike);
                spikeChance = BotPlanner.IsHighNearNet(pos.z, pos.y, spikeMaxNetDistance, spikeHandHeight - 0.2f) && (pos.z < 0f ? -1f : 1f) == side;
            }

            BotHit choice = BotPlanner.ChooseHit(touches, spikeChance);
            HitType type = choice == BotHit.Set ? HitType.Set : HitType.Bump;
            Vector3 target = Vector3.zero;
            float contactTime;
            bool useJump = false;

            if (choice == BotHit.Over && spikeChance) { type = HitType.Spike; useJump = true; contactTime = tSpike; }
            else
            {
                float h = me.ContactOffset(type).y;
                float t = TimeAtHeight(h);
                contactTime = float.IsNaN(t) ? Mathf.Min(0.1f, ball.TimeToLanding) : t;
            }
            Vector3 contact = BallAt(contactTime);
            Vector3 flatBall = new Vector3(contact.x, 0f, contact.z);
            float estCharge = Mathf.Clamp01((pressLead - releaseLead) / Mathf.Max(0.01f, me.SpikeCharge.secondsToFull));
            setPressLead = pressLead;
            setTracksHuman = false;

            if (choice == BotHit.Bump) target = new Vector3(mateX, 0f, side * bumpDistanceFromNet);
            else if (choice == BotHit.Set)
            {
                // A set for a human teammate is full charge and lands near the human; the bot holds the button long enough.
                bool mateIsHuman = mate != null && mate.Brain is HumanBrain;
                float mx = mate != null ? mate.transform.position.x : 0f;
                BotPlanner.PlanSet(mateIsHuman, mx, match.HalfWidth, targetInset, side, estCharge, setDistanceFromNet,
                    setChargeForHuman, setDistanceFromNetForHuman, out estCharge, out target);
                setTracksHuman = mateIsHuman;
                setCharge = estCharge;
                setPressLead = mateIsHuman ?estCharge * me.SpikeCharge.secondsToFull + releaseLead : pressLead;
            }
            else
            {
                // Over the net: try random spots in the opponent's half until the shot clears the net (spike first, then a bump over).
                bool ok = type == HitType.Spike && PickOver(HitType.Spike, contact, flatBall, estCharge, out target);
                if (!ok)
                {
                    type = HitType.Bump;
                    useJump = false;
                    float t = TimeAtHeight(me.ContactOffset(HitType.Bump).y);
                    contactTime = float.IsNaN(t) ? Mathf.Min(0.1f, ball.TimeToLanding) : t;
                    contact = BallAt(contactTime);
                    flatBall = new Vector3(contact.x, 0f, contact.z);
                    PickOver(HitType.Bump, contact, flatBall, estCharge, out target);
                }
            }

            // Mistakes: now and then release late or send it a little off target, so rallies end.
            lateRelease = false;
            if (Random.value < errorChance)
            {
                if (Random.value < 0.5f) lateRelease = true;
                else target += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * aimErrorMetres;
            }

            planType = type;
            planJump = useJump;
            planContactAt = Time.time + contactTime;
            Vector3 toTarget = target - flatBall; toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : new Vector3(0f, 0f, -side);
            planStand = flatBall - dir * me.ContactOffset(type).z;
            planAim = preview.AimFor(type, estCharge, planStand, target);
        }

        // Tries random targets in the opponent's half, 'targetInset' inside the lines; true if one clears the net.
        bool PickOver(HitType type, Vector3 contact, Vector3 from, float charge, out Vector3 target)
        {
            float oppSide = -me.CourtSide;
            target = Vector3.zero;
            for (int i = 0; i < targetTries; i++)
            {
                target = new Vector3(Random.Range(-(match.HalfWidth - targetInset), match.HalfWidth - targetInset), 0f,
                                     oppSide * Random.Range(targetInset, match.HalfLength - targetInset));
                var input = new HitInput
                {
                    type = type, charge = charge, timingError = 0f, closestDistance = 0f, reachRadius = me.ReachRadius,
                    contactHeight = contact.y, aimPoint = preview.AimFor(type, charge, from, target), hitterPosition = from, ballPosition = contact,
                };
                HitResult r = preview.Resolve(input);
                Vector3 centre = new Vector3(r.target.x, ball.Radius, r.target.z);
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(contact, centre, r.flightTime, ball.Gravity);
                if (BallTrajectory.ClearsNet(contact, v0, r.flightTime, BallTrajectory.NetTopHeight + netClearMargin, ball.Radius, 0f, ball.Gravity))
                    return true;
            }
            return false;
        }

        // Walk to the stand spot, jump (spikes), press the hit button early and let go just before the ball arrives.
        void Execute()
        {
            float remaining = planContactAt - Time.time;
            move = Toward(planStand);
            if (setTracksHuman && !released)
            {
                // follow the human: re-aim at their current x every frame until the set is released
                PlayerController mate = match.Teammate(me);
                BotPlanner.PlanSet(true, mate.transform.position.x, match.HalfWidth, targetInset, me.CourtSide, setCharge,
                    setDistanceFromNet, setChargeForHuman, setDistanceFromNetForHuman, out _, out Vector3 t);
                planAim = preview.AimFor(HitType.Set, setCharge, planStand, t);
            }
            aim = planAim;

            if (planJump && !jumped && remaining <= jumpRiseSeconds + jumpMarginSeconds) { jump.want = true; jumped = true; }

            Btn button = planType == HitType.Spike ? spike : planType == HitType.Set ? set : bump;
            float lead = planType == HitType.Set ? setPressLead : pressLead;
            if (!pressed && remaining <= lead) { button.want = true; pressed = true; }
            float releaseAt = lateRelease ? releaseLead - lateReleaseSeconds : releaseLead;
            if (pressed && !released && remaining <= releaseAt) { button.want = false; released = true; }
        }

        // ---------------- helpers ----------------

        void GoHome()
        {
            move = Toward(match.BasePosition(me));
            aim = new Vector3(0f, 0f, -me.CourtSide * 4f);   // face the other side
            if (match.State != MatchState.Serve) { spike.want = bump.want = set.want = false; }
        }

        void ClearPlan()
        {
            planValid = false;
            planLaunch = -1;
            pressed = released = jumped = false;
            if (match.State != MatchState.Serve || !me.ServeMode) spike.want = false;
            bump.want = set.want = false;
        }

        // A walking vector (world x, z) toward a spot; slows down when close so the bot does not jitter.
        Vector2 Toward(Vector3 spot)
        {
            Vector3 d = spot - me.transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.08f) return Vector2.zero;
            Vector3 n = d / dist * Mathf.Clamp01(dist / 0.4f);
            return new Vector2(n.x, n.z);
        }
    }
}
