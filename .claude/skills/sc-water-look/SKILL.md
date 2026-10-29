---
name: sc-water-look
description: Build or tune Sleeping Calamity's water in the locked stylised-real look - cistern pools, flooded cellar floors, the town fountain once it flows, the lake below Hollowmere, liquids in props - with depth colour, reflection, refraction, foam and the generic one-knob capture-and-measure tuning method. Use for any water or liquid shader work, "the water looks cyan/plastic/flat", or adding a water feature.
---

# Water look

**Status: dormant in Phase 1.** Phase 1 has no water surface: the fountain is dry, the Cisterns band (teal water) comes later, and the lake shows only as a distant backdrop from the hilltop square. Small liquids in props (a puddle, a crock's contents, a potion) use the fake-refraction method in sc-asset-craft section 1. Use this skill when a water feature is planned; no water shader exists in the repo yet.

## The target
- **Dark scenes in rich teal and gold** (handoff 2.1, the remake's Water Temple): the Cisterns' water is teal in shadow and gold where lamplight hits it. The Cellars stay warm (PHASE1 3.1, [P34]): any cellar puddle or flooded floor reflects gold lamps, with teal only from the grade.
- **Depth reads in colour:** clear and warm over shallow stone or brick, deeper teal over depth, near-black teal only where it is really deep, never pure black.
- **Still water mirrors its surroundings:** vaults, lamps and columns in a cistern; the town and sky in the lake and fountain.
- **Movement where water moves:** foam and white water only at spouts, falls and inflows, not as a ring round still water.
- **Stylised-real, not toon:** glints are real specular that break up with distance; no hard white sticker shapes.

## How to build it
- **Research the common URP practice first** (depth-based colour, screen-space or probe reflection, refraction from the opaque texture, flow maps for foam) and adopt a proven approach before writing a custom shader.
- **Fake refraction and reflection are acceptable and often better:** refract or reflect the view ray into a procedural room or a probe, with Schlick Fresnel, as the stand's glass and liquids did (handoff 4.3).
- **Reflection probes** over every body of water; a planar reflection only where a capture shows the probe isn't enough.
- **Wave height from one shared function** if anything on the CPU depends on it (buoyancy, fishing later), so CPU and GPU agree.
- **A shared water shader:** each new property defaults to off, so existing water materials draw exactly as before, guarded by an EditMode test of their values.
- **Per-pixel cost:** list it in the shader header when adding work. Quality comes first (CLAUDE.md), but four co-op cameras share the frame.

## Tuning loop (generic)
1. Capture the water from fixed views at play positions (the PHASE1 3.7 capture builder once it exists).
2. Measure the water region with `python -I Art/Blender/tools/sc_ref_palette.py <capture.png> --crop <box> --k 7`: value, warmth and masses, against the reference row (photos of cisterns, flooded cellars, fountains, lakes) and the scene's PHASE1 3.1 band.
3. Diagnose:

| Seen | Usual knob |
|---|---|
| cyan, saturated, plastic | shallow and deep colours toward the measured teal or gold; saturation down |
| no depth read | depth range, the deep colour stop, how far the bed shows through |
| flat, nothing reflected | reflection strength and floor, the probe's box over the water |
| white rings round still water | foam only where flow says it moves |
| sparkle noise far away | glint softening with distance |

4. Change one knob per capture, and log before -> after with the measured numbers in the report.

## In the cloud
No Unity: edit material values and the shader with tests and prototype colour on existing captures. Every water change needs a PC compile and capture before it merges.
