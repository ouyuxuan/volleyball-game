using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandspike.UI
{
    /// <summary>
    /// One place that turns keyboard and every gamepad into menu moves, once per frame (call Poll).
    /// Keyboard: arrows / WASD, Enter or Space = confirm, Esc = back and pause.
    /// Gamepad: D-pad or left stick, South = confirm, East = back, Start = pause. A stick push counts once until it returns to the middle.
    /// </summary>
    public class MenuInput
    {
        public int dx, dy;                     // -1 / 0 / +1: left-right and down-up (dy = -1 means down)
        public bool confirm, back, pause, esc;  // esc = the keyboard's Esc only

        readonly Dictionary<int, Vector2Int> stick = new Dictionary<int, Vector2Int>();   // last stick direction per gamepad

        public void Poll()
        {
            dx = dy = 0;
            confirm = back = pause = esc = false;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dx = -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dx = 1;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dy = 1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dy = -1;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) confirm = true;
                if (kb.escapeKey.wasPressedThisFrame) esc = back = pause = true;
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 s = pad.leftStick.ReadValue();
                var dir = new Vector2Int(s.x < -0.6f ? -1 : s.x > 0.6f ? 1 : 0, s.y < -0.6f ? -1 : s.y > 0.6f ? 1 : 0);
                stick.TryGetValue(pad.deviceId, out Vector2Int before);
                stick[pad.deviceId] = dir;
                if (pad.dpad.left.wasPressedThisFrame || (dir.x == -1 && before.x != -1)) dx = -1;
                if (pad.dpad.right.wasPressedThisFrame || (dir.x == 1 && before.x != 1)) dx = 1;
                if (pad.dpad.up.wasPressedThisFrame || (dir.y == 1 && before.y != 1)) dy = 1;
                if (pad.dpad.down.wasPressedThisFrame || (dir.y == -1 && before.y != -1)) dy = -1;
                if (pad.buttonSouth.wasPressedThisFrame) confirm = true;
                if (pad.buttonEast.wasPressedThisFrame) back = true;
                if (pad.startButton.wasPressedThisFrame) pause = true;
            }
        }
    }
}
