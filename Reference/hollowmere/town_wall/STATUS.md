# Q8 town wall, Lower Gate and East Gate: status

State: IN PROGRESS
Session: Bell tower: bake, integrate, check, merge
Job: Q8 Town wall, Lower Gate and East Gate (cloud session, branch claude/brave-keller-3d91gk; bell tower, Q1, Q3 done before it)
Progress: 60%
Sheet: Reference/hollowmere/town_wall/wall_sheet.jpg (Blender; the Unity town sheet follows)
Blocker: none

Started 2026-10-31. Plan: `Art/Blender/town/sc_town_wall.py` exports a tileable wall kit (straight modules, buttress, lane-opening ends, walkway stair; Ruined
breach and crumbled variants, Rebuilt patched) and the two gatehouses (Lower Gate, East Gate; shell and restored, timber doors) on the shared B1 tileables
(Blender agent, worktree). Then the builder tiles the kit along HollowmereLayout's wall run at its heights and places the gatehouses between their piers
(Unity agent). Captures: low east, lake shore, both gates, both presets.

Progress:
- Art done and merged into the branch (b1124df): 8 wall pieces (Straight A/B 3.4-3.8k tris, Patched, Breach, Crumbled, Buttress, End, Stair) and LowerGate /
  EastGate (Frame 31.3k / 26.0k, Shell 18.5k / 20.8k, Part_Door 1.2k; piers included). Self-score 28.5-30; controller read: gates about 29, kit about 27
  (stone reads a little uniform tan). Slots TW_* on sandstone/oak_timber plus FK_*; FK_Decal_crack on the Shells.
- Unity tiling code in (e6ccf5c, test-edit 618/618): TownWallPlan (pure C#, 9 tests), HollowmereTownWall.cs, Cap_HM_LowerGate / Cap_HM_EastGate. Plan: 5 runs,
  55 modules, 10 ends, 2 stairs; Ruined 4 breach / 8 crumbled / 11 buttresses, Rebuilt 4 patched / 13 buttresses.
- Now: wiring the real contract (End/Stair mirroring, pivot at the highest ground, gate children, real TW_ slots, colliders with open gate passages), one capture,
  the town sheet, check, merge.

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
