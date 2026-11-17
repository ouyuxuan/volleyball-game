using System;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>Setup = the join screen: players stand at their bases and nothing happens until a human starts the match.</summary>
    public enum MatchState { Serve, Rally, PointScored, MatchOver, Setup }

    /// <summary>
    /// Runs the match: Serve -> Rally -> PointScored -> (Serve again | MatchOver).
    /// It places the players, holds the ball for the server, feeds hits and landings to the RulesEngine,
    /// and shows nothing itself: the Hud reads State, Rules and the point info from here.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] BallController ball;
        [Tooltip("Slots 0, 1 = team A, 2, 3 = team B. Any slot can be a human (LocalPlayers decides); the rest are bots.")]
        [SerializeField] PlayerController[] players = new PlayerController[4];

        [Header("Match")]
        [Tooltip("First to this many points (11, 21 or 25), and you must win by 2.")]
        [SerializeField] int targetScore = 11;
        [SerializeField] int winBy = 2;
        [Tooltip("On: the first to the cap (11 -> 15, 21 -> 25, 25 -> 30) wins even without a 2 point lead.")]
        [SerializeField] bool pointCap;
        [SerializeField] int firstServingTeam = 0;
        [Tooltip("1 = a single set, 2 = best of 3 (first to 2 sets).")]
        [SerializeField] int setsToWin = 1;

        [Header("Court (metres)")]
        [SerializeField] float halfWidth = 4f;
        [SerializeField] float halfLength = 8f;
        [SerializeField] float baseX = 2f;            // base positions: x = -2 / +2 ...
        [SerializeField] float baseDepth = 4f;        // ... and 4 m from the net
        [SerializeField] float serveDepth = 9f;       // the server stands this far from the net (behind the baseline)

        [Header("Timing")]
        [SerializeField] float pointPauseSeconds = 1.5f;
        [SerializeField] float setBannerSeconds = 2f;    // the "Set 1: Team A" banner between sets
        [SerializeField] float practiceFeedSeconds = 3f;   // practice: a new ball this long after the last one is dead
        [SerializeField] float serveBallLift = 0.15f; // the held ball floats this far above the server's hand
        [SerializeField] float serveClockSeconds = 15f; // the server must toss within this long, or it is a serve fault

        [Header("AI")]
        [Tooltip("How good all three bots are. Changing it (even during play) updates every bot.")]
        public AIDifficulty difficulty = AIDifficulty.Normal;

        [Header("Debug")]
        [Tooltip("Swaps player 1's brain for a SimpleBotBrain, so a whole match can be watched.")]
        public bool autoPlayHuman;

        public RulesEngine Rules { get; private set; }
        /// <summary>The sets of this match (one set unless Best of 3 is on). Rules is the set being played.</summary>
        public MatchSets Sets { get; private set; }
        public MatchState State { get; private set; }
        public PlayerController[] Players => players;
        /// <summary>The human brain of each slot, or null for a bot. LocalPlayers fills it, then calls ApplyBrains.</summary>
        public HumanBrain[] Humans { get; } = new HumanBrain[4];
        public bool IsHumanSlot(int slot) => Humans[slot] != null;
        public int HumanCount { get { int n = 0; foreach (var h in Humans) if (h != null) n++; return n; } }
        /// <summary>Asked at match over: true when somebody pressed "play again" (LocalPlayers: any human's Jump).</summary>
        public Func<bool> PlayAgain;
        public float ServeStartTime { get; private set; }
        /// <summary>Seconds the server has left to toss (stops counting at the toss).</summary>
        public float ServeClockLeft { get; private set; }
        public float HalfWidth => halfWidth;
        public float HalfLength => halfLength;
        public int TargetScore => targetScore;
        public bool LastPointViaNet { get; private set; }
        public int PointCount { get; private set; }     // counts every point, so the HUD can tell a new one happened
        public PlayerController Server => players[Rules.ServerId];

        /// <summary>Raised when a point is scored: winning team, reason, true if the ball touched the net first.</summary>
        public event Action<int, PointReason, bool> OnPoint;

        float stateTimer;
        bool netTouched;
        bool autoPlayApplied;
        AIDifficulty difficultyApplied = (AIDifficulty)(-1);
        readonly SimpleBotBrain[] bots = new SimpleBotBrain[4];
        readonly AIDifficulty?[] slotDifficulty = new AIDifficulty?[4];   // per-bot overrides of 'difficulty' (null = follow it)

        void Start()
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].PlayerId = i;
                players[i].OnBallTouched += OnTouched;
                players[i].OnBlocked += OnBlocked;
                PlayerController owner = players[i];
                owner.TeamTouchCount = () => Rules != null ? Rules.TouchesFor(owner.Team) : 0;   // touches before this one
                owner.LastTouchTeam = () => Rules != null ? Rules.LastTouchTeam : -1;            // who has the ball (only the other team's ball is blocked)
                var bot = players[i].GetComponent<SimpleBotBrain>();
                if (bot != null) { bot.Setup(this, players[i], ball); bots[i] = bot; }
            }
            ball.OnGroundContact += OnLanded;
            ball.OnNetTouch += _ => netTouched = true;

            NewMatch();
            if (LocalPlayers.Active) State = MatchState.Setup;     // wait on the join screen until a human starts the match
            ApplyAutoPlay();
            ApplyDifficulty();
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundContact -= OnLanded;
            foreach (var p in players) if (p != null) { p.OnBallTouched -= OnTouched; p.OnBlocked -= OnBlocked; }
        }

        /// <summary>Match options from the setup screen; they take effect at the next NewMatch.</summary>
        public void SetOptions(int target, bool cap, int setsToWin = 1) { targetScore = target; pointCap = cap; this.setsToWin = setsToWin; }

        /// <summary>Scores back to 0-0 and starts the first serve.</summary>
        public void NewMatch()
        {
            Sets = new MatchSets(setsToWin, firstServingTeam);
            StartSet();
            PointCount = 0;
            BeginServe();
        }

        // A fresh score for the next set: same options, and the team that lost the last set serves first.
        void StartSet() => Rules = new RulesEngine(targetScore, winBy, 3, halfWidth, halfLength, ball.Radius, Sets.NextServingTeam, pointCap);

        /// <summary>Back to 0-0 with everyone at their bases, but waiting for a human to start (the join screen).</summary>
        public void EnterSetup()
        {
            NewMatch();
            State = MatchState.Setup;
            foreach (var p in players) p.ServeMode = false;
        }

        void Update()
        {
            if (autoPlayHuman != autoPlayApplied) ApplyAutoPlay();
            if (difficulty != difficultyApplied) ApplyDifficulty();

            switch (State)
            {
                case MatchState.Serve:
                    HoldBall();
                    if (!Practice && !Server.Tossed)       // the serve clock runs until the toss (not in practice)
                    {
                        ServeClockLeft = Mathf.Max(0f, ServeClockLeft - Time.deltaTime);
                        if (ServeClockLeft <= 0f) { Rules.ServeFault(); EndPoint(); }
                    }
                    break;
                case MatchState.PointScored:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        if (Practice) BeginPracticeBall();
                        else if (!Rules.IsMatchOver) BeginServe();
                        else if (Sets.IsOver) State = MatchState.MatchOver;
                        else { StartSet(); BeginServe(); }     // the set ended but not the match: 0-0 again
                    }
                    break;
                case MatchState.MatchOver:
                    // "Press Jump to play again": any human, same line-up
                    if (PlayAgain != null && PlayAgain()) NewMatch();
                    break;
            }
        }

        // ---------------- practice ----------------
        // A solo court: the one human, no bots, no score, no serve clock. State runs as usual (a fed ball is a Rally; a dead ball is PointScored, which only
        // waits for the next ball). The Serve drill uses the normal Serve state and the toss serve.

        public bool Practice { get; private set; }
        public Drill PracticeDrill { get; private set; }

        /// <summary>Starts practice (LocalPlayers has already built the one human's view): the other three players are put away.</summary>
        public void StartPractice(Drill drill)
        {
            Practice = true;
            Sets = new MatchSets(1, 0);
            for (int i = 0; i < players.Length; i++) if (Humans[i] == null) players[i].gameObject.SetActive(false);
            SetDrill(drill);
        }

        /// <summary>Practice is over (back to Title): everyone is brought back.</summary>
        public void EndPractice()
        {
            Practice = false;
            foreach (var p in players) p.gameObject.SetActive(true);
        }

        /// <summary>Switches drill: the player goes to the drill's spot and the first ball comes after a moment.</summary>
        public void SetDrill(Drill drill)
        {
            PracticeDrill = drill;
            Rules = new RulesEngine(targetScore, winBy, 3, halfWidth, halfLength, ball.Radius, 0, false);
            foreach (var p in players) { p.ResetState(); p.ServeMode = false; }
            if (!PracticeDrills.Feeds(drill)) { BeginServe(); return; }
            for (int i = 0; i < players.Length; i++)
            {
                if (Humans[i] == null) continue;
                players[i].TeleportTo(PracticeDrills.StartSpot(drill));
                players[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // face the net
            }
            ball.PlaceAt(new Vector3(0f, ball.Radius, 6f));
            State = MatchState.PointScored;
            stateTimer = 1.5f;
        }

        // The next practice ball: a fresh (scoreless) set of rules, then the serve toss or a fed ball aimed by where the player stands now.
        void BeginPracticeBall()
        {
            Rules = new RulesEngine(targetScore, winBy, 3, halfWidth, halfLength, ball.Radius, 0, false);
            if (!PracticeDrills.Feeds(PracticeDrill)) { BeginServe(); return; }
            Rules.Serve(1);                          // the far side "hit" it
            State = MatchState.Rally;
            netTouched = false;
            Vector3 me = Vector3.zero;
            for (int i = 0; i < players.Length; i++) if (Humans[i] != null) me = players[i].transform.position;
            DrillBall b = PracticeDrills.BallFor(PracticeDrill, me, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            ball.Launch(b.start, b.target, b.flightTime, false, b.type);
        }

        // ---------------- serve ----------------

        public Vector3 BasePosition(PlayerController p)
        {
            float x = (p.PlayerId % 2 == 0) ? -baseX : baseX;
            return new Vector3(x, 0f, p.CourtSide * baseDepth);
        }

        public PlayerController Teammate(PlayerController p) => players[p.PlayerId ^ 1];

        void BeginServe()
        {
            State = MatchState.Serve;
            netTouched = false;
            ServeStartTime = Time.time;
            ServeClockLeft = serveClockSeconds;
            Rules.Serve(Rules.ServingTeam);
            int serverId = Rules.ServerId;

            foreach (PlayerController p in players)
            {
                p.ResetState();
                Vector3 spot = BasePosition(p);
                if (p.PlayerId == serverId) spot.z = p.CourtSide * serveDepth;     // behind the baseline
                p.TeleportTo(spot);
                p.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -p.CourtSide), Vector3.up);   // face the net
                p.ServeMode = p.PlayerId == serverId;
            }
            HoldBall();
        }

        // The ball floats above the server's hitting hand until the serve.
        void HoldBall()
        {
            if (ball.IsInFlight || Server.Tossed) return;     // once tossed, the ball is no longer held
            ball.PlaceAt(Server.ContactPointAt(HitType.Serve) + Vector3.up * serveBallLift);
        }

        // ---------------- rally ----------------

        void OnTouched(PlayerController p)
        {
            if (State == MatchState.Serve && p.PlayerId == Rules.ServerId)
            {
                State = MatchState.Rally;      // the serve is on its way (the rules already know about it)
                p.ServeMode = false;
                return;
            }
            if (State != MatchState.Rally) return;
            PointReason fault = Rules.Touch(p.PlayerId, p.Team);
            if (fault != PointReason.None) EndPoint();
        }

        // A block is one of the blocking team's three touches; the blocker may touch next (no double-touch fault).
        void OnBlocked(PlayerController p)
        {
            if (State != MatchState.Rally) return;
            if (Rules.Touch(p.PlayerId, p.Team, true) != PointReason.None) EndPoint();
        }

        void OnLanded(Vector3 point)
        {
            if (State == MatchState.Serve && Server.Tossed)    // the toss hit the sand before the server hit it
            {
                Rules.ServeFault();
                EndPoint();
                return;
            }
            if (State != MatchState.Rally) return;
            if (Rules.BallLanded(point.x, point.z) != PointReason.None) EndPoint();
        }

        void EndPoint()
        {
            State = MatchState.PointScored;
            stateTimer = Practice && PracticeDrill != Drill.Serve ? practiceFeedSeconds : pointPauseSeconds;
            if (Rules.IsMatchOver) { Sets.EndSet(Rules.Winner, Rules.Score[0], Rules.Score[1]); if (!Sets.IsOver) stateTimer = setBannerSeconds; }
            LastPointViaNet = netTouched && Rules.LastReason == PointReason.BallIn;
            PointCount++;
            foreach (var p in players) p.ServeMode = false;
            Debug.Log($"[Point] team {(char)('A' + Rules.LastPointWinner)} scores: {ReasonText(Rules.LastReason, LastPointViaNet)}  ->  A {Rules.Score[0]} - B {Rules.Score[1]}{(Rules.IsMatchOver ? "  MATCH OVER" : "")}");
            OnPoint?.Invoke(Rules.LastPointWinner, Rules.LastReason, LastPointViaNet);
        }

        public static string ReasonText(PointReason reason, bool viaNet)
        {
            if (viaNet) return "net";
            switch (reason)
            {
                case PointReason.BallIn: return "in";
                case PointReason.BallOut: return "out";
                case PointReason.FourTouches: return "4 touches";
                case PointReason.DoubleTouch: return "double touch";
                case PointReason.ServeFault: return "fault";
                default: return "";
            }
        }

        // ---------------- AI difficulty ----------------

        AIDifficulty LevelOf(int slot) => slotDifficulty[slot] ?? difficulty;

        /// <summary>One bot's own difficulty (null = follow the global one). Takes effect at once.</summary>
        public void SetSlotDifficulty(int slot, AIDifficulty? level) { slotDifficulty[slot] = level; difficultyApplied = (AIDifficulty)(-1); }

        /// <summary>Gives every bot (including the auto-play one) its difficulty: the slot's own override, else the global one.</summary>
        void ApplyDifficulty()
        {
            difficultyApplied = difficulty;
            for (int i = 0; i < players.Length; i++)
                foreach (var bot in players[i].GetComponents<SimpleBotBrain>()) bot.SetDifficulty(LevelOf(i));
        }

        // ---------------- debug auto-play ----------------

        void ApplyAutoPlay()
        {
            autoPlayApplied = autoPlayHuman;
            ApplyBrains();
        }

        /// <summary>The bot brain of a slot (made on demand: only slots 1 to 3 have one in the scene).</summary>
        public SimpleBotBrain BotFor(int slot)
        {
            if (bots[slot] == null)
            {
                bots[slot] = players[slot].gameObject.AddComponent<SimpleBotBrain>();
                bots[slot].Setup(this, players[slot], ball);
                bots[slot].SetDifficulty(LevelOf(slot));
                bots[slot].enabled = false;
            }
            return bots[slot];
        }

        /// <summary>
        /// Gives each slot the brain it should have: its human's HumanBrain, or a bot. autoPlayHuman puts a bot in slot 0 for watching a match.
        /// A slot nobody has asked about (no human, no bot made yet) is left alone.
        /// </summary>
        public void ApplyBrains()
        {
            for (int i = 0; i < players.Length; i++)
            {
                bool humanDrives = Humans[i] != null && !(i == 0 && autoPlayHuman);
                SimpleBotBrain bot = (i == 0 && autoPlayHuman) ? BotFor(0) : bots[i];
                IPlayerBrain want = humanDrives ? Humans[i] : bot;
                if (want == null) continue;
                if (Humans[i] != null) Humans[i].enabled = humanDrives;
                if (bot != null) bot.enabled = !humanDrives;
                players[i].SetBrain(want);
            }
        }
    }
}
