using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>Why a rally ended. 'None' means play goes on.</summary>
    public enum PointReason { None, BallIn, BallOut, FourTouches, DoubleTouch }

    /// <summary>
    /// The rules of the match as plain C# (no scene objects), so it is easy to unit test.
    /// Teams are 0 (A, z below the net) and 1 (B). Players are ids 0..3 (team = id / 2).
    /// The MatchManager feeds it three kinds of events: Serve, Touch and BallLanded.
    /// </summary>
    public class RulesEngine
    {
        // ---- settings (set in the constructor, shown in the MatchManager Inspector) ----
        public readonly int targetScore;      // 11, 21 or 25
        public readonly int winBy;            // 2
        public readonly int maxTouches;       // 3 per side
        public readonly float halfWidth;      // court x limit (4)
        public readonly float halfLength;     // court z limit (8)
        public readonly float ballRadius;     // a ball touching the line counts as in

        // ---- state ----
        public int[] Score { get; } = new int[2];
        public int ServingTeam { get; private set; }
        readonly int[] serverIndex = new int[2];      // which of the team's two players serves (0 or 1)
        public int PossessionTeam { get; private set; } = -1;   // the team whose touches are being counted
        public int Touches { get; private set; }               // touches by PossessionTeam this possession
        public int LastToucherId { get; private set; } = -1;   // player who touched last (-1 = nobody yet)
        public int LastTouchTeam { get; private set; } = -1;   // team of the last touch (a serve counts)
        public bool RallyActive { get; private set; }
        public int LastPointWinner { get; private set; } = -1;
        public PointReason LastReason { get; private set; }

        public RulesEngine(int targetScore = 11, int winBy = 2, int maxTouches = 3, float halfWidth = 4f, float halfLength = 8f, float ballRadius = 0.11f, int firstServingTeam = 0)
        {
            this.targetScore = targetScore;
            this.winBy = winBy;
            this.maxTouches = maxTouches;
            this.halfWidth = halfWidth;
            this.halfLength = halfLength;
            this.ballRadius = ballRadius;
            ServingTeam = firstServingTeam;
        }

        public static int TeamOf(int playerId) => playerId / 2;

        /// <summary>The player id of the current server.</summary>
        public int ServerId => ServingTeam * 2 + serverIndex[ServingTeam];

        public bool IsMatchOver => Winner >= 0;

        /// <summary>The winning team (first to the target AND ahead by winBy), or -1.</summary>
        public int Winner
        {
            get
            {
                for (int t = 0; t < 2; t++)
                    if (Score[t] >= targetScore && Score[t] - Score[1 - t] >= winBy) return t;
                return -1;
            }
        }

        /// <summary>How many touches the team has used on the ball it currently holds (0 if the ball is the other team's).</summary>
        public int TouchesFor(int team) => PossessionTeam == team ? Touches : 0;

        /// <summary>A new rally starts: the serving team's server is about to hit.</summary>
        public void Serve(int team)
        {
            ServingTeam = team;
            RallyActive = true;
            PossessionTeam = team;
            Touches = 0;
            LastToucherId = -1;
            LastTouchTeam = team;   // a serve that lands out is the server's fault
            LastReason = PointReason.None;
        }

        /// <summary>
        /// A player touched the ball (not the serve). Returns the fault reason, or None if play goes on.
        /// A block (isBlock) counts as one of the team's touches, but the blocker may make the next touch (no double-touch fault).
        /// </summary>
        public PointReason Touch(int playerId, int team, bool isBlock = false)
        {
            if (!RallyActive) return PointReason.None;

            if (team != PossessionTeam)       // the ball changed sides: this team starts counting from zero
            {
                PossessionTeam = team;
                Touches = 0;
                LastToucherId = -1;
            }

            LastTouchTeam = team;
            if (!isBlock && LastToucherId == playerId)    // the same player twice in a row
            {
                Award(1 - team, PointReason.DoubleTouch);
                return PointReason.DoubleTouch;
            }
            Touches++;
            LastToucherId = isBlock ? -1 : playerId;     // after a block anyone (the blocker too) may touch
            if (Touches > maxTouches)
            {
                Award(1 - team, PointReason.FourTouches);
                return PointReason.FourTouches;
            }
            return PointReason.None;
        }

        /// <summary>Is a ball landing here inside the lines? The ball radius counts, so a line ball is in.</summary>
        public bool IsInside(float x, float z)
        {
            return Mathf.Abs(x) <= halfWidth + ballRadius && Mathf.Abs(z) <= halfLength + ballRadius;
        }

        /// <summary>The ball reached the sand at (x, z). In: the other half's team scores. Out: the last toucher's team loses the point.</summary>
        public PointReason BallLanded(float x, float z)
        {
            if (!RallyActive) return PointReason.None;
            if (IsInside(x, z))
            {
                int landedHalf = z < 0f ? 0 : 1;          // team A defends z < 0
                Award(1 - landedHalf, PointReason.BallIn);
                return PointReason.BallIn;
            }
            int toucher = LastTouchTeam >= 0 ? LastTouchTeam : ServingTeam;
            Award(1 - toucher, PointReason.BallOut);
            return PointReason.BallOut;
        }

        // Scores the point, and on a side-out hands the serve over and rotates that team's server.
        void Award(int winner, PointReason reason)
        {
            Score[winner]++;
            LastPointWinner = winner;
            LastReason = reason;
            RallyActive = false;
            if (winner != ServingTeam)
            {
                ServingTeam = winner;
                serverIndex[winner] = 1 - serverIndex[winner];
            }
        }
    }
}
