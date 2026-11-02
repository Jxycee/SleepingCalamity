# Floor 3 locked door (seed 12345): verdict BOT / HARNESS ONLY (2026-11-02)

Question: the quad soak host bot reported "DoorOpen but its gate object still blocks" (F3 at 252.6,-8.9 and F2 at 241.5,-31.6) and then sat 23 min on floor 3, stuck at Wall_12_2_East. Real game bug?

## Evidence

1. **Core (generator/validator)**: the locked room is a leaf (exactly one link, validated), the key chest and the gate-key carrier must be reachable without passing the lock, and the carrier may never be in the locked room (CellarFloorValidator: "the gate-key carrier is in the locked room" is an error). So the stair route never needs the locked door. The F3 gate is at local (52,-6) = the east door of room cell (12,1); Wall_12_2_East is the next cell down (local z -8 to -12), i.e. the plain wall beside the corridor cell (13,y). It is a legitimate wall, not a wall in a door cell.
2. **Builder (EditMode, new `CellarGateOpeningTests`)**: for seed 12345 floors 1-3 and seeds 7/99/4242, the LockedGate is the only solid thing in its opening: with it hidden, a hero capsule (r 0.4, h 1.8) fits at every 0.25 m step from 1.5 m before to 1.5 m after the opening; with it shown, the opening is blocked. PASS.
3. **Sync (PlayMode, new `Door_Floor3Seed12345_OpenLeavesNoGateColliderOnAnyPeer_AndAHeroWalksThrough`, host + 1 client, floor 3 seed 12345)**: key chest, then door open. Host and client `GateBlocking` both go false, no active `LockedGate` collider remains anywhere in the scene (host and client floors share one physics world there), and a CharacterController walks 6 m through the doorway. PASS.
4. **Why the bot printed it**: `DiveBot.RebakeIfDoorOpened` runs the frame `State.DoorOpen` flips and reads `GateBlocking` at once; `CellarContentsSync.ShowState` hides the gate in its own `Update`, so the bot can read it a frame early. The door opened (the log says "locked door opened: NavMesh re-baked"). The same message on F2 fits that.
5. **The long stuck spell**: dive 2, the bot's NavMesh (baked while the gate is solid) gives PathPartial to the gate, so it parks at the nearest point in the corridor (2.9 m, on the 3 m edge, with the jamb close to the line of sight). It later reports the hero at y = 1.6 (the floor is y 0) against the wall, 0.00 m moved: a hero 1.6 m up is standing on something, most likely another peer's `PlayerCapsule(Clone)` (1.8 m high; the quad harness puts four peers' hero copies on one floor and different heroes collide). The same harness shows identical "blocked by PlayerCapsule(Clone)" stuck events on F0 and F4. Not proven from the log alone (no probe of that exact spell), but nothing static or synced blocks the route.

## Verdict

Bot navigation/harness artefact. No game change. The two new tests stay as regression guards (fast tier).

## Remaining caveat for the playtest

If a real player stands on a friend's head in a corridor they could in principle perch; that is a separate co-op collision question, not the gate.
