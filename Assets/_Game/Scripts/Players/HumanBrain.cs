using System.Collections.Generic;
using Sandspike.Aim;
using Sandspike.CameraRig;
using Sandspike.Controls;
using Sandspike.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Sandspike.Players
{
    /// <summary>Which devices a human's brain listens to. Any = every device (the lone player, who may swap devices mid-match).</summary>
    public enum DeviceScope { Any, Keyboard, Pad }

    /// <summary>
    /// Reads the keyboard / mouse / gamepad through the Input System and presents it as an IPlayerBrain.
    /// Owns its own generated VolleyballControls object. With two or more humans each brain is restricted to its own
    /// devices (Bind), so one person's input can never drive another person's player.
    /// </summary>
    public class HumanBrain : MonoBehaviour, IPlayerBrain, IAimSource
    {
        [SerializeField] PlayerCameraRig cameraRig;      // gives us the aim point and the view direction

        [Header("Gamepad feel")]
        [Range(0f, 0.9f)] [SerializeField] float stickDeadZone = 0.2f;

        [Header("Aim assist (gamepad aim only, never the mouse)")]
        public bool aimAssist = true;

        public VolleyballControls Controls { get; private set; }
        public DeviceScope Scope { get; private set; } = DeviceScope.Any;
        public Gamepad Pad { get; private set; }

        MatchManager match;
        PlayerController player;

        void Awake()
        {
            Controls = new VolleyballControls();
            ControlBindings.Apply(Controls);                 // the player's rebinding (global per scheme)
            ControlBindings.Changed += Reapply;
            player = GetComponent<PlayerController>();
        }

        void OnEnable() { Controls.Gameplay.Enable(); }
        void OnDisable() { Controls.Gameplay.Disable(); }
        void Reapply() => ControlBindings.Apply(Controls);

        void OnDestroy() { ControlBindings.Changed -= Reapply; Controls?.Dispose(); }

        /// <summary>Connects the brain to its match and camera rig, and restricts it to the devices of 'scope' (pad = the gamepad for Scope.Pad).</summary>
        public void Bind(MatchManager m, PlayerCameraRig rig, DeviceScope scope, Gamepad pad)
        {
            match = m; cameraRig = rig; Scope = scope; Pad = pad;
            var list = new List<InputDevice>();
            if (scope == DeviceScope.Keyboard) { if (Keyboard.current != null) list.Add(Keyboard.current); if (Mouse.current != null) list.Add(Mouse.current); }
            if (scope == DeviceScope.Pad && pad != null) list.Add(pad);
            Controls.Gameplay.Disable();
            Controls.devices = scope == DeviceScope.Any ? (UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>?)null : new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(list.ToArray());
            if (isActiveAndEnabled) Controls.Gameplay.Enable();
        }

        /// <summary>True if this person is aiming with a gamepad (a lone player: whichever device was used last).</summary>
        public bool UsingGamepad => Scope == DeviceScope.Pad || (Scope == DeviceScope.Any && InputDevices.UsingGamepad);

        /// <summary>Which button names to show this person.</summary>
        public PromptStyle Style =>
            Scope == DeviceScope.Pad ? (Pad is DualShockGamepad ? PromptStyle.PlayStation : PromptStyle.Xbox)
            : Scope == DeviceScope.Keyboard ? PromptStyle.KeyboardMouse : InputDevices.Style;

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
                Vector2 v = Vector2.ClampMagnitude(UsingGamepad ? ApplyDeadZone(raw, stickDeadZone) : raw, 1f);
                // Turn "up on the stick" into "away from the camera": rotate by the view's yaw (0 / 180 in 3rd person).
                float yaw = cameraRig != null ? cameraRig.MoveYaw : 0f;
                Vector3 world = Quaternion.Euler(0f, yaw, 0f) * new Vector3(v.x, 0f, v.y);
                return new Vector2(world.x, world.z);
            }
        }

        /// <summary>Raw right-stick / mouse-delta value, for the camera rig's look and aim code.</summary>
        public Vector2 RawAim => Controls.Gameplay.Aim.ReadValue<Vector2>();

        /// <summary>The aim point before any assist (what the stick or cursor says).</summary>
        public Vector3 RawAimPoint => cameraRig != null ? cameraRig.AimPoint : new Vector3(0f, 0f, 4f);

        /// <summary>Where the hit will go: the raw aim, nudged toward a sensible target for a gamepad player.</summary>
        public Vector3 AimPoint
        {
            get
            {
                Vector3 raw = RawAimPoint;
                if (!aimAssist || !UsingGamepad || match == null || player == null || match.Rules == null || match.Practice) return raw;   // no assist on an empty practice court
                var c = new AssistCourt { halfWidth = match.HalfWidth, halfLength = match.HalfLength, side = player.CourtSide };
                int other = player.Team == 0 ? 2 : 0;                      // the opponents are players 2, 3 (or 0, 1)
                c.mate = match.Teammate(player).transform.position;
                c.opponentA = match.Players[other].transform.position;
                c.opponentB = match.Players[other + 1].transform.position;
                return AimAssist.Apply(raw, Upcoming(), c, true, true);
            }
        }

        // The hit this player is about to make: a serve, a queued or winding-up hit, or (button not pressed yet) what the next press would be.
        AssistHit Upcoming()
        {
            if (player.ServeMode) return AssistHit.Spike;
            var b = player.Ball;
            if (match.State != MatchState.Rally || b == null || !b.IsInFlight || player.IsBlocking) return AssistHit.None;
            HitType t = player.HasQueuedHit ? player.QueuedHitType : player.WindingUp ? player.WindUpType : player.IsGrounded ? HitType.Bump : HitType.Spike;
            switch (t)
            {
                case HitType.Spike: return AssistHit.Spike;
                case HitType.Set: return AssistHit.Set;
                case HitType.Bump: return player.TeamTouchCount != null && player.TeamTouchCount() == 0 ? AssistHit.Bump : AssistHit.None;   // first touch only
                default: return AssistHit.None;
            }
        }

        public ButtonState Jump => Read(Controls.Gameplay.Jump);
        public ButtonState Spike => Read(Controls.Gameplay.Spike);
        public ButtonState Bump => Read(Controls.Gameplay.Bump);
        public ButtonState Set => Read(Controls.Gameplay.Set);
        public ButtonState Dive => Read(Controls.Gameplay.Dive);
        public bool Sprint => Controls.Gameplay.Sprint.IsPressed();

        static ButtonState Read(InputAction a) =>
            new ButtonState(a.WasPressedThisFrame(), a.IsPressed(), a.WasReleasedThisFrame());
    }
}
