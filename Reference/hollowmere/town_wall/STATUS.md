# Q8 town wall, Lower Gate and East Gate: status

State: DONE (merged to main ca383a8)
Session: Bell tower: bake, integrate, check, merge
Job: Q8 Town wall, Lower Gate and East Gate (cloud session, branch claude/brave-keller-3d91gk)
Progress: 100%
Sheet: Reference/hollowmere/town_wall/wall_town_sheet.jpg (Blender: wall_sheet.jpg, approved by the coordinator ~28/40)
SC RESULT: SC RESULT check PASS on 89e641b (test-edit 680/680, test-play 32 passed / 8 skipped / 0 failed; origin/main merged before the check); merge Assets equal the checked tip
Merge commit: ca383a8
Blocker: none

Result:
- Art: Art/Blender/town/sc_town_wall.py, 8 wall pieces + LowerGate / EastGate (Shell, Frame incl. piers, Part_Door) in Content/Art/Town/Wall/ (SOURCES.md there).
- Unity: TownWallPlan (pure C#, 10 tests) + HollowmereTownWall.cs tile 52 modules over 3 runs with 4 ends and 4 stairs (one beside each gate); Ruined 4 breach /
  8 crumbled, Rebuilt 4 patched; 12 buttresses; box colliders per module, open gate passages (pier colliders only); cameras Cap_HM_LowerGate / Cap_HM_EastGate.
- Weak / open (for Q12 or a follow-up): stone tone and coursing very even, merlon rhythm repeats; about 10 m without wall east of the Lower Gate (existing
  WallRun clearance near lots/lanes); the East Gate's pier line is about 25 degrees off the wall tangent so the wall meets it at an angle; a few buttress footings
  hang off the steep east bank; door leaves hidden (ShowDoorLeaves = false) so gates read open; a pale translucent arrow shape behind the wall in the LowEast
  frames (likely an existing scene marker, not checked); no PlayMode walk through the gates.

## Usage waste
Honest look back over this session (bell tower, Q1, Q3, Q8), rough shares of the total spend:
1. About 40%: one long-lived Opus controller context. The same session has carried four jobs, so every turn (including the many short stop-hook and
   pause/resume replies) re-sends a very large transcript. Fix: one fresh controller session per queue item, handing over through STATUS.md, and fewer
   stop/start cycles (each pause and resume costs a full-context turn here plus a context reload in every paused subagent).
2. About 30%: subagent render-and-review loops. The generator agents each used 300-400k tokens (cottage, workshop, wall), partly from re-reading after
   pauses, writing their own sheet/audit/verify scripts from scratch, and one agent's lingering background work after it had reported (stopped by hand).
   Fix: commit shared helpers (FBX re-import audit, reference-beside-render sheet) under Art/Blender/tools and point briefs at them; cap at 2-3 rounds;
   render at 960 px with fewer samples until the final round.
3. About 20%: Unity runs repeated for avoidable reasons. The bell tower's 'Lit' slot bug took four capture runs and a diagnostic run before the cause was found
   (absolute texture paths that resolve on the import machine); Q3 needed two extra build/check cycles because main moved twice under it; one capture timed out
   on the default limit. Fix: put the path_mode='STRIP' rule and the --timeout 90/120 defaults into sc-asset-craft / CLAUDE.md, merge origin/main immediately
   before the final check, and capture only the changed cameras (SC_CAPTURE_ONLY) during iteration.
The remaining ~10% is ordinary work (reading code, reviews, merges).
