// T24: the left-behind animals of Hollowmere's Ruined preset: 4 feral goats (churchyard, crag foot, a ruined Ring 2 lot, the orchard edge) and Tuppence the cat (the heroes' home sill and the
// fountain rim). Models are the local-AI pipeline's own (Hunyuan3D shape + TRELLIS.2 texture, UniRig skeleton, procedural clips; see Content/Art/Town/Animals/SOURCES.md), imported here as
// Generic rigs with one clip per take; AmbientAnimal mixes the clips at runtime. Built under Preset_Ruined/HM_AmbientLife_Ruined so the Rebuilt preset has none (farm animals return only
// when rescued, later). No colliders: spots are chosen where a capsule check finds nothing solid, and the walk legs are line-checked at hoof height. Determinism: spots come from a fixed
// anchor, a seeded hash and a fixed scan order. Authority: none (editor-time scene building; the runtime component states its own).
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereAnimals
    {
        const string Folder = "Assets/_Project/Content/Art/Town/Animals";

        sealed class Animal { public GameObject Model; public Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>(); }

        static Animal Load(string name, float smooth, string prefix = "animal_", bool rigged = true)
        {
            string dir = Folder + "/" + name, fbx = dir + "/" + prefix + name.ToLowerInvariant() + ".fbx";
            if (AssetImporter.GetAtPath(fbx) == null) { Debug.LogWarning("SC ANIMALS missing " + fbx); return null; }
            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            bool dirty = false;
            void Set<T>(T have, T want, Action apply) { if (!EqualityComparer<T>.Default.Equals(have, want)) { apply(); dirty = true; } }
            ModelImporterAnimationType at = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            Set(mi.animationType, at, () => mi.animationType = at);
            if (rigged) Set(mi.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, () => mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel);
            Set(mi.importAnimation, rigged, () => mi.importAnimation = rigged);
            Set(mi.animationCompression, ModelImporterAnimationCompression.Off, () => mi.animationCompression = ModelImporterAnimationCompression.Off);
            Set(mi.importCameras, false, () => mi.importCameras = false);
            Set(mi.importLights, false, () => mi.importLights = false);
            Set(mi.importBlendShapes, false, () => mi.importBlendShapes = false);
            Set(mi.optimizeGameObjects, false, () => mi.optimizeGameObjects = false);   // keep the bones: the head is looked up by name
            Set(mi.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, () => mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard);
            if (dirty) { mi.SaveAndReimport(); dirty = false; }

            Material mat = BeautyCornerArt.MaterialFromSet(dir, prefix + name.ToLowerInvariant(), "Animal_" + name);
            mat.SetTexture("_MetallicGlossMap", null); mat.DisableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", smooth);   // fur, not metal (TRELLIS ORM metal averages 0.35-0.45)
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);

            var names = new SortedSet<string>(StringComparer.Ordinal);
            var model0 = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>()) names.Add(m.name);
            foreach (Renderer r in model0.GetComponentsInChildren<Renderer>(true)) foreach (Material m in r.sharedMaterials) if (m != null) names.Add(m.name);
            foreach (var kv in mi.GetExternalObjectMap().ToArray()) if (kv.Key.type == typeof(Material) && !names.Contains(kv.Key.name)) { mi.RemoveRemap(kv.Key); dirty = true; }
            foreach (string n in names)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), n);
                if (!mi.GetExternalObjectMap().TryGetValue(id, out UnityEngine.Object cur) || cur != mat) { mi.AddRemap(id, mat); dirty = true; }
            }

            ModelImporterClipAnimation[] defs = rigged ? mi.defaultClipAnimations : null;
            if (defs != null && defs.Length > 0)
            {
                bool bar = defs.Any(c => c.takeName.Contains('|'));
                var next = new List<ModelImporterClipAnimation>();
                foreach (ModelImporterClipAnimation c0 in defs)
                {
                    if (bar && !c0.takeName.Contains('|')) continue;
                    ModelImporterClipAnimation c = c0;
                    int b = c.takeName.LastIndexOf('|'); c.name = b >= 0 ? c.takeName.Substring(b + 1) : c.takeName;
                    c.loopTime = c.loopPose = c.name == "Idle" || c.name == "Walk" || c.name == "Eat" || c.name == "Run";
                    c.wrapMode = c.loopTime ? WrapMode.Loop : WrapMode.Once;
                    c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = false;
                    c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                    next.Add(c);
                }
                next = next.OrderBy(c => c.name, StringComparer.Ordinal).ToList();
                var have = mi.clipAnimations;
                bool same = have != null && have.Length == next.Count && next.All(w => have.Any(h => h.takeName == w.takeName && h.name == w.name && h.loopTime == w.loopTime));
                if (!same) { mi.clipAnimations = next.ToArray(); dirty = true; }
            }
            if (dirty) mi.SaveAndReimport();

            var a = new Animal { Model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) };
            foreach (AnimationClip c in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()) if (!c.name.StartsWith("__preview__", StringComparison.Ordinal)) a.Clips[c.name] = c;
            Debug.Log("SC ANIMALS " + name + " clips: " + string.Join(",", a.Clips.Keys));
            return a;
        }

        public static void Build(Transform ruined, TownLayout t)
        {
            if (ruined == null) return;
            Physics.SyncTransforms();
            var holder = new GameObject(HollowmereAmbientBuilder.RootName + "_Animals").transform; holder.SetParent(ruined, false);
            Animal goat = Load("Goat", 0.18f), cat = Load("Cat", 0.22f);
            if (goat != null) BuildGoats(holder, goat, t);
            if (cat != null) BuildCat(holder, cat, t);
            Animal crow = Load("Crow", 0.35f, "bird_", false);
            if (crow != null) BuildCrows(holder, crow, ruined, t);
        }

        // ------------------------------------------------------------------------------------------------ goats
        static readonly (string Name, Vector2 Anchor, float Radius, int Count)[] GoatSpots =
        {
            ("Churchyard", new Vector2(-19.5f, 27.5f), 5.5f, 5),
            ("CragFoot",   new Vector2(-31.5f, 34.0f), 8.0f, 4),
            ("RingLot",    new Vector2(-40.5f, -4.0f), 5.0f, 4),
            ("OrchardEdge", new Vector2(38.5f, -49.5f), 5.0f, 4),
        };

        static void BuildGoats(Transform holder, Animal a, TownLayout t)
        {
            int seed = 501, placed = 0;
            foreach (var spot in GoatSpots)
            {
                List<Vector3> wps = FreeSpots(t, spot.Anchor, spot.Radius, spot.Count, 0.45f, seed);
                if (wps.Count < 2) { Debug.LogWarning("SC ANIMALS no free ground for goat at " + spot.Name + " (" + wps.Count + " spots)"); continue; }
                Spawn(holder, a, "Goat_" + spot.Name, AmbientAnimal.Kind.Goat, wps, seed++, 1.12f, 0.55f);
                placed++;
            }
            Debug.Log("SC ANIMALS goats placed: " + placed);
        }

        // Up to `count` ground points within `radius` of `anchor` (fixed scan order, seeded hash for the rank), with no solid collider within 0.45 m at body height and a clear line (at hoof height)
        // to the first one; spaced at least 1.6 m apart.
        static List<Vector3> FreeSpots(TownLayout t, Vector2 anchor, float radius, int count, float clearance, int seed)
        {
            var cand = new List<Vector3>();
            for (float dx = -radius; dx <= radius; dx += 0.75f)
                for (float dz = -radius; dz <= radius; dz += 0.75f)
                {
                    if (dx * dx + dz * dz > radius * radius) continue;
                    float x = anchor.x + dx, z = anchor.y + dz, y = SurfaceY(t, x, z);
                    if (float.IsNaN(y)) continue;
                    Vector3 p = new Vector3(x, y, z);
                    if (Physics.CheckCapsule(p + Vector3.up * (clearance + 0.12f), p + Vector3.up * 1.0f, clearance, ~0, QueryTriggerInteraction.Ignore)) continue;
                    // reject steep ground: slope from the height 0.75 m away
                    float s = Mathf.Max(Mathf.Abs(SurfaceY(t, x + 0.75f, z) - y), Mathf.Abs(SurfaceY(t, x, z + 0.75f) - y));
                    if (float.IsNaN(s)) continue;
                    if (s > (radius > 6f ? 0.65f : 0.5f)) continue;
                    cand.Add(p);
                }
            var picked = new List<Vector3>();
            foreach (Vector3 p in cand.OrderBy(p => SaltwakeRoadPlan.Hash01((int)(p.x * 13f + p.z * 53f), seed)))
            {
                if (picked.Any(q => (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).magnitude < 1.6f)) continue;
                if (picked.Count > 0 && Physics.Linecast(picked[0] + Vector3.up * 0.4f, p + Vector3.up * 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                picked.Add(p);
                if (picked.Count >= count) break;
            }
            return picked;
        }

        // The walkable surface at x,z: the highest upward-facing collider hit within 2 m of the layout ground (the terrain, a terrace top, a paved lane), NaN when there is none or only steep faces.
        static float SurfaceY(TownLayout t, float x, float z)
        {
            float g = HollowmereLayout.Ground(t, x, z), best = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, g + 2.5f, z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
                if (h.normal.y > 0.75f && h.point.y < g + 2f && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
            return best;
        }

        static void Spawn(Transform holder, Animal a, string name, AmbientAnimal.Kind kind, List<Vector3> wps, int seed, float scale, float walkSpeed)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(a.Model, holder);
            go.name = name;
            go.transform.position = wps[0];
            go.transform.rotation = Quaternion.Euler(0f, kind == AmbientAnimal.Kind.Cat ? 180f + (SaltwakeRoadPlan.Hash01(seed, 9) - 0.5f) * 40f : SaltwakeRoadPlan.Hash01(seed, 9) * 360f, 0f);   // Tuppence sits facing the lane (-z)
            go.transform.localScale = Vector3.one * scale;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = false;
            }
            var an = go.GetComponent<AmbientAnimal>() ?? go.AddComponent<AmbientAnimal>();
            an.Species = kind; an.Seed = seed; an.Waypoints = wps.ToArray();
            a.Clips.TryGetValue("Idle", out an.Idle); a.Clips.TryGetValue("Eat", out an.Graze); a.Clips.TryGetValue("Walk", out an.Walk);
            an.WalkSpeed = walkSpeed;
            an.Head = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr.name == "Head");
            if (an.Head == null) Debug.LogWarning("SC ANIMALS " + name + ": no Head bone");
            AnimationClip pose = kind == AmbientAnimal.Kind.Goat ? (an.Graze ?? an.Idle) : an.Idle;   // a settled pose for edit-mode captures (the runtime graph overrides it)
            if (pose != null) pose.SampleAnimation(go, pose.length * 0.35f);
            Bounds b = default; bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Debug.Log("SC ANIMALS " + name + " at " + wps[0] + " waypoints " + wps.Count + " bounds size " + b.size);
        }

        // ------------------------------------------------------------------------------------------------ perched crows
        // 3D crows (photo-textured, local AI pipeline) on the wall tops of the square's tall shells (F03, F05, F06): tops found by rays from above onto the lot's own colliders (as the roof pigeons),
        // each crow with 2 or 3 perch points on its top to hop between. Real size about 0.5 m beak to tail.
        static void BuildCrows(Transform holder, Animal a, Transform ruined, TownLayout t)
        {
            Transform lots = ruined.Find("Foundations");
            if (lots == null) return;
            int seed = 701, n = 0;
            foreach ((string id, int crows) in new[] { ("F03", 2), ("F05", 1), ("F06", 1) })
            {
                Transform lot = null;
                foreach (Transform c in lots) if (c.name.StartsWith(id + "_", StringComparison.Ordinal)) { lot = c; break; }
                if (lot == null) continue;
                Bounds? bb = null;
                foreach (Renderer r in lot.GetComponentsInChildren<Renderer>(true)) if (r is MeshRenderer) { if (!bb.HasValue) bb = r.bounds; else { var q = bb.Value; q.Encapsulate(r.bounds); bb = q; } }
                if (!bb.HasValue) continue;
                Bounds b = bb.Value;
                var tops = new List<Vector3>();
                for (float x = b.min.x + 0.4f; x < b.max.x; x += 0.8f)
                    for (float z = b.min.z + 0.4f; z < b.max.z; z += 0.8f)
                        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, b.max.y + 3f, z), Vector3.down, b.size.y + 6f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (!h.collider.transform.IsChildOf(lot) || h.normal.y < 0.8f || h.point.y < b.max.y - 1.5f) continue;
                            if (Physics.CheckSphere(h.point + Vector3.up * 0.3f, 0.12f, ~0, QueryTriggerInteraction.Ignore)) break;   // room for the bird
                            tops.Add(h.point); break;
                        }
                var picked = new List<Vector3>();
                foreach (Vector3 p in tops.OrderByDescending(p => p.y + 0.5f * SaltwakeRoadPlan.Hash01((int)(p.x * 9f + p.z * 31f), seed)))
                {
                    if (picked.Any(q => (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).magnitude < 2.2f)) continue;
                    picked.Add(p);
                    if (picked.Count >= crows) break;
                }
                foreach (Vector3 p in picked)
                {
                    // 3 perch points: this one and up to two others on the same top within 0.6-2.5 m, no more than 0.3 m lower or higher
                    var perches = new List<Vector3> { p };
                    foreach (Vector3 q in tops.OrderBy(q => SaltwakeRoadPlan.Hash01((int)(q.x * 7f + q.z * 17f), seed + 3)))
                    {
                        float d = (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).magnitude;
                        if (d < 0.6f || d > 2.5f || Mathf.Abs(q.y - p.y) > 0.3f || perches.Any(e => (new Vector2(e.x, e.z) - new Vector2(q.x, q.z)).magnitude < 0.5f)) continue;
                        if (Physics.Linecast(p + Vector3.up * 0.25f, q + Vector3.up * 0.25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        perches.Add(q);
                        if (perches.Count >= 3) break;
                    }
                    SpawnCrow(holder, a, "Crow_" + id + "_" + n++, perches, seed++);
                }
            }
            // the ruin dressing's flat-shaded Dress_Crow meshes on the chimney tops near the eye (within 60 m of the square) become textured crows on the same perch
            var dress = new List<Transform>();
            foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true)) if (tr.name == "Dress_Crow") dress.Add(tr);
            int replaced = 0;
            foreach (Transform d in dress.OrderBy(d => new Vector2(d.position.x, d.position.z).sqrMagnitude))
            {
                if (new Vector2(d.position.x, d.position.z).magnitude > 60f || replaced >= 10) break;
                Vector3 pos = d.position; float yaw = d.eulerAngles.y;
                UnityEngine.Object.DestroyImmediate(d.gameObject);
                SpawnCrow(holder, a, "Crow_Chimney_" + replaced++, new List<Vector3> { pos }, seed++, yaw);
            }
            Debug.Log("SC ANIMALS perched crows: " + n + " on shell tops, " + replaced + " replacing Dress_Crow of " + dress.Count);
        }

        static readonly Vector3 CrowFix = new Vector3(-90f, 0f, 0f);

        static void SpawnCrow(Transform holder, Animal a, string name, List<Vector3> perches, int seed, float yaw = float.NaN)
        {
            // an empty root carries AmbientPerchedBird (it sets the root's rotation); the model is its child, turned from the FBX's own axes (the bake exports nose-down, Z up) to nose +Z, Y up
            var root = new GameObject(name); root.transform.SetParent(holder, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(a.Model, root.transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(CrowFix); go.transform.localScale = Vector3.one;
            Bounds b = default; bool any = false;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds mb = mf.sharedMesh.bounds; Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
            }
            float len = Mathf.Max(b.size.x, b.size.z), scale = len > 1e-6f ? 0.5f / len : 1f;
            go.transform.localScale = Vector3.one * scale;
            float foot = -b.min.y * scale;   // pivot to the lowest point
            go.transform.localPosition = new Vector3(0f, 0f, 0f);
            var pts = perches.Select(p => p + Vector3.up * foot).ToArray();
            root.transform.position = pts[0];
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            var bird = root.AddComponent<AmbientPerchedBird>();
            bird.Perches = pts; bird.Seed = seed; bird.ModelYaw = 0f; if (!float.IsNaN(yaw)) bird.StartYaw = yaw;
            Debug.Log("SC ANIMALS " + name + " at " + pts[0] + " perches " + pts.Length + " model bounds " + b.size + " scale " + scale);
        }

        // ------------------------------------------------------------------------------------------------ Tuppence
        static void BuildCat(Transform holder, Animal a, TownLayout t)
        {
            var spots = new List<Vector3>();
            // 1. the heroes' home sill: the highest flat top within a metre or two of the door (12.5, 6.75), found by rays onto the colliders; a relaxed second pass when the strict one finds nothing
            if (TopNear(new Vector2(12.5f, 6.2f), 1.6f, 0.12f, 0.8f, true, t, out Vector3 sill) || TopNear(new Vector2(12.5f, 6.2f), 2.6f, 0.05f, 1.1f, false, t, out sill)) spots.Add(sill);
            else { float dy = SurfaceY(t, 13.1f, 6.3f); if (!float.IsNaN(dy)) spots.Add(new Vector3(13.1f, dy, 6.3f)); }   // the home's art has no collider on its step: the doorstep ground, 0.45 m off the door (door side faces -z)
            // 2. the fountain rim
            Vector3? rim = FountainRim(t);
            if (rim.HasValue && (spots.Count == 0 || !Physics.Linecast(spots[0] + Vector3.up * 0.35f, rim.Value + Vector3.up * 0.35f, ~0, QueryTriggerInteraction.Ignore))) spots.Add(rim.Value);
            if (spots.Count == 0) { Debug.LogWarning("SC ANIMALS Tuppence found no spot"); return; }
            Spawn(holder, a, "Tuppence", AmbientAnimal.Kind.Cat, spots, 601, 1.0f, 0.5f);
            Debug.Log("SC ANIMALS Tuppence spots: " + string.Join(" | ", spots.Select(s => s.ToString("F2"))));
        }

        static bool TopNear(Vector2 c, float r, float minAbove, float maxAbove, bool strict, TownLayout t, out Vector3 best)
        {
            best = default; float bestScore = float.MinValue; bool found = false;
            for (float dx = -r; dx <= r; dx += 0.2f)
                for (float dz = -r; dz <= r; dz += 0.2f)
                {
                    float x = c.x + dx, z = c.y + dz, g = HollowmereLayout.Ground(t, x, z);
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, g + maxAbove + 0.3f, z), Vector3.down, maxAbove + 0.4f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.normal.y < (strict ? 0.9f : 0.8f)) continue;
                        float above = h.point.y - g;
                        if (above < minAbove || above > maxAbove) continue;
                        // room for a sitting cat: nothing within 0.18 m at 0.1-0.4 m above the top
                        if (strict && Physics.CheckCapsule(h.point + Vector3.up * 0.3f, h.point + Vector3.up * 0.5f, 0.16f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        float score = -Mathf.Abs(dx) * 0.5f - Mathf.Abs(dz) + above * 0.5f;
                        if (score > bestScore) { bestScore = score; best = h.point; found = true; }
                    }
                }
            return found;
        }

        // The fountain has no collider in the scene yet: the rim top comes from its ring renderer's bounds (top of the bounds, 0.18 m in from the outer edge, on the side facing the home).
        static Vector3? FountainRim(TownLayout t)
        {
            Bounds? bb = null; string names = "";
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (tr.name != "FountainRing") continue;
                Bounds? tb = null;
                foreach (Renderer r in tr.GetComponentsInChildren<Renderer>(true)) if (r is MeshRenderer) { if (!tb.HasValue) tb = r.bounds; else { var q = tb.Value; q.Encapsulate(r.bounds); tb = q; } }
                if (tb.HasValue && tb.Value.size.x > 2.5f && tb.Value.size.x < 6f) { bb = tb; names += tr.name + " "; break; }
            }
            Debug.Log("SC ANIMALS fountain ring: " + (bb.HasValue ? bb.Value.ToString() : "none"));
            if (!bb.HasValue) return null;
            Bounds b = bb.Value;
            float rad = Mathf.Min(b.extents.x, b.extents.z) - 0.18f;
            Vector2 dir = new Vector2(0.55f, 0.83f).normalized;   // toward the home (+x, +z)
            return new Vector3(b.center.x + dir.x * rad, b.max.y, b.center.z + dir.y * rad);
        }

        static Bounds Grow(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
    }
}
