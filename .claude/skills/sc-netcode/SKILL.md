---
name: sc-netcode
description: SleepingCalamity's checklist for networked and saved gameplay (NGO 2.x host authority, host-checked RPCs, late-join snapshots, owner-only data, owner teleports across floor rebuilds, input context holds, world-save sections, UGS Sessions) - the patterns this project uses and the bugs its risk reviews keep finding. Use before writing or reviewing any NetworkBehaviour, RPC, NetworkVariable, scene or floor switch, or a new world-save section, and before claiming a test covers a network path.
---
# Networked and saved gameplay in SleepingCalamity

Each rule below names the class that does it right and, where there is one, the risk-review fix that taught it. Read the class-level `// Authority:` comment of the class you touch first: `CombatHost`, `Enemy`, `HeroCombat`, `HeroVitals` (Gameplay/Combat), `CellarFloorSync`, `CellarContentsSync`, `DiveDirector`, `DiveHaulSync`, `PlayerBagSync` (Presentation/Cellars), `TownSync` (Presentation/Town), `ArenaPlayerSpawner`, `WorldSaveFlow` (App), `HostWorld` (Runtime/Net).

## Authority (CLAUDE.md: host-authoritative; no anti-cheat)
- Every networked class states in one class-level comment who writes what (host vs owner), because reviewers judge the code against it. Players move themselves (owner-authoritative NetworkTransform, `HeroNetState`); everything the game decides is the host's.
- Only the host writes `NetworkVariable`s. Clients ask with `[Rpc(SendTo.Server)]` and the host re-checks every rule from its own state: `CellarContentsSync.RequestOpenChestRpc` re-checks index, not-yet-open, key held, XZ range from the host's copy of the player and line of sight (`WallBetween`, cbd4a601). A client-side gate is only a prompt.
- Take the sender from `RpcParams.Receive.SenderClientId`, never from an argument (`TownSync`). An object that belongs to one player refuses any sender that is not its owner (`PlayerBagSync.RequestPick`), and a client the roster has no key for gets nothing (`DiveHaulSync.ServerBagOf`): its RPC may still be in flight after it left.
- Make requests idempotent on the host so two presses in one tick apply once: `DiveDirector.RequestStairRpc(fromFloor)` advances only while the current floor equals `fromFloor`.
- A client gate that runs before the host's state has arrived fails closed (`CellarContentsSync.IsObjectiveGated`, 2cea7268), or a fast press slips through on the default value.
- Host-side tables are scoped to what they guard: one `AttackTokens` table per `CombatHost`, not one per room, because targeting is world-wide and per-room tables let a hero exceed the melee cap (88f6afb3). Pay-once rewards keep a mask per floor visit (2cea7268).
- Client prompt radii sit at least 0.5 m inside the host's accept radius (`DiveDirector.BankPromptRadius` vs `DiveHaulSync.BankRadius`), so a shown prompt is never refused for range.

## Late join and one-shots
- Lasting state goes in one host-written snapshot `NetworkVariable`, so a late joiner gets it with the spawn and nothing is replayed: `CellarFloorSync` (seed and floor in one value, so they land together), `TownSync.NetTown`, `CellarContentsSync.State` (tagged with its (seed, floor), so a peer ignores another floor's state), `DiveHaulSync`'s summary.
- One-shot feedback rides an RPC to the asker (`TownSync.ResultRpc`, `DiveDirector.NoticeRpc`) and must not fire again on late join or on a save restore: `TownSync.ServerSeedBakerMet` publishes a restored latch without the first-meet line.
- Per-player private data goes in a `NetworkVariable` with `NetworkVariableReadPermission.Owner` on the player's own object (`PlayerBagSync.Net`), never in a value every peer reads; per-player loot from ee9435ef. Key it by the roster `PlayerKey` (the client id only with no roster), so a rejoining player gets the same bag back.
- Seed an enemy's `NetworkVariable`s before `Spawn`, so the spawn carries them (cbd4a601).

## Ordering: an RPC can outrun a NetworkVariable
- A `NetworkVariable` change and an RPC sent in the same frame can arrive in either order, so never assume "the variable is already set on clients". The host never writes a player transform: `DiveDirector.TeleportRpc` goes to the owner (`SendTo.SpecifiedInParams`) with the (seed, floor) it needs, and the owner parks it until its own `CellarFloorSync` has built that floor, then calls `NetworkTransform.Teleport` (60f04865).
- After a floor change, positions are stale until the teleport lands: `CellarContentsSync` ignores a player for room entry and pickups until they move past `StaleMoveDistance` (434cfc03), and `DiveDirector` hides stair/bank prompts until the owner's last applied teleport matches its built floor.
- An owner-asked value later RPCs depend on is written by the host first: `HeroCombat.NetWeapon` is set through `SetWeaponServerRpc`, so it is in place before that owner's next hit intent is handled, and every hit intent also carries the weapon index.

## Locks, context holds and held input
- A host-granted lock is released on disconnect and on any flow change that leaves its mode: `TownSync`'s builder lock clears in `OnClientDisconnectCallback`, and a dive start force-releases it (751ee741).
- A local input-context hold must let go when the host would refuse the actions anyway, or it only freezes the hero: the bank screen holds `InputContext.BankScreen` only while the hero is in bank range and no teammate is downed nearby, so nobody is stuck while a friend needs reviving (8d8f7d97, `DiveDirector.LocalBankScreen`).
- A context that needs a fresh press arms on a zero first. The bank screen restarts its arm delay after a pause menu or a revive, so the press that closed the menu cannot push the party on (8d8f7d97). Menus raise `IInputService.MenuOpen` and use edge-triggered keys (`WeaponTreeHud`, 107d4187).
- Every new context needs its row in `InputContextRouter` and a router test (`InputContextRouterTests`), or its keys are zeroed or leak into gameplay.
- Holds report on a timer (revive every 0.2 s via `HeroCombat` to `HeroVitals`; valves every `FloorObjectiveTuning.ReportInterval`) and the host drops a hold whose reports stop (`ValveHold` resets after `ReportTimeout`). A downed hero's presses are refused on the host too.
- Push a snapshot only when it changed: `CombatHost.ServerSyncGrowth` pushes the weapon tree when `WeaponTreeSnapshot.SyncKey` changes (107d4187).

## World save (`WorldSave`, `WorldSaveRules`, `WorldSaveStore`, `HostWorld`, `WorldSaveFlow`)
- New state is an optional section with its own `Clean` (`WorldSaveRules.Clean`, `TownSaveRules`, `BagsSaveRules`, `SuspendSaveRules`, `WeaponsSaveRules`); a bad value drops only that section, record or member, never the file. Add its row to `WorldSaveStore.Sections` (and `ArraySections` / `MemberSections` for records and members), or one misfit value fails the whole parse and the world rolls back to `.bak`. Keep `WorldSaveStoreTests.GoldenV1_LoadsExactly` passing unchanged, and add a round trip plus a "bad value keeps the rest" case.
- Capture a copy, never the live object or the file's own object: an aliased section compares equal to itself, so the dirty check never fires and nothing is written (cdc0abb6; `HostWorld.CaptureWeaponsInto` cleans into a copy).
- Restore before the source goes live: `HostWorld.RegisterTownSource` calls `RestoreTown` first, so a save flush can never capture the still-empty state over the loaded town (cdc0abb6, a6a6ed90).
- A null capture means "unknown, keep the previous copy", and a latch never rolls back (`TownSaveRules.MergeLatches`, 39ef1ba4). A discarded suspend settles its dive as a failed one (`SuspendSaveRules.SettleAsFailed`, bcc15762).
- State that changes all the time (weapon growth, the town) is compared at every save in `WorldSaveFlow.CaptureSections`, not marked dirty per change. The last capture runs when the state turns `ConnState.ShuttingDown` or `ConnState.Failed`, before NGO stops (by then a later save may find the server gone), then again on the session's `Ended` event and on quit.
- Restore on the host without firing toasts. A client never opens a world file.
- PlayMode save tests set `WorldSaveStore.PathOverrideForTests`; never touch the real world file.

## NGO, UGS and Unity details
- Never start a scene event inside a scene-event callback. Spawn players on `OnLoadEventCompleted` and, for late joiners, `OnSynchronizeComplete` (`ArenaPlayerSpawner`).
- UGS uses the Multiplayer Services Sessions SDK (`UgsNetworkSession`): the SDK starts and stops NGO and sets the Relay data itself; `NetworkManager.Singleton` must exist before `CreateSessionAsync`; tear down with `ISession.LeaveAsync`, never `NetworkManager.Shutdown`; full-lobby and bad-code errors arrive as message text (`UgsErrorMap`). Wrap UGS calls in `UgsAsync.WithTimeoutAsync`.
- Unity's fake null: `?.` and `??` bypass Unity's `==` on destroyed objects; compare with `== null` or `if (obj)`.
- Statics survive Enter Play Mode with domain reload off: reset them in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (`WorldSaveStore.ResetStatics`). Pair every subscription with an unsubscribe, and clear host-side state in `OnNetworkDespawn` (434cfc03).
- `async void` only at fire-and-forget entry points, with try/catch inside (`SceneFlow.ObserveLeave`). Never touch Unity APIs off the main thread.

## Tests that prove what they claim
- `PlayModeHarness.TestClient` is a second NetworkManager in the same process with scene management off. It cannot catch owner-side NGO settings, scene sync to a real second machine, late join into the arena or Relay (`HostClientTests`' header). Say so in the test's `Catches:` / `NOT covered:` line and add the case to the manual 2-player walk in `docs/superpowers/handoff/USER_TODO.md`.
- Calling `Server*` methods directly skips the player's path; for input, drive `FakeInputService.Frame` (it routes through the real router).
- A test must exercise the path it names: if another call also writes the value, it proves nothing. Delete the code under test in your head: would the test fail? Wait on a counter (`HeroCombat.TreePushes`), not a fixed delay (107d4187).
- A layout test that compares a value to the formula that made it can't fail; raycast the built object instead.

## Before pushing
Run the `gate` (or `python3 Tools/cloud/sc_cloud_compile.py --project <worktree>` without a licence: every assembly PASS), list the EditMode/PlayMode tests and the manual walk steps. Networking or save changes get the `risk-reviewer` (`sc-delegate`).
