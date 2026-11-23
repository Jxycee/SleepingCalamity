# Hollowmere next pass, ranked (Neon, exchange 8, 2026-11-22)
Summary of Neon's md reply after the ground-blend results.

Keep our baker and helper fixes as the baseline.

## 1. Wall-kit regeneration + Megascans rubble/hedge/ivy swap (+3 to +5; OrchardGap, Orchard, Spawn)
- Regenerate the Blender wall kit (`Art/Blender/town/sc_town_wall.py`): no boxy merlon teeth, no floating parapet block, no "L" artifact. Stepped and eroded courses instead of ruler-straight tops and clean breach cuts; chamfered and displaced corners. Write SC_WEAR vertex colours (R grime, G moss, B polish) in the generator so the existing shader path fires.
- Swap the faceted rubble (OrchardGap), floating slabs (Orchard) and card/box hedges for Megascans 3D rubble, boulders, broken masonry corners and overlapping bush/bramble clusters (grounding helper, 15-25 % sink). Drape Megascans ivy/moss over wall tops and breach edges.

## 2. Ground and foliage cohesion (+2.5 to +4; Spawn, Orchard, Meadow)
- Cobble terrain layer: smoothness <= 0.25, normal strength 0.5-0.65x; remove the hard rectangular patch in Spawn; irregular dirt/moss splat edges at stall and wall bases.
- Clumped grass (3-6 mixed tufts per clump, bare rests between clumps). Ferns, clover, leaves and stones along the Orchard grass-to-dirt edge and on the flat OrchardGap green.
- Swap the procedural cart and the single props for Megascans wood/crate/barrel/sack clusters at wall corners and stall legs, ringed with micro-debris.

## 3. Backdrop and atmospheric depth (+1.5 to +3; Spawn, Meadow, HillWest, RoadOut)
- Dress the smooth cone peak with Megascans cliff/ridge meshes and low-contrast treeline cards; more height and distance haze.
- Orchard: a zone reflection probe or sky-bounce fill so shadowed barrels and crates don't crush to black.

Order: Pass A = 1 (fastest proof on the lowest cameras). Pass B = cobble calm + Spawn patch + clumped grass + the Spawn peak.

Coordinator note: this overlaps Run 8's planned T25 story pass and the area-by-area AAA pass. Fold it into Run 8's resume order when the user restarts Run 8.
