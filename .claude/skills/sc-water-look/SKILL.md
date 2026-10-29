---
name: sc-water-look
description: Tune or extend SleepingCalamity's water (SleepingCalamity/Water) toward the Riverlight concepts - depth colour, reflections, foam, glints, flow - without breaking the other maps' water. Use for any water colour/reflection/foam card, "the water looks cyan/plastic/flat", shoreline or weir white-water work, or adding a water feature.
---

# Water look

**The water is the hero and the most realistic thing on screen** (concept README rule 3). Players stare at it all game. Every water change is judged from the concept cameras and the stands.

Design and history: `docs/superpowers/design/2026-10-24-millpond-water-look.md` (target, diagnosis, the three phases, what is built, the tune log). Shader: `Assets/_Project/Shaders/Water.shader` (header items 1-18 explain every technique). Read both before changing anything.

## The target (concepts)
- **Depth reads in colour:** amber-olive over stones in the clear shallows (stones and fish shadows visible), teal over the open lake, near-black teal over the Deep Hole (`_AbyssColor` / `_AbyssDepth`).
- **Calm water mirrors the shore** (treeline, cottages): reflection probe (`_ProbeReflect`) plus a planar reflection on the High preset (`PlanarWaterReflection`).
- **White water only where it moves** (the weir lip, the rapids): `_FoamFlow` and `_RapidFoam` from the bed map's b channel (flow). No white band round the still lake.
- **Soft, not toon:** glints are sparkles that break up with distance. No hard white "sticker" ellipses (harbour round 4).

## Rules
1. **`SleepingCalamity/Water` is shared** (M_Water, M_Water_Harbour, M_Water_Surface, M_Water_Creek, M_Water_Millpond). Every new property defaults to **off**, so the other maps draw exactly as before. `WaterLookTests` guards that the non-Millpond materials keep their values. Keep that test green and extend it.
2. **Wave height only from `SleepingCalamityCommon.hlsl`** (`SC_WaveHeight`, with the non-finite guard): CPU and GPU parity (fishing, buoyancy). Never re-implement waves in the shader.
3. **Per-map look lives in `RenderMaterialLibrary`'s material block** (`M_Water_Millpond`). Bed data lives in the bed map (`MillpondBedMap`: r height, g zone tint, b flow).
4. **Performance:** target 60 fps on most platforms (user, 2026-10-24). The planar reflection runs on the High preset only, at half resolution. Count the shader's per-pixel cost in the header when adding work (it lists the current cost).
5. Change one thing per capture; log it in the design doc's **Tune log** with the measured numbers.

## Tuning loop
1. Capture (local, Unity): `MillpondCapture` concept views + `tug_deck`, `landing`, `mill_weir`, `island`, `dusk_tug_deck`.
2. Measure the water regions with `Art/Blender/tools/sc_concept_match.py` (crop the same water area in the concept and the capture: value, saturation, warm/cool). The design doc's tune 1 used concept-camera medians the same way.
3. Diagnose:

| Seen | Knob |
|---|---|
| cyan, saturated, plastic | `_ShallowColor` / `_DeepColor` toward the measured teal/olive; `_Saturation` down; `_LightTint` |
| no depth read | `_DepthRange`, the abyss stop, `_BedCover` (lets the stones show in the first metre) |
| flat, no shore in it | `_ProbeReflect`, `_ReflectStrength`, `_ReflectFloor`; the probe box over the water |
| white rings round the lake | `_FoamFlow` on (foam only where the bed map says it flows) |
| stickers / sparkle noise far away | glint softening past 18-60 m, `_GlintRipple`, `_GlintFlatten` |
| blotchy far water seen from above | `_FarCalm`, `_CalmDistance` |

4. Post the before/after captures and numbers on the board card.

## In the cloud
No Unity: you can read and reason, edit material values and the shader with tests, and prototype colour with `sc_concept_match.py` on existing captures under `Art/Previews/`. Every water change needs a local compile and capture before it merges: board card, owner Local PC, alert.
