State: IN PROGRESS
Session: Q2: Smithy, schoolhouse and mill (kit-ready buildings)
Progress: 65%
Sheet: Reference/hollowmere/mill/SHEET_mill_final.jpg
Needs: none

- Branch: claude/sharp-goldberg-vpyt8c (main merged at resume).
- Schoolhouse DONE (art): sc_schoolhouse.py, Ring2/Schoolhouse/Schoolhouse.fbx, 29,480 tris, self 28/40, sheet Reference/hollowmere/kit_ready/schoolhouse/SHEET_schoolhouse_final.jpg.
- Mill DONE (art): sc_mill.py, Ring3/Mill/Mill.fbx, Shell 24,784 / Frame + parts 31,532 tris, self 28/40, sheet Reference/hollowmere/mill/SHEET_mill_final.jpg.
- Smithy: in progress at pause (sc_smithy.py checkpointed; not exported yet).
- Integration drafted (compiles, Unity not run): BuildingKind Smithy/Schoolhouse on R2_01/R2_03 in Rebuilt + EditMode test; HollowmereArt specs for the three with their SH_/ML_ slots; mill on its landmark row (Shell when ruined, Frame + Part_Wheel/Part_Door when rebuilt).
- Before merge: merge origin/main (townhouse also edits the BuildingKind enum: order Empty..Garden, Townhouse, Smithy, Schoolhouse), regenerate Hollowmere.unity, unity.sh check, captures.

## Smithy at pause (2026-10-31)
- `sc_smithy.py` builds (imports sc_bakery helpers), round-1 renders only (scratch); ~31,480 tris (cap 35k); FOOTPRINT 7.20 x 5.60 m; bounds y min -4.06 (over +-4.0: front slabs or crane rope).
- Sockets (pre dimension fix): Forge (1.10, 0.60, 0.895), Anvil (2.30, -0.60, 0), Sign (1.60, -2.76, 2.53). New slots: BK_Water, BK_Leather, BK_Coal, BK_Ash, BK_Grindstone (+ BK_Ember emissive).
- Left: fix the y overrun and the weathervane offset, drop dead plank_leaf/put_leaf, 1-2 render rounds, score, export Ring2/Smithy/Smithy.fbx + SOURCES.md, REFS.md (2 CC0 forge refs in kit_ready/smithy/), final sheet. Then HollowmereArt slots for the five new BK_* names.
