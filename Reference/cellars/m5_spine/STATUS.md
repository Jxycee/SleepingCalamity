State: WORKING
Session: Local next: M5 spine (dive flow, floor transitions, sync objects in game)
Progress: 5% (plan; implementer starting)
Status: dive flow implementer starting on claude/m5-spine
Archive: not ready
Branch: claude/m5-spine (merges to main on its own green check, risk-reviewed; merges BEFORE the contents session)
Last check: none yet
Next step: implement dive flow + sync spawn + PlayMode tests; risk review; check; merge; tell contents session via coordinator
Sheet: none
Needs: none
Blocker: none

## Scope (coordinator, 2026-10-31; user walks floors 1-4 in 2-player tomorrow)
1. Dive flow: entry from the Undercroft stair + a debug start; host picks the seed; floors 1->2->3->4 by stair-exit interactables; floor-2 checkpoint; floor-4 rescue bank leads home; DiveFailed returns the party to the start (lost pieces back to floors, P20). Minimal host DiveState flow; town banking UI is M6.
2. DiveHaulSync + CellarFloorSync spawned as NetworkObjects for the dive; floor rebuilt on every peer on transition.
3. PlayMode: floor-4 3-peer case; host + client walk floor 1 -> 2 by the stair and see the same floor.
4. Stir-effects hook (CellarEncounterPlan, host EncounterPlan/EncounterChanged): finishing its reviewed check on claude/m5-stir-effects; it is a plan API only (no spawning), merged if green, then handed to the contents session to drive enemy spawning.

## File ownership
Mine: DiveHaulSync, CellarFloorSync, DiveState, dive bootstrap/scene, stair/checkpoint/rescue interactables, `CellarFloorBuilder.Dive.cs` (new partial). Contents session: `CellarFloorBuilder.Contents.cs` and what goes inside rooms.

## Results
(none yet)

## Handoff notes
(kept current at each step)
