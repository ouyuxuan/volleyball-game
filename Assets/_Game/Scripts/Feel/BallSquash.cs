using Sandspike.Ball;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// Ball squash and stretch, on the ball's visual only (the ball has no collider and its path is not touched).
    /// It stretches along its velocity (up to 1.35 times at 25 m/s) and squashes to 0.8 for 0.08 s on any contact and on landing.
    /// To scale along a world direction on a ball that spins, the ball's drawn parts are moved under two small holders:
    /// the outer one is turned to the direction and scaled, the inner one turns back, so the spin is kept.
    /// </summary>
    public class BallSquash : MonoBehaviour
    {
        public const float MaxStretch = 1.35f, FullSpeed = 25f, SquashScale = 0.8f, SquashSeconds = 0.08f;

        /// <summary>The scale along the direction of travel: 0.8 while a contact squash lasts, otherwise 1 growing to 1.35 at 25 m/s (and no more).</summary>
        public static float AlongScale(float speed, bool squashing)
        {
            if (squashing) return SquashScale;
            return Mathf.Lerp(1f, MaxStretch, Mathf.Clamp01(speed / FullSpeed));
        }

        BallController ball;
        Transform outer, inner;
        float squashLeft;
        bool landingSquash;
        Vector3 axis = Vector3.up;
        float current = 1f;

        void Start()
        {
            ball = FindFirstObjectByType<BallController>();
            if (ball == null) { enabled = false; return; }

            outer = new GameObject("Stretch").transform;
            outer.SetParent(ball.transform, false);
            inner = new GameObject("Unspin").transform;
            inner.SetParent(outer, false);
            var parts = new System.Collections.Generic.List<Transform>();      // in order, so "the first renderer" stays the Sphere
            foreach (Transform c in ball.transform) if (c != outer) parts.Add(c);
            foreach (Transform c in parts) c.SetParent(inner, false);          // the drawn parts keep their own local placement

            ball.OnHit += _ => Squash(false);
            ball.OnNetTouch += _ => Squash(false);
            ball.OnGroundContact += _ => Squash(true);
        }

        void Squash(bool landing)
        {
            squashLeft = SquashSeconds;
            landingSquash = landing;
        }

        void LateUpdate()
        {
            if (ball == null) return;
            squashLeft = Mathf.Max(0f, squashLeft - Time.deltaTime);
            Vector3 v = ball.Velocity;
            bool squashing = squashLeft > 0f;
            if (squashing && landingSquash) axis = Vector3.up;
            else if (v.sqrMagnitude > 0.01f) axis = v.normalized;

            float target = AlongScale(v.magnitude, squashing);
            current = squashing ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-30f * Time.deltaTime));   // snaps in, eases out
            float across = 1f / Mathf.Sqrt(current);                                                           // keeps the volume

            Quaternion q = Quaternion.Inverse(ball.transform.rotation) * Quaternion.FromToRotation(Vector3.forward, axis);
            outer.localRotation = q;
            outer.localScale = new Vector3(across, across, current);
            inner.localRotation = Quaternion.Inverse(q);
        }
    }
}
