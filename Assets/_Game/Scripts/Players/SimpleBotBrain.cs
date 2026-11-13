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
        public static BotHit ChooseHit(int teamTouches, bool ballHighNearNet, bool setOnSecondTouch = false)
        {
            // High, soft bumps now hang near the net at spike height, so the second touch must still be a set when asked to
            // (setOnSecondTouch), or the bot would spike its own teammate's pass and never set for the human.
            if (teamTouches == 1 && setOnSecondTouch) return BotHit.Set;
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

        /// <summary>
        /// Where an attack (spike, or a bump sent over) should land, by difficulty. 'oppSide' is the z sign of the opponents' half.
        /// Easy: deep middle. Normal: the spot farthest from the defender nearer to the ball's line. Hard: the most open spot (farthest
        /// from both defenders, so it picks line or cross by where they stand), and 'tip' makes it a short dink. Then the profile's aim
        /// spread is added and the result is clamped inside the court (inset from the lines). Uses UnityEngine.Random.
        /// </summary>
        public static Vector3 PickAttack(AIProfile p, float oppSide, Vector3 def1, Vector3 def2, float fromX,
            float halfWidth, float halfLength, float inset, bool tip)
        {
            float maxX = halfWidth - inset;
            Vector3 spot = new Vector3(0f, 0f, oppSide * halfLength * 0.7f);          // Easy: deep middle
            if (p.attackStyle != AIDifficulty.Easy)
            {
                // the defender nearer the ball's line is the one Normal avoids
                Vector3 near = Mathf.Abs(def1.x - fromX) <= Mathf.Abs(def2.x - fromX) ? def1 : def2;
                float[] depths = tip ? new[] { 2.2f } : new[] { halfLength * 0.55f, halfLength * 0.8f };
                float best = float.MinValue;
                for (int xi = -2; xi <= 2; xi++)
                    foreach (float d in depths)
                    {
                        var c = new Vector3(xi * maxX * 0.5f, 0f, oppSide * d);
                        float score = p.attackStyle == AIDifficulty.Normal
                            ? Flat(c - near)
                            : Mathf.Min(Flat(c - def1), Flat(c - def2));
                        score += Random.value * 0.3f;                                  // break ties differently each time
                        if (score > best) { best = score; spot = c; }
                    }
            }
            Vector2 spread = Random.insideUnitCircle * p.aimSpread;
            spot.x = Mathf.Clamp(spot.x + spread.x, -maxX, maxX);
            float z = Mathf.Clamp(Mathf.Abs(spot.z) + spread.y, tip ? 1.2f : inset, halfLength - inset);
            spot.z = oppSide * z;
            return spot;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>
        /// How long the bot takes to react to this ball. A spike it "read" (roll below the profile's spikeReadChance) gets the short
        /// readDelay; everything else gets the normal delay times this ball's jitter. A spike is in the air for only ~0.3-0.45 s, so
        /// the read chance decides how many spikes get dug, in a smooth, tunable way.
        /// </summary>
        public static float ReactionFor(AIProfile p, bool isSpike, float readRoll, float jitter)
        {
            return isSpike && readRoll < p.spikeReadChance ? p.readDelay : p.reactionDelay * jitter;
        }

        /// <summary>A ball the bot cannot reach on foot in time but could reach with a dive (distances flat, in metres).</summary>
        public static bool NeedsDive(float distToBall, float timeLeft, float runSpeed, float reach, float diveRange)
        {
            return distToBall > runSpeed * timeLeft + reach && distToBall <= diveRange + reach;
        }

        /// <summary>May this profile dive now? Hard always; Normal only if the dive does not burn it out; Easy never.</summary>
        public static bool DiveAllowed(AIProfile p, float stamina, float diveCost)
        {
            return p.diveMode == AIDiveMode.Always || (p.diveMode == AIDiveMode.IfStaminaAllows && stamina >= diveCost);
        }

        /// <summary>
        /// Seconds from now to press jump so the hands (hand height above the feet = handHeight) are at the ball's height when it
        /// crosses the shield in 'timeToCross' seconds. Negative = already late. The feet only rise as far as a tap jump goes.
        /// </summary>
        public static float BlockJumpIn(float timeToCross, float ballHeight, float handHeight, float tapApex, JumpArc arc)
        {
            float h = Mathf.Clamp(ballHeight - handHeight, 0.05f, tapApex);       // how high the feet must be
            return timeToCross - arc.TimeToHeight(tapApex, h);   // minus the time the feet need to rise that high
        }
    }

    /// <summary>
    /// The reaction delay: when the ball's arc changes (a new launch), Ready() stays false for 'delay' seconds, so the bot keeps
    /// acting on its old plan before it replans. Pure, so it is unit tested.
    /// </summary>
    public struct ReactionGate
    {
        int seen;
        float since;

        public bool Ready(int launchCount, float now, float delay)
        {
            if (launchCount != seen) { seen = launchCount; since = now; }
            return now - since >= delay;
        }

        public float Elapsed(float now) => now - since;
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
        [Tooltip("Toss charge (0..1) by level: Easy, Normal, Hard.")]
        [SerializeField] float[] tossChargeByLevel = { 0.2f, 0.6f, 0.8f };
        [SerializeField, Range(0f, 1f)] float normalJumpServeChance = 0.5f;   // Easy never jumps to serve, Hard always does

        // Difficulty numbers (reaction, mistakes, aim spread, charge, sprint, dive, block) come from AIProfile.Table.

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
                [SerializeField] float jumpMarginSeconds = 0.05f;
        [SerializeField] int targetTries = 8;
        [SerializeField] float spikeContactSlack = 0.35f;      // the bot checks a spike as if the ball were this much lower than planned...
        [SerializeField] float spikeNetSlack = 0.5f;         // ...and this much closer to the net (real contacts come a little low and near)
        [SerializeField] float spikeMinDepth = 3.5f;         // a spike lands at least this far past the net (a short one cannot clear it)
        [SerializeField] float spikeMinGoodSpeed = 14f;      // a spike the net rule had to slow to the floor is not worth taking: bump it over

        [Header("Dive / sprint / block")]
        [SerializeField] float diveLead = 0.3f;              // start the dive this long before the ball arrives
        [SerializeField] float sprintMinStamina = 12f;       // no sprint below this
        [SerializeField] float blockStandDistance = 0.7f;    // a blocker waits this far from the net

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
        readonly Btn jump = new Btn(), spike = new Btn(), bump = new Btn(), set = new Btn(), dive = new Btn();
        Vector2 move;
        Vector3 aim;
        bool sprint;
        AIDifficulty difficulty = AIDifficulty.Normal;
        AIProfile profile = AIProfile.For(AIDifficulty.Normal);
        ReactionGate gate;
        int jitterLaunch = -1;
        float reactionJitter = 1f;     // this ball's reaction delay is the profile's times 0.92..1.08
        float readRoll = 1f;           // this ball's dice roll for reading a spike early
        bool covering;                 // standing where the hitter aims (see CoverAttack)

        // block (the net-side bot of the defending team jumps at an incoming spike)
        bool blockMode, blockJumped, blockRoll;
        int blockRollLaunch = -99;

        // serve
        float lastServeStart = -1f, serveHoldUntil;
        bool serveHolding;
        Vector3 serveTarget;
        float serveCharge, serveTossCharge;
        bool serveJump, servePlan;     // this serve is a jump serve; the current plan is a serve plan (cleared when the rally starts)

        // the plan for the ball that is flying now
        int planLaunch = -1;
        bool planValid;
        HitType planType;
        bool planJump, jumped, pressed, released, dived;
        Vector3 planStand, planAim, planBall;
        float planContactAt, setPressLead, setCharge;
        bool setTracksHuman;     // a set for a human: the aim follows the human until the button is released

        public Vector2 Move => move;
        public Vector3 AimPoint => aim;
        public ButtonState Jump => jump.state;
        public ButtonState Spike => spike.state;
        public ButtonState Bump => bump.state;
        public ButtonState Set => set.state;
        public ButtonState Dive => dive.state;
        public bool Sprint => sprint;
        public AIDifficulty Difficulty => difficulty;
        public AIProfile Profile => profile;

        // A tap jump takes this long to reach the top; the bot times its spike so the hand meets the ball there (the hang gives it slack).
        float JumpRiseSeconds => me.Arc.TimeToApex(me.TapJumpHeight);

        void Awake() { me = GetComponent<PlayerController>(); }

        /// <summary>The MatchManager sets the level for all bots (at match start and whenever it changes).</summary>
        public void SetDifficulty(AIDifficulty d) { difficulty = d; profile = AIProfile.For(d); }

        /// <summary>Called by the MatchManager once everything exists.</summary>
        public void Setup(MatchManager m, PlayerController player, BallController b)
        {
            match = m; me = player; ball = b;
            preview = new HitResolver(player.Tuning);
        }

        void Update()
        {
            if (match == null || me == null || ball == null || match.Rules == null) return;

            if (jump.want) jump.want = false;          // a jump and a dive are one-frame taps
            if (dive.want) dive.want = false;
            sprint = false;
            switch (match.State)
            {
                case MatchState.Serve: ServeLogic(); break;
                case MatchState.Rally: RallyLogic(); break;
                default: GoHome(); ClearPlan(); blockMode = false; break;
            }
            move *= profile.moveSpeedMultiplier;       // Easy runs slower
            jump.Step(); spike.Step(); bump.Step(); set.Step(); dive.Step();
        }

        // ---------------- serve ----------------

        // A bot server waits, tosses, then walks under its own toss and hits it with the same spike plan/execute code as a rally spike.
        void ServeLogic()
        {
            if (!me.ServeMode) { ClearPlan(); GoHome(); serveHolding = false; return; }

            if (!Mathf.Approximately(lastServeStart, match.ServeStartTime))
            {
                // a new serve: pick a deep spot in the opponent's half, how high to toss, whether to jump, and how hard to hit
                lastServeStart = match.ServeStartTime;
                ClearPlan();
                float side = -me.CourtSide;
                serveTarget = new Vector3(Random.Range(-(match.HalfWidth - targetInset), match.HalfWidth - targetInset), 0f, side * Random.Range(match.HalfLength * 0.6f, match.HalfLength - targetInset));
                serveTossCharge = tossChargeByLevel[(int)difficulty];
                serveJump = difficulty == AIDifficulty.Hard || (difficulty == AIDifficulty.Normal && Random.value < normalJumpServeChance);
                serveCharge = difficulty == AIDifficulty.Easy ? profile.spikeChargeMin : Random.Range(profile.spikeChargeMin, profile.spikeChargeMax);
                serveHolding = false; spike.want = false;   // a restarted match must not leave the button held
            }
            aim = serveTarget;

            if (!me.Tossed)
            {
                move = Vector2.zero;
                if (!serveHolding && Time.time - match.ServeStartTime >= serveDelay)
                {
                    spike.want = true;
                    serveHolding = true;
                    serveHoldUntil = Time.time + serveTossCharge * me.TossCharge.secondsToFull;
                }
                else if (serveHolding && Time.time >= serveHoldUntil) spike.want = false;   // let go: the toss
                return;
            }

            if (!planValid || planLaunch != ball.LaunchCount) MakeServePlan();
            Execute();
        }

        // The plan for hitting my own toss: meet it on the way down (hand at the top of a tap jump for a jump serve), facing the target.
        void MakeServePlan()
        {
            planLaunch = ball.LaunchCount;
            planValid = servePlan = true;
            jumped = pressed = released = dived = false;
            float t = TimeAtHeight(serveJump ? spikeHandHeight : me.ContactOffset(HitType.Spike).y);
            if (float.IsNaN(t)) t = Mathf.Min(0.1f, ball.TimeToLanding);
            Vector3 contact = BallAt(t);
            Vector3 flatBall = new Vector3(contact.x, 0f, contact.z);
            Vector3 toTarget = serveTarget - flatBall; toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : new Vector3(0f, 0f, -me.CourtSide);
            planType = HitType.Spike;
            planJump = serveJump;
            planContactAt = Time.time + t;
            planBall = flatBall;
            planStand = flatBall - dir * me.ContactOffset(HitType.Spike).z;
            planAim = serveTarget;
            setTracksHuman = false;
            setPressLead = serveCharge * me.SpikeCharge.secondsToFull + releaseLead;   // hold long enough for the chosen charge
        }

        // ---------------- rally ----------------

        void RallyLogic()
        {
            if (servePlan) ClearPlan();    // the serve was hit: forget its plan
            if (!ball.IsInFlight) { GoHome(); ClearPlan(); blockMode = false; return; }
            if (ball.LaunchCount != jitterLaunch) { jitterLaunch = ball.LaunchCount; reactionJitter = Random.Range(0.92f, 1.08f); readRoll = Random.value; }   // people are not clockwork
            bool ready = gate.Ready(ball.LaunchCount, Time.time, BotPlanner.ReactionFor(profile, ball.LastHitType == HitType.Spike, readRoll, reactionJitter));   // false for the delay after a new hit
            if (BlockLogic(ready)) return;
            if (!ready) { if (planValid) Execute(); else if (covering) move = Vector2.zero; else GoHome(); return; }   // (a covering defender holds its spot)              // still reacting: carry on with the old plan
            if (planLaunch != ball.LaunchCount && planLaunch >= 0) ClearPlan();   // someone hit the ball: start over

            if (!pressed && !IsReceiver()) { covering = CoverAttack(); if (!covering) GoHome(); planValid = false; planLaunch = -1; return; }
            covering = false;
            if (!planValid) MakePlan();
            if (!planValid) { GoHome(); return; }
            Execute();
        }

        // Reading the hitter: while the opponents' set is in the air, the back defender (the one who is not at the net) who won this
        // ball's read roll drifts to where the hitter is aiming, so it is already close when the spike comes.
        bool CoverAttack()
        {
            PlayerController hitter = FindAttacker();
            if (hitter == null || IsBlocker() || readRoll >= profile.spikeReadChance) return false;
            Vector3 p = hitter.Brain.AimPoint;
            float maxX = match.HalfWidth - targetInset;
            Vector3 spot = new Vector3(Mathf.Clamp(p.x, -maxX, maxX), 0f, me.CourtSide * Mathf.Clamp(p.z * me.CourtSide, 2f, match.HalfLength - targetInset));
            GoHome();                      // buttons up, face the net...
            move = Toward(spot);           // ...but walk to the cover spot
            return true;
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
            if (!float.IsNaN(tSpike) && tSpike >= JumpRiseSeconds + jumpMarginSeconds + 0.03f)
            {
                Vector3 pos = BallAt(tSpike);
                spikeChance = BotPlanner.IsHighNearNet(pos.z, pos.y, spikeMaxNetDistance, spikeHandHeight - 0.2f) && (pos.z < 0f ? -1f : 1f) == side;
            }

            if (touches == 0) spikeChance = false;      // the first touch is a pass / dig, never a spike (real volleyball)

            // Hard sometimes dinks a spike chance short instead of jumping (no jump, a soft hit over the net).
            bool tip = spikeChance && Random.value < profile.tipChance;
            if (tip) spikeChance = false;

            BotHit choice = BotPlanner.ChooseHit(touches, spikeChance, true);
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
            float bumpCharge = Mathf.Clamp01((pressLead - releaseLead) / Mathf.Max(0.01f, me.SpikeCharge.secondsToFull));
            float estCharge = choice == BotHit.Over && spikeChance ? Random.Range(profile.spikeChargeMin, profile.spikeChargeMax) : bumpCharge;
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
                bool ok = type == HitType.Spike && PickOver(HitType.Spike, contact, flatBall, estCharge, AttackSpot(flatBall.x, false), out target);
                if (!ok)
                {
                    type = HitType.Bump;
                    useJump = false;
                    estCharge = bumpCharge;
                    float t = TimeAtHeight(me.ContactOffset(HitType.Bump).y);
                    contactTime = float.IsNaN(t) ? Mathf.Min(0.1f, ball.TimeToLanding) : t;
                    contact = BallAt(contactTime);
                    flatBall = new Vector3(contact.x, 0f, contact.z);
                    PickOver(HitType.Bump, contact, flatBall, estCharge, AttackSpot(flatBall.x, tip), out target);
                }
            }

            // Mistakes: now and then send it off target, so rallies end.
            if (Random.value < profile.mistakeChance)
                target += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * profile.mistakeMetres;

            if (type == HitType.Spike) setPressLead = estCharge * me.SpikeCharge.secondsToFull + releaseLead;   // hold long enough for the chosen charge
            planBall = flatBall;
            dived = false;
            planType = type;
            planJump = useJump;
            planContactAt = Time.time + contactTime;
            Vector3 toTarget = target - flatBall; toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : new Vector3(0f, 0f, -side);
            planStand = flatBall - dir * me.ContactOffset(type).z;
            planAim = preview.AimFor(type, estCharge, planStand, target);
        }

        // Where this bot's attack should land (the difficulty picks how smart the spot is).
        Vector3 AttackSpot(float fromX, bool tip)
        {
            Vector3 d1 = Vector3.zero, d2 = Vector3.zero;
            int n = 0;
            foreach (PlayerController p in match.Players)
                if (p.Team != me.Team) { if (n++ == 0) d1 = p.transform.position; else d2 = p.transform.position; }
            return BotPlanner.PickAttack(profile, -me.CourtSide, d1, d2, fromX, match.HalfWidth, match.HalfLength, targetInset, tip);
        }

        // Tries the preferred target first, then random ones in the opponent's half, 'targetInset' inside the lines; true if one clears the net.
        bool PickOver(HitType type, Vector3 contact, Vector3 from, float charge, Vector3 preferred, out Vector3 target)
        {
            float oppSide = -me.CourtSide;
            target = Vector3.zero;
            if (type == HitType.Spike)
            {
                // plan for a slightly low, slightly near contact so the shot clears even then
                contact.y -= spikeContactSlack;
                contact.z -= Mathf.Sign(contact.z) * Mathf.Min(spikeNetSlack, Mathf.Max(0f, Mathf.Abs(contact.z) - 0.6f));
            }
            for (int i = 0; i < targetTries; i++)
            {
                target = i == 0 ? preferred
                    : new Vector3(Random.Range(-(match.HalfWidth - targetInset), match.HalfWidth - targetInset), 0f,
                                  oppSide * Random.Range(type == HitType.Spike ? spikeMinDepth : targetInset, match.HalfLength - targetInset));
                var input = new HitInput
                {
                    type = type, charge = charge, timingError = 0f, closestDistance = 0f, reachRadius = me.ReachRadius,
                    contactHeight = contact.y, aimPoint = preview.AimFor(type, charge, from, target), hitterPosition = from, ballPosition = contact,
                    ballGravity = ball.Gravity, teamTouchIndex = match.Rules.TouchesFor(me.Team),
                };
                // The resolver already pushes / slows a spike until it clears the net; the bot checks the final shot
                // (half the resolver's margin) and that it still lands inside the court.
                HitResult r = preview.Resolve(input);
                if (Mathf.Abs(r.target.x) > match.HalfWidth || Mathf.Abs(r.target.z) > match.HalfLength) continue;
                if (type == HitType.Spike && r.speed < spikeMinGoodSpeed) continue;
                Vector3 centre = new Vector3(r.target.x, ball.Radius, r.target.z);
                Vector3 v0 = BallTrajectory.SolveLaunchVelocity(contact, centre, r.flightTime, ball.Gravity);
                if (BallTrajectory.ClearsNet(contact, v0, r.flightTime, BallTrajectory.NetTopHeight + me.Tuning.spikeNetClearance * 0.5f, ball.Radius, 0f, ball.Gravity))
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

            if (planJump && !jumped && remaining <= JumpRiseSeconds + jumpMarginSeconds) { jump.want = true; jumped = true; }

            Btn button = planType == HitType.Spike ? spike : planType == HitType.Set ? set : bump;
            float lead = planType == HitType.Bump ? pressLead : setPressLead;
            if (!pressed && remaining <= lead) { button.want = true; pressed = true; }
            if (pressed && !released && remaining <= releaseLead) { button.want = false; released = true; }

            // Sprint to a far spot / spike approach (Normal and Hard).
            Vector3 toStand = planStand - me.transform.position; toStand.y = 0f;
            float dist = toStand.magnitude;
            bool late = dist / me.MoveSpeed * 1.1f > remaining - 0.1f;
            sprint = profile.usesSprint && me.Stamina.Current > sprintMinStamina && dist > 1.2f && (late || planJump);

            // Dive for a ball that can't be reached on foot (the dig takes over inside the dive).
            Vector3 toBall = planBall - me.transform.position; toBall.y = 0f;
            if (!dived && me.IsGrounded && !me.ServeMode && !planJump && !me.IsDiving && remaining > 0f && remaining <= diveLead
                && BotPlanner.DiveAllowed(profile, me.Stamina.Current, me.Stamina.diveCost)
                && BotPlanner.NeedsDive(toBall.magnitude, remaining, me.MoveSpeed * (sprint ? me.SprintMultiplier : 1f), me.ReachRadius, me.DiveDistance))
            {
                dived = true;
                dive.want = true;
                move = new Vector2(toBall.x, toBall.z).normalized;       // the dive goes where the move input points
                button.want = false; pressed = released = true;
            }
        }

        // ---------------- blocking ----------------

        // The opponent about to attack: once their team's ball has been set (2+ touches, last hit a set or bump),
        // the attacker is the opponent nearest where it will come down, other than the one who just touched it.
        PlayerController FindAttacker()
        {
            if (ball.LastHitType != HitType.Set && ball.LastHitType != HitType.Bump) return null;
            RulesEngine rules = match.Rules;
            if (rules.PossessionTeam == me.Team || rules.PossessionTeam < 0 || rules.Touches < 2) return null;
            PlayerController best = null;
            float bestD = float.MaxValue;
            foreach (PlayerController o in match.Players)
            {
                if (o.Team == me.Team || o.PlayerId == rules.LastToucherId) continue;
                float d = Mathf.Abs(o.transform.position.x - ball.LandingPoint.x) + Mathf.Abs(o.transform.position.z - ball.LandingPoint.z);
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        // The net-side bot of the team blocks: the one nearer the net (ties: the lower id). A human teammate nearer the net blocks for themselves.
        bool IsBlocker()
        {
            PlayerController mate = match.Teammate(me);
            if (mate == null) return true;
            float mine = Mathf.Abs(me.transform.position.z), his = Mathf.Abs(mate.transform.position.z);
            return mine < his - 0.01f || (Mathf.Abs(mine - his) <= 0.01f && me.PlayerId < mate.PlayerId);
        }

        // Block when an opponent is about to spike (or already has): go to where the ball will cross the net, jump so the
        // hands are up as it crosses, and slide the hands toward the crossing point. Returns true while busy blocking.
        bool BlockLogic(bool ready)
        {
            if (profile.blockChance <= 0f) return false;
            float side = me.CourtSide;
            bool onTheirSide = ball.Position.z * side < 0f;
            bool incomingAttack = onTheirSide && ball.Velocity.z * side > 0f && ball.LastHitType == HitType.Spike
                                  && match.Rules.PossessionTeam != me.Team;
            PlayerController attacker = FindAttacker();
            if (!onTheirSide || (attacker == null && !incomingAttack)) { blockMode = false; return false; }
            if (!blockMode)
            {
                if (!IsBlocker()) return false;
                if (ball.LaunchCount - blockRollLaunch > 1)      // one dice roll per attack (the set and the spike share it)
                {
                    blockRollLaunch = ball.LaunchCount;
                    blockRoll = Random.value < profile.blockChance;
                }
                if (!blockRoll) return false;
                blockMode = true; blockJumped = false;
                Debug.Log($"[BlockPlan] p{me.PlayerId} starts blocking (attacker p{(attacker != null ? attacker.PlayerId : -1)})");
            }

            // Where will the ball cross my shield plane? Exact once the spike is flying (after the reaction delay), else a guess from the attacker's aim.
            float planeZ = side * (blockStandDistance - me.Block.forward);
            float crossX, crossY = 0f, tCross = -1f;
            if (incomingAttack && ready)
            {
                tCross = (planeZ - ball.Position.z) / ball.Velocity.z;
                Vector3 c = BallAt(Mathf.Max(0f, tCross));
                crossX = c.x; crossY = c.y;
            }
            else
            {
                PlayerController a = attacker;
                if (a == null) crossX = me.transform.position.x;     // reacting: hold position
                else
                {
                    float k = Mathf.Abs(a.transform.position.z) / (Mathf.Abs(a.transform.position.z) + Mathf.Abs(a.Brain.AimPoint.z) + 0.01f);
                    crossX = Mathf.Lerp(a.transform.position.x, a.Brain.AimPoint.x, k);   // the line from the attacker to their aim, at the net
                }
            }
            crossX = Mathf.Clamp(crossX, -match.HalfWidth, match.HalfWidth);

            Vector3 stand = new Vector3(crossX, 0f, side * blockStandDistance);
            aim = new Vector3(crossX, 0f, -side * 4f);                      // face the net
            if (me.IsGrounded)
            {
                move = Toward(stand);
                Vector3 d = stand - me.transform.position; d.y = 0f;
                sprint = profile.usesSprint && me.Stamina.Current > sprintMinStamina && d.magnitude > 1.5f;
                // A spike crosses the net ~0.07 s after contact, and the shield (2.45 m above the feet) only meets a ball at ~2.6 m while the
                // feet are low, so the jump starts right when the set ball reaches the attacker's hand height (the moment of contact).
                float tContact = TimeAtHeight(spikeHandHeight - 0.2f);   // a moment after the attacker meets the ball (the hands start low)
                if (!blockJumped && attacker != null && !float.IsNaN(tContact) && tContact <= 0.03f && d.magnitude < 1.2f)
                { jump.want = true; blockJumped = true; Debug.Log($"[BlockPlan] p{me.PlayerId} jumps at x={me.transform.position.x:F1} z={me.transform.position.z:F1}"); }
                else if (tCross > 0f && !blockJumped)
                {
                    float jumpIn = BotPlanner.BlockJumpIn(tCross, crossY, me.Block.handHeight, me.TapJumpHeight, me.Arc);
                    if (jumpIn <= 0.02f) { jump.want = true; blockJumped = true; }
                }
            }
            else move = new Vector2(Mathf.Clamp((crossX - me.transform.position.x) / me.Block.strafe, -1f, 1f), 0f);   // strafe the hands
            return true;
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
            servePlan = false;
            planValid = false;
            planLaunch = -1;
            pressed = released = jumped = dived = false;
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
