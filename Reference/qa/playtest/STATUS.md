# Alpha playtest (bug hunt)
Status: IN PROGRESS (BUGS.md written: 6 major + 12 minor + speculative; check running, then merge)
Blocker: none
Branch: claude/great-lovelace-r22yqw (restarted from main dd4526c)
Last check: none yet on this branch
Next: green check -> merge DiveBot + VoidEdge scan check + BUGS.md to main -> DONE
Session: World traversal scan (cloud), second job on the user's request
Job: Alpha tester: find as many play bugs as possible (DiveBot playthroughs of real solo dives + code read-throughs of dive flow, contents, combat)
Archive: no

## Results so far
- Bug list: Reference/qa/playtest/BUGS.md (owners per item: combat / enemy AI, dive flow, contents, town).
- Top issues:
  - Solo dives fail every 40-70 s (bot never past floor 2 without host top-ups).
  - Enemies have no aggro range or line of sight, and hits pass through walls.
  - Banking the checkpoint makes floors 1-2 unrevisitable (seen: the return dive starts on floor 3).
  - Bank spam = free full heal.
  - Downed heroes and revivers trigger stair/bank/go-home.
  - Parry growth farm.
- DiveBot (SC_BOT=1 only, `check` never runs it):
  - In tough mode it plays hub -> floors 1-4 -> heart -> home -> second dive on the pad with no failed presses or pickups.
  - Its earlier fall and entry-stuck findings were bot NavMesh-timing artefacts (fixed, not reported).
- Traversal scan gained a VoidEdge check: Cellars 0 over 200 floors; Hollowmere 64/68 rim spots at the west terrain edge (FIXLIST item 10).
