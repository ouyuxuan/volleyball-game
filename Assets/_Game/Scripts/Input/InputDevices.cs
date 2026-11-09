using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace Sandspike.Controls
{
    /// <summary>Which family of device the player touched last.</summary>
    public enum InputKind { KeyboardMouse, Gamepad }

    /// <summary>Which button names to show on screen.</summary>
    public enum PromptStyle { KeyboardMouse, Xbox, PlayStation }

    /// <summary>
    /// Tracks the last-used device, so aiming and the on-screen button prompts switch live
    /// between keyboard + mouse and a gamepad. Starts itself; nothing to add to the scene.
    /// A DualShock/DualSense (DualShockGamepad) gets PlayStation names, other gamepads get Xbox names.
    /// </summary>
    public static class InputDevices
    {
        public static InputKind Kind { get; private set; } = InputKind.KeyboardMouse;
        public static PromptStyle Style { get; private set; } = PromptStyle.KeyboardMouse;
        public static bool UsingGamepad => Kind == InputKind.Gamepad;

        /// <summary>Raised when Kind or Style changes.</summary>
        public static event Action Changed;

        // A stick or mouse has to move this much (0..1 for sticks, pixels for the mouse) to count as "used".
        const float StickThreshold = 0.25f;
        const float MouseThreshold = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Kind = InputKind.KeyboardMouse;
            Style = PromptStyle.KeyboardMouse;
            Changed = null;
            InputSystem.onEvent -= OnInputEvent;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            InputSystem.onEvent -= OnInputEvent;   // never subscribe twice
            InputSystem.onEvent += OnInputEvent;
        }

        static void OnInputEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            try { Classify(eventPtr, device); }
            catch (InvalidOperationException) { /* an odd or invalid event: ignore it */ }
        }

        static void Classify(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr, InputDevice device)
        {

            // Look only at controls that changed in THIS event and were actuated past a threshold.
            // (Resting sticks and sensors send tiny changes all the time, so they are ignored.)
            if (device is Gamepad pad)
            {
                if (!AnyActuated(eventPtr, device, StickThreshold)) return;
                SetKind(InputKind.Gamepad, pad is DualShockGamepad ? PromptStyle.PlayStation : PromptStyle.Xbox);
            }
            else if (device is Keyboard)
            {
                if (AnyActuated(eventPtr, device, 0.5f)) SetKind(InputKind.KeyboardMouse, PromptStyle.KeyboardMouse);
            }
            else if (device is Mouse)
            {
                if (AnyActuated(eventPtr, device, MouseThreshold)) SetKind(InputKind.KeyboardMouse, PromptStyle.KeyboardMouse);
            }
        }

        static bool AnyActuated(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr, InputDevice device, float threshold)
        {
            foreach (var control in eventPtr.EnumerateChangedControls(device, threshold))
                return true;
            return false;
        }

        static void SetKind(InputKind kind, PromptStyle style)
        {
            if (kind == Kind && style == Style) return;
            Kind = kind;
            Style = style;
            Changed?.Invoke();
        }

        /// <summary>For tests and debugging: pretend a device family was used.</summary>
        public static void ForceKind(InputKind kind, PromptStyle style) => SetKind(kind, style);
    }
}
