# Area 1 world: ruin shells and small props (staging, not yet in Assets/)

- Origin: ours, generated. No external assets, no new textures. Generator: `Art/Blender/town/sc_ruin_shells.py` (re-uses `sc_ruin_kit.py` by executing its source without `main()`).
- Regenerate: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_ruin_shells.py -- --out <scratch dir> --export --no-render` (Blender 5.2; `--only Ruin_M_B_Tall,Hatch_Oak` for a subset; drop `--no-render` for the contact sheets).
- Export: FBX, `path_mode='STRIP'`, -Z forward / Y up, metres, transforms baked, same settings as the low kit.
- Licence: our own work (the B1 tileable slots they name are CC0, see `Art/Textures/B1_town/SOURCES.md`).

## Ruin shells (18)
`Ruin_<S|M|L>_<A|B|C>_<Mid|Tall>.fbx`: whole replacement for `Ruin_<C>_<V>` (same footprint S 6 / M 8 / L 11 m, doorstep and `SOCKET_Doorstep`, door x from the base variant's seed).
Root empty plus children `Footing` (walls, door frame, doorstep, hearth, beams, rubble), `Chimney`, `FOOTPRINT` (flat box, hidden by the builder), `SOCKET_Doorstep`, `SOCKET_Smoke`, and box colliders `FOOTPRINT_COL_<n>` (the prefix is what `HollowmereBuilder.PlaceRuin` hides; the low kit has no collision). Slots `FK_Sandstone` / `FK_ChimneyStone` / `FK_PlinthJoint` / `FK_Oak`, UV0 metre box UVs (2 m per tile), CORNER colour attribute `wear` (R grime/char, G moss, B polish). No `Foliage` child (triangle budget).
Walls are 0.48-0.56 m thick blocks on a 0.58 m line (the base kit's footing width).

| Variant | Gable and stack | Tall long wall | Door |
|---|---|---|---|
| A | back (+Y) | left (-X) | low front end wall, jambs and lintel stand |
| B | left (-X) | back (+Y) | low front long wall |
| C | right (+X) | front (-Y) | opening in the tall front wall |

## Props
`Headstone_A/B/C.fbx` (round, pointed, snapped top; no text), `Signpost_A.fbx` (2.4 m oak post, two blank finger boards, stone cairn foot), `Hatch_Oak.fbx` (1.6 x 1.2 m oak trapdoor in a stone kerb, iron straps and pull ring on the `FK_PlinthJoint` slot). One mesh each, same slots and `wear` attribute as the shells.
