// The Hollowmere ground as a Unity Terrain (terrain pass, 2026-10-31). Heights come from the layout (HollowmereLayout.Ground: the square's shelf, the
// keep terrace, the lane terraces, every lot pad, the wall bank and ditch, the lake basin, the stream ravine), written with TerrainData.SetHeights, so
// every pad and doorstep is exact. The open hillsides and lake banks may carry erosion from the official Terrain Tools package (com.unity.terrain-tools
// 5.3.3: Perlin noise, then its hydraulic and thermal eroders, fixed seed): Bake() runs those on the GPU in a graphics run and writes the result to a
// 16-bit RAW in the repo (RawPath); Build() blends that RAW in only where ProtectMask is 0, so pads, lanes, walls, the shelf, the bank and the ditch keep
// their designed heights. The build itself needs no GPU and runs the same with or without the RAW (without it the ground is the designed heights alone).
//   Bake:   bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereTerrain.Bake --out <scratch>
// The Terrain Tools eroders and noise are internal to that package, so Bake reaches them by reflection, pinned to 5.3.3 (Packages/manifest.json).
// Layers (wave 2, 2026-10-31): the B1 ground tileables meadow_grass, packed_soil and worn_path plus the B1 cobble under the lanes and the square, each with a
// mask map (height in B) so the terrain height-blends them; painted from the layout and the slope (Splat). Four layers: one terrain pass, height blend on.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereTerrain
    {
        public const int Res = 513, SplatRes = 512;
        public static readonly Vector3 Origin = new Vector3(-150f, -24f, -170f);
        public static readonly Vector3 Size = new Vector3(300f, 96f, 300f);   // y -24..72: Bell Hill reaches about +45 at the north edge (area 1 rework; was 44)
        const float RawOriginY = -24f, RawSizeY = 44f;   // the erosion RAW keeps its own height range (baked before the rework): metres = raw * RawSizeY + RawOriginY
        public const string Folder = "Assets/_Project/Content/Terrain/Hollowmere";
        public const string DataPath = Folder + "/Hollowmere_TerrainData.asset";
        public const string RawPath = Folder + "/Hollowmere_Eroded_513.r16";   // 16-bit little-endian, Res x Res, row 0 = south edge, column 0 = west edge
        const int Seed = 1031;
        const float NoiseMetres = 1.4f;   // peak-to-peak height of the Terrain Tools noise on the open ground before erosion

        static float WorldX(int i) => Origin.x + i * Size.x / (Res - 1);
        static float WorldZ(int j) => Origin.z + j * Size.z / (Res - 1);

        // ------------------------------------------------------------------------------------------------ heights
        // Normalised designed heights, [row = z, column = x] as TerrainData.SetHeights takes them.
        public static float[,] Designed(TownLayout t)
        {
            var h = new float[Res, Res];
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                    h[j, i] = Mathf.Clamp01((HollowmereLayout.Ground(t, WorldX(i), WorldZ(j)) - Origin.y) / Size.y);
            return h;
        }

        // 1 where the designed height must hold (the shelf and the keep, every lot and landmark with its yard, every lane corridor, the wall bank and the
        // ditch), 0 on the open hillsides, the lake banks and the ravine, with a soft 2-3 m edge.
        public static float ProtectMask(TownLayout t, float x, float z)
        {
            var p = new TownPoint(x, z);
            float e = HollowmereLayout.PlateauDistance(x, z), m = 0f;
            if (z < HollowmereLayout.KeepWallZ + 14f) m = Mathf.Max(m, 1f - Smooth(4.5f, 7f, e));          // the shelf, its terrace walls, the keep
            m = Mathf.Max(m, 1f - Smooth(1.5f, 3.5f, Mathf.Abs(e - 29.5f) - 4.5f));                           // the bank (26-29.5) and the ditch (31)
            foreach (Foundation f in t.Foundations) m = Mathf.Max(m, 1f - Smooth(1.0f, 3.5f, Outside(f.Footprint, p)));
            foreach (Landmark l in t.Landmarks) m = Mathf.Max(m, 1f - Smooth(1.0f, 3.5f, Outside(l.Footprint, p)));
            foreach (Lane lane in t.Lanes) m = Mathf.Max(m, 1f - Smooth(0.75f, 3.5f, HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f));
            return m;
        }

        static float Outside(Footprint fp, TownPoint p)
        {
            TownPoint l = fp.ToLocal(p);
            float ox = Mathf.Max(0f, Mathf.Abs(l.X) - fp.HalfX), oz = Mathf.Max(0f, Mathf.Abs(l.Z) - fp.HalfZ);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        // Designed heights with the baked erosion blended in where the mask allows (no RAW: the designed heights).
        // The RAW holds eroded heights in metres WITHOUT the two landform terms added after its bake (Q15 OuterFall, the area 1 rework's BackHill): both are added
        // back under the erosion, so the RAW keeps working as the open ground's erosion detail on any later landform.
        public static float[,] Final(TownLayout t, out bool eroded)
        {
            float[,] h = Designed(t);
            float[,] raw = ReadRaw();
            eroded = raw != null;
            if (raw == null) return h;
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                {
                    float x = WorldX(i), z = WorldZ(j);
                    float m = ProtectMask(t, x, z);
                    float erodedY = raw[j, i] * RawSizeY + RawOriginY + HollowmereLayout.OuterFall(x, z) + HollowmereLayout.BackHill(x, z);
                    h[j, i] = Mathf.Lerp(Mathf.Clamp01((erodedY - Origin.y) / Size.y), h[j, i], m);
                }
            return h;
        }

        static float[,] ReadRaw()
        {
            string abs = BuilderUtil.Abs(RawPath);
            if (!File.Exists(abs)) return null;
            byte[] b = File.ReadAllBytes(abs);
            if (b.Length != Res * Res * 2) { Debug.LogWarning("SC HOLLOWMERE terrain RAW has the wrong size, ignored: " + RawPath); return null; }
            var h = new float[Res, Res];
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                {
                    int k = (j * Res + i) * 2;
                    h[j, i] = (b[k] | (b[k + 1] << 8)) / 65535f;
                }
            return h;
        }

        // ------------------------------------------------------------------------------------------------ the terrain object
        public static Terrain Build(Transform parent, TownLayout t)
        {
            BuilderUtil.EnsureFolder(Folder);
            TerrainLayer[] layers = { Ground("Grass", "meadow_grass", 3f), Ground("Dirt", "packed_soil", 3f), Ground("Path", "worn_path", 2.5f), CobbleLayer() };
            layers[0].diffuseRemapMax = new Vector4(0.85f, 0.84f, 0.68f, 1f);   // area 1 rework round 3: was (0.94, 0.90, 0.76); the meadow read pale lime under the stronger town sun (lake view p50 .66, target .44-.50)   // a little less saturated green (round 3); B3: olive, less lime (blue pulled down)
            layers[2].diffuseRemapMax = new Vector4(0.82f, 0.78f, 0.70f, 1f);   // area 1 rework round 4: the worn-path layer (the road out of the Lower Gate, the lots) read pale cream under the town sun (lake view p50 .69, target .44-.50)
            EditorUtility.SetDirty(layers[2]);
            layers[1].normalScale = 1.8f;                                      // a coarser read on the earth banks (round 3)
            layers[1].tileSize = new Vector2(1.7f, 1.7f);                         // A8: more texels on the steep banks (was 3 m)
            EditorUtility.SetDirty(layers[0]); EditorUtility.SetDirty(layers[1]);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null) { data = new TerrainData(); AssetDatabase.CreateAsset(data, DataPath); }
            data.heightmapResolution = Res;
            data.size = Size;
            data.alphamapResolution = SplatRes;
            data.baseMapResolution = 512;
            data.terrainLayers = layers;
            float[,] h = Final(t, out bool eroded);
            data.SetHeights(0, 0, h);
            data.SetAlphamaps(0, 0, Splat(t, data));
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("SC HOLLOWMERE terrain res=" + Res + " eroded=" + (eroded ? "yes (" + RawPath + ")" : "no (designed heights; run HollowmereTerrain.Bake to add Terrain Tools erosion)"));

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Origin + new Vector3(0f, -0.06f, 0f);   // just under the square's paving slab (top at -0.02, where the corner's surround sat)
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 220f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return terrain;
        }

        static Material TerrainMaterial()
        {
            const string path = "Assets/_Project/Content/Materials/Hollowmere/M_HM_Terrain.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? throw new InvalidOperationException("URP Terrain/Lit shader not found");
            Material m = LookTestBuilder.Ensure(path, shader);
            // Height blend (needs at most 4 layers, all with mask maps): soil shows between grass clumps and grass creeps over the path grit. The shader GUI would set
            // these keywords from the layers in the editor; a batch build sets them here.
            m.SetFloat("_EnableHeightBlend", 1f); m.SetFloat("_HeightTransition", 0.25f); m.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            m.SetFloat("_EnableInstancedPerPixelNormal", 1f); m.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_MASKMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        // Splat weights per texel (grass, soil, path, cobble): cobble on the square, the keep top and the lanes; worn path in a 1.6 m band along the lane edges and
        // on the lots (trodden ground round the old footings); soil on steep banks (24-38 deg), in the dry ditch, on the lake shore and in patches on the lots;
        // grass on the open ground, with large dry patches (path) and a few bare spots (soil) from low-frequency noise so the 300 m meadow does not read as one tile.
        static float[,,] Splat(TownLayout t, TerrainData data)
        {
            var a = new float[SplatRes, SplatRes, 4];
            for (int j = 0; j < SplatRes; j++)
                for (int i = 0; i < SplatRes; i++)
                {
                    float u = (i + 0.5f) / SplatRes, v = (j + 0.5f) / SplatRes;
                    float x = Origin.x + u * Size.x, z = Origin.z + v * Size.z;
                    var p = new TownPoint(x, z);
                    float grass, soil = 0f, path = 0f, cobble = 0f;
                    float lane = LaneEdge(t, p, false);
                    float road = CountryRoad(t, p, out float roadOff, out float roadS);
                    if (lane < 0f || IsPavedArea(x, z)) { cobble = 1f; grass = 0f; }
                    else if (road < 0.6f)
                    {
                        // Q15: the Saltwake Road past the gate apron is a rutted dirt road: worn path, two soil ruts about 0.8 m either side of the
                        // crown, a thin grass strip on the crown, grass creeping in over the edges.
                        float n2 = Mathf.PerlinNoise(x * 0.35f + 5f, z * 0.35f + 9f) - 0.5f;
                        float ao = Mathf.Abs(roadOff);
                        soil = 0.9f * (1f - Smooth(0.25f, 0.55f, Mathf.Abs(ao - 0.85f) + 0.15f * n2));
                        float crown = 0.55f * (1f - Smooth(0.15f, 0.4f, ao + 0.2f * n2));
                        float edge = Smooth(-0.9f, 0.6f, road + 0.6f * n2);
                        grass = Mathf.Max(crown, edge);
                        path = Mathf.Max(0f, 1f - soil - grass);
                        soil *= 1f - grass;
                        grass = Mathf.Max(0f, 1f - soil - path);
                        // the hand-over from the gate's cobbles: a ragged edge over ~7 m, then scattered setts in the dirt for ~9 m more
                        float n3 = Mathf.PerlinNoise(x * 0.6f + 31f, z * 0.6f + 17f);
                        float c = 1f - Smooth(GateApron - 4f, GateApron + 3f, roadS + 5f * (n3 - 0.5f));
                        c = Mathf.Max(c, Smooth(0.6f, 0.68f, n3) * (1f - Smooth(GateApron + 1f, GateApron + 10f, roadS)) * (1f - Smooth(1.6f, 2.4f, ao)));
                        grass *= 1f - c; soil *= 1f - c; path *= 1f - c; cobble = c;
                    }
                    else
                    {
                        float steep = data.GetSteepness(u, v), y = data.GetInterpolatedHeight(u, v) + Origin.y;
                        float e = HollowmereLayout.PlateauDistance(x, z);
                        float n1 = Mathf.PerlinNoise(x * 0.11f + 13f, z * 0.11f + 7f) - 0.5f;          // ~9 m breakup of every edge
                        float big = Mathf.PerlinNoise(x * 0.035f + 41f, z * 0.035f + 3f);                 // ~30 m dry patches
                        soil = Mathf.Max(Smooth(24f, 38f, steep), 1f - Smooth(HollowmereLayout.WaterY + 0.3f, HollowmereLayout.WaterY + 1.8f, y));
                        soil = Mathf.Max(soil, 0.85f * (1f - Smooth(1.2f, 3.0f, Mathf.Abs(e - 31f))));     // the dry ditch floor and its sides
                        path = 1f - Smooth(0.3f, 1.6f, lane + 0.5f * n1);                                  // the lane edges
                        foreach (Foundation f in t.Foundations)
                            if (f.Footprint.Contains(p, 1.2f)) { path = Mathf.Max(path, 0.75f); soil = Mathf.Max(soil, Smooth(0.55f, 0.75f, big + n1)); break; }
                        foreach (Landmark l in t.Landmarks)
                            if (l.Footprint.Contains(p, 1.0f)) { path = Mathf.Max(path, 0.6f); break; }
                        path = Mathf.Max(path, 0.28f * Smooth(0.55f, 0.80f, big));                          // dry meadow patches
                        soil = Mathf.Max(soil, 0.35f * Smooth(0.62f, 0.75f, Mathf.PerlinNoise(x * 0.09f + 77f, z * 0.09f + 19f)) * (1f - Smooth(0.3f, 0.5f, big)));   // bare spots
                        // round 3: 40-70 m colour drift on the open lawn: yellow-green dry patches (path layer, <= 0.3) and darker worn patches (soil layer, <= 0.2), other seeds and scales
                        path = Mathf.Max(path, 0.3f * Smooth(0.45f, 0.85f, Mathf.PerlinNoise(x * 0.017f + 203f, z * 0.017f + 57f)));
                        soil = Mathf.Max(soil, 0.2f * Smooth(0.5f, 0.9f, Mathf.PerlinNoise(x * 0.022f + 311f, z * 0.022f + 149f)));
                        FieldPaint(t, x, z, ref soil, ref path);   // Q15: the patchwork outside the wall
                        // B3 meadow: outside the wall the grass layer is broken up by seeded noise, no fifth layer (height blend allows four): darker olive patches (soil weight
                        // mixed into the green), straw drift (path layer), a fine stipple, and bare earth along the lanes. Subtle: every weight stays under about 0.5.
                        float outside = Smooth(33f, 46f, e);
                        if (outside > 0f)
                        {
                            float olive = Mathf.PerlinNoise(x * 0.045f + 101f, z * 0.045f + 23f), straw = Mathf.PerlinNoise(x * 0.028f + 57f, z * 0.028f + 181f);
                            float stipple = Mathf.PerlinNoise(x * 0.31f + 9f, z * 0.31f + 71f), bare = Mathf.PerlinNoise(x * 0.21f + 3f, z * 0.21f + 8f);
                            soil = Mathf.Max(soil, outside * 0.34f * Smooth(0.52f, 0.78f, olive));
                            path = Mathf.Max(path, outside * 0.48f * Smooth(0.50f, 0.76f, straw));
                            soil = Mathf.Max(soil, outside * 0.14f * Smooth(0.62f, 0.86f, stipple));
                            soil = Mathf.Max(soil, outside * 0.45f * (1f - Smooth(1.2f, 5.5f, lane)) * Smooth(0.35f, 0.7f, bare));   // bare earth along the lanes
                        }
                        // Area 1 rework: Bell Hill is woodland. Under the trees the ground is leaf litter and bare earth with thin grass, in big soft patches,
                        // and the crag bank at its foot is earth and stone (the steep rule below adds the stone).
                        float hill = HollowmereLayout.BackHill(x, z);
                        if (hill > 0f)
                        {
                            float wood = Smooth(0.2f, 3f, hill);
                            float litter = Mathf.PerlinNoise(x * 0.06f + 211f, z * 0.06f + 87f);
                            // Round 2: the first build read as a bare tan slope (soil + the steep rule's cobble, stretched). Woodland floor is mostly grass and moss
                            // in the shade, with litter patches; the crag keeps more earth.
                            float cragFace = wood * (1f - Smooth(HollowmereLayout.CragHeight - 1f, HollowmereLayout.CragHeight + 4f, hill));
                            soil = Mathf.Max(soil, wood * (0.10f + 0.30f * Smooth(0.45f, 0.8f, litter)) + 0.35f * cragFace);
                            path = Mathf.Max(path, wood * 0.10f * Smooth(0.55f, 0.85f, Mathf.PerlinNoise(x * 0.09f + 5f, z * 0.09f + 311f)));
                        }
                        soil = Mathf.Clamp01(soil + 0.18f * n1);
                        path = Mathf.Clamp01(path) * (1f - soil);
                        grass = Mathf.Max(0f, 1f - soil - path);
                        // A8 (town surfaces): a raw earth slope stretched one low-res soil tile over 3 m. Steep ground takes half its weight in the cobble layer (grey rubble
                        // flecks in the earth, its own mask map) and the earth tile is smaller (layers[1] below), so banks read as stony earth, not a stretched brown sheet.
                        float rock = 0.65f * Smooth(22f, 36f, steep) * (1f - Smooth(0.2f, 1.2f, -lane));
                        rock *= 1f - 0.85f * Smooth(0.2f, 3f, hill);   // Bell Hill (28-34 degrees all over) is not a rubble bank: the cobble layer stretched there read as tiles
                        grass *= 1f - rock; soil *= 1f - rock; path *= 1f - rock; cobble = rock;
                    }
                    a[j, i, 0] = grass; a[j, i, 1] = soil; a[j, i, 2] = path; a[j, i, 3] = cobble;
                }
            return a;
        }

        // Signed distance (m) from the nearest lane's edge: negative inside a lane. Without countryRoad the Saltwake Road past the gate apron is left out
        // (it is painted as a dirt road, CountryRoad); IsPaved keeps it, so grass and foliage still stay off it.
        static float LaneEdge(TownLayout t, TownPoint p, bool countryRoad = true)
        {
            float d = float.MaxValue;
            foreach (Lane lane in t.Lanes)
            {
                if (!countryRoad && lane.Id == SaltwakeRoadPlan.RoadLane && CountryRoad(t, p, out _, out _) < 3f) continue;
                d = Mathf.Min(d, HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f);
            }
            return d;
        }

        public const float GateApron = 9f;   // the Saltwake Road stays cobbled about this far from the Lower Gate, then turns to dirt (blended over GateApron -4..+10)

        // Signed distance (m) from the Saltwake Road's edge from the cobble/dirt hand-over on (float.MaxValue nearer the gate); offset = signed distance from its
        // centre line, s = arc length from the gate.
        static float CountryRoad(TownLayout t, TownPoint p, out float offset, out float s)
        {
            offset = 0f; s = 0f;
            Lane road = HollowmereLayout.FindLane(t, SaltwakeRoadPlan.RoadLane);
            float best = float.MaxValue, run = 0f, bestS = 0f;
            for (int i = 1; i < road.Points.Length; i++)
            {
                TownPoint a = road.Points[i - 1], b = road.Points[i];
                TownPoint c = HollowmereLayout.ClosestOnSegment(p, a, b, out float tt);
                float d = TownPoint.Distance(p, c), len = TownPoint.Distance(a, b);
                if (d < best)
                {
                    best = d; bestS = run + tt * len;
                    TownPoint dir = (b - a) * (1f / len), rel = p - c;
                    offset = rel.X * dir.Z - rel.Z * dir.X;   // + right of travel
                }
                run += len;
            }
            s = bestS;
            if (bestS < GateApron - 4f) return float.MaxValue;
            return best - road.Width * 0.5f;
        }

        // The field patchwork (SaltwakeRoadPlan.Field) where the open ground allows it: hay fields dry yellow-green (path layer), ploughed fields soil in
        // furrows, a soil band under the hedgerows on the field edges; pasture stays meadow.
        static void FieldPaint(TownLayout t, float x, float z, ref float soil, ref float path)
        {
            float w = SaltwakeRoadPlan.FieldWeight(t, x, z);
            if (w <= 0f) return;
            FieldKind kind = SaltwakeRoadPlan.Field(x, z, out float edge, out int cell);
            float inField = Smooth(1.2f, 3.5f, edge);
            if (kind == FieldKind.Hay) path = Mathf.Max(path, w * inField * 0.62f);
            else if (kind == FieldKind.Ploughed)
            {
                float a = SaltwakeRoadPlan.FurrowYaw(cell) * Mathf.Deg2Rad;
                float across = x * Mathf.Cos(a) - z * Mathf.Sin(a);
                float furrow = 0.5f + 0.5f * Mathf.Sin(across * (2f * Mathf.PI / 2.4f));
                soil = Mathf.Max(soil, w * inField * (0.55f + 0.4f * furrow));
            }
            soil = Mathf.Max(soil, w * 0.55f * (1f - Smooth(0.6f, 1.6f, edge)));   // the hedge bottom
        }

        // The paved areas that are not lanes: the square (the corner's surround paving slab) and the keep terrace's top beside the stairs and gate.
        static bool IsPavedArea(float x, float z) =>
            (x >= HollowmereLayout.SquareMinX && x <= HollowmereLayout.SquareMaxX && z >= HollowmereLayout.SquareMinZ && z <= HollowmereLayout.SquareMaxZ)
            || (Mathf.Abs(x) < HollowmereLayout.KeepHalfWidth && z > HollowmereLayout.KeepWallZ - 0.2f && z < HollowmereLayout.KeepWallZ + 11f);

        public static bool IsPaved(TownLayout t, float x, float z) => IsPavedArea(x, z) || LaneEdge(t, new TownPoint(x, z)) < 0f;

        static TerrainLayer Layer(string name, Texture2D albedo, Texture2D normal, float tile, Texture2D mask = null)
        {
            string path = Folder + "/HM_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = albedo; layer.normalMapTexture = normal; layer.maskMapTexture = mask; layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.05f; layer.metallic = 0f;
            layer.maskMapRemapMin = Vector4.zero; layer.maskMapRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static TerrainLayer CobbleLayer()
        {
            const string dir = "Assets/_Project/Content/Textures/B1_town/cobble/";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "cobble_albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "cobble_normal.png");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "cobble_mask.png");
            if (albedo == null) throw new InvalidOperationException("B1 cobble tileable missing: " + dir);
            TerrainLayer l = Layer("Cobble", albedo, normal, 2f, mask);
            l.diffuseRemapMax = new Vector4(0.62f, 0.6f, 0.58f, 1f);   // the bare B1 albedo reads far brighter on the terrain shader than the corner's setts: bring the lanes down to them
            return l;
        }

        // A B1 ground tileable (sc_tex_batches.py B1 + sc_tex_to_unity.py) as a terrain layer: albedo, normal and the terrain mask map.
        static TerrainLayer Ground(string layerName, string material, float tile)
        {
            string dir = "Assets/_Project/Content/Textures/B1_town/" + material + "/" + material;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "_albedo.png") ?? throw new InvalidOperationException("B1 ground tileable missing: " + dir + "_albedo.png");
            return Layer(layerName, albedo, AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "_normal.png"), tile, AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "_mask.png"));
        }

        // ------------------------------------------------------------------------------------------------ the erosion bake (graphics run)
        public static void Bake()
        {
            TownLayout t = HollowmereLayout.Create();
            float[,] designed = Designed(t);
            Assembly tt = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.TerrainTools.Editor")
                ?? throw new InvalidOperationException("Terrain Tools (com.unity.terrain-tools) is not loaded");
            if (!SystemInfo.supportsComputeShaders) throw new InvalidOperationException("Terrain Tools erosion needs compute shaders; this run has none (use --graphics)");

            var desc = new RenderTextureDescriptor(Res, Res, RenderTextureFormat.RFloat, 0) { enableRandomWrite = true, sRGB = false };
            var heightRT = new RenderTexture(desc); heightRT.Create();
            var noiseRT = new RenderTexture(desc); noiseRT.Create();
            var outRT = new RenderTexture(desc); outRT.Create();
            try
            {
                // 1. Terrain Tools noise (NoiseSettings defaults: fractal Perlin), moved by the seed and scaled to about 40 m features.
                Type nsType = tt.GetType("UnityEditor.TerrainTools.NoiseSettings", true);
                var ns = ScriptableObject.CreateInstance(nsType);
                FieldInfo trf = nsType.GetField("transformSettings");
                object tr = trf.GetValue(ns);
                tr.GetType().GetField("translation").SetValue(tr, new Vector3(Seed * 0.37f, 0f, Seed * 0.61f));
                tr.GetType().GetField("scale").SetValue(tr, new Vector3(7.5f, 7.5f, 7.5f));
                trf.SetValue(ns, tr);
                tt.GetType("UnityEditor.TerrainTools.NoiseUtils", true).GetMethod("Blit2D", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { nsType, typeof(RenderTexture) }, null)
                    .Invoke(null, new object[] { ns, noiseRT });
                float[,] noise = Read(noiseRT);
                float mean = 0f; foreach (float f in noise) mean += f; mean /= noise.Length;

                // 2. Designed heights plus the noise on the open ground only.
                var start = new float[Res, Res];
                for (int j = 0; j < Res; j++)
                    for (int i = 0; i < Res; i++)
                    {
                        float m = ProtectMask(t, WorldX(i), WorldZ(j));
                        start[j, i] = designed[j, i] + (1f - m) * (noise[j, i] - mean) * NoiseMetres / Size.y;
                    }
                Write(heightRT, start);

                // 3. Terrain Tools hydraulic erosion (its thermal pass runs inside it), default settings.
                Type erType = tt.GetType("UnityEditor.TerrainTools.Erosion.HydraulicEroder", true);
                object eroder = Activator.CreateInstance(erType, true);
                var inputs = (System.Collections.Generic.Dictionary<string, RenderTexture>)erType.GetProperty("inputTextures").GetValue(eroder);
                inputs["Height"] = heightRT;
                Vector2 texel = new Vector2(Size.x / (Res - 1), Size.z / (Res - 1));
                erType.GetMethod("ErodeHeightmap").Invoke(eroder, new object[] { outRT, Size, new Rect(0, 0, Res, Res), texel, false });
                float[,] eroded = Read(outRT);

                // 4. Keep the designed heights where they must hold, write the RAW.
                var bytes = new byte[Res * Res * 2];
                float maxDelta = 0f;
                for (int j = 0; j < Res; j++)
                    for (int i = 0; i < Res; i++)
                    {
                        float x = WorldX(i), z = WorldZ(j);
                        float m = ProtectMask(t, x, z);
                        // stored in the RAW's own range, in metres without the fall and the back hill (Final adds both). NOT re-run since the area 1 rework changed Size.y.
                        float metres = Mathf.Lerp(eroded[j, i], designed[j, i], m) * Size.y + Origin.y;
                        float v = Mathf.Clamp01((metres - HollowmereLayout.OuterFall(x, z) - HollowmereLayout.BackHill(x, z) - RawOriginY) / RawSizeY);
                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(metres - (designed[j, i] * Size.y + Origin.y)));
                        ushort q = (ushort)Mathf.RoundToInt(v * 65535f);
                        int k = (j * Res + i) * 2; bytes[k] = (byte)(q & 0xff); bytes[k + 1] = (byte)(q >> 8);
                    }
                BuilderUtil.EnsureFolder(Folder);
                File.WriteAllBytes(BuilderUtil.Abs(RawPath), bytes);
                AssetDatabase.ImportAsset(RawPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("SC HOLLOWMERE terrain bake wrote " + RawPath + " max change vs designed=" + maxDelta.ToString("0.00") + " m");
            }
            finally
            {
                heightRT.Release(); noiseRT.Release(); outRT.Release();
            }
        }

        static float[,] Read(RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, false, true);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Color[] px = tex.GetPixels();
            UnityEngine.Object.DestroyImmediate(tex);
            var h = new float[rt.height, rt.width];
            for (int j = 0; j < rt.height; j++) for (int i = 0; i < rt.width; i++) h[j, i] = px[j * rt.width + i].r;
            return h;
        }

        static void Write(RenderTexture rt, float[,] h)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RFloat, false, true);
            var px = new Color[Res * Res];
            for (int j = 0; j < Res; j++) for (int i = 0; i < Res; i++) px[j * Res + i] = new Color(h[j, i], 0f, 0f, 1f);
            tex.SetPixels(px); tex.Apply();
            Graphics.Blit(tex, rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
