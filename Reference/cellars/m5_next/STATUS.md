State: WORKING
Session: Local next: M5 floor 4 set-piece + banking (+ stir meter)
Progress: 5% (plan; floor 4 starting)
Sheet: none yet
Needs: coordinator - design choices Q1-Q5 below (floor 4 proceeds meanwhile)
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

## Results
(none yet)

## Usage waste
(none yet)
