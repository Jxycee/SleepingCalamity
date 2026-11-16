State: BLOCKED (paused for Fable Run 6, area 1 visual rework)
Task: B4 home, bakery lot, market row (B3 polish already merged: dbf5dbb3)

## B3 polish (done, on main)
check PASS on 77eb670d (EditMode 1819/1819, PlayMode 303/0/36). Sheet Reference/hollowmere/b3/B3_sheet_final2.jpg. Strict scores 20-23 (target 25 not reached after 3 rounds): HomeDoor 22, Fountain 23, SquareSouth 23, SquareWest 22, Ruins 22, Overview 20. Open lows: GradedCobbleAlbedo silent null if Fab source missing; DropRingBed name dependency.

## B4 round 1 (paused mid-run, UNVERIFIED)
Branch claude/b4-home-bakery-market, unmerged, no check run. The round-1 agent was stopped partway, so the committed WIP may not compile and the gate has not been run on it.
- Work in tree: HollowmereB4Spots.cs (new builder), Content/Art/Town/B4Kit/ (new kit assets), Art/Blender/town/sc_scaffold_kit.py (scaffold kit), edits to Core/Town/HollowmereDressing.cs + tests, HollowmereBuilder/Clutter/Hub.cs (spawn yaw DH8, build point), App/ArenaPlayerSpawner.cs.
- A BEFORE capture set was shot (scratchpad b4/before); no After capture, no sheet, no score.
- Paused by the user's choice 2026-11-15: Fable will rework area 1's visual look, so B4 dressing would land on ground that is about to change. Resume after Fable Run 6: re-verify compile/gate, re-shoot before, then rounds.
Archive: ready
