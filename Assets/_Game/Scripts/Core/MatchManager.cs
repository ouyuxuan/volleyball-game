using System;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Core
{
    public enum MatchState { Serve, Rally, PointScored, MatchOver }

    /// <summary>
    /// Runs the match: Serve -> Rally -> PointScored -> (Serve again | MatchOver).
    /// It places the players, holds the ball for the server, feeds hits and landings to the RulesEngine,
    /// and shows nothing itself: the Hud reads State, Rules and the point info from here.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] BallController ball;
        [Tooltip("Index 0 = the human (team A), 1 = teammate bot (A), 2 and 3 = team B bots.")]
        [SerializeField] PlayerController[] players = new PlayerController[4];
        [SerializeField] HumanBrain humanBrain;

        [Header("Match")]
        [Tooltip("First to this many points (11, 21 or 25), and you must win by 2.")]
        [SerializeField] int targetScore = 11;
        [SerializeField] int winBy = 2;
        [SerializeField] int firstServingTeam = 0;

        [Header("Court (metres)")]
        [SerializeField] float halfWidth = 4f;
        [SerializeField] float halfLength = 8f;
        [SerializeField] float baseX = 2f;            // base positions: x = -2 / +2 ...
        [SerializeField] float baseDepth = 4f;        // ... and 4 m from the net
        [SerializeField] float serveDepth = 9f;       // the server stands this far from the net (behind the baseline)

        [Header("Timing")]
        [SerializeField] float pointPauseSeconds = 1.5f;
        [SerializeField] float serveBallLift = 0.15f; // the held ball floats this far above the server's hand

        [Header("Debug")]
        [Tooltip("Swaps player 1's brain for a SimpleBotBrain, so a whole match can be watched.")]
        public bool autoPlayHuman;

        public RulesEngine Rules { get; private set; }
        public MatchState State { get; private set; }
        public PlayerController[] Players => players;
        public float ServeStartTime { get; private set; }
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
        SimpleBotBrain humanBot;

        void Start()
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].PlayerId = i;
                players[i].OnBallTouched += OnTouched;
                PlayerController owner = players[i];
                owner.TeamTouchCount = () => Rules != null ? Rules.TouchesFor(owner.Team) : 0;   // touches before this one
                var bot = players[i].GetComponent<SimpleBotBrain>();
                if (bot != null) bot.Setup(this, players[i], ball);
            }
            ball.OnGroundContact += OnLanded;
            ball.OnNetTouch += _ => netTouched = true;

            NewMatch();
            ApplyAutoPlay();
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundContact -= OnLanded;
            foreach (var p in players) if (p != null) p.OnBallTouched -= OnTouched;
        }

        /// <summary>Scores back to 0-0 and starts the first serve.</summary>
        public void NewMatch()
        {
            Rules = new RulesEngine(targetScore, winBy, 3, halfWidth, halfLength, ball.Radius, firstServingTeam);
            PointCount = 0;
            BeginServe();
        }

        void Update()
        {
            if (autoPlayHuman != autoPlayApplied) ApplyAutoPlay();

            switch (State)
            {
                case MatchState.Serve:
                    HoldBall();
                    break;
                case MatchState.PointScored:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        if (Rules.IsMatchOver) { State = MatchState.MatchOver; }
                        else BeginServe();
                    }
                    break;
                case MatchState.MatchOver:
                    // "Press Jump to play again"
                    if (humanBrain != null && humanBrain.isActiveAndEnabled && humanBrain.Jump.Down) NewMatch();
                    break;
            }
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
            if (ball.IsInFlight) return;
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

        void OnLanded(Vector3 point)
        {
            if (State != MatchState.Rally) return;
            if (Rules.BallLanded(point.x, point.z) != PointReason.None) EndPoint();
        }

        void EndPoint()
        {
            State = MatchState.PointScored;
            stateTimer = pointPauseSeconds;
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
                default: return "";
            }
        }

        // ---------------- debug auto-play ----------------

        void ApplyAutoPlay()
        {
            autoPlayApplied = autoPlayHuman;
            PlayerController p = players[0];
            if (autoPlayHuman)
            {
                if (humanBot == null)
                {
                    humanBot = p.gameObject.AddComponent<SimpleBotBrain>();
                    humanBot.Setup(this, p, ball);
                }
                humanBot.enabled = true;
                if (humanBrain != null) humanBrain.enabled = false;
                p.SetBrain(humanBot);
            }
            else
            {
                if (humanBot != null) humanBot.enabled = false;
                if (humanBrain != null) { humanBrain.enabled = true; p.SetBrain(humanBrain); }
            }
        }
    }
}
