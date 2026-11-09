using System;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// A hold-to-charge meter (plain C#). The player has two: one for jump, one for spike.
    /// Charge rises from 0 to 1 over 'secondsToFull' while held, but never above the stamina cap.
    /// </summary>
    [Serializable]
    public class ChargeMeter
    {
        public float secondsToFull = 0.8f;

        public float Value { get; private set; }
        public bool IsCharging { get; private set; }

        public void Begin() { IsCharging = true; Value = 0f; }

        /// <summary>Call every frame while held. 'cap' is Stamina.MaxChargeAllowed.</summary>
        public void Tick(float dt, float cap)
        {
            if (!IsCharging) return;
            Value = Mathf.Min(Value + dt / Mathf.Max(0.01f, secondsToFull), cap);
        }

        /// <summary>Stop charging and return the charge reached.</summary>
        public float Release()
        {
            float v = Value;
            IsCharging = false;
            Value = 0f;
            return v;
        }

        public void Cancel() { IsCharging = false; Value = 0f; }
    }
}
