# Save and netcode risk audit (Fable Run 2, 2026-11-12)

Audited tree: `main` at c71c5dba (PL-1, PL-2 and PL-3 merged). Checked against `.claude/skills/sc-netcode/SKILL.md` and `REVIEW.md`. Findings only; fixes go to later Sonnet sessions through `docs/superpowers/handoff/QUEUE.md`.

Method: eight Haiku inventory scans (four network inventories, the save sections, a per-moment dive walk, save tests, network tests), then a direct read of the save spine (`WorldSave`, `WorldSaveRules`, `WorldSaveStore`, `JsonSaveFile`, `AtomicFile`, `WorldLibrary`, `HostWorld`, `WorldSaveFlow`, `WorldPlayers`, `TownSaves`, `BagsSave`, `SuspendSave`, `TownStock`) and of the seams the scans pointed at (`ConnectionApprover`, `HostRoster`, `UgsNetworkSession.NgoEvents`, `SceneFlow`, `ArenaPlayerSpawner`, the dive end, placement and teleport code in `DiveDirector`, `CombatHost` growth). Paths below are relative to `Assets/_Project/`.

Not reported, by rule: anything about cheating, trusted client input or modded clients.

## 1. Summary

| Severity | Count |
|---|---|
| blocker | 2 (A1: confirmed in code, needs one live Relay check; A2: a decision for the user) |
| should-fix | 9 (A3 to A11; A3 and A5 are decisions for the user) |
| note | 12 (N1 to N12) |

The save format itself held up: every section has a `Clean`, a store row and round-trip tests, captures copy instead of aliasing, restore runs before a source goes live, and a newer file is never written. No path was found where a normal save, quit and reload loses or duplicates town pieces, bags or weapon growth. The real risks sit at the edges of the format: who a world belongs to (A2, A3), what happens when the file cannot be used (A5, A6, A7, A11), and the one rejoin path that tests cannot reach (A1, A9).

Risk review: section 6.

## 2. Coverage

### 2.1 World-save sections

| Section | Capture | Clean | Store rows | Restore | Round-trip tests | Findings |
|---|---|---|---|---|---|---|
| top level (`schemaVersion`, `savedAtUtc`, `worldSeed`, `worldId`, `createdAtUtc`, `Extra`) | `WorldSaveFlow.Write:230`, `WorldLibrary.Create:66` | `WorldSaveRules.Clean:34` | `WorldSaveStore.Sections:34-37` | `WorldSaveFlow.Open:128` | `WorldSaveStoreTests` golden v1, id, seed | N1 |
| `players` | `WorldPlayers.ToSave:207` on `HostWorld.Changed` | `WorldPlayers.TryRestore:227` | sections, array and member rows | `HostWorld.Load:227` before the host's own approval | store, rules, `SaveFlowTests`, `DisconnectRejoinTests` | A2, A3 |
| `weapons` | `HostWorld.CaptureWeaponsInto:89` (copy) | `WeaponsSaveRules.Clean` | section and array rows, no member rows | `HostWorld.LoadWeapons:54`, per player in `CombatHost.Growth:152` | store, `CombatHostWeaponsSaveTests`, rejoin | N2 |
| `stock`, `town`, `residents` | `TownSaves.Capture:479` through `DiveDirector.CaptureTown:161` | `TownSaveRules.CleanStock/CleanTown/CleanResidents`, then `Reconcile:383` | section and array rows, no member rows | `HostWorld.RegisterTownSource:144` then `TownStock.RestoreWorld:122` | `TownSaveTests`, `TownStockWorldTests` | N2 |
| `dive` | `TownStock.CaptureWorld:73` | `CleanDive:267` | section, array and member rows | `TownStock.RestoreWorld:136`; a mid-dive file loads as a failed dive (`Reconcile:389`) | store, `DiveSuspendTests` | A8, N5 |
| `bags` | `BagsSaveRules.Capture:151`, recorded players only (`HostWorld.RecordedBagsOnly:182`) | `BagsSaveRules.Clean:78` | section and array rows | `BagsSaveRules.Restore:196` | store, `BagsSaveTests`, `PerPlayerLootNetworkTests` | none |
| `suspend` | `SuspendSaveRules.Build:236`, `SuspendKnown` always set | `SuspendSaveRules.Clean:109`; a stale one is settled as a failed dive (`TownSaves.cs:320`) | section, array and member rows | `BagsSaveRules.RestoreSuspended:211`, `DiveDirector.ServerResumeSuspended:323` | store, `DiveSuspendTests`, `SuspendSaveNetworkTests` | N3 |
| file rules | `JsonSaveFile.Save:181`, `AtomicFile.WriteAllText:17` | n/a | n/a | `JsonSaveFile.LoadUnguarded:98` (main, `.bak`, defaults; newer = read-only) | `JsonSaveFileTests`, `AtomicFileTests` | A5, A6, A7 |
| world library | `WorldLibrary.Create:66`, `MigrateLegacy:103` | n/a | n/a | `WorldLibrary.List:48`, `Open:83` | `WorldLibraryTests` | A6, N4 |

### 2.2 Networked systems

| System | Authority | Late join | Disconnect | Findings |
|---|---|---|---|---|
| Session layer (`UgsNetworkSession`, `LocalNetworkSession`, `CompositeNetworkSession`) | host starts, SDK owns NGO | n/a | host removes the Lobby seat on `ClientDisconnected` | A1, A10, N10 |
| `ConnectionApprover`, `JoinRules`, `HostRoster` | host | slot reclaimed by key | slot kept, marked disconnected | A1, A2, A9 |
| `HostWorld` (start gate, Admit, RecordEntered) | host, not replicated | n/a | pending place freed | A2, A3, N6 |
| `SceneFlow`, `ArenaPlayerSpawner` | host | `OnSynchronizeComplete` spawn | player object destroyed by NGO | A4 |
| `DiveDirector` | host; owner applies its own teleport | NetworkVariables for state, placed within 0.2 s, loaves replayed from the ledger | `_placed` pruned | A8, N7, N11 |
| `CellarFloorSync` | host | `Refresh` in `OnNetworkSpawn` | none needed | none |
| `DiveHaulSync` | host | summary snapshot; bank visit added at the next poll | presence resize in the bank room | N11 |
| `CellarContentsSync` | host | `State` snapshot tagged (seed, floor) | holds time out | N9 |
| `PlayerBagSync` | host writes, owner reads | bag found by roster key | bag stays under its key | none |
| `TownSync`, `PlacementMode` | host; one builder lock | `EnsureView` on spawn | lock released, world saved | none |
| `CombatHost`, `Enemy`, `HeroCombat`, `HeroVitals`, `HealingDrop`, `HeroNetState` | host; owner moves itself and reports hits | growth pushed on the owner's `RequestSyncServerRpc` | growth kept by key; holds expire | N8, N9 |

## 3. Blockers

### A1. Online crash-rejoin: taking over a stale connection frees the rejoining player's own Lobby seat [net]
- **Where:** `Networking/ConnectionApprover.cs:182-202` (`TakeOverStaleConnection`), `Networking/UgsNetworkSession.NgoEvents.cs:48-62`.
- **Failure path:** an online session of host H and friend X (UGS player id P, NGO client 1). X's game crashes mid-dive. Before H's transport times client 1 out, X relaunches and joins by code as client 2 with the same key. The approver sees key P still connected, calls `roster.Disconnect(1)` and `nm.DisconnectClient(1, Kicked)`. NGO raises `ClientDisconnected(1)`; `UgsNetworkSession.OnConnectionEvent` looks up client 1's cached player id, which is P, and calls `host.RemovePlayerAsync(P)`. P is the player who is joining right now, so the Sessions SDK removes X from the session: the rejoin fails or X is dropped seconds after arriving. A second attempt works, because no stale connection is left.
- **Confidence:** medium. The code path is certain; what `RemovePlayerAsync` does to a player who has just rejoined, and whether the Lobby lets P join a second time before the seat is freed, can only be seen on real Relay. The takeover call itself never runs in tests (A9).
- **Fix direction:** when a disconnect is a takeover (the same key has a newer pending or connected client), do not remove the seat.
- **Proposed test:** `ConnectionApproverTests.Takeover_OldClientDisconnect_DoesNotFreeSeatOfSameKey` (needs a seam: the seat removal decided by a pure rule taking the roster and pending list). Written repro for the live check goes in `USER_TODO.md` (2-player walk: kill the client's process mid-dive, relaunch and rejoin within 10 s).

### A2. A lobby visit takes one of a multiplayer world's four places for good [save] (decision for the user)
- **Where:** `Networking/ConnectionApprover.cs:75-84` (`ClientConnected` calls `RecordEntered`), `Runtime/Net/HostWorld.cs:289-299`, `Core/World/WorldPlayers.cs:109-117`, saved at once by `App/WorldSaveFlow.cs:178-184`. `WorldPlayers` has no way to remove a record.
- **Failure path:** world W has two recorded players (host and A). The host opens W's lobby and shares the code. C joins, looks around and leaves before Start; D does the same. Both were written into W the moment their connection came up, and the file was saved. W now has four recorded players. B, the friend the fourth place was meant for, is refused as `WorldFull` in every later session. Nothing in the game can undo it.
- **The first lobby is affected too (risk review):** `WorldLibrary.Create:76` writes a players section with the kind and an empty list, so `WorldSaveFlow.Open:141` gives `HostWorld` a world with zero records before anyone connects. Every visitor to a brand-new world's first lobby is recorded on the spot, and the start gate takes the `CanStart` branch, not `TryCreate`.
- **Tests that pin today's behaviour (change them with the fix):** `ConnectionApproverTests.ANewPlayer_IsRecordedOnlyWhenTheirClientConnects`, `HostWorldTests.Admit_WritesNothing_RecordEnteredPersistsOnce`, and the `Recorded.Count == 2` assertion near `FrontDoorFlowTests.cs:400`.
- **Why it is a decision:** CLAUDE.md says a world "records up to 4 unique players"; it does not say whether joining the lobby counts as playing.
- **Options:** (a) record a joiner when the session starts (the gate already lists who is connected) and, for a join into a running session, when their hero spawns; the gate must record the new players before `CanStart` checks them, because it refuses anyone present who is not recorded (`WorldPlayers.cs:178`); (b) keep it and add a host-side "remove player" action in the world picker; (c) both.
- **Proposed test:** `HostWorldTests.JoinerWhoLeavesBeforeStart_IsNotRecorded`.

### A3. A multiplayer player whose UGS id changes loses their character, and a host can lose the world [save] (should-fix after the risk review; decision for the user)
- **Where:** `Networking/UgsNetworkSession.cs:271-281` (the key is `AuthenticationService.Instance.PlayerId` from an anonymous sign-in), `Runtime/Net/HostWorld.cs:255-270`, `Core/World/WorldPlayers.cs:164-183`.
- **Failure path (friend):** A plays W for weeks, then moves to a new PC or reinstalls Windows. The anonymous sign-in gives a new player id. If W has four records, A is refused as `WorldFull` for good. If it has fewer, A is recorded as a new player with fresh heirlooms, no gold and no stash; the old record stays and still uses a place. A's weapon growth and bags are still in the file under the old key, unreachable.
- **Failure path (host):** the host's id changes and W is full. The host is admitted (`HostWorld.Admit` never refuses the host), but `TryStartSession` calls `CanStart`, which returns `NotRecorded` (`WorldPlayers.cs:178`). The host can never start W again from that machine. If W is not full, the host is silently recorded as a new player when their own client connects: one more place used, and the host's own growth and bags stay under the old key.
- **Why should-fix and not blocker:** the id changes only when the sign-in token kept in PlayerPrefs is lost outside the game (a new PC, a Windows reinstall), not through play.
- **What already exists:** a Solo world rebinds its owner to the new key and renames weapons, bags and suspend records (`HostWorld.cs:258-268`). Multiplayer has no equivalent.
- **Options:** (a) a host-side "this player is now that player" action that renames one record (the Solo rename code already does the work); (b) sign in with a platform account instead of an anonymous one; (c) accept it and document it.
- **Proposed test:** `HostWorldTests.Multiplayer_RebindRecordedPlayer_MovesGrowthBagsAndSuspend` (after a decision); until then the repro above is the record.

## 4. Should-fix

A3 above belongs here (demoted by the risk review); it stays in section 3 so the identity findings read together.

### A4. A client can be left without a hero, and nothing retries [net]
- **Where:** `App/ArenaPlayerSpawner.cs:47-56` (spawns only `clientsCompleted`; `clientsTimedOut` is ignored), `:68-71` (`OnSynchronizeComplete` spawns only when the host's active scene is already a gameplay scene).
- **Failure path 1:** three players start a world. C's PC takes longer than NGO's scene-load timeout to load Hollowmere. `OnLoadEventCompleted` fires with C in `clientsTimedOut`. C finishes loading later and stands in the hub with no hero for the rest of the session; no event ever spawns one. Leaving and rejoining fixes it.
- **Failure path 2 (unverified, NGO ordering):** D joins by code while the host is still loading the first gameplay scene. D is not in `clientsCompleted`; if D's `OnSynchronizeComplete` arrives while the host's active scene is still the lobby, D gets no hero either.
- **Also:** every player waits for the slowest client, because heroes spawn only when the load event completes for all or times out.
- **Fix direction:** a host tick in gameplay scenes that calls `SpawnFor` for any connected client without a player object (it is already idempotent).
- **Proposed test:** `HostClientTests.ConnectedClientWithoutPlayerObject_GetsHeroFromSpawnerTick`. NOT covered by the harness: real scene sync; add to the 2-player walk.

### A5. A read-only world still starts, and the whole session is played unsaved [save] (decision for the user: refuse or confirm)
- **Where:** `App/WorldSaveFlow.cs:146-147` (a 6 s toast is the only signal), `Runtime/Save/JsonSaveFile.cs:86-94` (load fault: defaults, read-only), `:141`, `:183-187` (`Save` returns true and writes nothing).
- **Failure path:** the world file was written by a newer build (the user tried a newer build, then went back), or `LoadUnguarded` threw. The host starts the session. With the newer-file case the saved town loads and play continues, but nothing is written; with the load-fault case `store.Current` is the default, `HostWorld.Players` is null, and the start gate creates a brand-new world in memory. Hours of play end with nothing on disk.
- **Proposed test:** `SaveFlowTests.ReadOnlyWorld_StartIsRefusedUntilConfirmed`.

### A6. A damaged world disappears from the picker and the host flow opens a different world [save]
- **Where:** `Runtime/Save/WorldLibrary.cs:147-155` (`Read` returns null when neither file loads, or when `Summarize` finds no usable players section), `App/WorldSaveFlow.cs:151-160` (`Resolve` falls back to the most recently played other world, or creates one).
- **Failure path:** W's `players.kind` is damaged (or both `world.json` and `.bak` are unreadable). W no longer lists. The player presses Host; `Resolve` does not find the selected id and silently opens their second world, or makes a new empty one. The files of W are still on disk, but nothing says so, and the player may place buildings in the wrong world before noticing.
- **Fix direction:** list such a file as "damaged" (not selectable), and never fall back to another world when a selected id was given.
- **Proposed tests:** `WorldLibraryTests.UnreadableWorld_IsListedAsDamaged`, `SaveFlowTests.SelectedWorldMissing_DoesNotOpenAnotherWorld`.

### A7. A save-layer exception while opening the world leaves the session running with no save and stale world state [save]
- **Where:** `App/WorldSaveFlow.cs:119-148` (`Open` is not guarded; it runs inside the session's `StateChanged`, which `EventIsolation` wraps, so the exception is logged and swallowed), `Runtime/Save/WorldLibrary.cs:51-52` (`Directory.GetFiles`), `Runtime/Net/HostWorld.cs:235-240` (`Reset` keeps `Players`, the weapons and the town of the previous session).
- **Failure path:** the worlds folder cannot be listed at the moment the host connects (a cloud-synced folder, an antivirus lock, a permission fault). `Open` throws before `store` is set. The session carries on. `HostWorld` still holds whatever the previous hosted session in this process loaded (or nothing), the start gate runs against it, and no toast appears. Nothing is saved. `REVIEW.md` asks that the save service never throw into gameplay; here it does not crash, but it fails silently, which is worse for the player.
- **Fix direction:** guard `Open`; on a fault clear `HostWorld` (`Load(null)`, `LoadWeapons(null)`, `LoadTown(null)`), publish `save.world_load_failed`, and treat the session as A5's read-only case.
- **Proposed test:** `SaveFlowTests.OpenFault_ClearsWorldAndToasts`.

### A8. The end of a dive is not saved until the next 10 s flush [save]
- **Where:** `Presentation/Cellars/DiveDirector.cs:469-488` (`EndDive` asks for no save; `OnPushedOn:496` does), `App/WorldSaveFlow.cs:25`.
- **Failure path:** the party finishes a dive on a floor that is not the bank (`ServerTryFinish`). The last file on disk, at most 10 s old, was captured mid-dive, so its bags are the failed-dive copies and `dive.inDive` is true. The host's PC loses power 5 s after the result card appears. On reload the dive counts as failed: carried items wait in satchels on that floor and half the dive gold is gone.
- **Fix direction:** `HostWorld.RequestSave()` at the end of `EndDive`.
- **Proposed test:** `DiveResultNetworkTests.EndDive_RequestsWorldSave`.

### A9. The stale-connection takeover is never executed by a test [net]
- **Where:** `Networking/ConnectionApprover.cs:200` (`if (nm.IsServer) nm.DisconnectClient(...)`); `ConnectionApproverTests` uses a NetworkManager that is not running, and `DisconnectRejoinTests` leaves gracefully (`DisconnectRejoinTests.cs:10` says so).
- **Why it matters:** a crash followed by a quick relaunch is the most common way a friend rejoins. It calls `DisconnectClient` for one client from inside another client's approval callback, mid-dive, and nothing proves the bag, slot and hero come back.
- **Proposed test:** `DisconnectRejoinTests.SameKeyJoinsWhileOldConnectionIsUp_TakesOver_KeepsBagSlotAndHero` (a third NetworkManager with the first client's key).

### A10. Host quit may not tell the clients [net] (unverified)
- **Where:** `App/AppBootstrap.cs:205-224` (`SaveNow`, then `LeaveAsync` fire-and-forget; the local teardown awaits a yield and the UGS one awaits the SDK).
- **Failure path:** the host closes the window mid-session. The save is written (good), but the process can exit before NGO sends its disconnect. Clients freeze on the last state until the transport times out, then see "host left".
- **Check:** 2-player walk step: host presses Alt+F4, time how long the client takes to return to the title.

### A11. The world is attached before its weapons and town are loaded [save] (from the risk review, low confidence)
- **Where:** `App/WorldSaveFlow.cs:132` sets `store`; `:142-143` call `LoadWeapons` and `LoadTown`.
- **Failure path:** if either load throws (swallowed as in A7), `HostWorld` keeps the previous session's weapons or town while `store` already points at the newly selected world. The next `CaptureTownInto` writes the other world's sections into this file (`HostWorld.cs:217-223`). No concrete throw was found in `CleanSnapshot` or `MigrateLegacyLoot`, so this is a guard to add with A7, not a known loss.
- **Fix direction:** set `store` only after the loads succeeded.
- **Proposed test:** `SaveFlowTests.LoadFault_AfterAnotherWorld_NeverWritesTheOldWorldIntoTheNewFile`.

## 5. Notes

- **N1. Migration contract.** `WorldSaveRules.Clean:38` keeps `SchemaVersion` as read and `WorldSaveStore.Upgrade:313-319` has only a commented loop. The first real step must set `schemaVersion` in the JSON it returns, or the file is saved as v1 again and migrated on every load. `Upgrade`'s `JObject.Parse` is safe only because `JsonSaveFile.DeserializeOrNull:229-233` wraps the hook.
- **N2. Member rows cover `players`, `dive` and `suspend` only** (`WorldSaveStore.cs:77-101`). A scalar where a list belongs costs the whole section elsewhere: `"heroes": 5` drops the entire `weapons` section (every player's growth; the file is kept as `.corrupt`). Add member rows for `weapons.weapons`, `weapons.heroes`, `bags.players`, `stock.pieces`, `stock.residents`, `town.buildings`, `residents.residents`. Test: `WorldSaveStoreTests.Weapons_ScalarWhereHeroesListBelongs_KeepsWeapons`.
- **N3. `suspend.presentIds` is written and cleaned but never read** (`SuspendSave.cs:95`, `:126-134`; `Resume:284` takes the live lists).
- **N4. `WorldLibrary.MigrateLegacy:121-131` copies only the seed, players and `Extra`.** Correct for real legacy files (the library landed 2026-10-29, the first section beyond players on 2026-11-01), but a newer-format file placed at the legacy path would lose its sections (the old file is kept as `.migrated`).
- **N5. `HostWorld.RegisterTownSource:148` restores with `failMidDive: false`.** A second `DiveDirector` registering during a dive in the same session would restore `inDive` with failed-copy bags. Not reachable today (hub and dive share one scene); a future scene change during a dive must end the dive first.
- **N6. `HostWorld.Reset:235` keeps `Players`.** It relies on `WorldSaveFlow.Open` always running after it. With `WorldSaveFlow.Enabled` off (tests) a second hosted session in one process reuses the first session's world.
- **N7. A client without a hero passes the host's range and "is up" checks** (`DiveDirector.cs:597-601`, `AskerIsUp`). Harmless alone (no hero, no prompt), but worth closing with A4.
- **N8. Late joiner cosmetics:** an enemy's attack in progress has no telegraph for the joiner (`Enemy.cs:196-214`), and an arrow in flight is not shown (`HeroCombat.cs:193-206`).
- **N9. Small host-side leftovers:** `CombatHost._states` is never pruned per client id; `_enemyHitDedupe` is cleared whole at 512 entries (`CombatHost.cs:387`); `CellarContentsSync.PumpSecondsOverrideForTests:114` has no static reset. Scanner report, unverified: `ValveHoldRpc` shifts by the valve byte before its range check (`CellarContentsSync.cs:1131`).
- **N10. `DisconnectClassifier.cs:19-21`** reports "host left" to a connected client whose own link dropped.
- **N11. Defaults a client can read before the host's value arrives:** `NetHubStair` is zero until `DiveHub` calls `ServerConfigure` (`DiveDirector.cs:841`); `NetHealSecondsLeft` 0 shows the orb as ready (`DiveHaulSync.cs:763`). Prompt-only; the host refuses.
- **N12. Comments that no longer match the code:** `HostWorld.cs:242` ("Nothing is recorded here", but `Admit` rebinds a Solo owner at `:258-268`); `DiveDirector.cs:158` (says residents are passed as null; `:166` passes `_residents`).

### Test gaps worth a queue item (from the two coverage scans)
- Save: no test for a failed write (locked or full disk) staying dirty and succeeding on the next flush; none for a corrupt `.bak` beside a good main file; one inline golden file only (v1, players only): add a golden with every section as written today, so a later change that cannot read today's files fails a test.
- Network: no client-driven test for `CastServerRpc`, `ParryServerRpc`, `ReviveHoldServerRpc`, `RequestFinishRpc`, `SetGhostRpc`; no late-join assertion for enemies, opened chests, room state or the healing point; no disconnect test while downed, mid-revive or during a floor change; no host-drop test seen from a client mid-dive.

## 6. Risk review

One Opus `risk-reviewer` pass traced A1 to A3 in the code and checked A4 to A8 for reachability.

| Finding | Verdict | Severity after review |
|---|---|---|
| A1 | Confirmed. NGO queues a disconnect that carries a reason, so `ClientDisconnected(1)` fires a tick after client 2 is approved; `_ugsIdByClient[1]` still holds P and `RemovePlayerAsync(P)` runs (`UgsNetworkSession.NgoEvents.cs:50-61`). The cache is the cause, not a guard: without it the lookup would miss. Open on real Relay only: whether the Lobby lets P join again while the old membership is still there (if not, the rejoin fails one step earlier), and how fast the kick arrives. | blocker, medium-high confidence |
| A2 | Confirmed and widened: the first lobby of a new world records visitors too. Three tests pin the current behaviour. | blocker (decision for the user), high confidence |
| A3 | Confirmed, with one more case (a host on a world that is not full is recorded as a new player). Demoted: the key changes only outside play. | should-fix (decision for the user) |
| A4 | Reachable. Path 1 needs a load longer than NGO's scene-load timeout (project setting not checked; default 120 s). | should-fix |
| A5 | Reachable; a toast is the only signal. | should-fix (decision for the user) |
| A6 | Reachable, milder than written: `FrontDoorModel.HostSelected:272-285` creates a world when the list is empty, else takes the first; the files are untouched. | should-fix |
| A7 | Reachable but rare (needs `Directory.GetFiles` to fail). | should-fix |
| A8 | Reachable. Going home from the bank is covered too, but there the file still holds the bank-room suspend, which resumes rather than loses. | should-fix |
| A11 | Added by the reviewer. | should-fix, low confidence |

Proof for the blockers: A2 has a failing test marked `[Ignore("AUDIT A2: ...")]`. A1 has an ignored test if the session can be driven with a fake host session; otherwise the written repro in `docs/superpowers/handoff/USER_TODO.md` (2-player walk) is its record.

## 7. Rules this audit adds

To `sc-netcode` and `REVIEW.md` (done in the same commit as the review result):
- A disconnect handler that frees a per-player resource by player id checks first that the same player has no newer connection (A1).
- Anything written into the world for good (a recorded player) is written when the player plays, not when they connect, and has an undo (A2).
- A session whose world cannot be saved does not start silently (A5, A7); a failed open clears `HostWorld`.
- Every moment a player would call "done" (dive end, build finished, player recorded) asks for a save; the 10 s flush is the fallback (A8).
- A new section with lists needs member rows as well as array rows (N2); a migration step sets `schemaVersion` (N1).
- A host call that only runs on a live NetworkManager needs a PlayMode test; an EditMode test with a stopped manager skips it (A9).
