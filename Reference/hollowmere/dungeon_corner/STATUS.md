State: IN PROGRESS
Session: Q4: Dungeon corner (B5 cellar vault kit and the room)
Progress: 5%
Needs: none

- Branch: claude/sharp-goldberg-vpyt8c (restarted from main after the Q2 merge 89d361d).
- Contract: Reference/hollowmere/dungeon_corner/CONTRACT.md (kit pieces, module grid, axes, room layout, lights, cameras).
- Plan: a Sonnet agent writes Art/Blender/cellars/sc_cellar_vault.py (kit FBX + sheet); a second Sonnet agent writes DungeonCornerBuilder + DungeonCornerCapture (greybox fallback first, then the FBX), unity.sh check, captures. Controller scores, reviews, merges.
- Score/tris/check/merge: none yet. Blockers: none.
