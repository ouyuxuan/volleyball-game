using UnityEngine;

namespace Sandspike.Art
{
    public enum CourtTime { Day, Evening, Night }

    /// <summary>
    /// Changes the court's time of day with lighting and sky colours only (no objects, no gameplay).
    /// Day puts back exactly what the scene had the first time Apply ran, so the approved look never drifts.
    /// All the Evening and Night numbers are in the Table below: tune them there (the colours are in Palette.cs, "Court times").
    /// </summary>
    public static class CourtTimes
    {
        /// <summary>One time of day. sunSize below 0 = leave the sky's sun size alone.</summary>
        public struct Look
        {
            public Vector3 sunRotation; public Color sunColour; public float sunIntensity;
            public Color ambientSky, ambientMid, ambientGround;
            public Color skyTint; public float exposure, atmosphere, sunSize;
            public Color shade;     // the toon shader's shadow tint
        }

        // ---- THE NUMBERS: one entry per CourtTime, in enum order ----
        // Day only supplies the shade tint; everything else is read back from the scene (see Remember).
        public static readonly Look[] Table =
        {
            /* Day     */ new Look { shade = Palette.Shadow },
            /* Evening */ new Look
            {
                sunRotation = new Vector3(14f, 300f, 0f), sunColour = Palette.SunEvening, sunIntensity = 1.05f,
                ambientSky = Palette.AmbientEveningSky, ambientMid = Palette.AmbientEveningMid, ambientGround = Palette.AmbientEveningGround,
                skyTint = Palette.SkyEvening, exposure = 1.0f, atmosphere = 1.0f, sunSize = -1f, shade = Palette.ShadowEvening,
            },
            /* Night   */ new Look
            {
                sunRotation = new Vector3(50f, 300f, 0f), sunColour = Palette.MoonLight, sunIntensity = 0.75f,
                ambientSky = Palette.AmbientNightSky, ambientMid = Palette.AmbientNightMid, ambientGround = Palette.AmbientNightGround,
                skyTint = Palette.SkyNight, exposure = 0.35f, atmosphere = 0.4f, sunSize = 0.03f, shade = Palette.ShadowNight,
            },
        };

        static readonly int TintId = Shader.PropertyToID("_SkyTint"), ExposureId = Shader.PropertyToID("_Exposure"),
                            AtmosphereId = Shader.PropertyToID("_AtmosphereThickness"), SunSizeId = Shader.PropertyToID("_SunSize");

        // What the scene looked like before we touched it.
        static Light sun;
        static Quaternion sunRotation; static Color sunColour; static float sunIntensity;
        static Color ambientSky, ambientMid, ambientGround;
        static Material originalSky, skyCopy;

        /// <summary>The scene's main directional light: RenderSettings.sun, else the first directional light found.</summary>
        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) return l;
            return null;
        }

        static void Remember(Light s)
        {
            sun = s;
            sunRotation = s.transform.rotation; sunColour = s.color; sunIntensity = s.intensity;
            ambientSky = RenderSettings.ambientSkyColor; ambientMid = RenderSettings.ambientEquatorColor; ambientGround = RenderSettings.ambientGroundColor;
            if (RenderSettings.skybox != skyCopy) originalSky = RenderSettings.skybox;     // our own copy is never "the original"
        }

        /// <summary>Forgets the remembered scene look (tests use this so each starts fresh).</summary>
        public static void Forget()
        {
            if (skyCopy != null) Object.DestroyImmediate(skyCopy);
            sun = null; originalSky = skyCopy = null;
        }

        /// <summary>Sets the sun, ambient light, sky and toon shade tint for one time of day.</summary>
        public static void Apply(CourtTime t)
        {
            Light s = FindSun();
            if (s != null && s != sun) Remember(s);           // first call (or a new scene): remember the scene's own values
            Look look = Table[(int)t];
            bool day = t == CourtTime.Day;

            if (s != null)
            {
                s.transform.rotation = day ? sunRotation : Quaternion.Euler(look.sunRotation);
                s.color = day ? sunColour : look.sunColour;
                s.intensity = day ? sunIntensity : look.sunIntensity;
            }
            RenderSettings.ambientSkyColor = day ? ambientSky : look.ambientSky;          // ambient mode is Trilight: set the three colours directly
            RenderSettings.ambientEquatorColor = day ? ambientMid : look.ambientMid;
            RenderSettings.ambientGroundColor = day ? ambientGround : look.ambientGround;

            if (day) RenderSettings.skybox = originalSky;     // the asset itself is never changed
            else if (originalSky != null)
            {
                if (skyCopy == null) skyCopy = new Material(originalSky) { name = originalSky.name + " (court time)" };
                skyCopy.SetColor(TintId, look.skyTint);
                skyCopy.SetFloat(ExposureId, look.exposure);
                skyCopy.SetFloat(AtmosphereId, look.atmosphere);
                skyCopy.SetFloat(SunSizeId, look.sunSize >= 0f ? look.sunSize : originalSky.GetFloat(SunSizeId));
                RenderSettings.skybox = skyCopy;
            }
            ToonGlobals.SetShade(look.shade);
            DynamicGI.UpdateEnvironment();                    // the sky's reflections follow the new sky
        }
    }
}
