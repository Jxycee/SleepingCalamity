# Beauty corner: shared brief for the art team (M9, 2026-10-30)

Every art session on the beauty corner reads this first, then `docs/PHASE1.md` section 3 (style bible) and section 4 (batches).
The controller (PC session) owns this file, the Unity corner scene and all merges.

## 1. The corner (PHASE1 4)
One half-timbered facade beside the dry fountain on cobbles, with one market stall, barrels, crates and sacks, a
bleached banner, ivy and crack weeds, and haze with god rays. Midday capture plus one low-sun shot.

Layout (Unity metres, +Z north, origin = fountain centre). Build to these footprints so pieces drop in without rework:

| Piece | Batch | Footprint / size | Placement |
|---|---|---|---|
| Plaza cobbles | B6 | 16 m x 14 m patch, slight dish toward the fountain (5 cm) | x -8..8, z -6..8 |
| Fountain flagstone ring | B6 | 1.2 m wide ring around the basin | centred on origin |
| Dry fountain | B10 | basin outer diameter 3.4 m, rim 0.55 m high; spout column to about 2.4 m | origin |
| Banner pole + bleached banner | B10 | pole about 4.5 m, banner 0.7 x 2.2 m | x 2.4, z 1.2 |
| Facade (2 storeys + roof) | B4 | front 7.5 m wide, eave 6 m, ridge about 9 m, ground storey stone plinth 0.6 m, upper storey jettied out 0.45 m | front face on z 7.5, x -5..2.5, facing -Z |
| Market stall | B8 | 2.6 m wide x 1.6 m deep, awning top about 2.7 m | x 4.5, z 4.5, rotated 200 deg (faces the fountain) |
| Props cluster | B7 | 3 barrels, 3 crates, 4 sacks, 2 buckets, 3 baskets, 2 jugs | by the stall and the facade door |
| Foliage | B9 | ivy on the facade's left 2 m, crack weeds along the facade foot and the fountain ring, moss on the basin's shaded side | |
| Lanterns | B8 | 1 on the stall, 1 bracket lantern beside the door | |

Capture cameras (1920x1080, PHASE1 3.7), built by `BeautyCornerBuilder`:
- **Wide:** from (1, 1.8, -9) looking at (-1, 3, 6). Sees the whole facade front, fountain, stall.
- **Mid:** from (5, 1.7, -2.5) looking at (-1.5, 1.8, 5). Fountain rim, facade ground storey, props.
- **Close:** from (1.6, 1.4, -1.6) looking at (0, 1.0, 0.6). Spout, basin carving, cobbles at the foot.

## 2. What to spend on: only what the cameras see
- **Spend:** facade front and its left return to about 2 m deep; the fountain all round (it is seen from three sides); the stall's front and the fountain-facing side; prop tops and fronts; cobbles inside the wide camera's frame.
- **Don't spend:** backs of the facade, roof backs, undersides, interiors behind closed shutters, prop bottoms, anything outside all three frames. A back face can be a flat plane with the shared tileable. Nobody sees it.
- Silhouette at 20 m first, then mid-distance surface (wear, chamfers, colour variation), then close detail only on the fountain spout and basin rim (the close camera).
- Check visibility by rendering from the three camera positions above, not only turntables.

## 3. Scoring (calibrated to the user, 2026-10-30)
The user reads our rubric scores about 8-10 points low ("your 20/40 is more like a 30/40 to me"). So:
- Score with the `sc-asset-review` rubric as written, then **stop at 25/40**. That is shippable for this corner. Don't chase past it in this round; park extra ideas in `docs/PARKING_LOT.md`.
- Score only what the three cameras see (section 2). Don't deduct for hidden faces, topology, UV layout or code structure.
- Each fix must be a change visible at the mid camera. Name it as a visible change ("roof ridge sags 4 cm/m", "darker grime band at the cobble line"), never "improve quality".
- Below 18/40 after two rounds: stop and report to the controller with renders instead of a third variation.
- Concrete anchors for one rubric line: 3 = reads correctly at mid distance but plain; 4 = reads and has placed story (wear where hands, water and feet go); 5 = a frame from a shipped game.

## 4. Deliverables and paths (so sessions never collide)
- Generator: `Art/Blender/town/sc_<asset>.py` (one per asset family, header docstring like `Art/Blender/cellars/sc_cellar_lantern.py`: what it is, run line, status, score).
- Review renders and bakes: your scratchpad, never the repo.
- **Cloud sessions** commit the generator only (no FBX, no textures), plus a `--stage bake --res 1024` proof render in the message. The PC runs the 2K/4K bake and exports.
- **Local (PC) sessions** also run the final bake and write the export to `Assets/_Project/Content/Art/Town/<Batch>/<Asset>/` (FBX, `_BaseColor`, `_Normal`, `_ORM` PNG; no `.meta`: the controller's Unity import makes them). Adopted models keep a `SOURCES.md` with source and licence beside the export.
- Shared materials: use the graded tileables in `Art/Textures/B1_town/` and trims in `Art/Textures/B3_trims/`; don't make a new plaster or cobble unless the batch owns it.
- Don't touch `Assets/_Project/Editor/`, scenes, prefabs, `CURRENT.md` or another batch's folder. The controller builds the corner scene.
- Branch per session; push the branch; report the branch, the commit and three renders (wide-camera view, mid-camera view, one close) with the score. The controller reviews and merges.

## 5. Reference
- Free references: `Reference/hollowmere/` (architecture, market_square, materials, paintings) with REFS.md files.
- Study-only frames: `Reference/private/` (PC only, gitignored). The controller adds Dark Cloud town captures (PCSX2 at 1440p) under `Reference/private/dc1_town/` and posts a contact sheet.
- Real-world: Alsace and Normandy half-timber (Colmar, Riquewihr, Eguisheim), Rothenburg ob der Tauber, sandstone market fountains (Rottweil, Rothenburg Marktplatz), French and German weekly markets.
