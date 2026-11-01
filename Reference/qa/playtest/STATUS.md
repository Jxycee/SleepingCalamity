# Alpha playtest (bug hunt)
Status: DONE (BUGS.md: 6 major + 12 minor + speculative leads; DiveBot, VoidEdge scan check and the bug list merged to main on a green check)
Blocker: none
Branch: claude/great-lovelace-r22yqw (restarted from main dd4526c)
Last check: PASS at 5acf6b6 (EditMode all passed, PlayMode 68 passed 15 skipped: the bot runs are SC_BOT-only)
Next: owners take BUGS.md items (combat/enemy AI: 1, 2, 6-8, 14-16; dive flow: 3-5, 9-11; contents: 12-13; town: 17). With M6 S3 on main, re-run TraversalScan on the new Hollowmere hub and WriteBaseline before working FIXLIST town items.
Session: World traversal scan (cloud), second job on the user's request
Job: Alpha tester: find as many play bugs as possible (DiveBot playthroughs of real solo dives + code read-throughs of dive flow, contents, combat)
Archive: ready

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
