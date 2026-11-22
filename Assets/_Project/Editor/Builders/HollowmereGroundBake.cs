// Authority: none (editor build step). Ground-blend step: bakes the Hollowmere ground (SCGroundBaker, CPU) over the playable town and puts the SCGroundBlendBinder on the scene root.
// Called by HollowmereBuilder.BuildScene after every collider exists and before the combine/externalize/split passes. Needs no GPU, so it runs the same under -nographics.
// Bounds: the town wall ring (plateau about +-70 x, z -64..44), the lake shore, the Lower Gate road and the mill, i.e. every eye camera's ground: x -100..100, z -110..50 (200 x 160 m, about 0.39 x 0.31 m a texel at 512).
// Street colliders: only SquarePaving (a BoxCollider slab). The lanes and the square are Terrain cobble layers painted into the splat (no street mesh collider exists), so the terrain pass carries them.
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereGroundBake
    {
        public static readonly Vector2 Min = new Vector2(-100f, -110f), Size = new Vector2(200f, 160f);
        public const int Resolution = SCGroundBaker.DefaultResolution;
        public const string HeightPath = HollowmereTerrain.Folder + "/Hollowmere_GroundBake_Height.bytes";
        public const string AlbedoPath = HollowmereTerrain.Folder + "/Hollowmere_GroundBake_Albedo.bytes";

        public static void Run(Transform root, Transform set)
        {
            Terrain terrain = set.GetComponentInChildren<Terrain>(true);
            var streets = new System.Collections.Generic.List<Collider>();
            Transform sq = set.Find("SquarePaving");
            if (sq != null) { Collider c = sq.GetComponent<Collider>(); if (c != null) streets.Add(c); }
            SCGroundBaker.Result r = SCGroundBaker.Compute(terrain, streets, Min, Size, Resolution);
            SCGroundBaker.Write(r, HeightPath, AlbedoPath, out TextAsset height, out TextAsset albedo);
            var binder = root.gameObject.GetComponent<SCGroundBlendBinder>() ?? root.gameObject.AddComponent<SCGroundBlendBinder>();
            binder.HeightBytes = height; binder.AlbedoBytes = albedo; binder.Resolution = r.Resolution;
            binder.GroundBounds = r.GroundBounds; binder.GroundHeightParams = r.GroundHeightParams;
            binder.Bind();
            Debug.Log("SC GROUNDBAKE res " + r.Resolution + " bounds " + r.GroundBounds + " heightParams " + r.GroundHeightParams + " streetTexels " + r.StreetTexels
                + " heightHash " + Hash(r.HeightBytes) + " albedoHash " + Hash(r.AlbedoBytes));
        }

        static string Hash(byte[] b)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(b), 0, 6).Replace("-", "");
        }
    }
}
