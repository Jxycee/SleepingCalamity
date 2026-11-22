// Presentation/Cellars/CellarFloorLook.cs: the per-floor look table of the Cellars (design dungeon1-cellars section 3, proposal P1, decided 2026-11-13). One row per floor 1-7: wall
// tint and limewash weight, lamp tint, ambient top and bottom, fog tint and density, breath (red glow scale), and the 0-1 overlay weights (wet, moss, roots, dust, soot, salt).
// A pure lookup: no Random, no scene access; floors outside 1-7 clamp to the nearest row. CellarFloorBuilder (lamps, fog, wall material variants, overlays) and
// CellarFloorTestBuilder (trilight ambient) read it. Every value is a starting point for the lighting corner (the design says so); hex values come straight from the section 3 table.
// Rule from PHASE1 3.1 that the rows keep: every shadow stays warm (no teal, no green) and nothing is pure black.
// Authority: none (static presentation data).
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class CellarFloorLookRow
    {
        public readonly int Floor; public readonly string Name;
        public readonly Color WallTint;        // multiplies the brick / wall material colour (a tint, so at most 1 per channel)
        public readonly float Limewash;        // 0-1: how far the wall material is pulled toward the limewash colour
        public readonly Color LampTint;        // the flame colour of this floor's lamps (blended into CellarArtTuning.LampGold)
        public readonly float LampScale;       // lamp intensity multiplier (the Wine Vaults are the brightest floor, the Coal Cellars sparse)
        public readonly Color AmbientTop, AmbientBottom;
        public readonly Color FogTint; public readonly float FogDensity;
        public readonly float Breath, Wet, Moss, Roots, Dust, Soot, Salt;   // 0-1 weights; Roots: 1 = thick at the entry; Dust 0.4 = the table's 40
        public CellarFloorLookRow(int floor, string name, Color wallTint, float limewash, Color lampTint, float lampScale, Color ambientTop, Color ambientBottom, Color fogTint, float fogDensity,
            float breath, float wet, float moss, float roots, float dust, float soot, float salt)
        {
            Floor = floor; Name = name; WallTint = wallTint; Limewash = limewash; LampTint = lampTint; LampScale = lampScale; AmbientTop = ambientTop; AmbientBottom = ambientBottom;
            FogTint = fogTint; FogDensity = fogDensity; Breath = breath; Wet = wet; Moss = moss; Roots = roots; Dust = dust; Soot = soot; Salt = salt;
        }
        public Color AmbientEquator => Color.Lerp(AmbientTop, AmbientBottom, 0.5f);
    }

    public static class CellarFloorLook
    {
        public const int FirstFloor = 1, LastFloor = 7;
        public const float LampTintWeight = 0.6f;   // how far a floor's lamp tint replaces the shared lamp gold

        // "#rrggbb" to a colour (sRGB bytes read as the 0-1 colour the table gives: the same convention as the town and corner values).
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length != 7) throw new System.ArgumentException("expected #rrggbb: " + hex);
            int v = System.Convert.ToInt32(hex.Substring(1), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static readonly CellarFloorLookRow[] Table =
        {
            //                       floor name                    wallTint                limewash lampTint        lampScale ambientTop        ambientBottom     fogTint                fogDens breath wet   moss  roots dust  soot  salt
            new CellarFloorLookRow(1, "Church Cellar",   C(0.90f, 0.88f, 0.82f), 0.65f, Hex("#F2D58A"), 1.50f, Hex("#3a2c26"), Hex("#2a1c18"), C(0.16f, 0.13f, 0.11f), 0.007f, 0.15f, 0.6f, 0.5f, 1.00f, 0.40f, 0.00f, 0f),
            new CellarFloorLookRow(2, "Cold Stores",     C(0.95f, 0.93f, 0.88f), 0.70f, Hex("#F2D58A"), 1.00f, Hex("#3a2e2a"), Hex("#2a1e1c"), C(0.15f, 0.14f, 0.13f), 0.009f, 0.20f, 0.5f, 0.3f, 0.35f, 0.30f, 0.20f, 0f),
            new CellarFloorLookRow(3, "Drowned Bakery",  C(0.52f, 0.43f, 0.41f), 0.00f, Hex("#E8A63C"), 0.80f, Hex("#34282a"), Hex("#241a1c"), C(0.14f, 0.12f, 0.12f), 0.011f, 0.25f, 1.0f, 0.2f, 0.00f, 0.20f, 0.00f, 0f),
            new CellarFloorLookRow(4, "Wine Vaults",     C(0.80f, 0.98f, 0.66f), 0.00f, Hex("#E8A63C"), 1.50f, Hex("#40302a"), Hex("#2e1e1a"), C(0.17f, 0.13f, 0.10f), 0.006f, 0.35f, 0.7f, 0.1f, 0.00f, 0.35f, 0.00f, 1f),
            new CellarFloorLookRow(5, "Cistern Dome",    C(0.74f, 0.70f, 0.66f), 0.00f, Hex("#E8A63C"), 1.00f, Hex("#3c2a26"), Hex("#2c1a18"), C(0.18f, 0.12f, 0.09f), 0.010f, 0.60f, 0.4f, 0.0f, 0.00f, 0.20f, 0.00f, 0f),
            new CellarFloorLookRow(6, "Coal Cellars",    C(0.46f, 0.42f, 0.40f), 0.00f, Hex("#D2562A"), 1.50f, Hex("#30221e"), Hex("#1e1412"), C(0.14f, 0.09f, 0.06f), 0.012f, 0.75f, 0.0f, 0.0f, 0.00f, 0.80f, 0.80f, 0f),
            new CellarFloorLookRow(7, "Stokehold",       C(0.30f, 0.20f, 0.17f), 0.00f, Hex("#D2562A"), 0.75f, Hex("#38201a"), Hex("#22120e"), C(0.18f, 0.09f, 0.05f), 0.012f, 1.00f, 0.0f, 0.0f, 0.10f, 0.90f, 1.00f, 0f),
        };

        public static int Count => Table.Length;

        // The row for a floor; a floor below 1 or above 7 (a test floor, a future one) clamps to the nearest row.
        public static CellarFloorLookRow For(int floor) => Table[Mathf.Clamp(floor, FirstFloor, LastFloor) - FirstFloor];
    }
}
