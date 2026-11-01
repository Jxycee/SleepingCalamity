State: WORKING
Session: Local next: M5 spine (dive flow, floor transitions, sync objects in game)
Progress: 85% (review fixes + kit + gate-key API done; check PASS 250aff0; main gained Asset commits, so re-merging main + re-check running)
Status: built, in review
Archive: not ready
Branch: claude/m5-spine (merges to main on its own green check, risk-reviewed; merges BEFORE the contents session)
Last check: PASS 250aff0 (edit 815/815, play 42 pass 0 fail 8 skip); re-check after merging main running now
Next step: on green re-check merge spine to main, add walk start steps to USER_TODO.md, tell contents session
Sheet: none
Needs: none (Hollowmere entry -> M6, coordinator)
Blocker: none

## Scope (coordinator, 2026-10-31; user walks floors 1-4 in 2-player tomorrow)
1. Dive flow: entry from the Undercroft stair + a debug start; host picks the seed; floors 1->2->3->4 by stair-exit interactables; floor-2 checkpoint; floor-4 rescue bank leads home; DiveFailed returns the party to the start (lost pieces back to floors, P20). Minimal host DiveState flow; town banking UI is M6.
2. DiveHaulSync + CellarFloorSync spawned as NetworkObjects for the dive; floor rebuilt on every peer on transition.
3. PlayMode: floor-4 3-peer case; host + client walk floor 1 -> 2 by the stair and see the same floor.
4. Stir-effects hook (CellarEncounterPlan, host EncounterPlan/EncounterChanged): finishing its reviewed check on claude/m5-stir-effects; it is a plan API only (no spawning), merged if green, then handed to the contents session to drive enemy spawning.

## File ownership
Mine: DiveHaulSync, CellarFloorSync, DiveState, dive bootstrap/scene, stair/checkpoint/rescue interactables, `CellarFloorBuilder.Dive.cs` (new partial). Contents session: `CellarFloorBuilder.Contents.cs` and what goes inside rooms.

## Results
- Stir-effects hook merged before the switch: main e39e475 (check PASS 75a0df3).
- Spine (claude/m5-spine 1564ef1, not merged): test-edit 814/814, test-play 42 pass 0 fail 8 skip (5 new DiveSpineNetworkTests). New session scene Arena_Dive (pad, stair-mouth stand-in, DiveHub) in SceneNames.BuildOrder; floors built in place 200 m east (no scene load in a dive). DiveDirector (host) spawns CellarFloorSync + DiveHaulSync once per session, seed per session, floor 1; interacts (E) at the stair (floors 1-3), bank (2, 4) and go-home (4) are range-checked client RPCs; teleport = owner RPC after its floor built (seed, floor), CharacterController off, NetworkTransform.Teleport; host placement pass every 0.2 s; dive end keeps the syncs (banked + checkpoint survive); return dive from floor 3 after the checkpoint; DiveFailed / F8 return to the pad; F7 debug start (dev only).
- How to start: dev panel "Start in the Cellars dive hub" / menu SleepingCalamity/Dev/Dive Hub / -dev-dive; solo or host; violet column in the stair mouth, E.
- Contents hooks: DiveDirector.StairOpen (Func<int,bool>, default open), DiveStarted, FloorChanged(int), DiveEnded(DiveEndReason); DiveHaulSync.Banked, DiveFailed, EncounterPlan/EncounterChanged.
- Review fixes 250aff0: prompts hidden until the owner's teleport matches its built floor (no floor skip); range refusals send "You are too far away."; bank/go-home prompt 4.5 m (< host 5 m); contents API: DiveDirector.Instance, RequireGateKey (default false), ServerOpenStair(int floor) (StairOpen Func removed); runtime CellarFloorKit saved as prefabs (Content/Art/Cellar/FloorKit) and set on the CellarFloorSync prefab; camera warp via CinemachineCore.OnTargetObjectWarped. Arena_Dive still has no CombatHost (built inline in SceneBuilder).
- Gaps: Hollowmere not a session scene (decision above); floors flat greybox (Kit null at runtime, fixing); no enemies/keys/loot (contents session); out-of-range stair press silent; interact key not in PlayMode tests.

## Handoff notes
If this session stops: risk review of claude/m5-spine is the open step; then wire the kit, merge origin/main, one check (wrapper with -ProjectRoot the spine worktree C:\Dev\SleepingCalamity\.claude\worktreesgent-a1996cefce57949ad, -TimeoutMinutes 30), merge commit to main, tell the contents session.
