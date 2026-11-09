using System;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// A player's stamina (plain C#, so it is easy to test).
    /// Charged jumps and charged spikes spend it in proportion to the charge, it refills over time
    /// (faster standing still, after a short delay) and low stamina lowers the maximum charge.
    /// </summary>
    [Serializable]
    public class Stamina
    {
        public float max = 100f;
        public float jumpCostMax = 25f;     // cost of a full-charge jump
        public float spikeCostMax = 20f;    // cost of a full-charge spike
        public float hitCostMax = 8f;       // cost of a full-charge bump or set
        public float regenMoving = 15f;     // per second while moving
        public float regenStill = 30f;      // per second while standing still
        public float regenDelay = 0.5f;     // seconds after the last spend before regen starts
        public float lowThreshold = 40f;    // below this, the charge cap shrinks
        public float capAtZero = 0.5f;      // max charge allowed at 0 stamina

        [NonSerialized] float current = -1f;   // -1 = "not set yet, start full"
        [NonSerialized] float delayLeft;

        public float Current
        {
            get { if (current < 0f) current = max; return current; }
        }
        public float Normalized => max <= 0f ? 0f : Current / max;

        /// <summary>The highest charge (0..1) the player may reach right now.</summary>
        public float MaxChargeAllowed
        {
            get
            {
                float s = Current;
                if (s >= lowThreshold) return 1f;
                return Mathf.Lerp(capAtZero, 1f, s / lowThreshold);
            }
        }

        public void Refill() { current = max; delayLeft = 0f; }

        public void SpendJump(float charge) => Spend(jumpCostMax * Mathf.Clamp01(charge));
        public void SpendSpike(float charge) => Spend(spikeCostMax * Mathf.Clamp01(charge));
        public void SpendHit(float charge) => Spend(hitCostMax * Mathf.Clamp01(charge));   // bump or set

        void Spend(float amount)
        {
            if (amount <= 0f) return;
            current = Mathf.Max(0f, Current - amount);
            delayLeft = regenDelay;
        }

        /// <summary>Call every frame. Regen only starts once the delay after the last spend has passed.</summary>
        public void Tick(float dt, bool isMoving)
        {
            float s = Current;
            if (delayLeft > 0f)
            {
                float used = Mathf.Min(dt, delayLeft);
                delayLeft -= used;
                dt -= used;
            }
            if (dt <= 0f) return;
            current = Mathf.Min(max, s + dt * (isMoving ? regenMoving : regenStill));
        }
    }
}
