using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>One button's state this frame: just pressed, being held, just released.</summary>
    public struct ButtonState
    {
        public bool Down;
        public bool Held;
        public bool Up;

        public ButtonState(bool down, bool held, bool up) { Down = down; Held = held; Up = up; }
    }

    /// <summary>
    /// What a player "wants to do" this frame. PlayerController only talks to this interface,
    /// so a human (HumanBrain) and an AI (later) drive exactly the same controller.
    /// </summary>
    public interface IPlayerBrain
    {
        /// <summary>Where to walk, in WORLD space: x = world X, y = world Z. Length 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>The spot on the sand the player is aiming at (y = 0).</summary>
        Vector3 AimPoint { get; }

        ButtonState Jump { get; }
        ButtonState Spike { get; }
        ButtonState Bump { get; }
        ButtonState Set { get; }
        ButtonState Dive { get; }
    }
}
