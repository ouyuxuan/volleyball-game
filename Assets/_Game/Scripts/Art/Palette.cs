using UnityEngine;

namespace Sandspike.Art
{
    /// <summary>
    /// The single source of every colour in the game (see docs/ART_BIBLE.md).
    /// Materials, lights, fog, sky, particles and UI must read from here.
    /// This is the ONLY file where colour literals (hex values) may appear.
    /// </summary>
    public static class Palette
    {
        // ---- The palette: one colour per job ----
        public static readonly Color Sand       = Hex("#F0D49A"); // warm sunlit sand: court, beach
        public static readonly Color CourtLines = Hex("#FFF6E6"); // warm white: boundary tape, net, UI text
        public static readonly Color Sea        = Hex("#1FB0C4"); // turquoise water behind the court
        public static readonly Color Sky        = Hex("#6EC6F0"); // bright daytime sky
        public static readonly Color TeamA      = Hex("#E8554A"); // coral red: team A
        public static readonly Color TeamB      = Hex("#3B5BDB"); // indigo: team B
        public static readonly Color Shadow     = Hex("#5B6C9E"); // cool dusk blue: shaded sides, ambient ground, vignette
        public static readonly Color Accent     = Hex("#FFC53D"); // sun yellow: ball, score pops, highlights

        // ---- Colours derived from the palette (still no new literals) ----
        public static readonly Color SunLight    = Color.Lerp(CourtLines, Accent, 0.15f); // warm key light, never pure white
        public static readonly Color SkyTop      = Color.Lerp(Sky, Shadow, 0.35f);        // deeper blue overhead
        public static readonly Color AmbientSky  = Color.Lerp(Sky, CourtLines, 0.25f);    // cool tint from above
        public static readonly Color AmbientMid  = Color.Lerp(Sand, Sky, 0.5f);           // neutral-ish equator
        public static readonly Color AmbientGround = Color.Lerp(Sand, Shadow, 0.45f);     // dark warm bounce from the sand

        // Converts "#RRGGBB" to a Color. Called once per field when the game starts.
        static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out var c);
            return c;
        }
    }
}
