# Q13 Undercroft stair: kit and scene contract (draft 2026-10-31, before any code)

Unity terms throughout (+Y up, X across, Z along). Matches the B5 StairEntry: step rise 0.2, inner width 1.2 (x +-0.6), walls 0.4 (to +-1.0).

## New kit pieces (in `Art/Blender/cellars/sc_cellar_vault.py`, exported as a SEPARATE `CellarStair.fbx`, root `CellarStair`,
## folder `Assets/_Project/Content/Art/Cellar/B5/CellarStair/`, so the approved CellarVault.fbx stays byte-for-byte as it is)
Same CV_* slots, same wear attribute, each piece at its own pivot at the origin.
- `StairFlight`: origin = centre of the top edge, floor y 0 (the previous tread), descends toward +Z: 8 steps, tread 0.25, top of step k
  at y -0.2(k+1) over z 0.25k..0.25(k+1); run 2.0, drop 1.6; nosing line y = -0.8 z. Sloped brick side walls x +-0.6..+-1.0 from y -1.8 to
  nosing + 1.75. Vault: 4 rings of arc stones (0.5 m long each, stepped 0.4 down per ring; ring i springing y 1.5 - 0.4 i, inner radius 0.6,
  radial depth 0.6..1.0) with a continuous sloped mortar backing shell behind, so no slot opens to the void. A proud brick arch band at z 0..0.2.
- `StairTurn`: right-hand winder quarter turn. Origin = centre of the entry edge, floor y 0, entering heading +Z, exits heading +X.
  Well x -0.6..0.6, z 0..1.2; 4 winders fanned about the inner corner C = (0.6, 0): rays from C at 0/22.5/45/67.5/90 deg from -X toward +Z,
  winder k top y -0.2(k+1) (exit level -0.8; exit centre (0.6, -0.8, 0.6)). Round stone newel r 0.12 at C. Outer walls x -1.0..-0.6
  (z 0..1.6) and z 1.2..1.6 (x -1.0..1.0), corner pier x 0.6..1.0, z -0.4..0; walls y -1.8..2.7. Sail vault over the well: sphere r 0.85 at
  (0, 2.0, 0.6) clipped to the square (wall-line crown 2.6). Exit tympanum x 0.6..1.0 above the next flight's arch (centre z 0.6, springing
  y 0.7, r 0.6) up to the vault, with a brick voussoir ring r 0.6..0.85. Brick arch band at the entry face.
- `StairDoor`: the heroes' home cellar door + top landing. Origin = start of the first flight (floor y 0, z 0). Landing z -1.2..0 (flagstone,
  walls, barrel vault springing 1.7 r 0.6); door wall z -1.6..-1.2, x -1.6..1.6, y -0.2..3.2, arched doorway 1.0 wide, springing 1.6;
  vault-stone jambs + voussoirs on the house face, worn threshold; oak plank door (5 boards, arched top, iron strap hinges, ring) hinged at
  x -0.5 on the house side, open ~105 deg toward -Z; house floor flagstones z -3.6..-1.6.

## Scene `Assets/_Project/Scenes/UndercroftStair.unity` (`UndercroftStairBuilder.Build`, after DungeonCorner in ProjectBuilder.RunAll)
- Refactor DungeonCornerBuilder so the room set + its lighting/profile/ambient/fog build into a given root (DungeonCorner scene output unchanged);
  the stair scene builds that room at the origin, then the stair chain behind StairEntry (whose top is (0, 2.6, -4.0), going down = +Z):
  StairDoor (3.2, 9.0, -4.0) yaw 180; StairFlight A same; StairTurn1 (3.2, 7.4, -6.0) yaw 180; StairFlight B (2.6, 6.6, -6.6) yaw -90;
  StairTurn2 (0.6, 5.0, -6.6) yaw -90; StairFlight C (0, 4.2, -6.0) yaw 0 -> ends at StairEntry's top. Greybox per piece while the FBX is missing.
  A vault-stone plug over StairEntry's tunnel mouth (x +-0.6, y 4.7..5.2, z -4.05..-3.9).
- Light grows downward: cool-neutral daylight spot from the house behind the door; a candle on a corbel on Turn1's front wall (~1.4, r 3.5);
  a B11 lantern on Turn2's front wall at about (-0.55, 6.4, -6.8) (gold, ~4, r 7, soft shadows, motes); a warm bounce point mid StairEntry
  (0, 2.5, -2.5); the room's own lanterns spill up. Same DungeonCorner_Profile, trilight ambient, fog. A reflection probe per zone.
- Captures (1920x1080, `UndercroftStairCapture.CaptureAll`, SC_CAPTURE_DIR): `Cap_Stair_Top` (2.95, 10.65, -1.6) -> (3.2, 8.0, -5.6) fov 60;
  `Cap_Stair_Turn` (1.6, 7.4, -6.4) -> (-0.2, 4.5, -6.3) fov 62; `Cap_Stair_Bottom` (0, 3.6, -3.0) -> (0.2, 1.0, 5.0) fov 60.
  Positions are a first guess; tune them on the first captures.
