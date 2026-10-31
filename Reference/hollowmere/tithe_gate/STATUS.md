State: DONE
Session: Wave 2: Tithe gate (fixed landmark)
Progress: 100%
Sheet: Reference/hollowmere/tithe_gate/tithe_gate_sheet_final.jpg
Blocker: none

Score: ~31/40 (r1 26.5 -> r2 30 -> r3 31, coordinator approved r3 ~30; r4 polish: 7 bands, heavier lock plate). Unity captures: tithe_gate_sheet_final.jpg (HM_TitheGate both presets, midday + low sun), tithe_gate_sheet_close.jpg, captures/.
Tris: 22,133 (gate 16,665 + B9 foliage 5,468)
SC RESULT check PASS (build PASS, test-edit 592/592, test-play 32 pass 8 skipped; SC MARKER cf94cb4 = branch tip)
Merge commit: 6260a84 (main)

## Log
- Generator `Art/Blender/town/sc_tithe_gate.py` (T2: B1 sandstone + iron tileables via the B4 FK_* slots, linear `wear` attribute for WearLit, emissive TG_Breath, flat TG_Clay/Straw/Wax/Chalk, B9 ivy/weeds/moss). Export + SOURCES.md in `Assets/_Project/Content/Art/Town/Ring1/TitheGate/` (cloud Blender 4.5, like the Ruins kit).
- Fits the keep wall gap (WallRun leaves world x 3.0..9.0): 6.9 m surround, arch opening 3.4 x 4.25 m (sill 0.45, springing 3.0), gable to 6.55 + finial over the 4.9 m parapet, pilasters with pinnacles, keystone carved with a wheat sheaf. Right leaf sprung (twisted 1.2-4.2 deg, dropped, gap widest at the top). Breath glow through cracks, the sprung gap, keyhole and leaf edges; breath soot above the arch. Worn, dished steps; bowls of grain (one tipped), candle stubs, sheaves; tithe tallies carved on the left jamb and pilaster and chalked on the left leaf.
- Builder: `Editor/Builders/HollowmereTitheGate.cs` (TryPlace at the row's own height y 0 with ArtYaw 180, slot materials, warm spill point light, box colliders with walkable steps; `CaptureGate` = Cap_HM_TitheGate + a close view, both presets x Midday/LowSun). `HollowmereBuilder` TitheGate case calls it first, greybox kept as the fallback.
- Unity fixes: one-sided cards (cracks, tallies, breath card) faced backwards and were culled -> wound to the front; breath colour shifted redder (1, 0.30, 0.06) x 1.8 because the corner grade pushes amber to yellow-green; spill light softened and moved out over the steps.
- Checked before building (user rule): reused the project's proven T2 kit route (sc_ruin_kit / sc_facade_kit: shared tileables + wear colour + WearLit) instead of a new system; references via sc_ref_fetch.py (Openverse, CC BY 2.0, REFS.md).
- Parked: the spill light reads a little red on the steps at low sun; a slow breath pulse (emission/light animation) and warm motes drifting from the gap would sell "breathing".
- Note: every Unity run here re-serializes HeroAnimator.controller (unrelated); reverted each time, never committed.
