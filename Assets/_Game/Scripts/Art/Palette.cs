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
        public static readonly Color AmbientMid  = Color.Lerp(Shadow, Sand, 0.25f);       // cool blue-grey equator (shadows stay blue, never green)
        public static readonly Color AmbientGround = Color.Lerp(Sand, Shadow, 0.45f);     // dark warm bounce from the sand

        // ---- M1 additions: more derived roles (each is a blend of the palette above) ----
        public static readonly Color Skin        = Color.Lerp(Sand, TeamA, 0.30f);        // player skin tone
        public static readonly Color PalmLeaf    = Color.Lerp(Sea, Accent, 0.40f);        // palm fronds: sea turquoise warmed with sun yellow
        public static readonly Color PalmTrunk   = Color.Lerp(TeamA, Shadow, 0.50f);      // warm dusty brown
        public static readonly Color NetBody     = Color.Lerp(CourtLines, Shadow, 0.35f); // the net mesh itself (the top tape is CourtLines)
        public static readonly Color SkyHorizon  = Color.Lerp(Sky, CourtLines, 0.55f);    // pale blue where the sky meets the sea
        public static readonly Color SandShade   = Color.Lerp(Sand, Shadow, 0.12f);       // slightly cooler sand around the court
        public static readonly Color UiPanel     = Shadow;                                // HUD panel backgrounds (use WithAlpha)
        public static readonly Color UiBarEmpty  = Color.Lerp(Shadow, TeamB, 0.35f);      // empty part of a meter

        public static readonly Color HitCue      = Color.Lerp(Accent, CourtLines, 0.35f); // "hit now" glow and ring: brighter than the ball's yellow
        public static readonly Color HitRing     = Color.Lerp(TeamB, Shadow, 0.3f);       // the "hit now" ring: dark enough to read on sand
        public static readonly Color BallOutline = Hex("#2A3558");                        // dark navy outline around the ball (inverted hull), reads on sand and sky

        // ---- Character models (CC0 low-poly people): every body part reads one of these ----
        public static readonly Color SkinDeep    = Color.Lerp(Skin, PalmTrunk, 0.55f);       // a second, deeper skin tone
        public static readonly Color HairDark    = Color.Lerp(PalmTrunk, BallOutline, 0.55f); // dark brown hair
        public static readonly Color HairLight   = Color.Lerp(Accent, PalmTrunk, 0.40f);      // sandy blond hair
        public static readonly Color Shorts      = Color.Lerp(BallOutline, Shadow, 0.20f);    // navy shorts, same for both teams
        public static readonly Color Belt        = Color.Lerp(CourtLines, Shadow, 0.15f);     // pale waistband
        public static readonly Color Eyes        = BallOutline;                               // dark eyes that read on any skin tone

        // ---- Feel pass: visual feedback ----
        public static readonly Color Dust        = Color.Lerp(Sand, CourtLines, 0.35f);   // dust puffs: sand, a little paler so they read on the court

        /// <summary>Same colour with a different alpha (so other files never need a Color literal).</summary>
        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // Converts "#RRGGBB" to a Color. Called once per field when the game starts.
        static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out var c);
            return c;
        }
    }
}
