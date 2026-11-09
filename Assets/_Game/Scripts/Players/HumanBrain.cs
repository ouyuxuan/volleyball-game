using Sandspike.CameraRig;
using Sandspike.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandspike.Players
{
    /// <summary>
    /// Reads the keyboard / mouse / gamepad through the Input System and presents it as an IPlayerBrain.
    /// Owns the generated VolleyballControls object (so other scripts can read extra buttons from it).
    /// </summary>
    public class HumanBrain : MonoBehaviour, IPlayerBrain
    {
        [SerializeField] PlayerCameraRig cameraRig;      // gives us the aim point and the view direction

        [Header("Gamepad feel")]
        [Range(0f, 0.9f)] [SerializeField] float stickDeadZone = 0.2f;

        public VolleyballControls Controls { get; private set; }

        void Awake()
        {
            Controls = new VolleyballControls();
        }

        void OnEnable() { Controls.Gameplay.Enable(); }
        void OnDisable() { Controls.Gameplay.Disable(); }
        void OnDestroy() { Controls?.Dispose(); }

        public float StickDeadZone => stickDeadZone;

        /// <summary>Applies a radial dead zone and rescales so the output still reaches 1.</summary>
        public static Vector2 ApplyDeadZone(Vector2 v, float deadZone)
        {
            float m = v.magnitude;
            if (m <= deadZone) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - deadZone) / (1f - deadZone));
        }

        public Vector2 Move
        {
            get
            {
                Vector2 raw = Controls.Gameplay.Move.ReadValue<Vector2>();
                Vector2 v = Vector2.ClampMagnitude(InputDevices.UsingGamepad ? ApplyDeadZone(raw, stickDeadZone) : raw, 1f);
                // Turn "up on the stick" into "away from the camera": rotate by the view's yaw (0 in 3rd person).
                float yaw = cameraRig != null ? cameraRig.MoveYaw : 0f;
                Vector3 world = Quaternion.Euler(0f, yaw, 0f) * new Vector3(v.x, 0f, v.y);
                return new Vector2(world.x, world.z);
            }
        }

        /// <summary>Raw right-stick / mouse-delta value, for the camera rig's look and aim code.</summary>
        public Vector2 RawAim => Controls.Gameplay.Aim.ReadValue<Vector2>();

        public Vector3 AimPoint => cameraRig != null ? cameraRig.AimPoint : new Vector3(0f, 0f, 4f);

        public ButtonState Jump => Read(Controls.Gameplay.Jump);
        public ButtonState Spike => Read(Controls.Gameplay.Spike);
        public ButtonState Bump => Read(Controls.Gameplay.Bump);
        public ButtonState Set => Read(Controls.Gameplay.Set);

        static ButtonState Read(InputAction a) =>
            new ButtonState(a.WasPressedThisFrame(), a.IsPressed(), a.WasReleasedThisFrame());
    }
}
