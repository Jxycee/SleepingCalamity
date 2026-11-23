# AI hairstyle authoring on the fitted pipeline (Neon task)

Status 2026-11-23. The pipeline that turns a web recipe into fitted, simulated hair on Tobin is done and measured (`STATUS.md`, `PIPELINE.md`). This is the task for the AI tool (Neon) that perfects new hairstyles on top of it. Sessions only produce evidence; Neon judges.

## What exists (all scripted)
- **Recipe** = JSON (`recipes/halfup059.json` is the template): `presetId`, `builder` (archetype + sliders + seed), `shader` (palette and spec), `physics`.
- **Geometry**: `npx tsx tools/web_export.ts <recipe> <outDir>` (cwd = AetherStrand clone) -> `PresetGeometry.json` (full precision plus solver extras).
- **Unity**: `AetherStrandWebImport.Apply` (baked mesh, bone physics) or `ApplyLive` (exact web solver). Both fit the hair to the character head and push it out of the real head surface.
- **Renders**: `tools/TobinFitTest.cs` (4 views, web light rig, three's ACES). Web reference renders: capture URL `?capture=1&sim=rest&cfg=<base64 recipe>`.
- **Numbers**: `tools/HairEval.cs` (head penetration, root gap) and `tools/cmp*.py` (web-vs-Unity overlap and pixel error).

## Measured baseline (Tobin, 12 archetypes + 5 named presets, rest pose)
| Metric | Result | Pass line |
|---|---|---|
| Vertices inside the head | 0.03-0.06% (2-6 mm deep) | under 0.2%, depth under 6 mm |
| Roots inside the head | 0% | 0% |
| Root gap to scalp (p95) | 6.6-11.6 mm | under 12 mm |
| Web vs Unity hair overlap (12 archetypes, hair only) | 0.956-0.968 | over 0.95 |
| Mesh vs web, per vertex | 0.008 mm mean | under 0.1 mm |

## The loop for a new look
1. Input: a reference image or description of the target hairstyle (front, side, back if available).
2. Pick the closest archetype (12 ids in `hairBuilder.ts`; Layered Shag, SOLDIER Spikes, Silky Cascade, Crown Wolf-Cut, Sleek Blade Bob, Valkyrie Asym, Dolphin-Tail, Half-Up Cascade, Swept-Back, Battle Ponytail, Dual Twin-Tails, Side-Swept Fringe) or a named preset (Leon, SOLDIER, Silver, Noctis, Crimson).
3. Set sliders (ranges in `HairBuilderConfig`, `src/types/hair.ts`): partPosition, bangsLength, bangsSweep, crownVolume, spikeLift, sideLength, backLength, layerShag, density, strandWidth, seed. Set palette (`CURATED_HAIR_PALETTES` or hex values) and shader sliders.
4. Run the pipeline, render Tobin, run `HairEval`. Reject any round that breaks the pass lines above.
5. Neon compares the Tobin renders with the reference and returns the next slider changes. Stop after two rounds in a row with no visible improvement.
- Shapes outside the 12 archetypes: Blender addon operator `aetherstrand.curves_to_locks` converts hand-drawn guide curves to locks; export them in the same `PresetGeometry.json` format and the rest is unchanged.
- Do not edit the plugin or the web generator from this task; if a style cannot be reached with recipe values, report the missing capability instead.

## Open items that limit "perfect"
- Eyes: long fringes cover Tobin's eyes in several archetypes (web behaviour). A per-character fringe-length cap is not implemented.
- Torso collider of the live solver is the web bust's, not Tobin's shoulders.
- The web generator still emits the old plugin zip; the fixed plugin is in `unity-plugin/` of the AetherStrand repo.
