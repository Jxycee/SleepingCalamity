State: merged to main (5b 871802e0, step 6 d07261d7)
Branch: claude/dive-5b-water (from origin/main 366d0126)
Archive: ready

## Plan 5b (2026-11-15)
Findings: the 12 pool combat rooms are `RoomDataV2.FromV1` wrappers (no regions); only the pilot hanging hall has a sump. The generator and spawner read v1 `CellarRoom`; v2 feeds tests, encounters and the greybox. `RoomValidatorV2` already loops every state vector (rules 6-9).
Conflict: DC7 (`dungeon1-cellars.md`, user 2026-11-13) = no standing water in Phase 1; spec section 5/7 = water gimmick floors 1-5. `dungeon1-cellars.md` wins: states are built and validated, switched OFF on dungeon 1.
1. `Core/Rooms/CellarWaterRooms.cs`: water-authored v2 variant of each of the 12 pool combat rooms: one sump region (letter w) with states {drained 0, wade 40 or flooded 80+}, a valve Control socket on a dry wall cell, stairs/levels only if the room needs a dry route. Same id/geometry as the pool room (shared CellarRoom object).
2. Every variant passes `RoomValidatorV2` over all state vectors (rules 1-9), fits-party and encounter-table checks keep passing; golden test lists each room's states and standable-cell counts per state.
3. `Core/Cellars/CellarWater.cs` (pure policy): `Enabled(dungeon, floor)` = false for dungeon 1 (all 7 floors), true for later dungeons floors 1-5; `Bubbling(dungeon, floor)` floors 4-5; `StartState(seed, floor, roomIndex)` deterministic via its own named stream; `BrimmingHoldSeconds` = 90 (spec), applied only when Enabled (dungeon 1: no-op, Brimming keeps its heal/loot effect).
4. Tests: EditMode for validator on all 12 variants, per-state reach, policy off on dungeon 1, determinism per seed, Brimming no-op. Soak (Core/Cellars touched) run before merge.
5. Out of scope, noted for the coordinator: runtime water mesh/replication and valve interaction wiring. Dungeon 1 has water off, so there is no networked state this step; a later dungeon must add a host-authoritative room-water sync (sc-netcode) when it turns water on. Presentation: `RoomGreybox` already draws water per state.
Review: `reviewer` (pure Core), then `sc-merge`.

## Step 6 plan v2 (2026-11-15, branch claude/dive-6-jobs; advisor-reviewed; replaces v1)
Scan: special-room kinds (EliteDen/Treasury/Shrine/Quiet, CellarSpecialRoom.cs) are greybox only; healing point = flat 51% non-dead-end cell; collapse already path x1.5, 60 s min, early close (that IS the research's "fairness": distance-scaled + min + early close); crash saver unbuilt beyond bank suspend.
Rulings: no parallel picker. The spec says jobs "give the existing special-room kinds real effects", so effects hang on SpecialKind: Quiet = mending spring (heal tick, reuses DiveHaulSync healing path), EliteDen = challenge hall (clear = extra loot rolls, reuses 5c extra-roll grant), Treasury = vault (extra chest rolls on reaching/clearing, same grant), Shrine = stir altar (one-time DiveStir delta). Selection stays seed-only (CellarSpecialRoom unchanged, no party-HP input: hurt/healthy gating is dropped for dungeon 1, listed as a later host-side option). Farthest-first already holds (Arc/Branch preferred over Spine in CellarFloorRoute); dead ends never lock = the special room is a normal combat room whose exits lock until clear, so no lock rule change.
Work, in order:
1. Collapse start bug (host, DiveHaulSync.TickCollapseStart): if the floor is not built in 3 s the timer starts with path 0 = 60 s minimum on any floor size. Fix: keep waiting for the floor (cap long, e.g. 30 s), then fall back to Manhattan path from layout, never 0. Test: PlayMode/EditMode. [net]
2. Core: SpecialJobTuning + pure effect helpers (heal amount, extra rolls, stir delta) with EditMode tests; floors 1-3 numbers small (dungeon-one guardrail).
3. Host wiring of the four effects through existing paths only (DiveHaulSync heal tick, CellarContentsSync extra-roll grant, DiveStir delta), per-room once flags, client-less (host authoritative, cleared-room latches like _roomPaid). [net]
4. Reviews: Core part reviewer; host parts risk-reviewer. Stop merge-ready, board needs: coordinator (networking merge needs the user's go).
Crash saver: NOT simple, skipped. Pieces it needs: floor-start dive snapshot writer in the host save, Application.quitting clean-exit marker, next-launch crash-evidence check (Player-prev.log / crash folder), resume-at-floor-N offer UI, and IsResumable change (today only bank room / floor 5 after push-on). All save data + App + UI; spec and QUEUE say skip if complicated. Filed for the coordinator.
Churn bug to report: after `check`, the build rewrote 9+ Hollowmere materials (M_GD_Leaf, M_GD_Produce, M_GD_Straw, M_GD_patch_rust, M_GD_patch_seam, M_HM_Surf_Moss, M_HM_Surf_Stain, M_HM_Surf_Wear, M_StoneCottage_B_SC_Rubble .mat and maybe more under Content/Materials/Hollowmere); discarded, not committed (session-usage.md).

## Step 6 final (2026-11-16)
State: merged to main (5b 871802e0, step 6 d07261d7)
Check PASS on 97b9952a: EditMode 1843/1843, PlayMode 307 passed 0 failed 36 skipped; soak PASS 47/47. Risk review: no blockers; should-fixes applied in 9c457131 (shrine bonus in StirMismatch, 4 PlayMode job tests, seed isolation in modifier gold tests).
Flakes seen once under load, passed on rerun: StairHandoverTests Rtt300Loss5 (timeout, 3 sessions on the PC); FloorCardTests soak sweep hit the 180 s default (now [Timeout(600000)]).
Also on main from this session: 5b 871802e0, stale M_HM_Surf_* mats fix 88a8a6c5. Open churn bug: build rewrites M_GD_Leaf/Produce/Straw/patch_rust/patch_seam + M_StoneCottage_B_SC_Rubble .mat (not committed).
Not built: crash saver (not simple: floor-start snapshot writer, quitting marker, crash-evidence check, resume UI, IsResumable change; all save data). Rewards of EliteDen/Treasury stack with Brimming/Fat Larder rolls in the same room (reviewer nit 4, read as intended). Hurt/healthy gating dropped for dungeon 1.
Archive: ready
