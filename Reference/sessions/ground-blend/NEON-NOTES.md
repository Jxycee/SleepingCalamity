# Notes on Neon's ground-blend code (for relay to Neon)

Applied 2026-11-22 on branch claude/ground-blend-4d3fa9. Neon's code was close; these are the places where our project differed from its assumptions or where it needed a fix.

## Shader (SC_WearForwardPass.hlsl, RUINSKIN block)
- Merged, not replaced: SCHash/SCNoise already existed (identical), so only the four globals were added. The `_SpecColor` / `_DetailAlbedoMapScale` / `_DetailNormalMapScale` ruin parameters stay as before.
- `scGroundNormalWS = normalize(half3(...))`: the dh/dx terms were cast to half before the normalise, which loses precision on gentle slopes. We normalise in float and cast after.
- The ground-albedo sample is only taken inside the foot band (`aboveGround < footH * 1.4`); good, kept. When no bake is bound `_SC_GroundBounds.z` is 0 and the old pivot measure is the fallback (needed: other scenes, LookTest, cellars use this shader without a bake).
- The normal blend must come before `InitializeBakedGIData` (it reads `inputData.normalWS`): kept Neon's placement after `InitializeInputData`.
- Not verified on a GPU yet: the Unity shader importer reports no messages in a -nographics run, but variants compile lazily. A graphics capture is the real test.

## Baker (SCGroundBaker.cs)
1. `Physics.Raycast` against a `LayerMask` of "cobble streets": there is no such layer. Our streets are Terrain cobble layers painted into the splat map (the terrain height already holds the lane terraces), and the only street collider is one BoxCollider slab (`SquarePaving`). The baker now takes an explicit `IList<Collider>` and uses `Collider.Raycast`, so a wall or prop can never be mistaken for ground.
2. `terrain.SampleHeight` needs the terrain active in a scene; we use `TerrainData.GetInterpolatedHeight` (pure CPU, scene independent).
3. `TerrainLayer.diffuseRemapMin/Max` were ignored. Our builder sets them to tame the meadow green and the path cream, so without the remap the baked colour was brighter than what the terrain shows. Now applied.
4. `GetAlphamaps` index: alphamaps are [z, x, layer] (Neon had this right); the nearest-texel rounding was floor, we use round-to-nearest.
5. Non-readable textures: Neon's PNG/JPG LoadImage fallback works for our B1 ground tileables (all PNG). Kept.
6. `Buffer.BlockCopy` of ushort[] to bytes is host-endian; replaced by explicit little-endian writes so the file never depends on the machine.
7. .asset output replaced by raw .bytes per exchange 4 (LFS pattern added to .gitattributes: `Hollowmere_GroundBake_*.bytes`). Files are written only when the bytes changed, so a second build touches nothing.
8. A BoxCollider has no `hit.textureCoord` (it returns zero, so Neon's `SampleStreetHit` would read texel (0,0) of the street texture, a wrong flat colour). We use the texture's mean colour x tint for non-mesh colliders; MeshCollider with a readable mesh uses the real UV.

## Binder / placement
- Binder lives in SleepingCalamity.Presentation (namespace SleepingCalamity.Presentation); fields are TextAsset refs plus resolution; textures are rebuilt on enable (`R16` linear for height, `RGBA32` sRGB for albedo). OnDisable zeroes `_SC_GroundBounds` so the shader falls back. `Texture2D` creation under -nographics works.
- `TryGroundAndSink`: (a) no `Physics.SyncTransforms()` before the rays, so rays hit stale transforms in a batch build; (b) hits were not filtered, so a tree or neighbouring wall counted as the host (default host is now terrain only, a predicate can name others); (c) enabling/disabling the target's colliders replaced by skipping hits under the target's own hierarchy; (d) the target is now left untouched on failure; (e) tilt defaults to off (our Put calls already author a tilt, and caps must not tilt).
- Used only on the OrchardGap breach rubble (GapRubble/GapHeap/GapFore/GapStep/GapEast), the Orchard foreground slabs (`Broken_*` in OrchardLaneKit) with 3 support hits and 20 % sink.

## Not done / needs a decision
- "Parapet caps in OrchardGap" (minSupportHits 4, sink 0.22): the parapet merlons and caps of the town wall modules are geometry inside the FBX (Art/Blender/town/sc_town_wall.py, `box(...)` for each merlon and cap), not builder-placed objects, so the placement helper cannot ground them. The lifted block in the right parapet of Eye_OrchardGap is most likely the "cap fallen on the walkway" `rock(..., block=True)` of a `miss` merlon (z 2.27, over the walkway) seen above a low stub; the stray "L" is not found in builder code. Both need a wall-kit regeneration in Blender (an asset change), so they were left for a decision.
