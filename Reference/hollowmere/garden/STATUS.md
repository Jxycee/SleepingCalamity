State: IN PROGRESS
Session: Q6. Garden and allotment decorations
Progress: 8%
Sheet: none
Blocker: none

Score: -
Tris: -
Check: -
Merge: -

## Log
- 2026-10-31: claimed Q6 (`8782020`) after the ground tileables + foliage merge (`9c9e891`). Branch claude/ecstatic-bardeen-pr8wbk (restarted from main). Reading design 5.3 / section 6, the B7 prop generator and contact-sheet flow, and the ring 2 home placement.
- 2026-10-31: plan. Generator `Art/Blender/town/sc_garden.py` (procedural, headless Blender 4.5): VegBed_A/B, Trellis, Scarecrow, WaterButt, Fence_Run, Fence_Gate, ColdFrame, exported to `Content/Art/Town/Garden/` with FOOTPRINT + SOCKET_* (design 5.2-5.3). Materials by slot onto the shared B1 tileables (oak timber, canvas, iron, sandstone, soil, grass): no bakes. Unity: `HollowmereGarden.cs` replaces the greybox allotments on the Rebuilt garden lots (R2_07, R2_18, R2_22, R3_02) and adds a few pieces in ring 2 yards. Checked first: the B7 generator `sc_props_adopt.py` conventions and the existing `Garden` lot kind (greybox). Running on a Sonnet implementer.
