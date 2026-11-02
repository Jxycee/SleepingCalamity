# Floor 3 "fall" at cell (13,13), seed 12345 (pair bot) - verdict: not a floor hole

Date 2026-11-02. Verdict: bot-timing artefact of the stair hand-over. No floor defect, no game change.

## Evidence
- The finding is mislabelled by floor. The bot's floor counter had already flipped to 3 when the hero was still standing on FLOOR 2's stair. Core layout dump (seed 12345):
  - Floor 2 (preset Ring): StairExit = cell (13,13), world (254, 0, -54). The hero trail (230.4,-39.1 = floor 2 checkpoint bank at cell (7,10); then walking to (252.9,-52.3)) is floor 2.
  - Floor 3 (preset Spine): the grid corner (13,13) is not occupied (no room, no corridor), so it has no slab by design. Floor 3's stair is at (8,10). Every occupied cell gets a BoxCollider slab in CellarFloorBuilder.Floor, so there is nothing to patch.
- Mechanism: ServerTryStair swaps the floor at once (FloorSync.ServerSetFloor destroys floor 2 and builds floor 3 at the same origin). The press leaves the hero where floor 2's stair was, over a cell floor 3 does not have. The hero is moved to floor 3's entry only by the host placement pass, which runs every DiveDirector.PlacementInterval (0.2 s of Time.unscaledTime) and then TeleportRpc/ApplyPending. DiveDirector already knows the window exists (LocalInteract: "not yet teleported onto this floor: standing where the last one's stair was").
- Reproduced with the same pair bot (CoopBot_Pair_Seed12345, 5 min) with temporary trace logging (removed): F1 stair taken at frame 13966 (t 10.254), teleport sent at frame 13995 (t 10.438, 29 frames later), hero already at y -4.62; applied one frame later; the Fall finding fired on the F1 to F2 stair (cell (13,3)). A host-only probe with no bot (stairs F1 then F2, seed 12345): hero teleported 2 frames after the F2 to F3 press, ~190 tiny-step frames (~0.1 s real) after the F1 to F2 press; no floor hole.
- Why it got to y -6 in the original run: the bots run at Time.captureFramerate 60 (game time 1/60 per frame) but PlacementInterval counts real unscaled time, and two peers each rebuild the floor (heavy frames). The same 0.2 s real wait is many more game frames in the bot than in play, so the hero fell ~6 m before the teleport arrived (FallNet re-places a hero below origin-15 as a backstop). It did not repeat in the quad run because the stair cell happened to fall in different frame timing (and floor 1/2/3 stair cells overlap the next floor's cells on some seeds, which hides the drop).

## Player impact
Real play at 60 fps: up to ~0.2 s plus one network tick over a cell with no new floor, so the hero visibly sinks a little before the arrival teleport. Not a collision hole; the hero is always placed. No action needed for tonight.

## Optional hardening (not done, outside this brief)
Run PlacementPass straight from ServerTryStair/ServerTryDescend (reset _nextPlace) so the hand-over takes one frame; or hold the CharacterController gravity of the pressing hero until its teleport lands.
