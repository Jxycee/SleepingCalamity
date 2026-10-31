State: IN PROGRESS
Session: Wave 1: Heroes' home (fixed landmark)
Progress: 70%
Sheet: Reference/hollowmere/heroes_home/heroes_home_sheet_r2.jpg
Blocker: none

Score: 27.5/40 (r1 26.5: silhouette 4, proportion 3.5, form 3.5, surface 3, colour 3, wear 3.5, dressing 3.5, glass/metal 2.5; r2 +1: warmer honey stone, warm mortar joints, softer washing). Target was 31.5; stopped after r2 (gain < 1.5, brief rule). Biggest gap to the refs is vegetation at the feet and on the walls: Wave 2 foliage extension.
Tris: cistern 51.3k, annex 19.1k (budget 75k each)
SC RESULT: no check yet
Merge commit: none

## Log
- 2026-10-31: started. Layout rows read: HeroesHome_Cistern (12.5, 11.25) 9 x 7.5 m, y 0, door south (12.5, 6.75), Undercroft stair (15, 12.5);
  HeroesHome_Annex (11.9, 24) 5.5 x 5 m, y = KeepHeight 4, door east (15.3, 24), beside the bell tower (5.6, 24).
- Generator Art/Blender/town/sc_heroes_home.py written (self-contained, B4 FK_* slots + HH_* slots, wear attribute). Round 1 fixes: rubble
  courses (bigger, irregular, dark joints), shutters folded flat, door re-hinged so the stair's warm light shows, paint mode for
  red/blue/ochre boards, inner liner cut at openings. Found and fixed a Blender gotcha: adding the colour attribute after the UV layer
  left a stale UV handle that wrote UVs into the wear colours (create both, then fetch by name).
- Checked before building (user rule): no existing open-source "stone cottage" generator fits the B4 slot/wear contract; reused the
  in-repo practice (sc_ruin_kit tile_mat + pillow stones, sc_facade_kit conventions, BeautyCornerArt slot mapping) instead of new tools.
- Round 2: warm honey stone (HH_OldStone), warm lime mortar joints (HH_Mortar instead of the near-black FK_PlinthJoint), lichen/moss on tops,
  smoother washing, grimier list panel. Exported to Assets/_Project/Content/Art/Town/Ring1/HeroesHome/ (SOURCES.md). Unity integration running
  (HeroesHomeArt.cs + the HeroesHome case in HollowmereBuilder), then unity.sh check.
