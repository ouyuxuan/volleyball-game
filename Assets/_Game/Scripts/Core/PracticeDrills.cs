using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>The five practice drills. Serve has no feeder: the player holds the ball and serves every time.</summary>
    public enum Drill { Receive, Set, Spike, Block, Serve }

    /// <summary>One ball the feeder sends: where from, where it lands (on the sand), how long it flies, and what kind of hit it counts as.</summary>
    public struct DrillBall
    {
        public Vector3 start, target;
        public float flightTime;
        public HitType type;
    }

    /// <summary>
    /// What ball each drill sends, as plain maths (no scene objects), so it can be tested.
    /// The practice player is always on the near half (z below the net), so every target is there.
    /// jx / jz are -1..+1 random nudges (passed in, so tests can use the extremes).
    /// </summary>
    public static class PracticeDrills
    {
        public static readonly string[] Names = { "Receive", "Set", "Spike", "Block", "Serve" };
        const float MaxX = 3.5f, NearNet = -1.5f, FarBack = -7f;     // the part of the near half a fed ball may land in
        public const float BlockBallSpeed = 18f;                     // m/s along the line from the far hitter to the landing spot

        public static Drill Next(Drill d, int dir) => (Drill)(((int)d + dir + Names.Length) % Names.Length);

        /// <summary>True when the drill sends balls (all but Serve).</summary>
        public static bool Feeds(Drill d) => d != Drill.Serve;

        /// <summary>Where the player is put when a drill starts (Serve uses the normal serving spot).</summary>
        public static Vector3 StartSpot(Drill d)
        {
            switch (d)
            {
                case Drill.Set: return new Vector3(0f, 0f, -4f);
                case Drill.Spike: return new Vector3(0f, 0f, -3f);
                case Drill.Block: return new Vector3(0f, 0f, -1.5f);
                default: return new Vector3(0f, 0f, -5f);
            }
        }

        static float ClampX(float x, float max = MaxX) => Mathf.Clamp(x, -max, max);

        /// <summary>The next ball for this drill, given where the player stands now. Call only for drills that Feed.</summary>
        public static DrillBall BallFor(Drill d, Vector3 player, float jx, float jz)
        {
            switch (d)
            {
                case Drill.Set:      // a soft, high ball straight to the player: a long flight, nearly down onto them
                    return new DrillBall
                    {
                        start = new Vector3(0f, 2f, 7f),
                        target = new Vector3(ClampX(player.x + jx * 0.4f), 0f, Mathf.Clamp(player.z + jz * 0.4f, FarBack, NearNet)),
                        flightTime = 2.4f, type = HitType.Set,
                    };
                case Drill.Spike:    // a high set near the net on the player's side, thrown from behind them (it never crosses the net)
                {
                    float x = ClampX(player.x, 2.5f);
                    var start = new Vector3(x, 2f, -5.5f);
                    return new DrillBall
                    {
                        start = start,
                        target = new Vector3(ClampX(x + jx * 0.4f), 0f, -1.8f),
                        flightTime = BallTrajectory.FlightTimeForApex(start.y, 4.3f, BallTrajectory.BallRadius), type = HitType.Set,
                    };
                }
                case Drill.Block:    // a spike-speed ball from just behind the net on the far side, across the net where the player is
                {
                    float x = ClampX(player.x + jx * 0.5f);
                    var start = new Vector3(x, 3.6f, 0.8f);
                    var target = new Vector3(x, 0f, -4.5f);
                    return new DrillBall { start = start, target = target, flightTime = BallTrajectory.FlightTimeForSpeed(start, target, BlockBallSpeed), type = HitType.Spike };
                }
                default:             // Receive: a serve-like ball over the net, landing near the player
                    return new DrillBall
                    {
                        start = new Vector3(0f, 1.8f, 7f),
                        target = new Vector3(ClampX(player.x + jx), 0f, Mathf.Clamp(player.z + jz, FarBack, NearNet)),
                        flightTime = 1.8f, type = HitType.Serve,
                    };
            }
        }
    }
}
