using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// Starts the visual-feedback objects (jump streak and dust, landing rings, hit bursts ...) when play starts, so the scene
    /// needs no edit, like AudioDirector. Each effect only draws: it reads the game and never changes it.
    /// </summary>
    public static class FeelFx
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Object.FindFirstObjectByType<JumpFx>() != null) return;
            var go = new GameObject("FeelFx");
            go.AddComponent<JumpFx>();
            go.AddComponent<BallFx>();
            go.AddComponent<SpikeChargeFx>();
            go.AddComponent<HitBurst>();
            go.AddComponent<BallSquash>();
            go.AddComponent<BotAimFx>();
        }

        static Texture2D softDot;

        /// <summary>A white disc with a soft edge, drawn once. Particles use it so puffs are round, not square.</summary>
        public static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 32;
            softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d)));
                }
            softDot.Apply();
            return softDot;
        }

        /// <summary>An unlit, alpha-blended particle material (tinted by the particles' own colour).</summary>
        public static Material ParticleMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetTexture("_BaseMap", SoftDot());
            return m;
        }
    }
}
