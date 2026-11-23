# AetherStrand v2.1.0: integration feedback for Neon (hair-aetherstrand trial, 2026-11-22)

What we found while bringing the plugin into Sleeping Calamity (Unity 6000.6.5f1, URP 17.6, PC renderer = Forward+). The coordinator relays this.
The plugin is used as shipped. Where we had to work around something, it is said below, and the workaround lives in our code, not in the package.

## Blocker (found in the Unity capture run)
0. **The shader does not compile on URP 17.6:** `Shader error in 'AetherStrand/SculptedHair_URP': redefinition of 'ShiftTangent' at Packages/com.aetherstrand.sculptedhair/Shaders/AetherStrand_Hair.shader(159) (on dx11)`. URP's included shader library already defines `ShiftTangent`, so the hair renders magenta. Rename it (e.g. `AS_ShiftTangent`), and keep every helper in an `AS_` namespace prefix. The compile-only gate did not catch this: it showed up only when the shader compiled for a graphics device.

## Audit numbers with the shipped defaults (Tobin, MetaPerson head, 44 chains)
- With AutoSetupPhysics' own head-height estimate (H clamped to 0.28 m, so the cranium sphere has r 0.118 m): `chains 44 penetrating 0 kink 150.3 deg stretch 242.05 % tipSway 10.40 cm` after 90 frames. Head-turn snap: `kink 140.4 stretch 242.32 % tipSway 15.96 cm`. The oversized colliders push the scalp-hugging nodes out, and the root node is not projected, so segment 1 stretches. That is the heuristic in item 8.
- With H set to 0.23 m (cranium r 0.097 m), margin 11 mm: `kink 150.5 deg stretch 76.76 % tipSway 3.95 cm`. Snap: `kink 96.4 stretch 76.68 % tipSway 9.71 cm`.
- Same, margin 3 mm (our Tobin preset): `penetrating 0 kink 150.5 deg stretch 52.46 % tipSway 3.42 cm`. Snap: `kink 96.4 stretch 52.15 % tipSway 9.47 cm`. Settled: `kink 151.7 stretch 52.76 %`.
- **The kink limit fails at rest, before any simulation.** We measured the exported rig in Blender: Hair_Rear_19 already bends 155.5 deg between bones, and 35 of 44 chains bend more than 28 deg. Two causes:
  (a) `maxKinkAngleDeg` is the absolute angle between segments. A curved or spiky groom always has large rest angles, so measure the change from the rest angle instead.
  (b) The generator averages the member locks' spines per cluster (item 19). Locks that leave nearby roots in different directions (the crown, the back whorl) average into a folded chain. Cluster on root plus tip direction, or give each lock its own chain.
- The stretch error stays high (52-77 %) while the bone lengths themselves never change (rotation-only write-back, 0.00 % measured on bones). So the error is in the sim nodes. It is most likely the folded chains and the unprojected root node fighting the collider push in segment 1.
- For comparison, the same bone measure on our current HairDeck spring chains (wind 0): tip sway 0.30 cm idle, 0.93 cm at the end of a 60 deg / 0.1 s head turn, stretch 2.6 % idle. The AetherStrand chains with wind 0: tip sway 2.96 cm idle (max 12 cm), 7.2 cm after the turn. They do not hold the rest shape: gravity 4.4 against stiffness 0.62 lets the long rear chains sag.

## Packaging
1. No `.asmdef` files. We added `AetherStrand.Runtime` and an editor-only `AetherStrand.Editor`. Please ship them.
2. The texture import settings are not in the package (no `.meta` files): GrooveAO, FlowIDShift and AlphaCard need linear + Repeat, Normal needs to be a normal map. We set them with an AssetPostprocessor. Ship `.meta` files with these settings.
3. The Blender add-on sits outside the Unity package. That's fine. We vendored it unmodified under `Art/Blender/tools/experimental/tobin_hair/aetherstrand/vendor/`.

## Editor API (`AetherStrandSetupWindow`)
4. **`GetComponent<T>() ?? AddComponent<T>()` never adds in the editor.** When a component is missing, the editor's `GetComponent` returns Unity's fake-null object, which C# `??` treats as non-null. `AutoSetupPhysics` then gets a dead `AetherStrandPhysics`, and `AddOrUpdateCollider` gets a dead `AetherStrandCollider` on every new collider object, so the first property write throws `MissingComponentException`. Use `var c = go.GetComponent<T>(); if (c == null) c = go.AddComponent<T>();`. Our workaround: the builder pre-creates the physics component and the six `AS_Col_*` objects with their colliders before it calls `AutoSetupPhysics`.
5. **The bone prefix match is case-insensitive** (`StartsWith(bonePrefix, OrdinalIgnoreCase)`). `"Hair_"` also matches the `hair_<k>_<i>` bones of other hair rigs, so two solvers end up on the same bones whenever two rigs share a root. Match with `Ordinal`, or let the caller pass a comparison.
6. **Collider offsets and radii are world metres applied in head-bone local space.** They assume local +Y is up, +Z is forward and the scale is 1. Then `AetherStrandCollider` multiplies by `lossyScale` again (`radius * scale`). On a rig whose head bone is scaled, or whose axes are rotated, the colliders land in the wrong place or at the wrong size. Nothing checks for this: if the humanoid lookup fails, you get zero colliders and the audit still reports `penetratingNodes == 0`. Suggestions: compute offsets in world space and convert them with `InverseTransformPoint`/`InverseTransformVector`, divide out `lossyScale`, and make `ComputeAudit` report `colliderCount` (or fail when it is 0).
7. `TryBindTexture` finds textures through `AssetDatabase.FindAssets(name)` across the whole project. Any asset with the same name (a copy, an older version) can be bound instead. Load by package path (`Packages/com.aetherstrand.sculptedhair/Textures/...`).
8. `H = clamp(neckToHead * 2.5, 0.18, 0.28)` is a heuristic. MetaPerson's head bone sits at the skull base, so the result is whatever the clamp allows. Let the caller pass the head mesh bounds instead.

## Runtime (`AetherStrandPhysics`)
9. **Fallback wind is always on** (`fallbackWindStrength` 0.55 when no wind zone is set), so hair sways indoors and in caves. Default it to 0, or only apply it when a wind zone exists. The audit's `meanTipSwayCm` 0.2-5 cm band also depends on that wind: a still head with no wind should be allowed close to 0.
10. `ClassifyZone` falls back to position thresholds in head-bone local space (z > 0.03 is the front, y > 0.14 is the crown). These assume the head bone origin sits at the neck base and the axes are Y-up, Z-forward. Names cover it when you use the generator's names.
11. Determinism: `Simulate(dt, simTime)` is deterministic for a fixed step, as stated. `LateUpdate` uses `Time.deltaTime`, so play itself is frame-rate dependent within the substeps (expected; just noting it).

## Shader (`AetherStrand/SculptedHair_URP`)
12. **Forward+ lights.** URP 17.1+ renamed the Forward+ keyword to `_CLUSTER_LIGHT_LOOP` (`_FORWARD_PLUS` is the old name). Also, the additional-light block is gated on `#if defined(_ADDITIONAL_LIGHTS)`, which Forward+ never sets. So under Forward+ (our PC renderer), point and spot lights such as torches do not reach the hair at all. Use `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP` and gate the loop on `defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)` (see URP's Lit forward pass). Measured result: see torch.txt in the trial folder.
13. `ShadowCaster` handles directional lights only (`_LightDirection`). It has no `_CASTING_PUNCTUAL_LIGHT_SHADOW` / `_LightPosition` path, so point and spot light shadows from the hair are wrong.
14. There are no `_ADDITIONAL_LIGHT_SHADOWS`, `_LIGHT_LAYERS` or `_SHADOWS_SOFT_LOW/MEDIUM/HIGH` variants (URP 17 splits soft shadow quality).

## Blender generator (`aetherstrand_hair_cards.py`)
15. **Root rings are skinned to chain bone 1, not the head.** `vg_idx = max(1, min(k, ...))` puts ring 0 (the scalp end) on the first deforming bone, so when that bone swings, the roots lift off the scalp. Ring 0 should follow the non-deforming bone 0 or the Head. Our workaround: a post step re-weights ring 0 to Head (`tobin_aetherstrand.py`, `--no-root-fix` turns it off).
16. Skin weights are hard (1.0 per ring, one bone each), so the mesh creases at every ring. Blend neighbouring bones (for example 0.5/0.5 at ring boundaries).
17. Lock resolution is tied to the bone count: a lock has `bones_per_chain + 1` rings (6 by default). Long curved locks read as straight segments, and the tips end blunt. Resample the ring count separately from the bones (for example 12-16 rings mapped onto 5 bones).
18. The rig is always built at the world origin (`Head` bone at 0,0,0), and the lock coordinates assume a head whose bone sits at the origin with +Y forward. A head anywhere else needs the caller to move everything. Use the active head's position, or an explicit head origin.
19. A cluster's bones follow the averaged spine of its member locks. Locks far from that average are skinned to bones several centimetres away, so they swing around a distant pivot.
20. The export operator sets neither `path_mode='STRIP'` nor axes. We export with our own call (`axis_forward='-Z', axis_up='Y', FBX_SCALE_ALL`, linear colours, STRIP).

## v3.0.0 intake (2026-11-22): status of the items above
Fixed in v3 (verified by reading the code):
- 0: `AS_ShiftTangent`. 1: asmdefs shipped. 2: texture metas shipped (linear, Repeat, Normal as a normal map).
- 4: `GetComponent` plus an explicit null check. 5: the bone prefix uses `Ordinal`. 6: colliders are computed in world space and converted per bone. `colliderCount` is in the audit.
- 7: textures load by package path first. 9: fallback wind defaults to 0. 12-14: `_CLUSTER_LIGHT_LOOP`, light layers, additional-light shadows, soft-shadow quality variants and punctual shadow casting.
- 15/16: the root ring is pinned to Head, with blended weights along the lock. 17: lock rings follow the preset points (12 rings) and are no longer tied to the bone count. 20: the export operator sets STRIP and the axes.

Still open:
- 3/kink: `maxKinkAngleDeg` is still the absolute angle between segments. A curved groom fails it at rest; see the v3 audit numbers below.
- 8: the `headMeshWorldBounds` path uses `SkinnedMeshRenderer.bounds`-sized input. On Tobin, in the editor, those bounds are 0.28 m wide (inflated), which gives cranium r = 0.84 × min extent = 0.118 m again. We pass `headHeightOverride` 0.23 m instead. Suggest computing the bounds from the mesh vertices (`sharedMesh.bounds` transformed), or document that callers must pass tight bounds.
- 10: the zone fallback still uses head-local metre thresholds (`DetectZone`). Names cover it.
- 18: the rig is still built at the origin. The shipped `PresetGeometry.json` is in viewport units (roots span about 0.54 units ear to ear; Tobin's scalp is about 0.17 m). To use it on a real character, the caller has to scale, turn and place it. We fit it per axis from the root clouds (x 0.317, y 0.433, z 0.375): the preset's skull is narrower and deeper than Tobin's, so a uniform scale left the sides jutting out. The roots then needed a median 17 mm (p90 40 mm) snap onto Tobin's scalp. Suggest: export in metres with a head-origin convention, or ship the scalp the locks were authored on, so a fit can be checked.
- 19: k-means still averages member spines per cluster. Check folded chains after the v3 audit.
- Shipped `ExportedLook.json` colours are neutral dark grey (#090a0c / #25272b / #4c5057), not the chestnut in the shared preview image. For the A/B we kept the shipped shader and physics values and set Tobin's chestnut root/base/tip colours.

## v3.0.0 results on Tobin
- Audit (H 0.23 m, shipped physics): `colliders 6 penetrating 0 kink 68.8 deg stretch 58.82 % tipSway 1.53 cm`. Head-turn snap (60 deg in 0.1 s): `tipSway 18.33 cm`, settled +0.9 s `12.21 cm`. Sim-node stretch stays at 56-59 % while bone lengths never change, so the solver's nodes drift from the bones.
- Motion: in a 60 deg / 0.1 s head turn and a 5 m/s run stopped in 0.15 s, the locks fly 18-28 cm off the head and are still 6 cm out after 1.2 s. The response is far too loose for a hero: head inertia 0.84 against the tether, and stretch never recovers. Our spring chains on the same head move 0.9 cm and settle in 0.5 s.
- Look: with the shipped preset fitted to Tobin, the locks read as flat ribbons, and the fringe covers the eyes. The shading takes point lights well.

## v2.5 intake (Neon's rebuild, round 2 of the trial, 2026-11-22 evening)
- `package.json` says 2.5.0, lower than the installed 3.0.0, so UPM treats it as a downgrade. We set it to `3.1.0-neon`. Please version newer builds upward.
- The `.meta` files for .cs, .shader, .asmdef and .json are missing (the texture metas are present). Unity generates them; we keep the v3 metas where the file names match.
- The ids disagree: `PresetGeometry.json` and `ExportedLook.json` say presetId `parted_shag_medium` with presetName "Layered Swept Long", and the README/playbook say `layered_swept_long`. The geometry is the same 430-lock layout as v3's (same lock count and zones); only the header changed.
- The playbook writes the material to `Assets/AetherStrand_layered_swept_long_Mat.mat` at the project root, and `CreateMaterialAsset` defaults to `Assets/AetherStrand_Hair_Mat.mat`. Default to a folder the caller passes. We write ours under Content/Characters/TobinBase/AetherHair.
- New `AetherStrandMeshGenerator.GenerateAndAttach`: we use it, since it builds the mesh and the Hair_ chains under the Head bone with no Blender round trip.
  - It maps the preset into head-bone local space with a uniform auto-normalise (crown at 0.23 m). So it assumes the head bone's axes are Y-up, Z-forward and its origin sits at the preset pivot, and it can't fit a wider or narrower skull (Tobin's root cloud is about 0.73x the preset's x/y ratio).
  - It creates new GameObjects on every call. A builder that re-runs it makes new fileIDs each time, which is not byte-stable; we call it only when the prefab is first built.
- `ExportedLook.json` colours are now dark brown (#1e1815 / #3a2f28 / #54463c). We keep Tobin's chestnut for colour parity with round 1.

## v2.5 results on Tobin (round 2)
- In-Unity generator, defaults (scale 1, no offset): 430 locks, 54,180 tris, 220 Hair_ bones. The hair sits 3-8 cm high and back: the forehead and front scalp are bald (ab_v25/stills_unfitted.jpg).
- Fitted with the generator's own knobs (overallScale 1.2, headLocalOffset (-0.005, -0.064, -0.002), from the root clouds): the back and sides cover, but the forehead stays bald. The fringe roots land inside the skull, and nothing projects roots onto the scalp. **Ask:** add a scalp projection to `GenerateAndAttach` (raycast or closest point onto the head mesh, like the v2.1 Blender BVH step), or a non-uniform fit (separate width/depth/height scales).
- Audit (fitted): `colliders 6 penetrating 0 kink 38.7 deg stretch 7.60 % tipSway 1.52 cm`. Head-turn snap: `tipSway 5.20 cm`, settled `1.67 cm`. That is much better than v3 (kink 69, stretch 59 %, snap 18 cm). Kink and stretch are still above the 28 deg / 3.5 % limits.
- Motion is now good. Our bone measure with wind 0: tip sway 1.14 cm idle, 3.45 cm at the end of a 60 deg / 0.1 s turn, back to 1.14 cm at +0.9 s. The run-to-stop recovers in about 1 s. The locks move as coherent clumps and don't explode. Our HairDeck chains move only 0.93 cm on the same turn. So for motion, AetherStrand v2.5 is the livelier one.
- The torch reaches the hair (hair value 3.8 off, 52.3 on).

## v2.6 round 3 on Tobin (facts only: what we ran, settings, numbers, paths; no verdict)
Run 2026-11-22 night, Unity 6000.6.5f1. Installed as `3.2.0-neon` (package.json said 2.6.0). Gate PASS (compile, EditMode 1867/1867).
- Intake: `AetherStrandMeshGenerator.cs` unchanged (no scalp projection). New `AetherStrandAiDiagnostics.cs`; `ProceduralBuilder` defaults changed (headLocalOffset (0,0.005,0.032), bangsLength 0.72); `PresetGeometry.json` regenerated. The zip has no `.meta` files for .cs/.json, Unity made them. `PresetGeometry.json` presetId is still `parted_shag_medium` vs "Layered Swept Long".
- Diagnostics run: our headless entry `SleepingCalamity.EditorTools.TobinHairDiag.Run` calls `ExportBundle(prefabInstance, folder)` (folder outside Assets; the default `Assets/AetherStrand_AI_Diag` is not used). It ran fine headless. Note: `ExportBundle` writes the JSON with a UTF-8 BOM (`Encoding.UTF8`), so `json.load` in Python fails until the BOM is stripped. The 4-up turnaround PNG was NOT produced by us (batchmode run has no Setup window); a `-nographics` render is blank grey, so we used our own `TobinHairABCapture` frames (needs `-Graphics`) instead.
- Two builds, both via `GenerateAndAttach` on a fresh prefab (30,000+ verts: 33,540 verts, 54,180 tris, 221 bones, 44 chains):
  1. **Default knobs** (`new HairSculptParams()`: scale 1, offset 0, bangsLength 0.88, widthScale 1). Diagnostics `diag_v26/default/`: Front Overhang -22.5 mm (hair front BEHIND the forehead), Crown Lift +72.4 mm, Back Reach +25.9 mm, Width Delta +59.7 mm. Chains Bangs 10, Crown 9, Sides 1, Rear 24. Rest/turn/sprint audit: kink 53.5/53.6/53.6 deg, stretch 24.3/23.9/24.2 %, inter-lock clips 1, penetrating 0, mean sway 2.25/4.49/8.27 cm, verdict FAIL (score 54).
  2. **Round-2 fit** (overallScale 1.2, headLocalOffset (-0.005,-0.064,-0.002)). Diagnostics `diag_v26/fit_r2/`: Front Overhang +4.5 mm, Crown Lift +60.4 mm, Back Reach +46.5 mm, Width Delta +110.0 mm. Audit rest/turn/sprint: kink 37.7/40.8/43.4 deg, stretch 8.62/9.32/14.90 %, inter-lock clips 0, penetrating 0, mean sway 4.72/6.62/7.08 cm, verdict WARN (score 82/78/65). Flag: crown 60.4 mm above skull top.
- Skull data from the report (head-local, m): head bounds min (-0.097,-0.05,-0.0667) max (0.095,0.1877,0.1669); head bone fwd (0,0.122,0.993), up (0,0.993,-0.122) in character space; lossy scale 1.
- Stills (`ab_v26/stills.jpg`, `ab_v26/aether_default_vs_fit.jpg` top row default, bottom row fit; `ab_v26/default_knobs/` raw frames): default knobs leave the forehead and temples bare, hairline well above the brows. The fit covers the forehead down to the brows with the fringe. In the fit, two locks flare out sideways at the ears (visible front, 3/4 and back), and one long lock arcs out over the right of the crown (also in default). Torch reaches the hair (mean value on/off 61.6/3.6, diff 58.0; HairDeck 99.7/23.3, diff 76.4).
- Motion (`ab_v26/motion.jpg`, `ab_v26/log.txt`, fit build, wind 0 except idle plugin wind 0.55; tip displacement from head-rigid rest, mean/max cm): AetherStrand rest-pose tips already sit 2.14/17.59 cm off the head-rigid pose (HairDeck 0.30/0.43). Head turn: 2.13/17.53 at t0, 4.21/15.02 at +0.10 s, 1.70/8.03 at +0.8 s, 1.36/7.88 at +1.2 s (HairDeck 0.30/0.43, 1.32/3.23, 0.30, 0.30). Run-stop: 10.35/15.19 at t0, 4.92/8.29 at +0.5 s, 1.44/7.28 at +1.2 s (HairDeck 2.90/4.66, 0.30, 0.30). Idle: 2.59/17.19 at t0, 1.87/9.63 at +1.2 s.
- Not run: scalp-projection or non-uniform fit (needs generator code); no tuned bangsLength/widthScale sweep beyond the two builds above.
- Branch `claude/hair-aetherstrand-v26`; committed prefab is the round-2-fit build; the default-knob build is reproducible with `SC_HAIR_FIT=default` (see `TobinHairABBuilder.FitParams`).
