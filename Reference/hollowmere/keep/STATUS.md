State: IN PROGRESS
Session: Wave 1: Bakery (L, frame + parts)
Job: Q9: Keep terrace (keep hall and terrace dressing)
Progress: 80%
Needs: none

- Branch: claude/sharp-goldberg-vpyt8c (restarted from main after Q4 merge 9cf4559). Contract: Reference/hollowmere/keep/CONTRACT.md.
- Plan: Sonnet agents write sc_keep_hall.py (landmark, Shell + Frame + Part_Door) and sc_keep_terrace.py (wall/buttress/planter/stair/balustrade/newel/bench kit); then one integration agent (layout row KeepHall, BuildKeep on the kit, captures, check). Also in flight on the same branch: the coordinator's dungeon-corner polish pass (Q4 follow-up).
- Art done (committed): KeepHall.fbx (Shell 22,706 / Frame+door 30,983 tris, self 27.5/40, SHEET_keep_hall.jpg) and KeepTerrace.fbx (20,666 tris, 26.5/40, SHEET_keep_terrace.jpg). Contract corrections at the end of CONTRACT.md (hall yaw 0, door node (-9.2, 25.0), slot tables).
- RESUMED (coordinator session 5). Integration agent (Sonnet) running Unity: KeepHall row + door edge + test, hall placer, BuildKeep on the kit, keep captures, then one check and merge.
- Left: integration (layout row KeepHall + door edge + test, BuildKeep on the kit, hall placer), captures, check, merge.

## Usage waste (this session, honest look back; rough shares of what it spent)
1. **One long Opus controller session carrying everything (~40%).** This session has run the bakery, Q2, Q4 and Q9 back to back, so every turn re-sends a huge context; on top of that I did the bakery art myself on Opus (render/inspect loops, reading big parts of sc_facade_kit.py, full-size images in context). Fix: one queue item per fresh session, with the controller only planning, scoring and merging; art and Unity loops go to Sonnet from the first step, with a short brief instead of the controller's history.
2. **Repeated full `unity.sh check` and capture runs (~25%).** About ten full checks (15-20 min each) and many captures. Causes: main moving between my check and my merge (re-merge, re-check); generated files that differ between graphics captures and -nographics builds (the MotionVectors pass on materials, HeroAnimator/BeautyCorner churn), so commits after a check broke the marker; a capture bug (full-strength baked reflection probe) that made identical settings give different frames and cost two extra tuning rounds. Fix: regenerate with a -nographics build, commit, then run a single check right before merging; never commit materials written by a graphics capture; make builders write identical bytes in both modes (fixed for the probe, still open for the MotionVectors flip); serialize merges through the coordinator so main doesn't move under a green check.
3. **Subagent overhead (~20%).** Each agent re-read 1,000+-line templates (sc_bakery.py, sc_facade_kit.py) to learn the helpers; render rounds at higher res/samples than needed; one agent kept re-sending the same report (stopped by hand); stop-hook-driven WIP checkpoint commits mid-run. Fix: a one-page helper API note (what to import from sc_facade_kit/sc_bakery and how) handed to every art agent; fixed render caps (1000 px, 20 spp, 3 rounds) in the brief; stop agents as soon as they report.
