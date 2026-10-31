# Schoolhouse plan (Q2, paused 2026-10-31 before any code)

Generator to write: `Art/Blender/town/sc_schoolhouse.py` (root `Schoolhouse`, class M), export `Assets/_Project/Content/Art/Town/Ring2/Schoolhouse/Schoolhouse.fbx`.
Brief: the shared Q2 brief (bakery template `sc_bakery.py`, contract 5.2, path_mode STRIP, deterministic seeds), budget < 40k tris.

- Body x +-2.6, y -2.7..3.5 (footprint ~5.4 x 6.4 m); eave plate z 4.4, ridge 7.5 (~50 deg), eave overhang 0.45, verge 0.4 (inside +-3.95 m).
- Walls: limewashed stone over a stone ground course, pale quoins, heavy oak lintels; gables green-painted boarding with battens (distinct from bakery/facade).
- Roof: steep full gable, red tile, ridge front to back. Three tall deep-set multi-paned windows per long side, two at the back; dark interior liner.
- Bell-cote: small open timber cote with its own tiled gable roof astride the ridge at y ~ -2.55; empty state = bare iron bearing blocks.
- Front: tiled pent hood on knee brackets over the door, bench to the left, notice board (slate, pinned papers) to the right; two empty iron sign hooks above the hood.
- One small ridge chimney at y ~ +2.5. Yard: stepping slabs front, hopscotch slabs along +X, water butt back right.
- Parts: Part_Door (FK.kit_door), Part_Bell (bronze bell, oak headstock, iron straps/gudgeons, half wheel, rope + toggle), Part_Sign (green board, gilt lettering blocks, chains); each pivoted at its SOCKET_*.
- Slots: FK_Plaster, FK_Oak, FK_OakDoor, FK_Sandstone, FK_ChimneyStone, FK_RoofTile, FK_Shutter, FK_DoorPaint, FK_Iron, FK_Glass, BK_Gilt + new SH_Limewash, SH_Slate, SH_Bronze, SH_Chalk, SH_Rope, SH_Interior (no emissive).
- Refs folder `Reference/hollowmere/kit_ready/schoolhouse/` not created yet.
