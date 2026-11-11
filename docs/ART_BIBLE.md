# Sandspike art bible

1. **Style:** Bright, chunky low-poly beach volleyball: flat-faced shapes with soft shadows, saturated sunny colours, everything slightly rounded and readable from a distance.
2. **Palette** (the code copy lives in `Assets/_Game/Scripts/Art/Palette.cs`, the only place hex values may appear):

   | Role | Name | Hex |
   |---|---|---|
   | Court and beach | Sand | `#F0D49A` |
   | Boundary lines, net, UI text | CourtLines | `#FFF6E6` |
   | Water behind the court | Sea | `#1FB0C4` |
   | Daytime sky | Sky | `#6EC6F0` |
   | Team A | TeamA (coral red) | `#E8554A` |
   | Team B | TeamB (indigo) | `#3B5BDB` |
   | Shaded sides, ambient ground, vignette | Shadow (cool dusk blue) | `#5B6C9E` |
   | Ball, score pops, highlights | Accent (sun yellow) | `#FFC53D` |

   Character parts (skin tones, hair, shorts, waistband, eyes) are blends of these, defined in `Palette.cs`.

3. **Characters:** friendly stylized cartoon people with big heads, simple faces and normal limbs, in
   a coral or indigo shirt with navy shorts. The style target is `docs/concepts/concept-a-soft-toon.png`
   and `docs/concepts/player-ref-coral.png` (soft, rounded, toy-like). The models in the game are
   faceted low-poly (see `docs/CREDITS.md`), recoloured from the palette. They share the target's
   proportions and colours, but not its smooth surfaces.
4. **Mood:** Late-morning sun high and to the side, warm and a little golden; shadows fall cool blue (never black) and the sand bounces warm light back up. The ball (Accent) is the brightest, glowing thing in frame.
