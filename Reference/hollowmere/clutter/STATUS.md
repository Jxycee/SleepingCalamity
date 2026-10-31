State: IN PROGRESS
Session: Q14. Decorations batch 2: market and lane clutter
Progress: 15%
Sheet: none yet
Blocker: none

Score: -
Tris: -
SC RESULT: -
Merge commit: -

Plan: CONTRACT.md (asset names, footprints, pivots, CL_* slots on shared B1 tileables, no bakes). Two agents running:
- art: Art/Blender/town/sc_clutter.py (handcart, crate/sack stacks, rain barrel, flower box, well, 6 trade signs, washing-line parts; each with a ruined look), export to Content/Art/Town/Clutter, contact sheet with references.
- Unity: Core HollowmereClutter (ring 1/2 rows, Validate incl. path graph), EditMode tests, Editor HollowmereClutter placement + Cap_HM_Clutter.
Then: merge origin/main, build scene, capture Cap_HM_Clutter, unity.sh check, merge.

## Usage waste
- (filled at the end)
