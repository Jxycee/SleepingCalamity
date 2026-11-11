---
name: sc-netcode
description: SleepingCalamity's checklist for networked and saved gameplay (NGO 2.x host authority, in-scene NetworkObjects, parenting riders, world save sections) - the patterns this project uses and the bugs its risk reviews keep finding. Use before writing or reviewing any NetworkBehaviour, RPC, NetworkVariable, parenting change, scene switch, or a new world-save section, and before claiming a test covers a network path.
---
# Networked and saved gameplay in SleepingCalamity

Distilled from FishGame's 2026-10-25 risk reviews of tug driving, world save, weir boards and the digs. Each line below was a real finding. Those classes were not ported, so names like `TugBody`, `WeirBoardsState`, `DigSite`, `WorldSession` and `PlayerSpawner` below are FishGame's; the patterns still apply. In this repo, read the class comments of `CombatHost` (Gameplay/Combat), `WorldSaveFlow` and `SceneFlow` (App) and `DiveSession` (Core/Cellars) for working examples.

## Authority (CLAUDE.md: host-authoritative; no anti-cheat)
- One class-level comment per networked class naming who writes what (host vs owner). Only the host writes `NetworkVariable`s; clients ask with
  an RPC and the host re-checks every rule (`CanInteract` again inside `Server*`). A client-side gate is a prompt, never the rule.
- Rate limits and debounces live on the host, per site or per object (`DigSite` one swing per 0.6 s per site; crank presses debounced 0.3 s).
  Two presses in one tick must not double-apply.
- Discrete state (cleared bits, counts, board positions) goes in `NetworkVariable`s so late joiners get it with the spawn. Toasts and one-shots
  are RPCs and must not replay on late join or on a save restore.

## Parenting and scene switches (the tug's riders)
- NGO keeps only **unparented** players through a Single scene load. Every Single load from a scene with a parent-able object must first close
  boarding, wait a tick plus a relay round trip, release riders, wait, release again, then load (`SceneFlow.HandleDevLoadSpot`). A last net:
  `OnNetworkDespawn` unparents the local player.
- `NetworkVariable` changes land a tick after the RPC/unparent sent in the same frame: never rely on "the flag is already false on clients".
- In-scene objects spawn before `OnLoadEventCompleted`, so `PlayerSpawner` can parent under them; nested in-scene `NetworkObject`s under a moving
  root are not allowed (one networked root).

## Input
- Every new interaction mode needs its own `InputContextRouter` row, or keys get zeroed (the anchor key was dead under `Free`, which zeroes
  `DropPressed`). Add an EditMode router test for the row.
- A held input resent every 0.2 s looks "new" forever: state changes that need a fresh press (cast off after mooring) must arm on a zero first.

## World save (R89: `WorldSave` v1, `WorldSaveRules.Clean`, `WorldSaveStore`, `WorldSession`, `WorldSaveFlow`)
- New state = an **optional** v1 section, cleaned on its own in `WorldSaveRules`; a bad value drops only that section (or that entry: digs),
  never the file. Mask bit fields to the valid bits instead of rejecting. Keep the golden v1 fixture passing unchanged.
  Every new section also needs its row in `WorldSaveStore.Prepare`'s `Sections` table (name, type, rejection label): without it a
  mistyped value fails the whole `WorldSave` parse and the world rolls back to `.bak` (found twice: `tugCasts`, `wishes`). Add a
  `WorldSaveStoreTests` round trip and a "bad value keeps the rest" case.
- Restore on the host in `OnNetworkSpawn`, before subscribing and refreshing, and without firing toasts.
- **Capture:** anything that changes continuously (a pose, a turning crank) must be written in `WorldSession.CaptureForSave`, not only at
  events. The last capture runs on `ConnState.ShuttingDown`/`Failed` (before NGO stops); by `Ended` the server and clocks are gone.
- PlayMode save tests use `WorldSaveStore.PathOverrideForTests`; never touch the real `world.json`.

## NGO 2.x and Unity details worth remembering
(Vetted from Nice-Wolf-Studio/unity-claude-skills, MIT, 2026-10-25; its own examples are owner-authoritative, so they don't apply here.)
- RPCs: `[Rpc(SendTo.Server)]` / `SendTo.Everyone` / `SendTo.NotServer` / `SendTo.SpecifiedInParams` with `RpcTarget`; `Delivery.Unreliable`
  only for cosmetic, superseded data; `DeferLocal` when the host must apply its own copy next frame like everyone else.
- Never start a scene event inside a scene-event callback. In-scene objects: use `OnNetworkPostSpawn` / `OnInSceneObjectsSpawned` when a spawn
  needs its siblings to exist.
- Relay + Lobby: `CreateAllocationAsync(maxPlayers - 1)` (the host isn't counted); the join code in Member-visibility lobby data; await the
  lobby heartbeat; `RemovePlayerAsync` on leave.
- Unity's fake null: `?.` and `??` bypass Unity's `==` on destroyed objects; compare with `== null` or `if (obj)`.
- Statics survive Enter Play Mode with domain reload off: reset them in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (as
  `WorldSaveStore.PathOverrideForTests` does). Pair every `OnEnable` subscription with an `OnDisable` unsubscribe; use
  `destroyCancellationToken` for async work owned by a MonoBehaviour.
- `async void` only for Unity event entry points, with try/catch inside (`SceneFlow.LoadAfterRiderRelease`); a newer request cancels the
  previous one through its own `CancellationTokenSource`. Never touch Unity APIs (Transform, NetworkObject) off the main thread.
- Physics queries (raycasts, overlaps, casts from inside a collider, trigger hits): load the `unity-physics-queries` skill.

## Tests that prove what they claim
- A host-only PlayMode test cannot catch owner-side NGO settings (`AllowOwnerToParent`, `SyncOwnerTransformWhenParented`), client RPC filters or
  late join. Say so in its `Catches:` line and put the case on the PC's manual 2-player walk in handoff 8.
- Calling `Server*` methods directly skips the player's path. For input, drive `FakeInputService.Frame` (it routes through the real router).
- A save test must exercise the path it names: if another call also writes the value (an anchor drop writes the pose), it proves nothing.
  Delete the code under test in your head: would the test fail?
- A layout test that compares a value to the formula that made it can't fail; raycast the built object instead.

## Before pushing
`python3 Tools/cloud/sc_cloud_compile.py --project <worktree>` (13 assemblies PASS); Core tests under .NET + NUnitLite where possible; list the
EditMode/PlayMode tests and the manual walk for handoff 8. Networking or save changes get the `risk-reviewer` in pairs of slices.
