using Sandspike.Aim;
using Sandspike.Controls;
using Sandspike.Players;
using Unity.Cinemachine;
using UnityEngine;

namespace Sandspike.CameraRig
{
    public enum ViewMode { ThirdPerson, FirstPerson }

    /// <summary>
    /// Owns the two Cinemachine cameras for one player and the V / right-stick-click toggle.
    /// 3rd person: fixed behind the player, always facing the net, free cursor (CursorAim).
    /// 1st person: at head height with mouse / right-stick look, cursor locked, centre crosshair (CrosshairAim).
    /// Exposes AimPoint (from whichever mode is active) and MoveYaw (so WASD moves relative to the view).
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour, IAimSource
    {
        [Header("References")]
        [SerializeField] HumanBrain brain;
        [SerializeField] PlayerVisual playerVisual;
        [SerializeField] CinemachineBrain cinemachineBrain;
        [SerializeField] CinemachineCamera thirdPersonCamera;
        [SerializeField] CinemachineCamera firstPersonCamera;   // child of the player's head target
        [SerializeField] Transform headTarget;                 // rotated by mouse look in 1st person
        [SerializeField] CursorAim cursorAim;
        [SerializeField] CrosshairAim crosshairAim;

        [Header("Switching")]
        [SerializeField] ViewMode startMode = ViewMode.ThirdPerson;
        [SerializeField] float blendSeconds = 0.25f;
        [SerializeField] int activePriority = 20;
        [SerializeField] int inactivePriority = 10;

        [Header("1st person look")]
        [SerializeField] float mouseSensitivity = 0.12f;        // degrees per pixel of mouse movement
        [SerializeField] float stickLookSpeed = 140f;           // degrees per second at full stick
        [SerializeField] float stickDeadZone = 0.2f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float startPitch = 8f;
        [SerializeField] bool invertY = false;

        float yaw;
        float pitch;

        public ViewMode Mode { get; private set; }
        public bool IsFirstPerson => Mode == ViewMode.FirstPerson;

        /// <summary>Camera yaw (degrees) that WASD / left stick should be relative to: 0 in 3rd person (the net is "up").</summary>
        public float MoveYaw => IsFirstPerson ? yaw : 0f;

        public Vector3 AimPoint => IsFirstPerson ? crosshairAim.AimPoint : cursorAim.AimPoint;

        void Start()
        {
            yaw = 0f;
            pitch = startPitch;
            if (cinemachineBrain != null)
                cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);
            SetMode(startMode, true);
        }

        void Update()
        {
            if (brain == null) return;

            if (brain.Controls.Gameplay.ToggleView.WasPressedThisFrame())
                SetMode(IsFirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson, false);

            Vector2 rawAim = brain.RawAim;
            float dt = Time.deltaTime;

            if (IsFirstPerson)
            {
                Vector2 look;
                if (InputDevices.UsingGamepad)
                    look = HumanBrain.ApplyDeadZone(rawAim, stickDeadZone) * (stickLookSpeed * dt);
                else
                    look = rawAim * mouseSensitivity;   // mouse delta is already "per frame"

                yaw += look.x;
                pitch += (invertY ? 1f : -1f) * look.y;     // moving the mouse up looks up
                pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
                if (headTarget != null) headTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else
            {
                cursorAim.Tick(rawAim, dt);
            }

            UpdateCursor();
        }

        /// <summary>Switches view. 'instant' skips the blend (used at start-up).</summary>
        public void SetMode(ViewMode mode, bool instant)
        {
            Mode = mode;
            bool fp = mode == ViewMode.FirstPerson;

            if (instant && cinemachineBrain != null)
                cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            thirdPersonCamera.Priority.Value = fp ? inactivePriority : activePriority;
            firstPersonCamera.Priority.Value = fp ? activePriority : inactivePriority;

            if (instant && cinemachineBrain != null)
                cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);

            if (fp)
            {
                // Start looking where we were aiming, so the switch doesn't jerk the aim point around.
                Vector3 from = headTarget != null ? headTarget.position : Vector3.zero;
                Vector3 to = cursorAim.AimPoint - from;
                if (new Vector2(to.x, to.z).sqrMagnitude > 0.5f)
                {
                    yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float flat = new Vector2(to.x, to.z).magnitude;
                    pitch = Mathf.Clamp(Mathf.Atan2(from.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                }
                if (headTarget != null) headTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else
            {
                // Carry the crosshair spot back to the cursor/stick aim point.
                if (!instant) cursorAim.SetAimPoint(crosshairAim.AimPoint);
                if (headTarget != null) headTarget.localRotation = Quaternion.identity;
            }

            if (playerVisual != null) playerVisual.SetFirstPerson(fp);
            UpdateCursor();
        }

        // 3rd person with mouse: free visible cursor. 1st person with mouse: locked and hidden. Gamepad: hidden.
        void UpdateCursor()
        {
            bool fp = IsFirstPerson;
            bool pad = InputDevices.UsingGamepad;
            Cursor.lockState = fp ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !fp && !pad;
        }
    }
}
