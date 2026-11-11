<!-- Covers: symptom-first troubleshooting table for AI-model texture and shape problems. Moved from SKILL.md. -->

## Troubleshooting (ask the user what they see, then match)
| Symptom | Likely cause | Check | Fix |
|---|---|---|---|
| Grey smear on a wall behind or under something | Generator never saw it (P2) | Harness render; `tripo_repair` visibility map | Step 1; at the source, multi-view input |
| Pale ghost or halo around frames, doors, porches | Projection halo painted into that part's own texels (P2/P3), **not** UV overlap | Distance-to-other-part map (`near.py`) | Step 1 (chart quilting with the chroma gate) |
| Whole roof or wall soft or streaky | Few good texels on that surface | Soft fraction per class | Step 2 (`--only-soft`); alone if the surface has no good texels |
| Crisp in Blender, soft or blobby in game | an import-time filter on the texture (FishGame's Kuwahara clean did this, P9), or a mip/aniso issue at grazing angles | Compare the imported texture with the source; check the import settings | No smoothing filter on import; max size 2048, aniso 4 |
| Contrasty but blurry (checker floors) | sd metric misses it | "Mush" metric (share of texels with no 1-px detail), P8 | Step 2 catches it |
| Bricks on a fascia, window glass or door | Class picked by colour or area, not geometry | Which part and face got repainted (`pick.py` maps a render pixel to its chart) | Geometry rules: 25° charts, feature vetoes (mean face area < 0.004 m², warm or pale) |
| Visible tiling or mirrored motifs | One exemplar | Swatch image | Several quilted exemplars, swatch ≥ 6 m, no mirroring |
| Soft patch that no step fixes | Part's texel density far below the rest (M1: 80 px/m against a 62–305 spread) | Per-part density from intake | Re-generate, or Magic Brush; tell the user |
| Wood grain on painted metal (tin, iron) | Timber class sees red-brown as "warm wood" (M1) | Lab a* of the region > 12 | `SC_TIMBER_AMAX=12`, `--skip stone,slate` |
| New bright seams, or a moving part fused, after step 2 | Step 2's new UVs joined everything (M1) | Seams at island borders; part count | Always run `sc_tripo_retex_writeback.py` |
| Step 1 changed a wall that was already crisp | Flat-faced stones read as tiny holes (M1) | Before/after diff on a visible wall | `--extra "--nvg 1 --blob 0.10"` |
| A part that should move is fused with static pieces | Tripo groups loose pieces per part (M1) | `sc_tripo_wheel_split.py` highlight render | Split by loose piece before export |
| Tail stripes or other dark markings recoloured or holed by step 1 | Ghost test reads dark markings as ghosts (C1) | Colour shift on marking parts | Creature mode (no ghost test) |
| Dark fur flagged as blur everywhere | Absolute softness thresholds (C1) | Soft fraction by relative contrast | Relative softness (creature mode) |
| Repair effort wasted, or the metrics look awful on a creature | Buried duplicate shells (C1: 60 of 117 parts never visible) | Render-visibility per part | Judge visible texels only; consider culling buried shells (saves 44 % tris on the raccoon) |
| Wood grain on a face or fur | sc_tripo_retex classes on a creature (C1) | — | Don't run step 2 on creatures |
| Export looks darker than in Tripo's viewer | Raw texture compared with a lit viewer (I2) | Render the GLB with `sc_tripo_viewer_render.py` | Nothing to fix; judge in Unity under game light |
| Model too small or large in game | Arbitrary AI scale (P3) | Measure a door (2.2 m here, PHASE1 3.4) or a hero's height (3.6) | Scale by a known feature in the finalize config |
| Shading looks doubled (dark under eaves even in sun) | Lighting painted into the texture (P3) | Down-facing faces 2–4× darker in the atlas | Open: no proven de-lighting step yet. Log any attempt |
| Our generated model looks like "PlayDoh tubes" | Straight, separate primitives | Compare to the Tripo part | Section 5 |
| Proportions drift from the target (toy-like, chibi, or plain realistic) | Generator default proportions | Measure heads-tall, hand and eye scale against PHASE1 3.6 | Re-generate from multi-view inputs that show the target; for heroes, the MPFB2 body sets proportion and the AI parts follow it |
