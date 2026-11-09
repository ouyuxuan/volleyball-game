using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Sandspike.Core
{
    /// <summary>
    /// Gives the player an anonymous Unity identity when the game starts.
    /// Runs automatically (no scene object needed), in the background: the game never
    /// waits for it, and if the player is offline we just log a warning and carry on.
    /// </summary>
    public static class ServicesBootstrap
    {
        // Unity calls this once after the first scene has loaded.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Begin()
        {
            try
            {
                // 1. Start Unity Services (skip if something already did).
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                // 2. Sign in without a password; Unity remembers this player on this device.
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                Debug.Log("[ServicesBootstrap] Signed in anonymously.");
            }
            catch (Exception e)
            {
                // Offline or service trouble must never break play.
                Debug.LogWarning($"[ServicesBootstrap] Could not sign in (continuing offline): {e.Message}");
            }
        }
    }
}
