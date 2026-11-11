---
name: sc-asset-craft
description: Build or refine a Sleeping Calamity 3D model (town kit piece, building, cellar vault piece, prop family, hero prop, enemy prop-creature) as a procedural Blender generator in the locked style - Dark Cloud's whimsical town and dungeon shapes in OoT-2026 stylised realism (real PBR bakes, chunky storybook forms, tidy placed wear). Use for any new model, a detail or convergence pass, a batch of one family, or when a brief says "model", "prop", "kit", "hero prop" or "detail pass". Pairs with sc-asset-budget (numbers), sc-asset-review (scoring), sc-texture-paint (tileables and trims), sc-asset-adopt (external meshes) and sc-ai-model-pipeline (Tripo parts).
---

# Building a Sleeping Calamity model

Models are Python generators under `Art/Blender/` that build meshes from code, bake their maps and export FBX. Never hand-model in a .blend and never commit a .blend as the source: the generator is the source, the FBX and its texture set are its output.

**The look** (`docs/PHASE1.md` section 3, the style bible; it wins over anything here): Dark Cloud's whimsical town and dungeon themes in the stylised realism of the 2026 Ocarina of Time remake. Real PBR materials (albedo, normal, ORM), chunky readable storybook shapes, clean readable day colour, warm gold Cellars, tidy placed wear over clean base colour. Numbers below cite PHASE1 rather than copy it: PHASE1 is a draft whose `[Pn]` calls may change.

**Palette summary (PHASE1 3.1; the one place the key hues are listed in these skills, the others point here).** Day is moody and slightly muted, like the measured OoT frames: no chroma boost on stone, plaster or roofs; +10-15% only on foliage, sky, painted wood, cloth and accents. Key hues: plaster warm tan-cream `#cbbd9c`; roof red-terracotta `#9c4a34`; timber `#2e2520` to `#4e4844`; cobble `#817a7b` to `#b7b0aa`; foliage yellow-green `#6f8a34`; moss `#616e2e` / `#b6ab28`; day shadows slate-teal `#2f3a44`. Cellars are clean warm firelight with no teal or blue-green anywhere (user, 2026-10-30): lamp gold `#d6a24a`, lit stone `#c9aa76`, brick `#9d7d69` / `#bf7a38`, shadows warm umber to plum `#2a1c18` to `#3a2430`, ember accent `#c2412a`; saturated gold, crisp value steps, not muddy, no mass pure black. If this paragraph and PHASE1 3.1 disagree, PHASE1 wins.

## 0. Before code
1. **Tier it** (PHASE1 section 4, handoff 3.5). The tier decides the method and the budget:
   - **T1** tileable or trim: not a model, see **sc-texture-paint**.
   - **T2** modular kit piece (facade panel, beam, vault segment, stall): a low on the area's shared tileables and trims, wear by vertex-colour or mask blend. **No unique bake.**
   - **T3** instanced sculpted piece (cobble, roof tile, brick, rubble, root cluster): a few highs per family, baked once into one atlas, scattered many times.
   - **T4** hero prop (dry fountain, cellar lantern, breath grate): full high-to-low treatment with a unique bake and the blind A/B (sc-asset-review).
2. **Find the target.** The reference row for the subject: free-licence photos for form, construction and wear, paintings for value (**sc-reference**, `Reference/<area>/<subject>/REFS.md`). Once the beauty and dungeon corners are approved, their captures join the row (PHASE1 3.7). Take colour from the palette summary above (PHASE1 3.1), not from the photos.
3. **Check it doesn't exist.** Grep `Art/Blender/` for the stem and the family. One family goes in one generator file (all barrels and crates together, all market stalls together); extend it before starting a new file. Scratch tests write a standalone generator in the scratchpad and move into the family file when they land.
4. **Write the header comment first:** what it is, tier, refs (`refs: Reference/.../REFS.md` plus the 2-3 measured facts), run line, output paths, real size, origin and axes, triangle and texture budget (PHASE1 3.3), collider it needs, and a dated line per pass. The header is the asset's README; keep it current.


## 0b. Tools: what runs here, what is legacy, what is still in FishGame
Every other asset skill links to this table. Current: `sc_asset_audit.py` (PHASE1 3.3 budgets), `sc_ref_fetch.py`, `sc_ref_palette.py --k 7`, and the ported fg-aaa-prop tools (sc-aaa-prop). **Legacy, do not use:** `sc_asset_preview.py`, `sc_asset_score.py`, `sc_asset_census.py`, `sc_paint_pass.py`, `sc_tint.py`. Not ported: `ref_match.py` (use `Art/Blender/tools/sc_ref_palette.py`), `fg_adopt_find.py`, `fg_handmade.py`, `fg_kit_costs.py`, `fg_hull.py`.
- `references/tools.md` - read before running any Art/Blender tool you have not used this session (full status table and replacements).

## 0c. Characters
Heroes are not built with this skill's kit method: proportions are fixed in PHASE1 3.6; pipelines are **sc-ai-model-pipeline** and **sc-asset-adopt**. Budget: PHASE1 3.3.
- `references/characters.md` - read for hero heights, proportions and Tobin's direction.

## 0d. Craft library
Generic Blender production notes (MIT, arjun988/blender-skills) are in `library/` (see `library/README.md`). Read one only when you need it: topology, rigging, props, Unity export, LODs and colliders, VFX, PBR bakes. This skill and the style bible win over them.

## 1. The pipeline per tier
T4 hero and the high of any T3 family (sc-aaa-prop holds the round-by-round lessons):
```
part primitives (lathe / box / tube, real metres)
 -> high: voxel remesh (lists of parts are UNIONED), fillets only at the concave seams, authored damage (spalls, chips, dents, checks)
 -> smart material in part space (CC0 scan + procedural layers, `sc_aaa_mats2.py` style), edge and cavity masks
 -> low: clean source mesh for turned / hard-surface parts, collapse-decimated high for organic / chipped parts
 -> UV + pack -> one Cycles selected-to-active bake: BaseColor, Normal, Roughness, Metallic, AO, Emission
 -> `sc_pack_unity.py` for URP Lit -> review renders (sc-asset-review)
```
T2 kit pieces: low-poly with UVs in metres at the PHASE1 3.3 texel density, sharing the area's tileables and trim sheet; wear from vertex-colour or mask blends, **no unique bake**. T3: a few sculpted variants per family baked into one 2K atlas and instanced. **Batch, don't build one by one** (PHASE1 9): one generator per family, one headless Blender session, one joined Cycles bake and one contact sheet per batch. Join all highs into one object before a selected-to-active bake. Albedo carries no baked light.
- `references/tier-pipelines.md` - read before a T3/T4 bake or a building: voxel size, low strategy, glass and liquids, metals, building splits.

## 2. Conventions
- **Axes:** author in Unity axes (y up, front +z), metres, real size. Origin at the bottom centre (props, buildings), the wall line at one end (run modules), or as the header states. Blender = (-x, -z, y). Export FBX -Z forward, Y up, apply transforms; don't vary these per asset.
- **Textures:** packed for URP Lit by `sc_pack_unity.py`. Don't hand-pack channels from memory.
- **Texel density and budgets:** PHASE1 3.3. Ground and walls up to 3 m at 1024 px/m, upper storeys, roofs and vault crowns at 512 px/m, heroes 1024-2048 px/m.
- **Collision** is chosen at placement. A wide attachment (a spill, a leaning tool) inflates a box collider into an invisible wall: keep blocking attachments as separate placements, or name the collider in the header.


## 3. Look rules (style bible 3.1-3.4 plus the prop lessons)
Each rule names the review line it moves (sc-asset-review rubric). The ten rules: (1) silhouette first, readable at 20 m; (2) chunky storybook proportion (timber ~1.4x, props ~10% over, doors 2.2 m); (3) mass, then openness; (4) no dead-straight old work; (5) make it the way the real object is made; (6) edges and highlights from the normal field (square-arris spalls, curvature-gated chips); (7) value masses and one pale-against-dark pair; (8) clean base colour (no chroma boost on stone, plaster or roofs, no pure black); (9) tidy placed wear that tells a story (deliver every wear word in the brief); (10) when a material change has no effect, switch lights off one at a time.
- `references/look-rules.md` - read before any detail or convergence pass: the ten rules with their numbers and reasons.

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
- [ ] 6  `sc_snap.py` before any rebake you will A/B; export
```
Iterate with `--out <scratch>`; give generators `--only`, `--no-export` and `--out`; export to the repo only at the end and only when the output changed. Cloud sessions commit the generator, not the FBX or textures.
- `references/running-blender.md` - read before the first Blender run of a session: Blender path, generator and audit command lines, cloud differences.


## 5. Tripo-style construction (style-independent)
- `references/tripo-style-construction.md` - read when blocking out chimneys, dormers, roof slabs, barrels or frames: deform every part, overlap into compounds, lofted rings.

## 6. Determinism and hygiene
Every random draw comes from `random.Random(SEED ^ crc32(stream))`, never `random.random()` or time. No loose verts, no zero-area faces, no buried faces. Write big Python files with the Write tool (heredocs through Git Bash can fail).
- `references/determinism-and-hygiene.md` - read when a bake, lathe, origin or Blender-API call misbehaves.

## References
- `references/tools.md`, `references/characters.md` - tool status and hero numbers.
- `references/look-rules.md` - the ten look rules.
- `references/tier-pipelines.md`, `references/tripo-style-construction.md`, `references/determinism-and-hygiene.md` - construction detail.
- `creatures.md`, `vessels.md`, `stonework.md` - asset-specific notes (linked in section 3); `library/README.md` - generic Blender notes.
