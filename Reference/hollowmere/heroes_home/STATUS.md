State: IN PROGRESS
Session: Wave 1: Heroes' home (fixed landmark)
Progress: 85%
Sheet: Reference/hollowmere/heroes_home/heroes_home_sheet_r3.jpg
Blocker: paused by coordinator for usage

Score: 29/40 after round 3 (r1 26.5, r2 27.5; coordinator's r2 read ~26). r3: salvage shown (faded-blue sailcloth tarp lashed over a roof hole, boarded upper window, planked wall section, new oak lintel, rope-tied barrel), older stone (bigger blocks, rain streaks, north moss/lichen, ridges sag 4 cm/m), a lantern inside the door so warm light spills on the threshold. Art rounds closed.
Tris: cistern 45.9k, annex 18.6k (budget 75k each)
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
- Round 3 (coordinator's last round) exported; list panel lifted clear of the proudest stones. Unity integration + check in progress.
- Paused by the coordinator (usage). Done: art r3 (29/40), export, Unity code (HeroesHomeArt.cs + HollowmereBuilder HeroesHome case, pushed as WIP). Left: unity.sh check on the tip, merge, DONE.
- Unity agent: first unity.sh check PASS (build, 591/591 edit, 32 play passed / 0 failed) on the round-1 FBX; HeroesHomeArt now maps the annex's empty Glow slot and adds the door-lantern light (untested). Final check on round 3 pending after the pause.
