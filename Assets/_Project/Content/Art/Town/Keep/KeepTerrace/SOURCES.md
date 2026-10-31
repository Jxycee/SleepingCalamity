# KeepTerrace (Hollowmere keep terrace kit): sources

- Generator: `Art/Blender/town/sc_keep_terrace.py` (procedural, builds on the `sc_facade_kit.py` helpers). Cloud export (2026-10-31, Blender 4.5.3):
  `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_keep_terrace.py -- --out <scratch> --no-render --export --export-dir Assets/_Project/Content/Art/Town/Keep/KeepTerrace`.
  Texture paths are stripped (`path_mode='STRIP'`). Contract: `Reference/hollowmere/keep/CONTRACT.md`.
- Axes: authored as Unity (x, y, z); exported -Z forward, Y up, so Unity (x, y, z) = Blender (-x, z, -y). Every piece is a direct child mesh of root `KeepTerrace`,
  at its own pivot at the origin. 20,666 tris in all.

| Object | Tris | Unity local extent (x / y up / z) | Notes |
|---|---|---|---|
| `WallSegment` | 7,860 | -1..1 / -0.6..4.9 / -0.62..0.6 | square-side face at z -0.6; plinth, string course at y 4.0, parapet, coping overhangs to +-0.6. Tiles every 2 m (stones cut on the seam, UVs and wear repeat) |
| `WallEnd` | 1,264 | -0.31..0.31 / -0.6..4.9 / -0.61..0.61 | quoined pier, symmetric; centre it 0.3 m into the gap beside the last segment |
| `Buttress` | 3,704 | -0.58..0.57 / -0.6..4.0 / -1.31..-0.58 | battered, sloping weathered top (reaches y 4.0 at z -0.62) |
| `Planter` | 2,142 | -0.9..0.9 trough; ivy to -1.06..1.11 / 0..0.35 trough, ivy -0.88..0.64 / -0.82..0.35 | sits on the coping (y 4.9); soil, ivy bush above, ivy trails over the square side (-z) down to y -0.8 |
| `StairStep` | 108 | -1..1 / -1.0..0.22 / 0..0.40 | visible tread z 0..0.32 (+ hidden 0.08 tail), body hidden below y 0 so the soffit is solid; rise 0.222, run 0.367 |
| `Balustrade` | 1,872 | -0.99..0.99 / 0..1.0 / -0.17..0.17 | flat, 7 balusters |
| `BalustradeRaked` (extra) | 1,944 | -1.13..1.13 / -0.63..1.63 / -0.17..0.17 | built on the 0.222/0.367 slope (6 steps, run 2.2 m, rise 1.33), vertical balusters, origin mid base underside; rotate 90 deg to run along +z |
| `Newel` | 748 | -0.2..0.2 / 0..1.2 / -0.2..0.2 | |
| `Bench` | 1,024 | -0.9..0.9 / 0..0.5 / -0.25..0.25 | stone slab seat on two legs |

## Materials (slot names, exact)
| Slot | Source | Tint |
|---|---|---|
| `KT_Rubble` | `B1_town/sandstone` tileable | (0.56, 0.56, 0.56), normal 1.5 (the bell tower's grey rubble, a shade neutral) |
| `KT_Dressed` | `B1_town/sandstone` | (0.94, 0.94, 0.92), normal 1.3 (coping, plinth, quoins, treads, balustrade, newel, bench) |
| `KT_Mortar` | `B1_town/sandstone` | (0.26, 0.245, 0.21), the dark joint core |
| `KT_Soil` | flat colour | (0.034, 0.025, 0.017) linear, roughness 0.95 |
- Ivy uses the shared B9 foliage material (M_B9_Foliage, BeautyCorner materials); no texture copies here.

Metre UVs (2 m per tile). Wear is the linear CORNER colour attribute `wear` (R damp/grime, G moss, B polish): damp foot and moss on the plinth ledge, string course and coping top
(WallSegment, Buttress, WallEnd); polished tread centres and nosing; rail tops, newel caps and the bench seat.
Foliage atlas: ambientCG LeafSet017 and Moss002 (CC0), see `Assets/_Project/Content/Art/Town/B9/SOURCES.md`. Stone textures: B1 (`Art/Textures/B1_town/SOURCES.md`).
