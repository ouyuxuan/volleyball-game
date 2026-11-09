using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// M1 TRAINING FEEDER (M2's rules replace it): tosses a ball from the far side to a random spot
    /// on the player's half a few seconds after the last ball landed. Press R (gamepad Select) to feed now.
    /// </summary>
    public class BallFeeder : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] HumanBrain brain;               // only used to read the FeedBall button

        [Header("Toss")]
        [SerializeField] Vector3 tossFrom = new Vector3(0f, 1.8f, 7f);
        [SerializeField] float flightTime = 2.0f;
        [SerializeField] float secondsBetweenFeeds = 3.5f;

        [Header("Landing area on the player's half (side -1 = z below the net)")]
        [SerializeField] float playerSide = -1f;
        [SerializeField] float minX = -3.5f;
        [SerializeField] float maxX = 3.5f;
        [SerializeField] float nearestToNet = 1.5f;
        [SerializeField] float farthestFromNet = 7f;

        float idleTimer;
        bool feedingStarted;

        void Start()
        {
            if (ball != null) ball.OnGroundContact += OnBallLanded;
            idleTimer = 1.0f;           // first ball shortly after pressing Play
            feedingStarted = true;
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundContact -= OnBallLanded;
        }

        void OnBallLanded(Vector3 point) { idleTimer = secondsBetweenFeeds; }

        void Update()
        {
            if (ball == null) return;

            if (brain != null && brain.Controls.Gameplay.FeedBall.WasPressedThisFrame())
            {
                FeedNow();
                return;
            }

            // Wait a few seconds after the ball is at rest, then toss the next one.
            if (feedingStarted && !ball.IsInFlight)
            {
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f) FeedNow();
            }
        }

        /// <summary>Tosses a ball right now. Public so tests and debug code can call it.</summary>
        public Vector3 FeedNow()
        {
            float z = playerSide * Random.Range(nearestToNet, farthestFromNet);
            Vector3 target = new Vector3(Random.Range(minX, maxX), 0f, z);
            return FeedTo(target);
        }

        /// <summary>Tosses a ball to a chosen spot (used by the play-mode checks).</summary>
        public Vector3 FeedTo(Vector3 target)
        {
            Vector3 start = tossFrom;
            start.z = -playerSide * Mathf.Abs(tossFrom.z);     // far side from the player
            ball.Launch(start, target, flightTime, false);
            idleTimer = secondsBetweenFeeds;
            return target;
        }
    }
}
