---
name: sc-aaa-prop
description: The ported fg-aaa-prop toolchain and its lessons for Sleeping Calamity T3/T4 pieces - dense damaged highs, smart materials, baked PBR sets, fake glass and liquid, per-object A/B loop, null-test and reviewer-bias rules, building splits. Use when running or changing any Art/Blender/tools/sc_aaa_*.py, sc_props.py, sc_compose.py, sc_ab_sheet.py, sc_stand_ab.py, sc_imgdiff.py, sc_snap.py, or when designing an A/B for a hero (B10, B11) or a new family.
---

# AAA prop toolchain (ported from FishGame `claude/aaa-prop-pipeline`, M8)

Pipeline and geometry/material rules are in **sc-asset-craft** section 1 and **sc-texture-paint**. This file holds the tools and the round-by-round lessons (FishGame sections 6e-6m, with the potion-stand specifics dropped).

## Tools (`Art/Blender/tools/`, run with Blender 5.2 headless unless noted)
| Tool | Use |
|---|---|
| `sc_aaa_kit.py` | geometry (box, lathe, tube, smooth_profile), highs (`erode_box`, `wear_generic`, `scoops`, `dimples`, `tooled`), `make_part`, `bake_prop`, `game_material`. Carries the four library fixes. |
| `sc_aaa_mats.py`, `sc_aaa_mats2.py`, `sc_aaa_scan.py` | smart materials (oak, iron, stone, copper, ceramic, glass/liquid, ...); `AAA_SCANS=<folder>` adds CC0 scan layers; `AAA_FULL=1` full scan rollout. Many `AAA_*` env switches are FishGame A/B variants kept for reference. |
| `sc_aaa_cuts.py`, `sc_aaa_sculpt.py` | authored form as geometry (spalls, chamfers, cracks, chisel facets, hand-built dabs). |
| `sc_props.py` | one function per prop, `--only a,b`. The potion-stand props are **examples only**; copy the pattern. |
| `sc_compose.py`, `sc_review.py` | review rig: hearth + studio moods, metal environment and probe, `AAA_LIGHTOFF=<light>` diagnostic. |
| `sc_ab_sheet.py`, `sc_stand_ab.py` | blind A/B sheets (random order, key file written next to the JPG). |
| `sc_imgdiff.py` | mean pixel difference of two renders (plain python). |
| `sc_snap.py` | snapshot/restore a baked prop **with its textures** (plain python). |
| `sc_pack_unity.py` | pack a bake for URP Lit; `--orm` converts an ORM texture. |
| `sc_ck_lib.py` | the cottage fork of the kit (Cycles CPU, atlas bakes, building helpers). |
| `sc_fetch_textures.py` | Poly Haven CC0 fetch with a source record. |

Always pass `--python-exit-code 1` and an `--out <scratch>`. Library fixes already in `sc_aaa_kit.py`: `view_layer.update()` after setting `.location`; numpy vectors converted to tuples before node sockets (segfault); clamped bases under fractional powers (NaN when t > 1); absolute image paths before `images.load` and `save_as_mainfile`. `bpy.ops.object.join` keeps the first object's transform: apply transforms first.

## Lessons (kept from the 12 rounds)
**Process**
- **Per-object loop:** change one variable, A/B it blind with two fresh reviewers on a JPEG sheet, keep only wins. Run different objects' A/Bs in parallel. On a split, use a third reviewer and ask for "the specific visual difference that decided it"; count it only when the stated reason matches what actually changed.
- **Null test (reviewers confabulate):** an invisible change (mean diff 0.01-0.22 of 255) still drew a ~2 point preference with confident invented reasons. A margin of about 2 points on a whole scene was noise in those rounds. Run `sc_imgdiff.py` on every pair first; as a working heuristic from those rounds (not a rule), under about 0.5 at review size skip the A/B or crop to where the change is.
- **Snapshot before every rebake you may A/B** (`sc_snap.py`): a `.blend` references its baked maps by path, so a copied `.blend` is not a baseline (copper v2 was compared with itself).
- **Send review sheets as JPEG:** the image viewer palettises big PNGs into contour banding that is not in the file. For glass and small props, crop the close view into the sheet: at full-scene scale a vessel is about 100 px and real changes read as "identical".
- **The reference row decides the verdict:** judged against real photos a gritty scan scored 16-18; against the game-reference stand the same grit was "sandpaper". Put a crop of the target object in the A/B reference row whenever the change chases a game target.
- **Rubric calibration (same expectation as sc-asset-review):** the /40 rubric is harsher than shipped AAA itself (blind: 21/40 for the reference game's own art vs 32/40 for a reviewer who recognised it). Use the relaxed rubric (about 4 per line = shipped art) and prefer pairwise A/B, or a blind pair test (which one is the AAA reference? parity = about 50%), over absolute scores.
- **Self-judged or one-reviewer results are weak evidence.**
- **Rig bugs look like material bugs:** when a material change has no visible effect, switch lights off one at a time before touching the material again (a fire light inside a clay wall burnt every hole white for two rounds). Fire cues must sit where the light is, or be light themselves; dark soot on a pot in shadow reads as shadow.
- **Visible beats correct** at review distance: if a feature is invisible at the camera, it does not exist (detail under about 1 mm, a 0.45-strength stain, a dimmer glow). Check at the review camera, not in a close-up, before spending an A/B.
- **A named defect is not a preference.** Reviewers list what is most visible, including features that score as character (a "stuck-on slab" that reads as a crack, mis-sorted liquid "tongues" that read as refraction, an uneven glaze "teeth" edge). Only the A/B decides; improve the feature reviewers accept instead of replacing it, and never strip a named defect untested.
- **A requested form change is worth nothing if it removes a detail reviewers already credit.** Parts of one object must share a material read (a wooden-club pestle next to a stone bowl cost more than any surface detail).

**Materials**
- Metal: the scale of the normal detail sets the read (fine planishing facets turned satin plastic into hammered copper); metals need an environment and a reflection probe; tarnish half-metallic and brown; verdigris pale and chalky so it survives warm light.
- Fired clay: hue and fire history move the score, not form; warm terracotta beats desaturated grey ("concrete"); hand-cut irregular holes beat round machined ones; kiln flashing and iron speckle help.
- Stone: lowering scan contrast reads as blur, not calm. Spalls need a square arris: on a rounded rim they bake as white discs; re-aim spall cutters whenever a lathe profile changes. Chips by curvature gate only.
- Edges: a straight glaze/mask line reads as a paint mask; ragged, per-tongue width/length with a rounded tip and a pooled bead wins.
- Glass and liquid: liquid must read see-through with internal variation (fake refraction: refract the view ray into a procedural room); an opaque or uniformly clean liquid with a hard level top reads as plastic. Haze and patchy dirt read as old glass (breaking it up beats thinning it; visible dirt beats correct dirt). A fake reflection probe (REFLECT lookup of a procedural room with a crisp window patch, times Schlick Fresnel) gives the highlights; the highlight shape follows the normal field, not the silhouette. A uniform Fresnel glow reads as a fake outline: gate it by the light direction. Eevee screen-space refraction fails inside spheres; do not use it as the stand-in for game glass.
- Glow masks must be true distances from the feature centre, and the material's feature list must be the same positions the geometry cuts (else the glow bakes as squares beside the holes).

**Buildings (cottage v1)** (budgets in PHASE1 3.3)
- Tile or unique-bake big surfaces; instance sculpted kits for repeated members (slates, stones: 60-tri lows, cull buried faces at assembly); unique high-to-low bakes only for heroes. Plaster walls: unique-UV low baked from a smart plaster with storytelling masks plus a tiling detail normal. Timber frame: dense eroded high, baked unique. Soffit: tiling planks, UVs in metres.
- Voxel size `min(w, h) / 12`, floor 4 mm, cap 6 mm. Join all highs before a selected-to-active bake (Cycles re-syncs once per selected high: 1 h to 4.5 min).
- Cottage v1 faults to avoid: plaster too white (the target is tan-cream `#cbbd9c`), slates powder-blue, puffy chimney stones with black voids (use a smaller bevel plus mortar backing), hair-card grass 1-2 m tall (`use_advanced_hair=False`).

**Gotchas:** Blender 5.2 engine id `BLENDER_EEVEE` (4.5: `BLENDER_EEVEE_NEXT`); `ShaderNodeMix` sockets by index; relative image paths without `//` don't save, use absolute; big heredocs through Git Bash fail, use the Write tool; in PowerShell call Blender with `& $b ...` (the Git Bash path with spaces is refused in worktree-isolated sessions).
