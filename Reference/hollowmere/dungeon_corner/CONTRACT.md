# Q4 dungeon corner: kit and scene contract (2026-10-31)

Shared between the Blender generator (`Art/Blender/cellars/sc_cellar_vault.py`) and the editor builder (`DungeonCornerBuilder`).
Sources: `docs/PHASE1.md` 3.1 (Cellars band), 3.5, 3.7 (captures), 4 (B5 row, the dungeon corner line), decision A24
(`docs/superpowers/handoff/decisions-for-review.md`: the user's private cellar image is the mood target: darker than our band,
warmth from local light pools (lanterns, candles, embers) on neutral grey-brown stone; captures measure value p10/p50/p90 about
0.06-0.10 / 0.24-0.32 / 0.55-0.68, warmth +0.25 to +0.35, no teal anywhere).

## Kit FBX
- `Assets/_Project/Content/Art/Cellar/B5/CellarVault/CellarVault.fbx` + `SOURCES.md`. Root `CellarVault`; every kit piece is a direct
  child mesh named exactly as below, each authored with its own pivot at the world origin (pieces overlap in the file; the builder clones
  each child to its placements). T2: shared B2 cellar tileables (`Art/Textures/B2_cellar/`: old_brick, vault_stone, limewash, flagstone,
  barrel_oak, soot_coal, root_bark) + B1 iron/oak where needed, metre UVs (2 m per tile), linear CORNER float colour `wear`
  (R grime/soot/damp, G moss, B polish) as the town kits, export path_mode='STRIP'. Material slot names prefixed `CV_` for new ones.
- Axes. Author in Blender, export -Z forward / Y up like the town kits. Mapping: Unity (x, y, z) = Blender (-x, z, -y).
  Below, everything is given in UNITY terms: +Z runs along the vault (away from the entrance), +Y up, X across.
- Module: 2.0 m along Z. Inner span 5.0 m (walls' inner faces at x = +-2.5), wall thickness 0.5 m (outer faces +-3.0),
  springing line y 1.6, semicircular barrel vault, crown inner y 4.1. Floor top y 0.

| Piece | Extent (Unity, metres) | Notes |
|---|---|---|
| `VaultSegment` | x -3..3, z 0..2, y 0..4.6 | both side walls (old brick to the springing, a stone springing course) and the barrel vault (vault rubble with brick bands); no floor |
| `ArchRib` | transverse brick rib in the plane z = 0, 0.45 deep (z -0.225..0.225), standing 0.12 proud of the vault inside | with its two engaged `Pillar`s merged in |
| `Pillar` | free-standing square stone pier 0.6 x 0.6, y 0..1.6, with a chamfered cap | spare, for wider rooms |
| `EndWall` | plane z = 0, wall body z -0.5..0, faces +Z, fills the vault section | limewashed brick, damp tide line |
| `NicheWall` | as EndWall | 3 x 3 arched wine niches with bottles lying in them (glass, a few broken/empty niches) |
| `StairEntry` | as EndWall, with a 1.2 m doorway at x 0 | stone steps rising toward -Z out of the room to y 2.6 (outside z -0.5..-4.0), a closed door at the top is NOT needed (dark) |
| `FloorTile` | x -1..1, z 0..2, top y 0 | irregular flagstones, joints dark, slight wear dish |
| `CoalChute` | mounts on the RIGHT wall (inner face x = +2.5), centred z 0, y 0..4 | wooden chute from a small iron hatch high in the wall down to a coal heap on the floor; the hatch opening is where the daylight shaft enters |
| `LarderShelf` | mounts on the LEFT wall (x = -2.5), z 0..2, y 0..2.2 | oak shelves on iron brackets with glazed jars, crocks and a cheese |
| `BreathGrate` | x -0.6..0.6, z -0.6..0.6, top y 0.01 | heavy iron floor grate; slot `CV_Breath` is a faint warm emissive under the bars (the town's tithe-gate breath motif) |
| `Roots` | hanging cluster, origin at its top on the vault crown (y 4.1), drops to about y 2.4 | root_bark, tapered, through a crack |
| `Barrels` | a group of 3 (two lying on a cradle, one standing), origin on the floor | barrel_oak staves, iron hoops |

Budgets: whole kit under 60k tris; a single piece under 12k.

## Scene
- `Assets/_Project/Scenes/DungeonCorner_Cellars.unity`, built by `SleepingCalamity.EditorTools.DungeonCornerBuilder.Build()` (menu
  `SleepingCalamity/Art/Build Dungeon Corner`), wired into `ProjectBuilder.RunAll`; never hand-edit the scene. Greybox fallback per piece
  when the FBX is missing (like the town builders).
- Room: 4 VaultSegments z 0..8 on 2 x 4 FloorTiles; StairEntry at z 0 (facing +Z), NicheWall at z 8 turned to face -Z; ArchRibs at
  z 0, 4, 8; LarderShelf on the left wall z 1..3 and 3..5; CoalChute on the right wall at z 5.5; BreathGrate at (0.6, 0, 6.2); Roots at
  (-0.4, 4.1, 3.2) and (0.9, 4.1, 5.0); Barrels against the left wall near z 6.6; two B11 cellar lanterns
  (`Assets/_Project/Content/Art/Cellar/B11/CellarLantern`, `BeautyCornerArt.EnsureCellarLantern`) on wall hooks at (-2.3, 2.3, 2.0) and
  (2.3, 2.3, 4.0), one candle cluster in a niche; a 1.75 m figure only in a debug object that captures hide.
- Light (PHASE1 3.5, Cellars): realtime Forward+ point lights at the lanterns, candle and grate, shadows only on the two lantern lights;
  a daylight shaft from the coal hatch (a cool-neutral, not teal, narrow spot + an additive dust cone like LookTest's `LightConeCellar`);
  dust motes in the lamp shafts (a small particle system or the LookTest's pollen-style motes, warm); per-band trilight ambient (warm umber
  to plum, as LookTest_Cellar), fog, SSAO, the `LookTest_Cellar_Profile` volume or a copy tuned to the A24 band. No teal.
- Capture cameras (1920x1080, PHASE1 3.7): `Cap_Cellar_Wide` from the stair landing (0.3, 1.7, 0.6) looking down the vault to the niche
  wall (0, 1.6, 8); `Cap_Cellar_Mid` from (1.6, 1.6, 2.4) toward the larder and barrels (-2.0, 1.2, 5.5); `Cap_Cellar_Close` from
  (0.4, 1.3, 6.4) toward a lantern + niche (−0.5, 1.4, 7.9). A `DungeonCornerCapture.CaptureAll` (SC_CAPTURE_DIR, like
  `HollowmereCapture`) renders them; run it with `bash Tools/cloud/unity.sh exec --graphics`.
