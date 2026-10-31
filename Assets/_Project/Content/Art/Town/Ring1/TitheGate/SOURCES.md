# TitheGate: the sealed tithe gate (Hollowmere fixed landmark, Wave 2)

- Generator: `Art/Blender/town/sc_tithe_gate.py` (procedural, seeded, reproducible from text). Cloud export (2026-10-31, Blender 4.5.3):
  `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_tithe_gate.py -- --out <scratch> --export`.
- `TitheGate.fbx`: root empty `TitheGate` with `Gate` (masonry, iron, breath glow, offerings), `Foliage` (B9 meshes), `SOCKET_Breath` (empty).
  Axes -Z forward, Y up, metres, transforms baked; pivot = keep wall centre line at ground under the gate centre; front = local +Z, so
  `HollowmereTitheGate` places it with ArtYaw 180 on the `TitheGate` layout row at the row's height. Fills the keep wall gap (world x 3.0..9.0).
- Materials (T2, no unique maps): `FK_Sandstone`, `FK_ChimneyStone`, `FK_PlinthJoint`, `FK_Iron` = the B4 kit slots on the B1 tileables
  (WearLit, vertex colour `wear` linear: R grime/rust/soot, G moss, B polish); `TG_Breath` emissive warm amber; `TG_Clay`, `TG_Straw`,
  `TG_Wax`, `TG_Chalk` flat; `M_B9_Foliage` = `T_B9_Foliage_*` here (byte-identical copies of the B9 atlas, one LFS object).
- Tris: 20,855 (gate 15,387 + foliage 5,468).
- Licences: B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`); B9 foliage CC0 (`../../B9/SOURCES.md`).
  No other external assets. References (study only): `Reference/hollowmere/tithe_gate/REFS.md` (CC BY 2.0).
