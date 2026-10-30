---
name: sc-asset-craft
description: Build or refine a Sleeping Calamity 3D model (town kit piece, building, cellar vault piece, prop family, hero prop, enemy prop-creature) as a procedural Blender generator in the locked style - Dark Cloud's whimsical town and dungeon shapes in OoT-2026 stylised realism (real PBR bakes, chunky storybook forms, tidy placed wear). Use for any new model, a detail or convergence pass, a batch of one family, or when a brief says "model", "prop", "kit", "hero prop" or "detail pass". Pairs with sc-asset-budget (numbers), sc-asset-review (scoring), sc-texture-paint (tileables and trims), sc-asset-adopt (external meshes) and sc-ai-model-pipeline (Tripo parts).
---

# Building a Sleeping Calamity model

Models are Python generators under `Art/Blender/` that build meshes from code, bake their maps and export FBX. Never hand-model in a .blend and never commit a .blend as the source: the generator is the source, the FBX and its texture set are its output.

**The look** (`docs/PHASE1.md` section 3, the style bible; it wins over anything here): Dark Cloud's whimsical town and dungeon themes in the stylised realism of the 2026 Ocarina of Time remake. Real PBR materials (albedo, normal, ORM), chunky readable storybook shapes, saturated clean day colour, warm gold Cellars, tidy placed wear over clean base colour. Numbers below cite PHASE1 rather than copy it: PHASE1 is a draft whose `[Pn]` calls may change.

## 0. Before code
1. **Tier it** (PHASE1 section 4, handoff 3.5). The tier decides the method and the budget:
   - **T1** tileable or trim: not a model, see **sc-texture-paint**.
   - **T2** modular kit piece (facade panel, beam, vault segment, stall): a low on the area's shared tileables and trims, wear by vertex-colour or mask blend. **No unique bake.**
   - **T3** instanced sculpted piece (cobble, roof tile, brick, rubble, root cluster): a few highs per family, baked once into one atlas, scattered many times.
   - **T4** hero prop (dry fountain, cellar lantern, breath grate): full high-to-low treatment with a unique bake and the blind A/B (sc-asset-review).
2. **Find the target.** The reference row for the subject: free-licence photos for form, construction and wear, paintings for value (**sc-reference**, `Reference/<area>/<subject>/REFS.md`). Once the beauty and dungeon corners are approved, their captures join the row (PHASE1 3.7). Take colour from PHASE1 3.1 (key hues and the +15-25% chroma shift), not from the photos.
3. **Check it doesn't exist.** Grep `Art/Blender/` for the stem and the family. One family goes in one generator file (all barrels and crates together, all market stalls together); extend it before starting a new file. Scratch tests write a standalone generator in the scratchpad and move into the family file when they land.
4. **Write the header comment first:** what it is, tier, refs (`refs: Reference/.../REFS.md` plus the 2-3 measured facts), run line, output paths, real size, origin and axes, triangle and texture budget (PHASE1 3.3), collider it needs, and a dated line per pass. The header is the asset's README; keep it current.

## 0b. Tools: what runs here, what is legacy, what is still in FishGame
Every other asset skill links to this table.

| Tool | Status |
|---|---|
| `Art/Blender/tools/sc_ref_fetch.py`, `sc_ref_palette.py` | Current (sc-reference). `sc_ref_palette.py --k 7` is the colour and value measure for renders and captures (PHASE1 3, [P33]). |
| `sc_asset_audit.py` | **Current (rewritten M8, 2026-10-30).** Reads the PHASE1 3.3 budgets (`--class kit|scatter|hero|building|character|enemy|prop`, `--tex-res`): FAILs on triangles and material slots, WARNs on texel density (px/m from UV0), smear and hygiene, and reports UV layers and every colour attribute (a T2 piece with a second UV map plus a mask-blend colour is fine). FishGame palette/Col encoding checks are gone. `--selftest` builds such a piece. |
| `sc_asset_preview.py` | **Legacy, rework before use.** It reads `Assets/_Project/Content/Art/ArtPalette.asset` (not in this repo), so it stops at startup, and it draws palette x vertex-colour toon shading, not PBR. Replaced by the fg-aaa-prop render scripts (`compose.py`, `review.py`, `sheet.py`) and Unity captures. |
| `sc_asset_score.py`, `sc_asset_census.py` | **Legacy.** Percentiles against FishGame's shipped Millpond library (the baseline JSONs aren't in this repo) and a "painterly" metric for the old style; the census walks Millpond folders. Replaced by the batch contact-sheet review and the blind A/B (PHASE1 9). |
| `sc_concept_match.py` | **Legacy** in its concept-crop mode (no concept paintings here). `--targets '#hex,...'` still compares a render to hexes, but the graded measure is `sc_ref_palette.py` on the render. |
| `sc_paint_pass.py`, `sc_tint.py` | **Legacy, not used.** They write the palette-slot/vertex-colour encoding (`sc_tint.py` also needs `ArtPalette.asset`). Wear now lives in the smart material and the bake (`aaa_mats2.py`), or in a vertex-colour/mask blend on T2 pieces. |
| `sc_asset_adopt.py` | **Legacy encoding, reusable steps.** `--list-parts` runs (checked 2026-10-29) and its prepare functions (import, join, scale, yaw, `--part-tris`, `--weld`, decimate) import cleanly. A full conversion fails: its colour fit reads FishGame's `Art/Blender/sc_harbour.py` and its export and `--preview-yaw` use that and the legacy preview. See sc-asset-adopt. |
| fg-aaa-prop tools (`sc_aaa_kit/mats/mats2/scan/cuts/sculpt.py`, `sc_props.py`, `sc_compose.py`, `sc_review.py`, `sc_sheet.py`, `sc_ab_sheet.py`, `sc_stand_ab.py`, `sc_imgdiff.py`, `sc_snap.py`, `sc_pack_unity.py`, `sc_ck_lib.py`, `sc_fetch_textures.py`) | **Ported (M8, PHASE1 8)** with the four library fixes; usage and the round-by-round lessons are in **sc-aaa-prop**. `aaa_kit` geometry helpers live in `sc_aaa_kit.py`. `sc_props.py` is potion-stand example content. |
| `fg_handmade.py` (wobble, sag, tbox, loft, compound), `fg_kit_costs.py`, `fg_hull.py` | **FishGame only** (`C:\Dev\FishGame\Art\Blender\tools\`). Port as `sc_*` when a generator needs them; strip palette-slot calls. |

## 0c. Craft library
Generic Blender production notes (MIT, arjun988/blender-skills) are in `library/` (see `library/README.md`). Read one only when you need it: topology, rigging, props, Unity export, LODs and colliders, VFX, PBR bakes. This skill and the style bible win over them.

## 1. The pipeline per tier
**T4 hero, and the high of any T3 family** (fg-aaa-prop pipeline; its SKILL.md sections 1-3 and 6e-6m hold the round-by-round lessons):
```
part primitives (lathe / box / tube, real metres)
 -> high: voxel remesh (lists of parts are UNIONED), fillets only at the concave seams, authored damage (spalls, chips, dents, checks)
 -> smart material in part space (CC0 scan + procedural layers, aaa_mats2.py style), edge and cavity masks
 -> low: clean source mesh for turned / hard-surface parts, collapse-decimated high for organic / chipped parts
 -> UV + pack -> one Cycles selected-to-active bake: BaseColor, Normal, Roughness, Metallic, AO, Emission
 -> pack_unity.py for URP Lit -> review renders (sc-asset-review)
```
- **Dense damaged high, baked to a low.** A bevelled mid-poly baked in place reads clean CG; the jump came from a damaged voxel high.
- **Voxel size** for building members and large parts: `min(w, h) / 12`, floor 4 mm, cap 6 mm. Finer cost 11M tris and a 6.3 GB peak.
- **Low strategy per part.** Collapse-decimate organic and chipped parts; keep the clean source low (Catmull-resampled lathe profiles) on turned and hard-surface parts, or they get facet bands.
- **Union, then fillet only the seams.** Separate intersecting parts read as glued-on; smoothing the whole mesh melts every rim.
- **Join all highs into one object before a selected-to-active bake.** Cycles re-syncs the scene once per selected high: about 1 h down to 4.5 min on the cottage.
- **Albedo carries no baked light.** AO ships as its own map.
- **Glass and liquids:** fake refraction and reflection (refract or reflect the view ray into a procedural room, Schlick Fresnel) instead of real transmission. Liquids dark and saturated; bright ones read neon.
- **Metals need something to reflect:** an empty dark room renders metallic black. Add an environment and a probe in the review scene, and a reflection probe in Unity.

**T2 kit pieces:** low-poly pieces with UVs in metres at the PHASE1 3.3 texel density, sharing the area's tileables and trim sheet (one fix to a tileable reaches every piece). Wear and grime come from vertex-colour or mask blends between tileables and from decals, not a unique bake.

**T3 instanced pieces:** a few sculpted variants per family (the cottage used 16 slates), baked into one 2K atlas, instanced with jitter (bond, tilt, scale). Cull faces buried in the wall or ground at assembly. The cottage's slate roof is the model.

**Buildings** split like the cottage: plaster walls unique-UV and baked from a smart plaster with storytelling masks, repeated members (tiles, stones) as T3 kits, timber frame baked unique, hero bakes only for the door and lantern. Budget: PHASE1 3.3 (a whole building at most 75k tris, 2x4K + 4x2K + 2x1K).

**Batch, don't build one by one** (PHASE1 9): one generator per family, all its items run headless in one Blender session, one joined Cycles bake per batch, one contact sheet per batch.

## 2. Conventions
- **Axes:** author in Unity axes (y up, front +z), metres, real size. Origin at the bottom centre (props, buildings), the wall line at one end (run modules), or as the header states. Blender = (-x, -z, y). Export FBX -Z forward, Y up, apply transforms; don't vary these per asset.
- **Textures:** packed for URP Lit by `pack_unity.py` (port). Don't hand-pack channels from memory.
- **Texel density and budgets:** PHASE1 3.3. Ground and walls up to 3 m at 1024 px/m, upper storeys, roofs and vault crowns at 512 px/m, heroes 1024-2048 px/m.
- **Collision** is chosen at placement. A wide attachment (a spill, a leaning tool) inflates a box collider into an invisible wall: keep blocking attachments as separate placements, or name the collider in the header.

## 3. Look rules (style bible 3.1-3.4 plus the prop lessons)
Each rule names the review line it moves (sc-asset-review rubric).
1. **Silhouette first** (silhouette, read). Readable at 20 m (PHASE1 3.4) and at thumbnail size on the contact sheet. Spend triangles on outline breaks: a sagging ridge, a chimney pot, a lid ajar, a hoop standing proud, a handle that stands off. Negative space reads better than closed boxes; a solid object gets its silhouette from what sits on or around it.
2. **Chunky storybook proportion** (proportion). Take dimensions from photos, then exaggerate on purpose: timber sections about 1.4x real (25 cm and up), arches and voussoirs about 1.3x, roof overhangs and chimney stacks bigger, props about 10% oversized. Doors 2.2 m against the 1.75 m hero. Check scale against a 1.75 m figure and a 2.2 m door in the review render.
3. **Mass, then openness.** At least one heavy mass (about a quarter of the silhouette): a thick top, a plinth, a stone base. Openwork alone reads as scaffolding. Slightly top-heavy reads best.
4. **No dead-straight old work** (edges, form). Beams and walls 1-3 deg off true, ridges sag 2-5 cm per metre, nothing aged runs straight for more than 2 m. Every hard edge seen at play distance has a chamfer of at least 1.5 cm. Each board, stave, tile and post gets its own deterministic irregularity.
5. **Make it the way the real object is made** (form, story). Planked, coopered, coursed, turned, pegged. Joinery gives form where rounding alone doesn't: overhanging tops, through-tenons and pegs standing proud, straps and bolts, a plinth or skid at the foot.
6. **Edges and highlights come from the normal field** (edges, roughness). Spalls get a square arris (a flat scar and a crisp edge, never a dome bite). Planar facets on a cylinder read as stripes: resample the profile. Gate chips by curvature (convex rims only), or they become islands all over smooth faces. Detail under about 1 mm doesn't register at review distance: work at centimetre scale.
7. **Value masses and a contrast pair** (colour, read). 3-4 masses per PHASE1 3.1 (day: pale plaster, dark timber, warm mid stone or roof; Cellars: gold-lit, deep shadow, warm brick). Contrast goes between masses; variation stays inside each. Give every asset one pale-against-dark pair (pale plaster on dark timber, a gold lamp on black iron).
8. **Clean, saturated base colour** (colour). Key hues and the chroma shift from PHASE1 3.1. Plaster stays clean near-white, roofs a confident red, painted wood and cloth confident blues, greens, ochres and madder. No mass is pure black.
9. **Tidy, placed wear that tells a story** (story). Deliver every wear word in the brief, each visible from the three-quarter view, before polishing anything else. Pick 2-3 beats; at least one changes the value pattern at thumbnail size (a damp band, a replaced lighter board, soot above a vent, moss on the up-facing edge). Wear goes where weather and hands go; never uniform grime. Contents are recognisable objects (a handle plus a head), not blocks.
10. **When a material change has no visible effect, switch lights off one at a time** before touching the material again (the brazier's "white disc" was a light inside its wall).

### Asset-specific notes
- **Creatures and prop-creatures** (Cellars enemies, animals): [creatures.md](creatures.md)
- **Open vessels and coopered work** (barrels, crocks, jars, buckets, troughs, the fountain basin): [vessels.md](vessels.md)
- **Stonework, vaults and modular runs** (walls, arches, kerbs, vault segments): [stonework.md](stonework.md)
- **Foliage:** **sc-foliage**. **External meshes:** **sc-asset-adopt**. **AI-generated parts:** **sc-ai-model-pipeline**.

## 4. Workflow loop
```
- [ ] 0  tier set, reference row found, name checked, header written
- [ ] 1  blockout: silhouette and masses, scale vs 1.75 m figure and 2.2 m door; one render
- [ ] 2  high + smart material (T3/T4) or low + tileable assignment (T2)
- [ ] 3  detail: outline breaks, joinery, contrast pair, wear beats; batch bake
- [ ] 4  audit (tris, hygiene) -> review render with the reference row -> sc-asset-review
- [ ] 5  worst three fixed, re-rendered (loop until the review passes)
- [ ] 6  determinism (two audits equal); snap.py before any rebake you will A/B; export
```
Locally Blender 5.2 is at `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe` (not on the Git Bash PATH; `blender` below means that path). Iterate with `--out <scratch>`; give generators `--only`, `--no-export` and `--out`. Export to the repo only at the end, and only when the output changed (unchanged FBX and PNG stay byte-identical: no LFS churn, no reimport).
```
blender -b --factory-startup --python-exit-code 1 --python Art/Blender/<area>/<gen>.py -- --out <scratch> [--only <stem>]
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <scratch>/audit.json --class kit <scratch>/*.fbx
```
**Cloud sessions** (Linux, Blender 4.5, `~/apps/blender-4.5.3-linux-x64/blender`) commit the generator, not the FBX or textures: a different Blender writes different bytes. The PC session runs the generator to write the outputs. Blender 4.5's EEVEE engine id is `BLENDER_EEVEE_NEXT` (5.2: `BLENDER_EEVEE`).

## 5. Tripo-style construction (style-independent)
A teardown of an AI-generated cottage showed why its shapes beat clean primitives: every part is deformed (bent, tapered, twisted, sagging, chamfered), parts are pushed into each other so the silhouette is a compound, faces buried inside another part are deleted, and the texture carries the fine detail. Use it for blockouts and kit silhouettes (chimneys, dormers, roof slabs, barrels, window frames):
1. **Deform every part**, one random draw per part, so no two are alike.
2. **Overlap into compounds** (sink a cap 3 cm into its shaft), then cull buried and flush faces. Occluders must be closed solids.
3. **Lofted rings** (a stack of offset, turned rings) are the cheapest way to match a measured AI profile; slice only where a deform needs vertices (slicing everywhere cost 2-4x tris).
The helpers are in FishGame's `fg_handmade.py` (`tbox`, `bend`, `twist`, `warp`, `loft`, `kink`, `compound`, `cull_buried`, `roof_slab`, `arch_ring`): port before use.

## 6. Determinism and hygiene
- **Gate on the audit JSON, not the FBX bytes** (FBX headers carry a timestamp): export twice to the scratchpad and diff the two audits.
- Every random draw comes from `random.Random(SEED ^ crc32(stream))`, one named stream per part. Never `random.random()` or time. `mathutils.noise` is position-only, so it's safe.
- **Lathe caps, not r = 0 points:** a profile point on the axis plus a cap leaves loose vertices. End at a small radius and cap.
- **Splitting a lathe** (two materials, two bands): build two lathes from the two parts of the profile, or compact the vertex list; passing all vertices with some faces leaves loose ones.
- **A lean or a sink:** rotate about the base edge on the low side so that corner stays at y 0, and state it in the header.
- **Origin with attachments:** bounding-box centring is pulled off the body by a spill or a ladder. Centre on the body, or state the offset.
- No loose verts, no zero-area faces, no faces hidden inside other parts (they cost tris and bake garbage).
- **Bake cage:** extrusion below half the narrowest gap (3 mm extrusion, 8 mm ray for a 10 mm crack).
- **Blender library bugs** (fixed in the cottage's `ck_lib.py`): call `view_layer.update()` after setting `.location` or `matrix_world` is stale; convert numpy vectors to tuples before node sockets (segfault); clamp t to 1 in `(1-t)**p` (NaN); make image paths absolute before appending. `bpy.ops.object.join` keeps the first object's transform: apply transforms first.
- Big Python heredocs through Git Bash sometimes fail with "unexpected EOF": write files with the Write tool.
