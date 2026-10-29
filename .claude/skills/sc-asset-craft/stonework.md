# Stonework, vaults and modular runs: walls, arches, kerbs, vault segments

Part of **sc-asset-craft**: read SKILL.md first; this file adds what is specific to stone and modular runs (batch B4 plinths, B5 vault kit, B6 scatter). Lessons from FishGame's 2026-10-25 dry-stone wall test and the AAA cottage; treat numbers as a starting point.

- **Pick the tier per surface.** A wall face is a T2 kit piece on the brick or stone tileable; repeated stones that must stand proud (plinths, steps, chimney stones, cobbles, rubble) are a T3 family: a few sculpted variants baked once, instanced and scaled. The cottage's first stone kit (324 blocks x 90 tris) blew the budget; 60-tri lows with the wall-facing and buried faces culled at assembly fixed it.
- **Spalls get a square arris.** A chipped stone edge is a flat scar with a crisp edge; dome bites read as bullet holes. Dressed stone (the fountain's sandstone, voussoirs) gets chisel-dressed facets on the high, with a pale fresh face only where a spall cut it.
- **Mortar backing:** cottage v1's stones read pillow-puffy with black voids between them; the designed fix is a smaller bevel plus a recessed lime-mortar bed behind them.
- **Course it** the way a mason would: bigger stones low, through-stones and copes on top, brick in a real bond, arches with voussoirs radiating from the springing (about 1.3x real, PHASE1 3.4).
- **Break the top line.** A dead-flat cope reads machined: stepped copes, a slumped section, a stone standing proud, a 1-3 deg lean on old work.
- **Run axis and origin.** Author runs along one axis and keep the family's convention so modules join; the origin sits on the wall line at one end (or its centre), at y 0. Bounding-box centring is pulled off the line by spill and plants. Render runs side-on, never end-on.
- **Barrel vaults** (B5): build the segment as one module whose springing and crown meet the neighbours exactly; the crown is at 512 px/m and the walls up to 3 m at 1024 px/m (PHASE1 3.3), so split the UVs at that line.
- **Living parts on stone** (ivy, crack weeds, moss tufts, roots) come from **sc-foliage** as separate placements or decals, not baked into the stone.
- **Budget:** kit pieces 0.5-5k tris, instanced pieces 50-400 each (PHASE1 3.3). Judge a run per metre against its neighbours in the same kit.
