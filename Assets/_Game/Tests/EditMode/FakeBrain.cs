using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>A plain-C# brain a test can set by hand: only the bump button matters for the hit tests.</summary>
    public class FakeBrain : IPlayerBrain
    {
        public ButtonState bump, jump, dive, spike;
        public Vector2 move;
        public Vector3 aim = new Vector3(0f, 0f, 4f);
        public bool sprint;

        public Vector2 Move => move;
        public Vector3 AimPoint => aim;
        public ButtonState Jump => jump;
        public ButtonState Spike => spike;
        public ButtonState Bump => bump;
        public ButtonState Set => default;
        public ButtonState Dive => dive;
        public bool Sprint => sprint;
    }
}
