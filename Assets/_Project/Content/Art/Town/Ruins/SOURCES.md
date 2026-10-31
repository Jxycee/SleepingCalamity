# Ruins: ruined-foundation kit (Hollowmere design 5.1-5.3)

- Generator: `Art/Blender/town/sc_ruin_kit.py` (procedural, seeded, reproducible from text).
- Cloud export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_ruin_kit.py -- --out <scratch> --export`. It writes `<scratch>/fbx/<Name>.fbx` and copies each to `<Name>/<Name>.fbx` here, with the B9 foliage atlas beside it.
- Each FBX: root empty `Ruin_<C>_<V>` with the children `Footing` (stones, rubble core, oak stubs), `Foliage` (B9 grass strips, crack weeds, grass clumps, moss tufts), `FOOTPRINT` (flat S x S x 0.1 m box, material `FOOTPRINT`, hidden in renders) and `SOCKET_Doorstep` (empty at the threshold top centre, z 0.22).
- Axes: -Z forward, Y up, metres, transforms baked. Pivot at the foundation centre at ground. Front (doorstep side) is Blender -Y = local +Z in Unity, like the B4 kit. Everything stays inside the S x S square, under 0.48 m (contract < 0.5), with 0 vertices inside the clear interior (only the outer 1 m band is used), so a building's plinth covers the footing.

| Folder | Class | Size | Tris (footing + foliage) | Doorstep x | Character |
|---|---|---|---|---|---|
| Ruin_S_A | S | 6 x 6 m | 5,786 (2,630 + 3,156) | -0.71 | top course mostly whole |
| Ruin_S_B | S | 6 x 6 m | 6,592 (3,260 + 3,332) | 0.00 | half the top course gone |
| Ruin_S_C | S | 6 x 6 m | 5,954 (3,010 + 2,944) | +0.75 | mostly one low course |
| Ruin_M_A | M | 8 x 8 m | 7,006 (3,350 + 3,656) | 0.00 | top course mostly whole |
| Ruin_M_B | M | 8 x 8 m | 7,034 (3,450 + 3,584) | 0.00 | half gone |
| Ruin_M_C | M | 8 x 8 m | 7,476 (4,020 + 3,456) | +0.96 | mostly one low course |
| Ruin_L_A | L | 11 x 11 m | 6,942 (4,050 + 2,892) | -0.86 | top course mostly whole |
| Ruin_L_B | L | 11 x 11 m | 7,454 (4,290 + 3,164) | 0.00 | half gone |
| Ruin_L_C | L | 11 x 11 m | 7,602 (4,630 + 2,972) | +0.92 | mostly one low course |

- **Materials (T2, no unique maps).** The Footing material slots reuse the B4 facade kit's slot names, so `BeautyCornerArt`'s facade material map binds them as they are: `FK_Sandstone` and `FK_ChimneyStone` (the B1 `sandstone` tileable, plain and darker tint), `FK_PlinthJoint` (sandstone x 0.30 for the dark rubble core), `FK_Oak` (B1 `oak_timber`). UV0 is metre UVs, 2 m per tile, like B4. Placed wear lives in the CORNER colour attribute `wear` (exported LINEAR, 0-1): R grime, damp and char (the foot band, block-to-block value, the charred stubs), G moss (tops, the back +Y side, north faces), B polish (the threshold and step dips). It needs the WearLit shader, like B4.
- `Foliage` uses `M_B9_Foliage`. `T_B9_Foliage_{BaseColor,Normal,ORM}.png` in each folder are byte-identical copies of the B9 atlas (one LFS object), so `BeautyCornerArt.MaterialFromSet(<folder>, "T_B9_Foliage", ..., foliage: true)` works from any ruin folder. Foliage meshes are the exported B9 FBXs (`SC_B9_GrassStrip`, `SC_B9_CrackWeed_A..E`, `SC_B9_GrassClump_A..C`, `SC_B9_MossTuft_A..C`), placed, scaled 0.95-2.0x (squashed vertically to stay under 0.5 m) and joined. Normals = Import, Alpha Clip 0.5, Render Face Both (B9's Unity notes).
- **Licences.** The B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`). The B9 foliage is CC0 (ambientCG, Poly Haven; `../B9/SOURCES.md`). No other external assets. References (study only, never shipped): `Reference/hollowmere/ruins/REFS.md` (CC BY 2.0).
- **Review.** Calibrated `sc-asset-review` rubric, reference beside render every round. Round 1: 26/40 (target became 31). Round 2: 28 (dressed coursed blocks; fixed a wear-attribute write bug that had left the attribute garbage). Round 3: 29.5 (door-jamb bases, rubble heaps, moss tufts all round). Round 4: 30 (block value mottling, collapse heaps, L back under 8k). Target 31 not reached within the 4-round limit. Final sheet: `Reference/hollowmere/ruins/ruin_kit_sheet_final.jpg`.
- **Next visible gains (parked):** taller rushes or nettles at the collapse heaps (Cold Soil ref), ochre lichen as its own wear channel on the tops (Far Nook ref), moss as a G mask the WearLit shader shows without the noise gate, an in-engine capture on the real plot ground.
