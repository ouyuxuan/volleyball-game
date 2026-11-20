using UnityEngine;

namespace Sandspike.Art
{
    /// <summary>
    /// Gives the toon shader (Shaders/SandspikeToon.shader) its shadow colour from the Palette, so the hex value lives
    /// only in Palette.cs. Runs when the game starts, and in the Editor whenever scripts load, so the Scene view matches.
    /// </summary>
    public static class ToonGlobals
    {
        static readonly int ShadowTintId = Shader.PropertyToID("_SandspikeShadowTint");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        public static void Apply() => Shader.SetGlobalColor(ShadowTintId, Palette.Shadow);
    }
}
