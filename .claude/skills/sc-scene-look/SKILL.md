---
name: sc-scene-look
description: Judge and tune how a Sleeping Calamity scene looks as a whole - light, atmosphere, colour grade, composition, density and dressing - against the style bible (docs/PHASE1.md 3.1, 3.5, 3.7), the URP stack and the reference row, using the fixed capture cameras and sc_ref_palette.py. Use for the Hollowmere beauty corner and the Cellars dungeon corner, "the scene looks flat/CG/empty/grey", look-test scene work, before calling a corner ready for the user's approval, when dressing many assets, or when choosing what to build next for the biggest visual gain.
---

# Scene look

The target (PHASE1 section 3): Dark Cloud's town and dungeon themes in OoT-2026 stylised realism. **Light and atmosphere carry the look** (handoff 2.1): volumetric god rays, haze and aerial perspective, drifting motes and leaves, a soft key with deep readable shadows, dense dressing.

A AAA-feeling frame comes from five layers, in order of impact. Fix the highest broken layer first; polishing a prop never rescues a frame whose light or composition is wrong.
1. **Light and atmosphere:** the key, the fill, haze matched to the horizon, volumetric shafts, particles
2. **Composition and value grouping:** 3-4 big readable masses, a focal point, foreground / middle ground / background
3. **Colour:** PHASE1 3.1 key hues (sc-asset-craft's palette summary); saturation spent where the player should look
4. **Density and storytelling:** dressing in every corner, life (motes, leaves, smoke, lamps), the rebuild-the-town arc
5. **Surface and asset quality:** sc-asset-* and sc-texture-paint

## The stack and its knobs (PHASE1 3.5, `docs/research/urp-stylised-realism.md` section 9)
URP 17.6, Forward+, Render Graph, HDR, GPU Resident Drawer. Build and compare each step in the look-test scene (town square plus a cellar room, M8) before adopting it. The look-test pipeline asset turns the GPU Resident Drawer off (decisions A23: it picked the wrong textures in batch renders), so the real game setting is not tested there.

| Layer | Day (Hollowmere square) | Cellars (Undercroft floors 1-4) |
|---|---|---|
| Key | one warm sun about 5200 K, cool sky fill; 4 cascades, about 100 m, 4096, soft High | Forward+ lamps, shadows only on the nearest 2-4; emissive lamps and embers |
| GI | Adaptive Probe Volumes | realtime (generated floors can't bake): per-band ambient gradient; if captures read flat, next test is additive room scenes with their own APV bake |
| AO | SSAO, Depth Normals, radius about 0.35 m, low direct-lighting strength | same |
| Atmosphere | exponential haze matched to the horizon colour; volumetric god rays; drifting pollen and leaves | dust motes in the lamp shafts; warm haze only, no teal or blue-green tint anywhere (PHASE1 3.1, user 2026-10-30) |
| Post | Neutral tonemapper plus grade (compare ACES once), bloom threshold about 1.0, modest intensity, high scatter, no lens dirt | same; the ember glow from below is a small red accent |

Phase 1 has no day/night cycle: the town is graded at midday plus one low-sun capture.

## The evidence loop
1. **Capture (PC, Unity):** fixed capture cameras per corner (wide, mid, close) at 1920x1080 so every round is identical (PHASE1 3.7). The look-test builder exists: `Assets/_Project/Editor/Builders/LookTestBuilder.cs` builds `LookTest_Day` and `LookTest_Cellar`, and `LookTestCapture.cs` writes the captures to `Art/Textures/looktest/` (1024 px JPGs). The corner scenes have their own builders and captures (BeautyCornerBuilder.cs, BeautyCornerCapture.cs, DungeonCornerCapture.cs, HollowmereCapture.cs in Assets/_Project/Editor/Builders/); use those cameras and keep the views fixed across rounds.
2. **Measure:** `python -I Art/Blender/tools/sc_ref_palette.py <capture.png> --k 7 --sheet <scratch>/palette.png` for value p10/p50/p90, warmth and masses; crop to regions (sky, facade, ground, the focal prop) with `--crop`. Compare with the PHASE1 3.1 bands:
   - Day: value 0.21-0.25 / 0.44-0.50 / 0.80-0.86, warmth +0.12 to +0.20, 3-4 masses (pale plaster or sky, dark timber or shadow, warm mid stone or roof); shadows slate-teal `#2f3a44`.
   - Cellars: value 0.12-0.16 / 0.30-0.40 / 0.65-0.75, warmth +0.20 to +0.30; clean warm firelight, no teal or blue-green: saturated gold lamps (`#d6a24a`) dominant, shadows deep umber to plum (`#2a1c18` to `#3a2430`), warm brick mid, crisp value steps, not muddy or brown-hazy; ember `#c2412a` a small accent; no mass pure black.
3. **Compare** with the reference row side by side: the paintings for value masses and atmosphere, the photos for material and density, the private style frames (`Reference/private/`, study only) for the target's level.
4. **Diagnose by layer** with the table below; write the numbers down.
5. **Prefer one layer's knob per capture,** recapture, re-measure, and record before -> after in the report. Changing two layers at once makes the numbers hard to attribute (a working habit, not a rule; the M8 rounds did move several related knobs together and logged them).

| Symptom (capture vs target) | Layer | Likely fix |
|---|---|---|
| p50 low, everything murky | 1 | sky fill / ambient up, or the albedos are too dark (check one asset in the studio render first) |
| distance as crisp as foreground | 1 | haze density and colour toward the horizon; volumetric strength |
| shadows crushed black, or no shape in the Cellars darks | 1 | fill or ambient gradient up; SSAO direct strength down; lift the grade's toe |
| flat, no light direction | 1 | stronger key-to-fill ratio; god rays on the key; contact shadows from SSAO |
| warmth outside the band | 1/3 | key colour temperature, grade white balance; Cellars lamps more gold |
| busy, noisy, no focal point | 2 | fewer, larger shapes; clear scatter from the focal area; darker dressing at the frame edges |
| photo-dull or grey | 3 | material chroma per PHASE1 3.1 (sc-texture-paint), then grade saturation |
| saturated everywhere | 3 | pull back the grade; keep saturation for roofs, cloth, painted wood and accents near play |
| empty, toy-like | 4 | dressing in every corner: barrels, sacks, crates, ivy, weeds, banners; motes and leaves |
| surfaces look CG | 5 | sc-texture-paint and sc-asset-review defects |

## Look-test lessons (M8, `Art/Textures/REVIEW.md`)
- **Volumetrics (A23):** shafts are our own additive light-cone meshes (`SC_LightCone.shader`) plus exponential fog, not the CristianQiu package; try the package later if the cones look cheap.
- **Which knobs moved the numbers:** day sun intensity, ambient intensity, grade contrast and lift, cobble tint and sky thickness moved day value; cellar lamp intensity, ambient, exposure and white balance moved the Cellar. Lift is easy to overdo (+0.07 washed the day out; 0.02 was enough).
- **Greybox limit:** the flat pale ground and the sky dominate the day p50 and p90, so those two cannot reach the band until real dressing and shadow shapes exist; do not over-tune the grade to compensate.
- **Capture hygiene:** bake the reflection probe in the capture batch (metals render black without one); a purple cobble was a low-sun fill problem, fixed with a neutral-warm fill; discard incidental `GraphicsSettings` / `QualitySettings` asset re-saves the capture run leaves behind; rebuild the look-test before committing after a capture, because the decal materials flip their premultiply keyword during a run and need their post-build state.

## Lessons from shipped games
- **Complexity comes from placement, not single assets** (Sea of Thieves, Lee Piper): density, size, colour and shape relationships between assets. An asset only needs enough detail to say what it is.
- **Test silhouettes in collections**, never one at a time.
- **A few strong shared materials**, not a material per asset: the area's tileables and trims.
- **Accent colour is for navigation and play.** Spend saturation where the player should look.
- **The engine is the final judge:** Blender renders and numbers are proxies until a capture agrees.
- **Paintovers:** paint over a capture to show the next version before building it, for the composition layer.

## Composition
- **Three depth layers per view:** detailed foreground (a barrel, ivy, a post), lit middle ground (the facade, the fountain), hazy background (the bell tower, the lake below the hilltop) in 2-3 value steps.
- **Value grouping:** 3-4 big masses at thumbnail size (PHASE1 3.1; the paintings in the reference row show where the darkest dark and lightest light sit). Squint test: shrink the capture to a small thumbnail (about 160 px wide works). If it's mush, the problem is layers 1-2, not assets.
- **The rebuild arc is visible:** the restored town gains saturated accents, smoke, lit windows, banners and bunting over the bleached, dry square.
- **Review from play positions,** at the game camera's height and distance, never fly-throughs.

## The approval gate (PHASE1 3.7)
Before showing the user: the captures fall inside the 3.1 bands, and a reviewer has scored them with the sc-asset-review rubric against the reference row. The user approves on captures; that is the only sign-off. If the user rejects, their notes are the next round. Approved captures become the reference row for every later batch. Before the M9 captures, compare the targets against the remake's official screens in `Reference/private/` (a check, not a blocker).

## In the cloud (no Unity)
You can review assets, measure existing captures, and change C# look constants or builders with tests. Every scene-level change then needs a PC compile and capture: name the exact views to compare and the numbers you expect to move. Never claim a look change works without a capture.
