using System;

namespace Sandspike.Controls
{
    /// <summary>
    /// Who sits in each of the four player slots (0, 1 = team A, 2, 3 = team B). Plain C#, no Unity types, so it is easy to test.
    /// A "device id" names one human: KeyboardId for keyboard + mouse (one person at most), or a gamepad's own device id.
    /// </summary>
    public class PlayerSlots
    {
        public const int Count = 4;
        public const int KeyboardId = 0;     // gamepads use InputDevice.deviceId, which is always above 0
        const int Empty = -1;

        readonly int[] owner = { Empty, Empty, Empty, Empty };

        public int Owner(int slot) => owner[slot];
        public bool IsHuman(int slot) => owner[slot] != Empty;
        public int SlotOf(int deviceId) => Array.IndexOf(owner, deviceId);
        public int HumanCount { get { int n = 0; foreach (int o in owner) if (o != Empty) n++; return n; } }

        /// <summary>A match needs at least one human.</summary>
        public bool CanStart => HumanCount > 0;

        /// <summary>Takes the first free slot. False if this device already joined, or all slots are taken.</summary>
        public bool Join(int deviceId)
        {
            int free = Array.IndexOf(owner, Empty);
            if (deviceId < 0 || SlotOf(deviceId) >= 0 || free < 0) return false;
            owner[free] = deviceId;
            return true;
        }

        public bool Leave(int deviceId)
        {
            int s = SlotOf(deviceId);
            if (s < 0) return false;
            owner[s] = Empty;
            return true;
        }

        /// <summary>Moves one step toward dir (-1 = left, +1 = right) to the next FREE slot; false if there is none that way.</summary>
        public bool Move(int deviceId, int dir)
        {
            int s = SlotOf(deviceId);
            if (s < 0 || dir == 0) return false;
            for (int t = s + dir; t >= 0 && t < Count; t += dir)
                if (owner[t] == Empty) { owner[t] = deviceId; owner[s] = Empty; return true; }
            return false;
        }
    }
}
