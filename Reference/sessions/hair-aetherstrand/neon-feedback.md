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
