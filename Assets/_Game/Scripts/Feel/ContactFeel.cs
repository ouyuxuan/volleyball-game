using Sandspike.Ball;
using Sandspike.Players;
using Unity.Cinemachine;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// The "feel" of each touch, put on the Ball next to the BallController. It listens to the ball's OnHit event and adds:
    /// spike = a short hit-stop, camera shake and heavy spin; bump = a quick squash and some spin; set = almost no spin;
    /// and a smaller shake when a spiked ball reaches the sand. Every number is a tunable.
    /// The camera shake needs a CinemachineImpulseSource on the ball and a CinemachineImpulseListener on each virtual
    /// camera; this script adds them itself if the scene doesn't have them.
    /// </summary>
    [RequireComponent(typeof(BallController))]
    public class ContactFeel : MonoBehaviour
    {
        [Header("Spike")]
        [SerializeField] float spikeHitStop = 0.06f;          // seconds (real time) the game is nearly frozen
        [SerializeField] float hitStopTimeScale = 0.05f;      // Time.timeScale during the freeze
        [SerializeField] float spikeSpin = -1400f;            // degrees/second (negative = topspin)
        [SerializeField] float spikeShakeMin = 0.08f;         // shake strength at 16 m/s ...
        [SerializeField] float spikeShakeMax = 0.25f;         // ... and at 34 m/s
        [SerializeField] float groundShake = 0.06f;           // a spiked ball hitting the sand

        [Header("Bump and set")]
        [SerializeField] float bumpSpin = 300f;
        [SerializeField] float bumpSquash = 0.85f;            // scale along the direction of travel
        [SerializeField] float bumpSquashSeconds = 0.08f;
        [SerializeField] float setSpin = 60f;

        BallController ball;
        CinemachineImpulseSource impulse;
        bool spikeInFlight;

        /// <summary>Debug counters: how many hit-stops started and how many restored the time scale.</summary>
        public int HitStopCount { get; private set; }
        public int RestoreCount { get; private set; }
        public bool IsStopped => stopped;

        // hit-stop state
        bool stopped;
        float stopUntil;
        float savedScale = 1f;

        // squash state
        float squashUntil;
        int squashAxis;
        Vector3 baseScale;

        void Awake()
        {
            ball = GetComponent<BallController>();
            baseScale = transform.localScale;
            EnsureImpulse();
        }

        void OnEnable()
        {
            ball.OnHit += OnHit;
            ball.OnGroundContact += OnGround;
        }

        void OnDisable()
        {
            ball.OnHit -= OnHit;
            ball.OnGroundContact -= OnGround;
            ReleaseHitStop();      // never leave the game frozen if play stops or this is switched off
            transform.localScale = baseScale;
        }

        void OnDestroy() { ReleaseHitStop(); }

        // Adds the impulse source here and a listener on every virtual camera (once).
        void EnsureImpulse()
        {
            impulse = GetComponent<CinemachineImpulseSource>();
            if (impulse == null)
            {
                impulse = gameObject.AddComponent<CinemachineImpulseSource>();
                impulse.ImpulseDefinition = new CinemachineImpulseDefinition
                {
                    ImpulseChannel = 1,
                    ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                    CustomImpulseShape = new AnimationCurve(),
                    ImpulseDuration = 0.2f,
                    ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                    DissipationDistance = 100f,
                    DissipationRate = 0.25f,
                    PropagationSpeed = 343f,
                };
                impulse.DefaultVelocity = Vector3.down;
            }
            foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
            {
                if (cam.GetComponent<CinemachineImpulseListener>() != null) continue;
                var listener = cam.gameObject.AddComponent<CinemachineImpulseListener>();
                listener.ChannelMask = 1;
                listener.Gain = 1f;
                listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction { AmplitudeGain = 1f, FrequencyGain = 1f, Duration = 1f };
            }
        }

        void OnHit(Vector3 landing)
        {
            spikeInFlight = false;
            switch (ball.LastHitType)
            {
                case HitType.Spike:
                    spikeInFlight = true;
                    ball.SetSpin(spikeSpin);
                    Shake(Mathf.Lerp(spikeShakeMin, spikeShakeMax, Mathf.InverseLerp(16f, 34f, ball.LaunchSpeed)));
                    HitStop(spikeHitStop);
                    break;
                case HitType.Bump:
                    ball.SetSpin(bumpSpin);
                    Squash();
                    break;
                case HitType.Set:
                    ball.SetSpin(setSpin);
                    break;
                case HitType.Block:
                    Shake(groundShake);      // medium shake: same strength as a spiked ball hitting the sand
                    break;
            }
        }

        void OnGround(Vector3 point)
        {
            if (spikeInFlight) Shake(groundShake);
            spikeInFlight = false;
        }

        void Shake(float strength)
        {
            if (impulse != null && strength > 0f) impulse.GenerateImpulseWithVelocity(ball.LaunchDirection * strength);
        }

        // ---- hit-stop: dip Time.timeScale, restore from unscaled time ----

        void HitStop(float seconds)
        {
            if (seconds <= 0f || stopped) return;
            stopped = true; HitStopCount++;
            savedScale = Time.timeScale;
            stopUntil = Time.unscaledTime + seconds;
            Time.timeScale = savedScale * hitStopTimeScale;
        }

        void ReleaseHitStop()
        {
            if (!stopped) return;
            stopped = false; RestoreCount++;
            // Only restore if nobody else (a pause menu, say) changed the scale in the meantime.
            if (Mathf.Approximately(Time.timeScale, savedScale * hitStopTimeScale)) Time.timeScale = savedScale;
        }

        // ---- squash: shorten the ball along the local axis closest to its direction of travel ----

        void Squash()
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * ball.LaunchDirection;
            squashAxis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            squashUntil = Time.unscaledTime + bumpSquashSeconds;
        }

        void Update()
        {
            if (stopped && Time.unscaledTime >= stopUntil) ReleaseHitStop();

            Vector3 s = baseScale;
            if (Time.unscaledTime < squashUntil) s[squashAxis] *= bumpSquash;
            transform.localScale = s;
        }
    }
}
