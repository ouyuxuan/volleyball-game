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

        float held, applied;   // for TickDelayed: seconds held, and how much of Value has been handed out

        public void Begin() { IsCharging = true; Value = 0f; held = 0f; applied = 0f; }

        /// <summary>
        /// Like Tick, but returns the charge gained that the caller may use now: nothing until the button has been
        /// held for 'grace' seconds (so a quick tap gets no boost), then everything built so far in one go, then each frame's gain.
        /// </summary>
        public float TickDelayed(float dt, float cap, float grace)
        {
            Tick(dt, cap);
            held += dt;
            if (held < grace) return 0f;
            float used = Value - applied;
            applied = Value;
            return used;
        }

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

        /// <summary>Stop charging but keep the value (a released wind-up keeps what it built).</summary>
        public void Lock() { IsCharging = false; }

        public void Cancel() { IsCharging = false; Value = 0f; held = 0f; applied = 0f; }
    }
}
