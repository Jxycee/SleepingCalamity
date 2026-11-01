# M5 floor contents: status

Session: M5 floor contents: enemies, chests, key + door, pieces
Job: coordinator (session 6) re-task 2026-11-01: per-room enemy spawning from CellarEncounterPlan, chests with a rank-filtered Core loot roll, the gate key from the carrier enemy, the locked room's key + door, bakery piece spots on floors 1-3 with pieces-left, stir effects consumed (extras at 40, placeholder hunter at 70). Host-authoritative, EditMode + PlayMode host/client tests.
Progress: 80%
Status: IN PROGRESS (review fixes in at 434cfc0; Unity suites running; waiting for the spine on main)
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

## Progress (2026-11-01)
- 03ebeba (merged into the branch at 05d8751): Core CellarLoot (12 placeholder items, ranks 1-5, triangular roll on "cellars.loot"; RoomKeyId 9001, GateKeyId 9002), CellarPieceSpots ("cellars.pieces"), CellarRoomClear + CellarEnemyMix; host-authoritative CellarContentsSync (prefab + NetworkPrefabs entry), CellarFloorBuilder.Contents.cs, HUD "Pieces left on this floor". Branch runs: build PASS, test-edit 821/821, test-play 43 passed / 8 skipped / 0 failed (6 new contents tests). These are not the merge check.
- Known bug to fix: a party wipe clears enemies and counts as room cleared, which drops the gate key.
- Spine hook ask (coordinator): DiveDirector spawns CellarFloorSync/DiveHaulSync (DiveDirector.cs ~182-189). Contents needs CellarContentsSync spawned there too, plus `AutoArm = false`, FloorChanged -> ServerArmFloor, DiveEnded -> ServerClearFloor, `contents.ExitUnlocked += f => ServerOpenStair(f)` and `RequireGateKey = true`. Unless you object, I add these few lines in DiveDirector myself after the spine is on main.
- Risk review (34c274a): no blockers on the branch alone. Should-fix items in the fix round:
  - the wipe cause (non-death despawn treated as a kill);
  - stale positions right after a floor change;
  - piece visuals placed at local coordinates but picked up at world ones (would break 200 m east);
  - keys added to the haul as loot;
  - pieces per floor taken from the defaults instead of the haul;
  - a despawn leaving enemies orphaned.
- Deferred until the spine wiring: one Interact press acting on both a chest and the bank/stair.
- Question for the coordinator: chests refill with the same loot on every dive (the spine reuses the world seed). Is that OK for Phase 1?
- Review fixes 434cfc0 (merged into the branch at adacfee):
  - A party wipe no longer counts as a kill: the room resets and respawns on re-entry, and the hunter re-arms.
  - Players count only after moving onto the armed floor.
  - Keys are tracked as state only, not as haul loot.
  - Pieces per floor come from the haul.
  - Enemies are cleared on despawn.
  - CellarFloorBuilder.Build now respects the floor root's offset: it builds at the origin, then parents the root, and Bounds/EntryCenter are in world space. Before this, the geometry ignored a parent at 200 m while the spine's WorldCell/TransformPoint markers did not, so the spine needs this fix too.
- For the spine merge: expect conflicts in NetworkPrefabsBuilder.cs, PrefabBuilder.cs and the NetworkPrefabs asset; I keep both entries and regenerate the asset with the builder.
- Disk: the session allowance filled with three worktree Libraries; two merged worktrees were removed.
