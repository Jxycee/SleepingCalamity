// Presentation/Cellars/CellarFloorBuilder.Overlays.cs: the surface overlays of the Cellars floors (design dungeon1-cellars section 7, proposal P5, B8). Presentation-only, seeded per
// floor by a hash of (seed, floor, cell, salt), so every peer builds the same cards; no Random, no Time, no colliders. Driven by the floor's CellarFloorLook weights:
//  - Wet: damp-stain cards on the floor (C03 mud/damp set, darkened, glossier) on a share of cells = wet x 0.30; leak streaks down the walls (C03 water streak card) at wet x 0.10 per closed side.
//    The floor material itself is darkened and glossed by the look (Mats.Floor), the "darken albedo, raise smoothness" half of the rule.
//  - Moss: olive-yellow cards (C03 moss patches and clumps) at the wall feet, only inside a lamp pool (within MossLampReach of a lamp), at moss x 0.45 per closed side.
//  - Soot: the stone material is darkened by the look's soot weight (Mats.Stone), floors 2 (a trace), 6 and 7.
// Skipped for want of an adopted asset (say so in the B8 report): cobwebs (C04 is not in Adopted/), salt bloom on floor 4 crowns, flour tide line, chalk marks, drip emitters, puddle
// reflections. Roots stay with the Roots socket dressing (Art.cs). All quads sit 1.5 cm off the surface they cover.
// Authority: none (presentation of static data).
using SleepingCalamity.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.Presentation
{
    public sealed partial class CellarFloorBuild
    {
        public int Overlays;   // overlay cards placed (damp, leak, moss)
    }

    public static partial class CellarFloorBuilder
    {
        public const string OverlayGroup = "Overlays";
        public const float MossLampReach = 5.5f;
        static readonly Color MossOlive = new Color(0.62f, 0.66f, 0.26f), DampTint = new Color(2.6f, 2.3f, 2.0f), LeakTint = new Color(0.78f, 0.74f, 0.68f);

        // A deterministic 0-1 value from (seed, floor, cell, salt): a 32-bit integer mix.
        public static float Rand01(uint seed, int floor, int x, int y, uint salt)
        {
            uint h = seed * 747796405u + (uint)floor * 2891336453u + (uint)x * 73856093u + (uint)y * 19349663u + salt * 83492791u + 0x9E3779B9u;
            h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        static readonly System.Collections.Generic.Dictionary<(Material, int, string), Material> DecalCache = new System.Collections.Generic.Dictionary<(Material, int, string), Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDecalCache() => DecalCache.Clear();

        static Material Decal(Material src, Color tint, float smoothness, string tag, int floor)
        {
            var key = (src, floor, tag);
            if (DecalCache.TryGetValue(key, out Material cached) && cached != null) return cached;
            var m = new Material(src) { name = "CellarDecal_f" + floor + "_" + tag }; DecalCache[key] = m;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (smoothness >= 0f && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        static void Overlays(CellarFloorLayout layout, Grid grid, Transform root, CellarFloorKit kit, CellarFloorLookRow look, CellarFloorBuild result)
        {
            if (kit == null || (kit.DecalDamp == null && kit.DecalLeak == null && kit.DecalMoss == null && kit.DecalMossClump == null)) return;
            Material damp = kit.DecalDamp != null && look.Wet > 0f ? Decal(kit.DecalDamp, DampTint, 0.25f, "Damp", look.Floor) : null;
            Material leak = kit.DecalLeak != null && look.Wet > 0f ? Decal(kit.DecalLeak, LeakTint, -1f, "Leak", look.Floor) : null;
            Material moss = kit.DecalMoss != null && look.Moss > 0f ? Decal(kit.DecalMoss, MossOlive, -1f, "Moss", look.Floor) : null;
            Material clump = kit.DecalMossClump != null && look.Moss > 0f ? Decal(kit.DecalMossClump, MossOlive, -1f, "MossClump", look.Floor) : null;
            if (damp == null && leak == null && moss == null && clump == null) return;

            Transform group = Group(root, OverlayGroup);
            Transform lampGroup = root.Find(LampGroup);
            var lamps = new System.Collections.Generic.List<Vector3>();
            if (lampGroup != null) foreach (Transform l in lampGroup) lamps.Add(l.position);

            for (int y = 0; y < CellarFloorLayout.GridSize; y++)
                for (int x = 0; x < CellarFloorLayout.GridSize; x++)
                {
                    if (!grid.Occ[x, y]) continue;
                    Vector3 c = CellCenter(x, y);
                    string id = x + "_" + y;

                    if (damp != null && look.Salt <= 0f && Rand01(layout.Seed, layout.Floor, x, y, 1) < look.Wet * 0.22f)
                    {
                        float size = 1.1f + 0.7f * Rand01(layout.Seed, layout.Floor, x, y, 2), yaw = 360f * Rand01(layout.Seed, layout.Floor, x, y, 3);
                        Vector3 at = c + new Vector3((Rand01(layout.Seed, layout.Floor, x, y, 4) - 0.5f) * 1.6f, 0.015f, (Rand01(layout.Seed, layout.Floor, x, y, 5) - 0.5f) * 1.6f);
                        Card(group, "Damp_" + id, at, Quaternion.Euler(90f, yaw, 0f), size, size, damp, result);
                    }

                    bool lit = false;
                    if (moss != null || clump != null) foreach (Vector3 lp in lamps) { float dx = lp.x - c.x, dz = lp.z - c.z; if (dx * dx + dz * dz <= MossLampReach * MossLampReach) { lit = true; break; } }

                    for (int k = 0; k < 4; k++)
                    {
                        CellSide s = Sides[k];
                        if (EdgeOf(grid, x, y, s) != Edge.Closed) continue;
                        Vector3 d = SideDir(s); uint salt = 10u + (uint)k * 4u;
                        if (lit && Rand01(layout.Seed, layout.Floor, x, y, salt) < look.Moss * 0.45f)
                        {
                            bool small = Rand01(layout.Seed, layout.Floor, x, y, salt + 1) < 0.35f;
                            Material m = small ? (clump ?? moss) : (moss ?? clump);
                            float size = (small ? 0.7f : 1.3f) + 0.4f * Rand01(layout.Seed, layout.Floor, x, y, salt + 2), yaw = 360f * Rand01(layout.Seed, layout.Floor, x, y, salt + 3);
                            Vector3 along = Vector3.Cross(d, Vector3.up) * ((Rand01(layout.Seed, layout.Floor, x, y, salt + 2) - 0.5f) * 2.4f);
                            Card(group, "Moss_" + id + "_" + s, c + d * (CellSize * 0.5f - FaceDepth - 0.5f) + along + new Vector3(0, 0.02f, 0), Quaternion.Euler(90f, yaw, 0f), size, size, m, result);
                        }
                        if (leak != null && Rand01(layout.Seed, layout.Floor, x, y, salt + 100) < look.Wet * 0.10f)
                        {
                            Vector3 along = Vector3.Cross(d, Vector3.up) * ((Rand01(layout.Seed, layout.Floor, x, y, salt + 101) - 0.5f) * 2.0f);
                            Card(group, "Leak_" + id + "_" + s, c + d * (CellSize * 0.5f - FaceDepth - 0.025f) + along + new Vector3(0, 1.7f, 0), Quaternion.LookRotation(d, Vector3.up), 1.4f, 2.2f, leak, result);
                        }
                    }
                }
        }

        // One alpha-clip quad (no collider, no shadows); its visible side faces -Z, so the rotations above turn that side up (floor) or into the room (wall).
        static void Card(Transform parent, string name, Vector3 pos, Quaternion rot, float w, float h, Material mat, CellarFloorBuild result)
        {
            GameObject go = Prim(parent, name, PrimitiveType.Quad, pos, new Vector3(w, h, 1f), mat);
            go.transform.rotation = rot;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            result.Overlays++;
        }
    }
}
