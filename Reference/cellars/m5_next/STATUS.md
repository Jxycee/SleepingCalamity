State: DONE
Session: Local next: M5 floor 4 set-piece + banking (+ stir meter)
Progress: 100% (all three merged: floor 4 df109f2, banking f1b34ac, stir b700d92)
Status: DONE
Archive: ready
Branch: claude/m5-next-639689 (floor 4 on a subagent worktree branch, merges back here)
Last check: PASS 586a673 (stir: edit 786/786, play 36 pass 0 fail 8 skip); banking PASS aa1fa5a (775/775, 35/0/8); floor 4 PASS d40fe81
Next step: none for this session. Follow-ups (not mine): dive flow that spawns CellarFloorSync + DiveHaulSync and calls ServerSetFloor/ServerBegin, wires DiveFailed; P47 healing at bank points; stir HUD reading latch flags; banked haul in the world save (M6); 2-player walk of floors 1-4
Sheet: none yet
Needs: none
Blocker: none

## Plan (each item merges on its own green check)
1. Floor 4 bakery oven maze: fixed hand-authored layout replacing `BuildStub` in `CellarFloorGenerator` (Core). Entry, a maze of oven/proofing rooms with loops, the baker's heart room (Bank feature = rescue bank point, full heal, leads home), Dough Warden guard spawn (Tier2) on the only approach, stair-exit replaced by the rescue. No locked room, no gate key. Validator + EditMode tests (deterministic, valid, golden hash), builder test case for floor 4, PlayMode floor-4 sync case. Reviewer: `reviewer`.
2. Checkpoint + banking: Core `DiveHaul` (host-held shared haul: pieces with origin floor + loot; Bank(); Fail() returns unbanked pieces to their floors). `DiveHaulSync` NetworkBehaviour, host-authoritative, banking at the floor-2 checkpoint Bank feature and floor-4 rescue. EditMode + PlayMode 3-peer tests. Reviewer: `risk-reviewer`.
3. Stir meter: Core `StirMeter` (0-100, +10/floor cleared, +5/unbanked piece, x(1+0.25(p-1)), thresholds 40/70/100, 90 s deep-closes timer), host-owned NetworkVariable, events for the three effects. EditMode + PlayMode tests. Reviewer: `risk-reviewer`.

## Questions for the coordinator (my proposal in brackets)
- Q1 Haul contents: [bakery pieces + chest loot both go in the shared haul; banking moves both to the banked store].
- Q2 Stir meter math: [floor term accumulates for the dive; piece term = pieces currently unbanked, so a checkpoint bank lowers it; full reset only on town bank; thresholds latch once crossed for the dive; player factor = players present now].
- Q3 Stir effects scope in M5: [meter + threshold events + the 90 s timer that fails the dive; "+1 enemy per room" exposed as a count the spawner reads; the rat-king hunter is an event + placeholder spawn marker only, real enemy later].
- Q4 Banking input: [any player interacts at the Bank feature; client asks via RPC, host banks; no auto-bank on walk-in].
- Q5 Dive flow (seed pick, floor transitions, return-from-checkpoint, failure): [M5 adds only a minimal host `DiveState` (current floor, checkpoint reached, cleared floors) that banking/stir need; the full flow and town banking UI are M6].

## Coordinator answers (session 6, 2026-10-31)
All Q1-Q5 proposals OK. Stir values/thresholds are P21 placeholders kept in one data/config spot; only pieces add the +5 term (chest loot rides in the haul, no stir); host owns meter and haul; bank RPC + DiveState get a risk-reviewer pass.

## Results
- Item 1 floor 4 oven maze: worktree-agent-adcba4d63b883e0ab a838e67, merged into this branch. 7 rooms (entry, ring of 4 oven rooms, antechamber, heart with Bank + baker), Dough Warden Tier2 on the approach, 7 chests, no stair/lock/key. New CellarBakeryPool.cs; enums appended (SubTheme.Bakery, RoomKind.Heart, FeatureKind.Baker). Golden floor 4 = 8894238083671442472 (all seeds). reviewer: approve, no blockers. SC RESULT check PASS d40fe81 after merging main with Cellars variety dbc8996 (conflicts in Layout/Validator resolved keeping LastResort + exit-distance rule beside the bakery checks). MERGED to main df109f2. Main art pass 0bc51ae merged in cleanly (fda3b8d).
- Item 2 banking: SC RESULT check PASS aa1fa5a (edit 775/775, play 35 pass 0 fail 8 skip); MERGED to main f1b34ac. claude/m5-banking 0111014 + review fix 6e30325. Core DiveHaul/DiveState, Presentation DiveHaulSync (host-written summary NV, RequestBankRpc SendTo.Server, host checks floor has bank + 5 m XZ range when a PlayerObject and the matching built floor exist). risk-reviewer: approve; should-fix (bank cell from a stale floor) and the floor-3 refusal test nit fixed in 6e30325. Floor 4 vignettes: off on the bakery (aa1fa5a, rides with banking; dressing + atmosphere stay; selection replay untouched). Not done: P47 healing; nothing spawns DiveHaulSync in game yet; banked state not saved (M6).

## Usage waste
- The wrapper's -ProjectRoot defaults to the main checkout: a subagent's first test-edit tested main (one wasted run). Briefs now always pass -ProjectRoot.
- First check in a fresh worktree hit the default 5 min build timeout during import, then LOCKED on its own leftover Unity: 2 wasted runs. Use -TimeoutMinutes 30 on a worktree's first run.
- The first check (baaba42) went stale when main gained the floor art pass and the variety branch was queued ahead: one full check to be repeated.
- One stir check re-run for the flaky parry test (CombatArenaTests.Solo_DaggerGuard_Reduces_AndAParryStopsTheHit, real-time parry window; also seen in the inn session). Worth a fix: drive that test off frames or widen its window.
- Item 3 stir meter: SC RESULT check PASS 586a673 (edit 786/786, play 36 pass 0 fail 8 skip; the first run failed once on the known-flaky real-time parry test CombatArenaTests.Solo_DaggerGuard_Reduces_AndAParryStopsTheHit, passed on re-run); MERGED to main b700d92. claude/m5-stir 09ecf7a + review fixes e6b17dc (DiveFailed host event, countdown dt clamped to 0.25 s, floor bound from MaxFloor). Core StirMeter + StirTuning.Default (all P21 numbers in one spot), owned by DiveState, host-ticked in DiveHaulSync, fields in the synced Summary (seconds left CeilToInt so one update per second). test-edit 732/732, test-play 36 pass 0 fail 8 skip (pre-fix). risk-reviewer: approve with notes (HUD should read latch flags, not compare StirValue).
