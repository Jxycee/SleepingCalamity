# AetherStrand web solver -> C# port

Branch `claude/aetherstrand-physics`. Plugin only (`Packages/com.aetherstrand.sculptedhair`), nothing under `Assets/_Project` touched.

## What was ported
`Runtime/AetherStrandWebSolver.cs` (namespace `AetherStrand`) is a line-for-line port of `HairPhysicsSystem` in `src/engine/hairPhysics.ts`:
- `step()`: per-substep Verlet/spring integration with head-motion target follow and inertia, wind (breath/ripple/flutter/turbulence), gravity sag, head impulse (decay, 5.2 clamp), root pin (k<=3) and max-displacement pull (k>=4), 4 anti-kink/bending + distance iterations, crown cap, head lathe/ellipsoid collider and neck/torso collider (`resolveExactHeadCollision`, `resolveExactBustCollision`), three.js `slerp` between head poses for substeps.
- `buildCohesionGraph` (4 nearest neighbour locks per node, same hash/linked-list order) and `applyCohesiveNeighborWeight`.
- `resolveInterLockVolumeAndWeight`: spatial hash, counting sort, half-shell traversal, roof-tile ordering, radial + lateral push, tangential momentum transfer, friction, the post-collision anti-kink/length/collider polish pass.
- The flip-detection branch of the telemetry block (it lerps `prevPos`, so it changes the physics). Instability/bounce statistics and clearance are not ported (visual-only in the web).
- `snapToHead`, `applyImpulse`.

`Runtime/AetherStrandWebMesh.cs`: web `buildVolumetricBuffers/updateVolumetricBuffers` ring/frame math on live particles (same math as `AetherStrandMeshGenerator.BuildVolumetricMesh`, now in Runtime). Verified identical to the static generator at rest: vertex positions 0.0001 mm, UV/uv2/colours/triangles equal.
`Runtime/AetherStrandWebHair.cs`: `AetherStrandWebHair` MonoBehaviour (loads PresetGeometry.json + optional recipe.json, owns solver and mesh, `LateUpdate` steps at fixed 60 Hz or variable dt, rebuilds the mesh with `MarkDynamic`, plain MeshRenderer, default material from shader `AetherStrand/SculptedHair_URP`).
`Runtime/AetherStrandWebData.cs`: dependency-free JSON reader + PresetGeometry/recipe loader.

Deliberate choices:
- Solver state is `double` (JS numbers are float64); the web's `Float32Array` stores (entry buffers, node tables, neighbour weights, velocity snapshot) are rounded to `float` at the same points. Pure math, no allocation per step, no randomness (the web solver has none: seeded LCG only in lock building).
- The solver runs in WEB space (right-handed, Y up); the component mirrors X at the boundary (position `-x`, quaternion `(x,-y,-z,w)`).
- Errors below are in WEB units x1000 = mm (web bust is a 1 m-scale model; scale with `unitScale`).

## Verification
Ground truth: `Reference/sessions/aetherstrand-combo/tools/web_trace.ts` runs the real `HairPhysicsSystem` headless in node (`loadCustomBuilder`, then `snapToHead`, then per frame `applyMotionPose(t=f/60)` + `step(1/60)` exactly like HairViewport capture mode; `applyMotionPose` is copied verbatim). Writes `trace.bin` (Float32 [frame][lock][node][xyz], three.js world space, frame 0 = after snap), `trace.json` (head pose per frame float64, impulses, physics), `init.json` (full-precision solver state + web cohesion graph) and a `PresetGeometry.json`. `tools/gen_traces.ps1` regenerates everything under `C:/Dev/scratch/ptraces`. Deterministic (no `Math.random` in solver or builder path).

Replay: `Editor/AetherStrandWebParity.cs` (pure C#) / `AetherStrandWebParityBatch.Run` (Unity exec). Two initialisations: `strict` = init.json (exact web state and the web's cohesion graph), `json` = PresetGeometry.json (product path; C# builds its own cohesion graph, which equals the web's: graphMismatch 0).

Error vs web, 300 frames, all particles, Unity editor run (`unity.ps1 exec`, PASS):

| archetype (locks) | idle | walk | turn | gust |
|---|---|---|---|---|
| halfup059 (303) | 0.0000 / 0.0002 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 |
| asym-cascade (278) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 |
| spiky-hero (328) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0002 | 0.0000 / 0.0001 |
| high-ponytail (248) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0010 |
| sleek-bob (249) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 |
| long-cascade (285) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 |
| twin-tails (243) | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 | 0.0000 / 0.0001 |

Cells are mean / max particle error in mm over 300 frames (worst frame). Strict and json paths give the same numbers (worst max 0.001 mm; the floor is the Float32 trace file). Target was mean < 0.5 mm, max < 3 mm: met by about three orders of magnitude on 7 archetypes x 4 modes. Extra stage traces (halfup059, console harness): rest pose, gravity-only (no wind, stiffness 0.05, inertia 0), collisions off, `solverSubsteps=4`, strong wind 6 / turbulence 1 / azimuth 45: all max <= 0.0004 mm.

Mesh (303 locks, 23634 vertices): dynamic mesh at rest vs static generator: max 0.0001 mm, UVs/colours/triangles identical.

## Performance (Unity editor, Mono, 303 locks, 1 substep)
Solver step 3.3 ms, mesh rebuild + upload 0.74 ms per frame (editor JIT, single thread; a console Release build is 2.0-3.7 ms for the same solver). Not Burst-compiled; not measured in a player build.

## Differences / known gaps
- `PresetGeometry.json` from `exportLockGeometry` rounds to 5 decimals and omits solver fields. That rounding flips contact branches (max error 5-180 mm, mean < 0.2 mm in an earlier run), so `web_trace.ts` writes a full-precision PresetGeometry (schema 2.1, same layout) with optional per-lock extras: `layerOrder`, `mass` (needs stiffnessBias), `restMidR`, `rootRawY`, `stiffnessWeight[12]`. Without extras the loader falls back to layerOrder 0 (base) / 2, mass = width*thickness, restMidR from node 4, stiffness bias 1: motion will be close but not identical. Recommended web-side change: add these fields to `serializeLocksForBlender` and stop rounding (not done, web source is read-only here). The coordinator's `web_export.ts` output (rest pose now mirrors `loadCustomBuilder`) still lacks the extras.
- Live strand-width change: the web re-bakes the inter-lock rest pose when `strandWidthScale` moves more than 0.03 from the baked value. Not ported (rest pose is whatever the geometry file contains). All test recipes use 1.0.
- `bakeCollisionFreeRestPose`/`bakeInterLockRestPose` themselves are not ported; rest pose comes from the geometry file.
- Instability / bounce telemetry and `aClearance`/`aInstability` vertex attributes: not ported (shader parity input only; not in the Unity mesh).
- Head/bust inputs: only identity-rooted rigs were verified. Torso collider with a moving/rotating `bustRoot` is ported but untested against the web. `AetherStrandWebHair` assumes the head pivot at Start is web identity orientation and (0,0.44,0)*unitScale, character facing +Z; the GameObject is forced to world identity rotation.
- The component was not run in a play session or rendered (no -Graphics run); only its pieces (loader, solver, mesh builder) are covered by the parity batch.
- `.meta` files for the new scripts were generated by Unity and committed.
