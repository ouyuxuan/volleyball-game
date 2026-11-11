using System;
using UnityEngine;

namespace Sandspike.Ball
{
    /// <summary>
    /// Moves the ball along an exact, pre-computed arc (no Rigidbody physics during flight).
    /// A hit calls Launch(start, target, flightTime). If the arc would not clear the net, the ball
    /// follows it up to the net, then drops on the hitter's side (OnNetTouch).
    /// Raises OnHit, OnNetTouch and OnGroundContact. Spin is only a visual.
    /// </summary>
    public class BallController : MonoBehaviour
    {
        [Header("Physics numbers (tunable)")]
        [SerializeField] float gravity = BallTrajectory.DefaultGravity;
        [SerializeField] float ballRadius = BallTrajectory.BallRadius;
        [SerializeField] float netTopHeight = BallTrajectory.NetTopHeight;
        [SerializeField] float netZ = 0f;

        [Header("Net drop")]
        [SerializeField] float netDropSpeed = 1.2f;   // slow push away from the net after touching it

        [Header("Spin (visual only)")]
        [SerializeField] float spinDegreesPerSecond = 540f;

        /// <summary>Raised when a player hits the ball. Argument: where it will land.</summary>
        public event Action<Vector3> OnHit;
        /// <summary>Raised when the arc touches the net. Argument: where.</summary>
        public event Action<Vector3> OnNetTouch;
        /// <summary>Raised when the ball reaches the sand. Argument: the contact point (y = 0).</summary>
        public event Action<Vector3> OnGroundContact;

        // One parabola segment. A normal hit is one arc; a net touch adds a second (the drop).
        struct Arc { public Vector3 start; public Vector3 v0; public float duration; }

        Arc arc;
        Arc dropArc;
        float elapsed;          // seconds since the current arc began
        bool inFlight;
        bool willHitNet;
        float netTime;          // when the first arc touches the net
        float totalFlight;      // predicted seconds from launch to landing (including a net drop)
        float flightClock;      // seconds since Launch
        Vector3 landingPoint;
        Vector3 spinAxis = Vector3.right;
        float spinDegrees;       // current spin rate in degrees per second (signed)

        /// <summary>Sets the spin rate for the current flight (degrees per second, about the axis perpendicular to travel). Visual only.</summary>
        public void SetSpin(float degreesPerSecond) { spinDegrees = degreesPerSecond; }

        public bool IsInFlight => inFlight;
        /// <summary>Counts launches, so a queued hit can tell someone else touched the ball.</summary>
        public int LaunchCount { get; private set; }
        /// <summary>What kind of hit made the current flight (read it inside OnHit). The ContactFeel uses it.</summary>
        public Sandspike.Players.HitType LastHitType { get; private set; }
        /// <summary>The ball's speed (m/s) at the moment of the last launch.</summary>
        public float LaunchSpeed => arc.v0.magnitude;
        /// <summary>The flat direction the ball travels in (for squash and shake).</summary>
        public Vector3 LaunchDirection => arc.v0.sqrMagnitude > 1e-4f ? arc.v0.normalized : Vector3.forward;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => inFlight ? BallTrajectory.VelocityAt(arc.v0, elapsed, gravity) : Vector3.zero;
        /// <summary>Where the ball will touch the sand (y = 0), counting a net drop.</summary>
        public Vector3 LandingPoint => landingPoint;
        public float TimeToLanding => inFlight ? Mathf.Max(0f, totalFlight - flightClock) : 0f;
        public float TotalFlightTime => totalFlight;
        public float Gravity => gravity;
        public float Radius => ballRadius;
        public bool WillHitNet => willHitNet;

        /// <summary>Puts the ball somewhere, at rest, not flying.</summary>
        public void PlaceAt(Vector3 position)
        {
            inFlight = false;
            transform.position = position;
        }

        /// <summary>
        /// Starts a flight from 'start' so that the ball's centre ends 'ballRadius' above 'groundTarget'
        /// after exactly 'flightTime' seconds. 'isHit' = a player hit it (raises OnHit).
        /// </summary>
        public void Launch(Vector3 start, Vector3 groundTarget, float flightTime, bool isHit = true, Sandspike.Players.HitType type = Sandspike.Players.HitType.Set)
        {
            LaunchCount++;
            LastHitType = type;
            spinDegrees = spinDegreesPerSecond;
            flightTime = Mathf.Max(0.05f, flightTime);
            Vector3 centreTarget = new Vector3(groundTarget.x, ballRadius, groundTarget.z);

            arc = new Arc
            {
                start = start,
                v0 = BallTrajectory.SolveLaunchVelocity(start, centreTarget, flightTime, gravity),
                duration = flightTime,
            };
            elapsed = 0f;
            flightClock = 0f;
            inFlight = true;
            transform.position = start;

            // Will the arc get over the net? If not, plan the drop now so the landing marker can show it.
            willHitNet = !BallTrajectory.ClearsNet(arc.start, arc.v0, arc.duration, netTopHeight, ballRadius, netZ, gravity)
                         && BallTrajectory.CrossesNetAt(arc.start, arc.v0, arc.duration, out netTime, out _, netZ, gravity);

            if (willHitNet)
            {
                Vector3 hit = BallTrajectory.PositionAt(arc.start, arc.v0, netTime, gravity);
                float side = arc.start.z >= netZ ? 1f : -1f;      // the hitter's side of the net
                Vector3 dropStart = new Vector3(hit.x, hit.y, netZ + side * (ballRadius + 0.02f));
                Vector3 dropV = new Vector3(0f, 0f, side * netDropSpeed);
                float fall = BallTrajectory.TimeToReachHeight(dropStart.y, 0f, ballRadius, true, gravity);
                if (float.IsNaN(fall)) fall = 0f;
                dropArc = new Arc { start = dropStart, v0 = dropV, duration = fall };
                totalFlight = netTime + fall;
                landingPoint = Ground(BallTrajectory.PositionAt(dropArc.start, dropArc.v0, dropArc.duration, gravity));
            }
            else
            {
                totalFlight = arc.duration;
                landingPoint = Ground(BallTrajectory.PositionAt(arc.start, arc.v0, arc.duration, gravity));
            }

            Vector3 flat = new Vector3(arc.v0.x, 0f, arc.v0.z);
            spinAxis = flat.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;

            if (isHit) OnHit?.Invoke(landingPoint);
        }

        /// <summary>Where the ball will be 'offset' seconds from now along the current arc (negative = past).</summary>
        public Vector3 PositionAtOffset(float offset)
        {
            return BallTrajectory.PositionAt(arc.start, arc.v0, Mathf.Clamp(elapsed + offset, 0f, arc.duration), gravity);
        }

        /// <summary>
        /// Finds the ideal contact moment: when the ball's arc passes closest to a (possibly moving) contact point.
        /// contactAt(offset) = where the contact point will be 'offset' seconds from now.
        /// timeOffset = ideal moment minus now (negative = already passed). The search covers the recent past and
        /// the next 'lookAhead' seconds of the arc. Landing time is never used.
        /// </summary>
        public bool TryFindContact(Func<float, Vector3> contactAt, float lookBack, float lookAhead, out float timeOffset, out float distance)
        {
            timeOffset = 0f;
            distance = float.MaxValue;
            if (!inFlight) return false;
            float tMin = -Mathf.Min(elapsed, lookBack);
            float tMax = Mathf.Min(CurrentArcRemainingEnd() - elapsed, lookAhead);
            timeOffset = ContactTiming.FindIdealTime(PositionAtOffset, contactAt, tMin, tMax, out distance);
            return true;
        }

        float CurrentArcRemainingEnd() => (willHitNet && arc.duration > netTime) ? netTime : arc.duration;

        void Update()
        {
            if (!inFlight) return;

            float dt = Time.deltaTime;
            elapsed += dt;
            flightClock += dt;

            // Reached the net on an arc that is too low: switch to the drop.
            if (willHitNet && elapsed >= netTime)
            {
                Vector3 touch = BallTrajectory.PositionAt(arc.start, arc.v0, netTime, gravity);
                float over = elapsed - netTime;
                arc = dropArc;
                elapsed = over;
                willHitNet = false;
                transform.position = arc.start;
                OnNetTouch?.Invoke(touch);
            }

            if (elapsed >= arc.duration)
            {
                Vector3 end = BallTrajectory.PositionAt(arc.start, arc.v0, arc.duration, gravity);
                transform.position = end;
                inFlight = false;
                OnGroundContact?.Invoke(Ground(end));
                return;
            }

            transform.position = BallTrajectory.PositionAt(arc.start, arc.v0, elapsed, gravity);
            transform.Rotate(spinAxis, spinDegrees * dt, Space.World);
        }

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);
    }
}
