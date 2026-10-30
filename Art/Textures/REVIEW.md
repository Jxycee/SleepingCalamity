# Texture batch reviews

## B1-B3 first pass (controller review, 2026-10-30, P50)
Contact sheets: `Art/Textures/B1_town/sheet.jpg`, `B2_cellar/sheet.jpg`, `B3_trims/sheet.jpg`.

**Scores:** B1 6/10, B2 6.5/10, B3 4/10. A solid scan base; the plaster hue matches the bible.

**Worst three (fix first):**
1. **Canvas reads as flat CG yellow with painted stripes.** It needs a visible weave, slub and sun fade, a muted ochre, and stripes that are dyed into the cloth rather than flat paint.
2. **Saturation is above the muted OoT bible (3.1, updated 2026-10-30).** Painted wood is cyan-blue and should be a faded, chalky blue-grey with wear to bare wood. Roof tile is a hot orange-red and should sit at or toward `#9c4a34`, with more variation from tile to tile and less high-frequency noise at gameplay distance.
3. **The decals barely read.** Crack and drip are almost invisible at sheet scale and need stronger normal and albedo contrast. Moss is a camouflage blotch: it needs clumped olive-to-yellow cushions with soft edges (`#616e2e` / `#b6ab28`) and real height.

**Also:**
- Oak timber is too grey and cool. Half-timber beams should be a dark, warm brown in `#2e2520` to `#4e4844`, with a visible grain direction.
- Root bark reads as planks with knots, not bark. It needs fibrous, twisting ridges and a damp sheen.
- Old brick leans pink-orange. Pull it toward a muted red-brown with varied bricks and soot.
- Plaster is flat. Add subtle trowel undulation and edge grime, keeping the hue.
- Limewash blotches read as a pattern. Break up their repetition.
- Iron is very dark without a reflection probe. Recheck it in the Unity look-test before changing it.
- Cobble repeats at 2 m. Fix with a detail-noise or macro variation layer.

**Next:** fix these, then measure in the Unity look-test scene against the 3.1 scene bands (M8 part 2).

## M8 part 2: fixes and the Unity look-test (implementer's own read, 2026-10-30)
**Fixes done** (generators `Art/Blender/textures/sc_tex_batches.py`, `sc_tex_b3.py`; sheets re-rendered and re-measured, SOURCES.md tables regenerated):
- Canvas: a procedural plain weave (8 px thread pitch) with slubs, muted sun-faded ochre `#b08c52`, three madder bands dyed into the threads with jittered edges, broad sun fade.
- Painted wood: chalky blue-grey `#62808c`, chroma 0.85. Roof tile: `#92503c`, chroma 0.82, per-tile value variation 0.20, fine colour noise softened.
- Decals: crack widened and deepened (stronger normal and alpha), drip wider with a dark core, moss rebuilt as about 420 clumped cushions (olive to yellow-green tops, soft alpha edge, real height). The sheet shows decals flat and close.
- Oak timber: warm dark brown `#3d2d22` with long grain streaks. Root bark: a real bark scan (`bark_brown_01`, fibrous ridges, damp furrows). Old brick: muted red-brown `#94654a` with soot patches. Plaster: undulation layer. Limewash: a plainer scan (`plastered_wall_03`) with two-scale domain-warped damp patches. Cobble: stone-by-stone tint plus larger variation.

**New sheet scores (own read):** B1 7.5/10, B2 7.5/10, B3 7/10. Still weak: iron is dark in a flat rig (judge it in the look-test), old brick still leans warm, the trim sheets are only judged in albedo plus a lit render.

**Look-test** (`LookTest_Day`, `LookTest_Cellar`, builder `LookTestBuilder`, captures `LookTestCapture`): captures in `Art/Textures/looktest/` (1024 px JPGs), measured with `sc_ref_palette.py --k 7` (pooled frames):
| | p10 / p50 / p90 | warmth | band (PHASE1 3.1) |
|---|---|---|---|
| Day, 3 midday + 1 low sun | 0.16 / 0.55 / 0.70 | +0.15 (in) | 0.21-0.25 / 0.44-0.50 / 0.80-0.86, +0.12 to +0.20 |
| Cellar, 3 views | 0.07 / 0.33 / 0.60 | +0.33 (just high) | 0.12-0.16 / 0.30-0.40 / 0.65-0.75, +0.20 to +0.30 |
Day: warmth in, p10 low, p50 high, p90 low. Cellar: p50 in, p10 and p90 low, warmth slightly high. Volumetrics: the CristianQiu package was not tried; the shafts are additive light-cone meshes (`SC_LightCone.shader`) plus exponential fog.

## M8 part 3: look tune (implementer, 2026-10-30)
Measured with `sc_ref_palette.py --k 7` on the pooled frames; the same seven captures each round.
| Round | Change | Day p10 / p50 / p90, warmth | Cellar p10 / p50 / p90, warmth |
|---|---|---|---|
| start (part 2) | | 0.16 / 0.55 / 0.70, +0.15 | 0.07 / 0.33 / 0.60, +0.33 |
| 1 | day sun 2.3 to 3.0, ambient 1.5 to 1.0, contrast +8, lift +0.07; cellar lamps 8 to 14, white balance -6, lift +0.06 | 0.34 / 0.64 / 0.82, +0.24 | 0.31 / 0.49 / 0.73, +0.27 |
| 2 | lift cut to 0.02 (washed out in round 1) | 0.24 / 0.62 / 0.79, +0.23 | 0.22 / 0.45 / 0.70, +0.33 |
| 3 | cobble tint 0.78, sky thickness 0.6; cellar lift 0, ambient 1.8, exposure 0, white balance -14 | **0.23 / 0.52 / 0.77, +0.07** | **0.13 / 0.35 / 0.62, +0.30** |
| band | | 0.21-0.25 / 0.44-0.50 / 0.80-0.86, +0.12 to +0.20 | 0.12-0.16 / 0.30-0.40 / 0.65-0.75, +0.20 to +0.30 |
Day: p10 in; p50 +0.02 high, p90 -0.03 low, warmth -0.05 low. Cellar: p10, p50, warmth in; p90 -0.03 low. Also changed: the reflection probe is baked in batch (a temporary EXR, switched to Custom for the capture, deleted afterwards), the low-sun fill is neutral-warm (the purple cobbles are gone), the ember grate emission and light are lower. What can't get there with greybox geometry: the day median is dominated by the flat pale cobble ground and sky, so p50 and p90 need real dressing and shadow shapes.
