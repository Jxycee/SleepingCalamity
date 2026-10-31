# HeroesHome: the heroes' home (fixed landmark; Hollowmere design 3 and 6, STORY 8.2)

- Generator: `Art/Blender/town/sc_heroes_home.py` (procedural, seeded, reproducible from text). Our own work; no external assets.
- Cloud export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_heroes_home.py -- --out <scratch> --export`.
  It writes `<scratch>/fbx/<Name>.fbx` and copies both FBX and `HH_List_BaseColor.png` here.
- `HeroesHome_Cistern.fbx` (layout row HeroesHome_Cistern, 9 x 7.5 m, ground 0) and `HeroesHome_Annex.fbx` (row HeroesHome_Annex, 5.5 x 5 m, ground 4 on
  the keep terrace). Children of the root empty: `Frame`, `Decals` (no shadows), `Glow` (emissive), `FOOTPRINT` (body-size 0.1 m box, never rendered),
  `SOCKET_Door`, and on the cistern house `SOCKET_UndercroftStair` (the stair head, HollowmereLayout.UndercroftStair).
- Axes: -Z forward, Y up, metres, transforms baked; pivot at the footprint centre at ground. Blender x = Unity east, y = north; the builder turns the art
  180 deg (ArtYaw 180, like the B4 facade). One state (lived in); story additions come later.
- Tris: cistern 51.3k, annex 19.1k (PHASE1 3.3 building budget 75k each).
- Materials (T2, no unique bake): B4 slot names `FK_*` bind through `BeautyCornerArt.BuildFacadeMaterials`; `HH_*` slots are tinted B1 tileables
  (`HH_OldStone`, `HH_Mortar`, `HH_Slate` from sandstone; `HH_PlankGrey` oak; `HH_Canvas`, `HH_Cloth*` canvas), WearLit paint mode boards
  (`HH_PaintRed/Blue/Ochre`, greyscale x paint / 0.17 like the B4 neighbours), flat `HH_Rope`, `HH_Dark`, emissive `HH_Glow`, and the
  alpha decal `HH_List` (`HH_List_BaseColor.png`, 1024 px: Wren's list, 120 placeholder resident names of our own, DejaVu Serif under the Bitstream
  Vera/DejaVu licence, free for any use). Mapped in `Assets/_Project/Editor/Builders/HeroesHomeArt.cs`. Wear: CORNER colour attribute `wear` (LINEAR,
  R grime, G moss, B polish) for WearLit.
- Reference: `Reference/hollowmere/heroes_home/REFS.md`; sheet `Reference/hollowmere/heroes_home/heroes_home_sheet_r2.jpg`. Self-review 27.5/40.
