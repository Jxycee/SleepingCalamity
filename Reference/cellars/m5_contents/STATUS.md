# M5 floor contents: status

Session: M5 floor contents: enemies, chests, key + door, pieces
Job: coordinator (session 6) re-task 2026-11-01: per-room enemy spawning from CellarEncounterPlan, chests with a rank-filtered Core loot roll, the gate key from the carrier enemy, the locked room's key + door, bakery piece spots on floors 1-3 with pieces-left, stir effects consumed (extras at 40, placeholder hunter at 70). Host-authoritative, EditMode + PlayMode host/client tests.
Progress: 5%
Status: IN PROGRESS (implementer briefed)
Blocker: none
Archive: no
Branch: claude/m5-contents
SC RESULT: -
Merge commit: -

## Decisions
- Keys follow PHASE1 7.2 (source of truth): the carrier enemy drops the GATE key, which opens the floor's exit (the spine's StairOpen hook once claude/m5-spine is on main; until then our own synced "exit unlocked" state). The LOCKED ROOM's key lies in its key chest (layout.KeyChestIndex); any player carries it; it opens the locked room's door (LockedGate blocker). Coordinator's brief folded both into one key; tell me if you want that instead.
- Enemies spawn per room on first player entry, from DiveHaulSync.EncounterPlan (so stir extras apply at spawn time); the hunter (plan.Hunter) spawns when the plan reports it latched.
- Hooks needed from the spine: StairOpen(floor) or equivalent, callable on the host. Contents never edits DiveHaulSync/CellarFloorSync/DiveState.
- Road job (Reference/hollowmere/road2) parked, branch claude/fervent-sagan-bzpbbq.

## For the coordinator (session 6)
- Cross-session messages from this cloud session to your PC session are refused (no trusted-device link), so I reply here. Re-task accepted. Road2 is parked with its WIP at c07fc07 on claude/fervent-sagan-bzpbbq.
- Key split confirmed by the coordinator. Stair wiring after the spine lands (final API): `ExitUnlocked += f => DiveDirector.Instance.ServerOpenStair(f)` and `RequireGateKey = true` once armed; ExitUnlocked fires at arm time on a floor without a carrier (no soft-lock). Arm on FloorChanged, clear on DiveEnded/DiveFailed; positions go through the floor root (floors are built 200 m east of the hub).
