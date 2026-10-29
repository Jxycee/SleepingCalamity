---
name: sc-scene-look
description: Judge and tune how a SleepingCalamity map looks as a whole - light, atmosphere, colour grade, composition, density and the water - against the Riverlight concept paintings, using the fixed concept-camera captures. Use for "the scene looks flat/CG/empty", look-tune cards, before calling a region "matching the concepts", when placing or dressing many assets, or when choosing what to build next for the biggest visual gain.
---

# Scene look (Riverlight, spec R102)

A AAA-feeling frame comes from five layers, in order of impact. Fix the highest broken layer first. Polishing a prop never rescues a frame whose light or composition is wrong.

1. **Light and atmosphere:** soft warm sun, cool coloured shade, layered distance haze
2. **Composition and value grouping:** big readable light/dark shapes, a focal point, a clear foreground / middle ground / background
3. **Colour:** the concept palette, desaturating with distance; a few saturated accents near people
4. **Density and storytelling:** dressing at the hands ring, life (smoke, birds, washing, lamps), the restore-the-village arc
5. **Surface and asset quality:** shapes, painterly grain (the sc-asset-* and sc-texture-paint skills)

## Where the knobs are
| Layer | Knob | File |
|---|---|---|
| sun warmth/strength, ambient lift, haze colour and amount, horizon | `MillpondLayout.Look()` (`sunTint #ffe3b8`-ish, `hazeColor`, `hazeAmount 0.75`, `horizonColor`, `ambientLift`) | `Assets/_Project/Editor/Builders/Millpond/MillpondLayout.cs` |
| lighting model | `SoftLight 1`, `SoftLightWrap 0.2`, `SoftAmbientGain 1.3` (SC_WorldLight); painterly colour `Paint` (SC_PaintVariation, R102) | same + `Shaders/SleepingCalamityCommon.hlsl` |
| fog distance | `FogDensity 0.0057` (end about 350 m), `FogWeatherFloor` | MillpondLayout |
| grade per season (warm tops, cool undersides, per-object hue noise) | `ArtPaletteDef.ToonGradeFor(season)` | `Data/Art/ArtPaletteDef.cs`, `Content/Art/ArtPalette.asset` |
| terrain colour and variation | `FGT_*` defines (brightness patches 9/31/87 m, warm/cool at 140 m, steep tint) and `kLayerTint` | `Shaders/ToonTerrainForward.hlsl` |
| day phases | `DayNightController` (drives the sun, `_ToonShade`, sky globals) | `Presentation/World/DayNightController.cs` |
| water (the hero) | `Shaders/Water.shader`; depth colours per spec R73 | |

## The evidence loop
1. **Capture (local only, Unity):** `MillpondCapture.Capture` renders the five `concept_*` views with the paintings' cameras and sun, plus gameplay shots (`tug_deck`, `landing`, `mill_weir`, `island`, `cottage_close`, `mill_close`, `landing_props_close`, `berth_close`, `dusk_tug_deck`). The editor must be closed for batch; in the open editor use SleepingCalamity/Millpond/Capture Concept Views.
2. **Compare:** concept vs capture side by side, full frame and per region, with `Art/Blender/tools/sc_concept_match.py` (value shift, contrast, saturation, warm/cool, 0-100 score). Do it for the whole frame, then for the far hills, the water, the village, and the near foreground.
3. **Diagnose by layer**, with the table below. Write the numbers down.
4. **Change one layer's knob,** recapture, re-measure. Record before -> after on the board card. Never tune two layers in one capture.

| Symptom (capture vs concept) | Layer | Likely fix |
|---|---|---|
| everything darker, contrast low | 1 | ambient lift / soft ambient gain up; or the assets' albedo too dark (sc-asset-review defect table): check one asset first |
| distance as crisp as foreground; hills same value as the village | 1 | haze amount/colour toward the concept's grey-green (#8a9089), fog density |
| shade side black or grey, not blue | 1 | `_ToonShade` / the grade's cool bottom tint; SoftAmbientGain |
| busy, noisy, no focal point | 2 | fewer, larger shapes; remove scatter in the focal area; darken the frame edges' dressing |
| saturated everywhere (CG look) | 3 | land desaturation, slot tints toward the README palette; keep saturation for accents near people |
| empty, toy-like | 4 | dressing at the hands ring (spec R79): crates, rope, nets, washing, smoke, lamps; life (birds, insects) |
| surfaces look CG | 5 | painterly colour on, texture chroma/kuwahara (sc-texture-paint) |

## Lessons from shipped stylised games
Sea of Thieves' environment rules (Lee Piper, art lead; GDC "Visual Adventures on Sea of Thieves") fit Riverlight closely:
- **Complexity comes from placement, not from single assets:** density, size, colour and shape relationships between assets. An asset only needs enough detail to say what it is.
- **Test silhouettes in collections, never one at a time.** An asset that reads alone can merge with its neighbours.
- **A few differentiated material presets**, not a material per asset. Here that's the materials table in sc-asset-craft.
- **No granular noise in textures:** brush shapes, not grit.
- **Graphic edges with painterly interiors** on foliage.
- **Accent colour is for navigation and play.** Spend saturation where the player should look.
- **The engine is the final judge** (3DSkillUp's pipeline guide says the same): Blender previews and scores are proxies until a capture agrees.
- **Paintovers** (Level Design Book): paint over a capture to show the next version before building it. Use them for the composition layer.

## Composition rules from the concepts
- **The water is the hero and the most realistic thing on screen.** Shore assets frame it. Never block the water view from the stands or the tug deck.
- **Three depth layers per view:** dark detailed foreground (reeds, a post, a boat), lit middle ground (the village, the landing), hazy background (hills, spruce ridges in 2-3 value steps).
- **Value grouping:** the concepts read as 3-5 big value shapes at thumbnail size. Squint test: shrink the capture to 160 px wide. If it's mush, the problem is layers 1-2, not assets.
- **Restoration is visible from across the lake:** rundown = muted greys, boarded, long grass; restored = a few saturated accents (mill red, teal doors, mustard shutters), smoke, lit windows, bunting.
- **Review from the play positions only** (spec R79): the stands, the tug deck, the camp and the arrival. Never fly-throughs.

## In the cloud (no Unity)
You can't capture, but you can:
- review and tune assets (sc-asset-review)
- prototype a look change in Blender with `sc_asset_preview.py` (its light constants mirror the soft model)
- change C# look constants and the shader with tests, which then need a local compile and capture: post a board card (kind review, owner Local PC, alert) naming the exact shots to compare, and the numbers you expect to move

Never claim a scene-level look change works without a capture.
