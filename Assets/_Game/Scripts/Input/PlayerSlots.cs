using System;
using Sandspike.Players;

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

        // ---- per-bot difficulty: each slot can override the Setup option. 0 = Match (follow the option), 1..3 = Easy, Normal, Hard ----
        public static readonly string[] BotLevelNames = { "Match", "Easy", "Normal", "Hard" };
        readonly int[] botLevel = new int[Count];

        public int BotLevel(int slot) => botLevel[slot];
        public void CycleBotLevel(int slot, int dir = 1) => botLevel[slot] = (botLevel[slot] + dir + BotLevelNames.Length) % BotLevelNames.Length;

        /// <summary>The override for this slot's bot, or null to follow the Setup option.</summary>
        public AIDifficulty? BotOverride(int slot) => botLevel[slot] == 0 ? (AIDifficulty?)null : (AIDifficulty)(botLevel[slot] - 1);

        /// <summary>The difficulty the bot in this slot plays at: its own override, or the Setup option.</summary>
        public AIDifficulty DifficultyFor(int slot, AIDifficulty setupOption) => BotOverride(slot) ?? setupOption;

        /// <summary>The owner of every slot, to put back later (practice borrows the slots for one player).</summary>
        public int[] Snapshot() => (int[])owner.Clone();

        public void Restore(int[] saved) { Array.Copy(saved, owner, Count); }

        /// <summary>Empties every slot.</summary>
        public void Clear() { for (int i = 0; i < Count; i++) owner[i] = Empty; }

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
