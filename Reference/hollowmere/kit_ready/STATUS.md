State: IN PROGRESS
Session: Q2: Smithy, schoolhouse and mill (kit-ready buildings)
Progress: 60%
Sheet: Reference/hollowmere/mill/SHEET_mill_final.jpg
Needs: none
Blocker: paused by coordinator for usage

- Branch: claude/sharp-goldberg-vpyt8c (main merged at resume).
- Schoolhouse DONE (art): sc_schoolhouse.py, Ring2/Schoolhouse/Schoolhouse.fbx, 29,480 tris, self 28/40, sheet Reference/hollowmere/kit_ready/schoolhouse/SHEET_schoolhouse_final.jpg.
- Mill DONE (art): sc_mill.py, Ring3/Mill/Mill.fbx, Shell 24,784 / Frame + parts 31,532 tris, self 28/40, sheet Reference/hollowmere/mill/SHEET_mill_final.jpg.
- Smithy: in progress at pause (sc_smithy.py checkpointed; not exported yet).
- Integration drafted (compiles, Unity not run): BuildingKind Smithy/Schoolhouse on R2_01/R2_03 in Rebuilt + EditMode test; HollowmereArt specs for the three with their SH_/ML_ slots; mill on its landmark row (Shell when ruined, Frame + Part_Wheel/Part_Door when rebuilt).
- Before merge: merge origin/main (townhouse also edits the BuildingKind enum: order Empty..Garden, Townhouse, Smithy, Schoolhouse), regenerate Hollowmere.unity, unity.sh check, captures.
