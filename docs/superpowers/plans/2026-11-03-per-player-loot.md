# Per-Player Loot: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dive's one shared host-held loot haul with a bag per player. Each player gets their own chest rolls, gold, bank slots and lost satchel, and only that player sees them. Keys, pieces and people stay one shared copy held by the host. A piece goes into town stock the moment it is picked up.

**Architecture:**
- The host keeps one Core `PlayerBag` per recorded player in a `PartyBags` owned by `DiveState`. Bags are keyed like `CombatHost` growth: the `PlayerKey` string from `IJoinContextProvider.TryGetKey`, or the client id when there is no roster (tests). A rejoin therefore gets the same bag back.
- Each player's view of their bag rides on a new `PlayerBagSync` component on the player prefab, as one `NetworkVariable` with `ReadPermission.Owner` and server write. Only the owner and the host receive it, and a late joiner gets it with the spawn.
- `DiveHaulSync` keeps stir, healing, floor state and the party bank action, and still holds the shared pieces. Its loot path goes away.
- `CellarContentsSync` rolls loot chests per player from a per-player stream. The key chest stays shared.
- A new optional world-save section, `bags`, stores each recorded player's gold, home stash and waiting satchel.

**Tech Stack:** C# 9 (Unity 6000.6.5f1), NGO 2.13 (`NetworkVariableReadPermission.Owner`, `[Rpc(SendTo.Server)]`), Unity.Collections `FixedList`, Newtonsoft JSON (world save), NUnit 3, `Pcg32` + `Rng.Derive` (`Core/Random`).

**Spec:** `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md`: decisions 5, 7, 33-35 and 39; sections 4.2, 4.3, 4.4 and 4.7. Roadmap row 2 of `docs/superpowers/plans/2026-11-03-dive-build-roadmap.md`. It builds on plan 1's Core classes (merged `e27a9608`): `BankSlots`, `DiveItemKind`, `SatchelSlot`/`LostSatchel` and `DiveGold`.

## Global Constraints
- Host-authoritative. Only the host writes `NetworkVariable`s. Clients ask by RPC, and the host re-checks every rule itself. Every networked class has a class-level `// Authority:` comment saying who writes what (`sc-netcode`).
- No anti-cheat: no extra cheat-proofing beyond normal host validation (CLAUDE.md).
- Core stays pure C#, with no `UnityEngine`. Core test files use only `System*`, `NUnit*` and `SleepingCalamity.Core*`, so `python Tools/cloud/sc_core_tests.py` runs them on the PC without Unity.
- World-save rules (`sc-netcode`):
  - A new state is an optional v1 section, cleaned on its own. A bad value drops only that entry, never the file.
  - The section gets its row in the `WorldSaveStore` `Sections` table.
  - The golden v1 fixture passes unchanged.
  - PlayMode save tests use `WorldSaveStore.PathOverrideForTests`.
- Restores never fire toasts. One-shots are RPCs and never replay on late join.
- Never edit `.prefab`/`.unity` YAML. The player prefab changes through `PrefabBuilder.BuildPlayer`, and a `build` run writes it. Commit the builder output.
- Unity runs go only through `C:\Dev\SleepingCalamity\.claude\worktrees\magical-liskov-576bbe\Tools\unity.ps1 <cmd> -OutDir <scratchpad>`. Logs stay outside the repo. Check that `SC MARKER` equals HEAD.
- New `.cs` files need Unity-made `.meta` files. The task's `gate` run creates them; commit them. Never hand-write GUIDs.
- Mark every new number *placeholder* and make it a `public const`.
- Leave `CURRENT.md` alone.
- Work on branch `claude/magical-liskov-576bbe` and commit after each task.

Test commands:
- Core-only tests (PC): `python Tools/cloud/sc_core_tests.py --where "class =~ <TestClass>"`, which exits 0 when all pass.
- Unity gate (compile + EditMode): `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\.claude\worktrees\magical-liskov-576bbe\Tools\unity.ps1 gate -OutDir <scratch>`.
- PlayMode subset: `... unity.ps1 test-play -Filter <regex> -OutDir <scratch>`. It queues on the machine-wide port lock and prints `SC WAIT`.

## Decisions this plan takes (the spec is silent; recorded in PARKING_LOT where plan 3 should revisit)
1. **Enemy drops.** Today no enemy drops loot (only healing orbs and the shared gate key). The bag API takes any source (`ServerAddItem`), but only chests are wired. Per-player enemy drops are parked.
2. **Where loot lands at home:** each player's own **stash** (decision 39), saved in `bags`.
   - The old world-wide `stock.loot` and `dive.bankedLoot` stay readable and are written as before, so old saves keep their shared loot as town storage.
   - No new loot enters them. `DiveHaul`'s loot members stay only for that restore path. Plan 3 deletes them along with `DiveHaul`.
3. **Stir.** Pieces stop feeding stir (spec 4.5): `StirMeter`'s piece term is removed, and stir then comes from floors cleared only. Valuables-driven stir is plan 3 (`DiveStir`).
4. **Banking in the live 4-floor dive:**
   - Entering a floor with a bank point opens a **bank visit** for every present player's bag, with capacity `BankSlots.SlotsFor(players, slot, extraOwner)`.
   - Each player picks carried items into slots by RPC.
   - The party bank action (the existing `ServerBank`) **pushes on**: picked items become safe and the visit closes. Slots still empty at that moment fill with the player's most valuable bankable items (highest rank first). That auto-fill is a *proposal* so a player with no bank UI yet still banks something; plan 3's bank room may drop it.
   - Going home in town brings everything carried home.
5. **3-player extra slot.** The owner is picked when the visit opens, using `BankSlots.PickExtraSlotOwner(diveSeed, presentSlots, previous)`. The previous owner is held in memory on the host for the session only. Saving it is plan 3.
6. **Player slot** (used for the roll stream and the extra slot): the roster slot (`RosterEntry.Slot`). With no roster, it is the order in which the host first saw each client id (host = 0).
7. **Mid-dive save.** `TownSaves.Reconcile` already turns a mid-dive save into a failed dive. A bag captured during a dive is therefore written **as if the dive failed at the current floor**:
   - stash += banked-this-dive items;
   - gold += `DiveGold.KeptOnFail(diveGold)`;
   - carried items become the waiting satchel.
8. **Lost satchels are stored and saved, not placed.** Placing a satchel on the floor at its depth and picking it up is plan 3, which owns floor content. The Core bag already supports pick-up and banking it.
9. **Item kinds** (`CellarLoot.KindOf`, *placeholder*): every table item 101-112 is `Large`. `RoomKeyId` and `GateKeyId` are `Key`. An unknown id is `Usable`. A piece is `Progress` (pieces never enter a bag).

## Review Focus
1. **A player drops and rejoins mid-dive.** The same `PlayerKey` gets the same bag, the same opened-chest mask and the same roll on an unopened chest, because the roll depends on the roster slot, not the client id. Test: Task 5, `Rejoin_GetsTheSameBagBack`.
2. **Another player's bag.** A client reads another player's `PlayerBagSync.Net` as `default`, and so does a late joiner. The host reads every bag. Test: Task 5, `View_OnlyOwnerAndHostSeeIt_LateJoinerGetsOwn`.
3. **Two players and one chest.** When B opens a chest A already opened, B gets B's own roll. A second open by A is refused. The key chest yields one room key for the party, never one each. Test: Task 6, `Chest_EachPlayerOpensOwnCopy_KeyChestStaysShared`.
4. **Mid-dive save and reload.** Picked-up pieces are in stock and never respawn. Unbanked items are in the satchel only: not in the stash, not in stock, not duplicated. Tests: Task 3, `CaptureMidDive_WritesTheFailedCopy` and Task 4, `PiecePickup_GoesToStockAtOnce_SurvivesFail`.
5. **Banking edge cases.** Picking a `Key`/`Progress` item, a full slot set, a picked index twice, or a pick while no visit is open is refused. A second failure replaces the old satchel, and a carried satchel is lost. Tests: Task 1.

---

### Task 1: Core bag model (`PlayerBag`, `PartyBags`)

**Files:**
- Create: `Assets/_Project/Core/Cellars/PlayerBag.cs`, `Assets/_Project/Core/Cellars/PartyBags.cs`
- Modify: `Assets/_Project/Core/Cellars/CellarLoot.cs` (add `KindOf`)
- Test: `Assets/_Project/Tests/EditMode/PlayerBagTests.cs`, `Assets/_Project/Tests/EditMode/PartyBagsTests.cs`

**Interfaces:**
- Consumes (plan 1): `DiveItemKind`, `BankSlots.IsBankable`, `SatchelSlot` (`Waiting`, `Carried`, `Restore`, `OnFail`, `PickUp`, `EmptyOutsideTheDungeon`), `DiveGold.KeptOnFail`.
- Produces:
  - `CellarLoot`: `static DiveItemKind KindOf(int itemId)`, `static int RankOf(int itemId)` (0 for an unknown id or a key).
  - `PlayerBag`:
    - Constant: `const int MaxCarried = 60` (*placeholder*).
    - Constant: `const int SatchelPick = -1` (the pick that means "the carried satchel").
    - Read: `IReadOnlyList<int> Carried` (item ids in pickup order), `IReadOnlyDictionary<int,int> BankedThisDive`, `IReadOnlyDictionary<int,int> Stash`, `int Gold`, `int DiveGold`, `SatchelSlot Satchel`.
    - Read (bank visit): `IReadOnlyList<int> Picks`, `int SlotCapacity`, `bool BankOpen`.
    - Read (chests, transient, never saved): `uint ChestSeed`, `int ChestFloor`, `ulong OpenedChests`.
    - Change: `bool Add(int itemId)`, `bool AddGold(int amount)`.
    - Bank visit: `void OpenBank(int capacity)`, `bool Pick(int carriedIndexOrSatchel)`, `bool Unpick(int carriedIndexOrSatchel)`, `void PushOn()`.
    - Dive end: `void GoHome()`, `void Fail(int floor)`, `PlayerBag CopyAsFailed(int floor)`.
    - Chests: `bool MarkChestOpened(uint seed, int floor, int chestIndex)`, `bool ChestOpened(uint seed, int floor, int chestIndex)`.
    - Save: `static PlayerBag Restore(int gold, IReadOnlyDictionary<int,int> stash, LostSatchel waiting)`.
  - `PartyBags`: `PlayerBag For(string playerId)` (creates on first use), `bool TryGet(string playerId, out PlayerBag bag)`, `IEnumerable<KeyValuePair<string, PlayerBag>> All` (ordinal order by id), `void PushOnAll()` (only bags with `BankOpen`), `void GoHomeAll()`, `void FailAll(int floor)`, `bool AnyCarried`, `void Adopt(string playerId, PlayerBag bag)`.

Rules the implementer must match (spec 4.3, decisions 33-35 and 39):
- `Add` refuses `Key`/`Progress` kinds, a bag with `MaxCarried` items, and an id that is 0 or less.
- `Pick` refuses in these cases:
  - no open visit;
  - a bad index;
  - a non-bankable kind;
  - an index already picked;
  - `Picks.Count == SlotCapacity`;
  - `SatchelPick` while `Satchel.Carried == null`.
- `PushOn`:
  1. Fills free slots with unpicked bankable carried items, highest `RankOf` first and the earlier pickup on a tie (decision 4 auto-fill).
  2. Moves picked items into `BankedThisDive`. A picked satchel goes in as its items, via `EmptyOutsideTheDungeon`.
  3. Clears `Picks` and closes the visit.
- `GoHome` moves into `Stash`: carried items, `BankedThisDive`, and the carried satchel's items. It adds `DiveGold` to `Gold` and zeroes it, then closes the visit.
- `Fail(floor)`, in order:
  1. `BankedThisDive` goes to `Stash`.
  2. `Gold += DiveGold.KeptOnFail(DiveGold)`, then `DiveGold = 0`.
  3. `Satchel.OnFail(floor, Carried)`: a carried satchel is lost, and a new satchel replaces the old one only when items were carried.
  4. `Carried` is cleared and the visit closes.
- `CopyAsFailed(floor)` returns a new bag equal to `Fail(floor)` applied to a copy. The original is unchanged.
- `MarkChestOpened` resets `OpenedChests` when `(seed, floor)` differ from the stored ones. It refuses `chestIndex` outside 0-63 and a bit already set.

- [ ] **Step 1: Write the failing tests.** Every listed name is one `[Test]`, and every bullet is an assertion.

```csharp
// PlayerBagTests (namespace SleepingCalamity.Tests.EditMode; usings System, System.Collections.Generic, NUnit.Framework, SleepingCalamity.Core)
Add_RefusesKeysProgressAndAFullBag
    // Add(101) true; Add(CellarLoot.RoomKeyId) false; Add(CellarLoot.GateKeyId) false; after MaxCarried adds, Add(101) false; Add(0) false
Pick_OnlyWhileTheBankIsOpen_UpToCapacity_OnceEach
    // closed: Pick(0) false. OpenBank(2): Pick(0) true, Pick(0) false, Pick(1) true, Pick(2) false (full), Pick(9) false, Pick(SatchelPick) false (none carried)
PushOn_BanksPicksAndAutoFillsByRank
    // carried 101(rank1),112(rank5),110(rank4); OpenBank(2); Pick(0); PushOn => BankedThisDive {101:1,112:1}, Carried [110], BankOpen false
Fail_HalvesDiveGold_PacksCarriedIntoSatchel_KeepsBanked
    // AddGold(7); carried [104]; banked {101:1}; Fail(3) => Gold 3, DiveGold 0, Stash {101:1}, Satchel.Waiting.Floor 3, Items [104], Carried empty
Fail_WithNothingCarried_KeepsTheOldSatchel
Fail_WhileCarryingTheSatchel_LosesIt_NewOneReplaces
    // Restore with waiting(2,[105]); PickUp; Add(106); Fail(4) => Waiting.Items [106], Carried null; 105 nowhere
GoHome_BringsEverythingHome
    // carried [101], banked {104:1}, carried satchel [105], DiveGold 9 => Stash {101:1,104:1,105:1}, Gold 9, Carried empty
SatchelCanBeBanked_AsOneSlot
    // carried satchel [105,106]; OpenBank(1); Pick(SatchelPick) true; PushOn => BankedThisDive {105:1,106:1}, Satchel.Carried null
CopyAsFailed_LeavesTheOriginalUntouched
ChestMask_ResetsOnANewFloor_RefusesTwice
    // MarkChestOpened(5,1,3) true, again false, ChestOpened(5,1,3) true; MarkChestOpened(5,2,3) true and ChestOpened(5,1,3) false; index 64 false
KindOf_TableItemsAreLarge_KeysAreKeys
```

```csharp
// PartyBagsTests
For_CreatesOncePerId_AllIsOrdinalById
FailAll_AndGoHomeAll_ApplyToEveryBag
PushOnAll_OnlyTouchesBagsWithAnOpenVisit
AnyCarried_TracksCarriedItems
```

- [ ] **Step 2: Run them and see them fail.** Run `python Tools/cloud/sc_core_tests.py --where "class =~ PlayerBagTests || class =~ PartyBagsTests"`. Expected: a compile failure (the types are missing).
- [ ] **Step 3: Implement** `PlayerBag`, `PartyBags` and `CellarLoot.KindOf`/`RankOf` with the signatures above. Each file starts with a header comment naming its path, its job and the spec decisions it covers.
- [ ] **Step 4: Run the tests and see them pass** (same command; expect PASS).
- [ ] **Step 5: Commit** with `git commit -m "Core: PlayerBag and PartyBags (per-player dive loot, plan 2 task 1)"`. The `.meta` files come with Task 2's gate run, or run `gate` here.

### Task 2: Per-player chest roll

**Files:**
- Modify: `Assets/_Project/Core/Cellars/CellarLoot.cs`
- Test: `Assets/_Project/Tests/EditMode/CellarLootPerPlayerTests.cs`

**Interfaces:**
- Produces: `static int CellarLoot.RollFor(uint seed, int floor, int chestIndex, int playerSlot)`. Use it for loot chests only; it throws `ArgumentOutOfRangeException` for `playerSlot` outside 0-3. `Roll(...)` stays as it is (existing tests and soak sweeps).

Approach:
- The category comes from `CellarLootMix.CategoryFor(seed, floor, chestIndex, KeyFlagsOfDive(seed, floor))`, exactly as `SteeredRank` does, so every player gets the same item type (decision 39).
- The rank draw and the item pick use their own stream: `Rng.Derive(seed, "cellars.loot.player", ((ulong)floor << 32) | ((ulong)(uint)chestIndex << 2) | (uint)playerSlot)`. No existing stream moves.

- [ ] **Step 1: Write the failing tests**

```csharp
RollFor_IsDeterministic                       // same args twice -> same id
RollFor_StaysInTheChestsCategoryBand          // for 200 (seed,floor,chest) and slots 0-3: RankOf(id) within CellarLootMixTuning.BandLo/Hi of CategoryFor(...)
RollFor_PlayersDoNotAllGetTheSameItems        // over 64 chests, slot 0 vs slot 1 differ on at least 16
RollFor_DoesNotMoveTheSharedRoll              // Roll(seed,floor,i,false) equals the value computed before this change for 3 fixed cases (copy them from a run of the unchanged code)
RollFor_RejectsSlotsOutside0To3
```

- [ ] **Step 2: Run them and see them fail.** Run `python Tools/cloud/sc_core_tests.py --where "class =~ CellarLootPerPlayerTests"`.
- [ ] **Step 3: Implement `RollFor`.**
- [ ] **Step 4: Run them and see them pass.** Also run `--where "class =~ CellarLoot"`: every existing loot test must still pass.
- [ ] **Step 5: Run `unity.ps1 gate`** (expect PASS). It creates the `.meta` files for Tasks 1-2.
- [ ] **Step 6: Commit** the code, tests and `.meta` files: `git commit -m "Core: per-player chest roll, same category per chest (plan 2 task 2)"`.

### Task 3: Shared haul becomes progress only; bags join `DiveState`; the `bags` save section

**Files:**
- Modify: `Assets/_Project/Core/Cellars/DiveHaul.cs`, `DiveState.cs`, `StirMeter.cs`
- Modify: `Assets/_Project/Core/Town/TownStock.cs`, `Assets/_Project/Core/Town/Placement/TownSaves.cs`
- Modify: `Assets/_Project/Core/Save/WorldSave.cs`, `WorldSaveRules.cs`, `Assets/_Project/Runtime/Save/WorldSaveStore.cs` (the `Sections` row)
- Modify: `Assets/_Project/Runtime/Net/HostWorld.cs` (`LoadTown`/`CaptureTownInto` carry `Bags`)
- Create: `Assets/_Project/Core/Save/BagsSave.cs` (`BagsSave`, `PlayerBagSave`, `SatchelSave`, `BagsSaveRules`)
- Test: rewrite the affected cases in `DiveHaulTests`, `StirMeterTests`, `TownStockTests`, `TownSaveTests` and `StirMeterHudTests` (don't delete them; change what they assert to the new rule). New: `BagsSaveTests.cs`. Add to `WorldSaveStoreTests.cs`.

**Interfaces:**
- Consumes: Task 1's `PartyBags`, `PlayerBag.Restore` and `CopyAsFailed`.
- Produces:
  - `DiveHaul`:
    - `AddPiece` makes the piece banked (safe) at once, and `FailDive` keeps every collected piece.
    - `UnbankedPieceCount` is removed. Its callers move to `BankedPieceCount`, or to "found" wording in the HUD.
    - New `event`-free API: `int FoundThisDive` (pieces added since the dive state was made), kept for the stats overlay.
  - `StirMeter(DiveHaul haul, StirTuning tuning)` keeps its signature, but its value is `PerFloorCleared * FloorsCleared` only. `StirTuning.PerUnbankedPiece` stays as an unused field marked `// unused since plan 2 (spec 4.5); DiveStir replaces it in plan 3`.
  - `DiveState`:
    - `PartyBags Bags { get; }`, created in the constructor.
    - `BankAtPoint()` also calls `Bags.PushOnAll()`. `BankInTown()` also calls `Bags.GoHomeAll()`. `FailDive()` also calls `Bags.FailAll(CurrentFloor)`, using the floor before it resets.
    - `CanBankHere` is `FloorHasBank(CurrentFloor) && (!BankUsed(CurrentFloor) || Bags.AnyCarried)`.
  - Save classes:
    - `BagsSave { List<PlayerBagSave> Players; [JsonExtensionData] Extra }`.
    - `PlayerBagSave { int Identity = -1; string Id = ""; int Gold; List<LootCountSave> Stash; SatchelSave Satchel; [JsonExtensionData] Extra }`.
    - `SatchelSave { int Floor; List<int> Items }`.
  - `WorldSave.Bags` (a `BagsSave`; null when nothing is saved yet). `WorldSaveRules.SectionBags = "bags"`.
  - `BagsSaveRules`:
    - `static BagsSave Clean(BagsSave raw, List<string> dropped)`.
    - `static BagsSave Capture(PartyBags bags, IReadOnlyDictionary<string, PlayerKey> keyOf, bool inDive, int currentFloor)`.
    - `static void Restore(BagsSave save, PartyBags into)`.
  - `TownSnapshot.Bags` (a `BagsSave`). `TownStock.Capture` fills it from `dive.Bags`, and `TownStock.Restore`/`BuildDive` put it back into the new `DiveState.Bags`.
  - The bag key is the same string `CombatHost.PlayerIdOf` uses: `PlayerKey.ToString()`. `Capture` writes only bags whose id is in `keyOf` (recorded players). Client-id fallback bags are never saved.

Clean rules:
- Drop an entry whose `Identity` is not a defined `IdentityKind`, or whose `Id` is empty or longer than `PlayerKey`'s max.
- Drop a duplicate `(Identity, Id)`, keeping the first.
- Keep at most `WorldPlayers.MaxRecorded` entries.
- Clamp `Gold` to 0..999999 (`TownSaveRules.MaxLootCount`).
- Drop stash rows with count ≤ 0 and clamp counts to the max.
- Drop a satchel whose floor is outside 1-`DiveRoute`'s last floor or whose item list is empty; drop item ids ≤ 0 from it.
- Each dropped entry adds `"bags.<id>"` to `dropped`.

- [ ] **Step 1: Write the failing tests**

```csharp
// DiveHaulTests (rewrite)
AddPiece_IsSafeAtOnce_FailDiveKeepsIt          // AddPiece(PieceId(1,0)); FailDive(); IsCollected true, IsBanked true, PiecesLeft(1) == assigned-1
// StirMeterTests (rewrite)
Stir_ComesFromFloorsClearedOnly_PiecesNoLongerCount
// DiveState (in DiveHaulTests or a new DiveStateBagsTests)
FailDive_FailsEveryBagAtTheCurrentFloor        // bag carries 104 on floor 3; FailDive => bag.Satchel.Waiting.Floor 3
BankAtPoint_PushesOnOpenBags_BankInTownBringsAllHome
// BagsSaveTests
Capture_WritesRecordedPlayersOnly
CaptureMidDive_WritesTheFailedCopy             // carried [104], banked {101:1}, DiveGold 7, floor 2, inDive true => Gold 3, Stash {101:1}, Satchel {2,[104]}; live bag unchanged
RoundTrip_GoldStashSatchel                     // Capture -> JSON (Newtonsoft) -> Clean -> Restore gives equal Gold/Stash/Satchel.Waiting
Clean_DropsOnlyTheBadEntry                     // one entry with Identity 99 + one good: good kept, dropped == ["bags.<badid>"]
Clean_ClampsAndDropsBadRows
// WorldSaveStoreTests (add)
Bags_RoundTripsThroughTheStore
Bags_MistypedSectionKeepsTheRestOfTheFile      // "bags": "oops" -> other sections load, Bags null, rejection label recorded
// existing golden v1 fixture test: unchanged and passing
```

- [ ] **Step 2: Run them and see them fail.** Run `sc_core_tests.py --where "class =~ DiveHaul || class =~ StirMeter || class =~ BagsSave || class =~ TownStock || class =~ TownSave"`. `WorldSaveStoreTests` is a Runtime test, so it runs in `gate`.
- [ ] **Step 3: Implement.** Follow the order in Interfaces: haul, stir, `DiveState`, save classes and rules, the `Sections` row, then the `TownSnapshot`/`TownStock`/`HostWorld` pass-through.
  - Old `stock.loot` and `dive.bankedLoot` keep their current read and write path unchanged (decision 2).
  - `WorldSave.Bags` follows the other sections' null handling, so a world without bags writes no `bags` key and the golden fixture stays byte-equal.
- [ ] **Step 4: Run the core tests (PASS), then `unity.ps1 gate` (PASS).** Fix any test that asserted the old pieces-return or stir-from-pieces rule by asserting the new rule.
- [ ] **Step 5: Run `unity.ps1 soak`** (Core/Cellars changed). Expect PASS. A failing seed becomes a fixed fast-tier case.
- [ ] **Step 6: Commit**: `git commit -m "Core: pieces safe on pickup, bags in DiveState, world-save bags section (plan 2 task 3)"`.
- [ ] **Step 7: Risk review.** The controller sends this to `risk-reviewer` (save data).

### Task 4: Host bag ownership in `DiveHaulSync`; pieces go to stock on pickup

**Files:**
- Modify: `Assets/_Project/Presentation/Cellars/DiveHaulSync.cs`, `Assets/_Project/Presentation/Cellars/DiveDirector.cs`, `Assets/_Project/Presentation/Cellars/DiveStatsOverlay.cs`, `Assets/_Project/UI/StirMeterHud.cs`
- Modify tests: `DiveHaulNetworkTests.cs`, `ReplicatedStateHash.cs`, `StateHashSmokeTests.cs`, and any test that read `Summary.UnbankedPieces`/`UnbankedLoot` (grep)
- Test: `Assets/_Project/Tests/PlayMode/PerPlayerLootNetworkTests.cs` (new; this task adds the first two cases)

**Interfaces:**
- Consumes: Task 3's `DiveState.Bags` and Task 1's `PlayerBag`.
- Produces, on `DiveHaulSync` (all host only; each returns false off the host or on bad input):
  - `IJoinContextProvider Identity { get; set; }`. When null, it is resolved from `ServiceRoots.App` like `CombatHost.ServerBegin` does.
  - `string PlayerIdOf(ulong clientId)` (the same rule as `CombatHost.PlayerIdOf`) and `int PlayerSlotOf(ulong clientId)` (decision 6).
  - `PlayerBag ServerBagOf(ulong clientId)`.
  - `bool ServerAddItem(ulong clientId, int itemId)` and `bool ServerAddGold(ulong clientId, int amount)`.
  - `bool ServerPick(ulong clientId, int carriedIndexOrSatchel)` and `bool ServerUnpick(ulong clientId, int carriedIndexOrSatchel)`. Both validate: `AskerIsUp`, the current floor has a bank, and `InRange` of the bank cell when it is known (the same as `ServerTryBankFor`).
  - `event System.Action<int> PieceAdded` (host; piece id).
  - `ServerAddLoot` is removed.
  - `Summary.UnbankedPieces` and `Summary.UnbankedLoot` are removed. The new `Summary.FoundPieces` is `Haul.FoundThisDive`.
- `ServerSetFloor(floor)` onto a floor with a bank calls `OpenBank` on the bag of every connected client, with `BankSlots.SlotsFor(n, slot, extraOwner)` (decision 5 for the extra owner).
- `DiveDirector`:
  - Subscribes to `PieceAdded` and calls `Town.Receive(HaulSync.HostState.Haul, _heartBanked)` at once (spec 4.4: safe on pickup). It unsubscribes on teardown next to `Banked`/`DiveFailed`.
  - The `EndDive` receive stays; it is idempotent.
- The HUD's "Pieces" line reads `FoundPieces` and `BankedPieces` with the words "found this dive" and "home". Update `StirMeterText` and its EditMode test.
- Update the class authority comment of `DiveHaulSync`:
  - The host owns one `PlayerBag` per recorded player inside `DiveState.Bags`.
  - No bag data rides on `Net`. Per-player views are on `PlayerBagSync` (Task 5).

- [ ] **Step 1: Write the failing PlayMode tests** in `PerPlayerLootNetworkTests`. Start a host plus 2 clients with `PlayModeHarness.StartClients`, and spawn the `DiveHaulSync` prefab the way `DiveHaulNetworkTests` does.
  - `HostKeepsOneBagPerPlayer_ItemsGoToTheRightBag`:
    - `ServerAddItem(clientA, 104)` and `ServerAddItem(clientB, 110)`.
    - `ServerBagOf(clientA).Carried` is `[104]`, B's is `[110]`, and the host's own bag is empty.
  - `PiecePickup_GoesToStockAtOnce_SurvivesFail`. Use `DiveDirector` if `DiveSpineNetworkTests` has a setup to copy; otherwise drive `ServerAddPiece` with a `TownStock` subscribed through the same handler.
    - The piece is in `Town.State` right after `ServerAddPiece`.
    - After `ServerFailDive` it is still there, and `Haul.IsCollected` is true.
- [ ] **Step 2: Run them and see them fail.** Run `unity.ps1 test-play -Filter PerPlayerLoot`.
- [ ] **Step 3: Implement the interfaces above.** Fix compile fallout in the tests from the removed `Summary` fields (`ReplicatedStateHash` and others) by hashing the new field.
- [ ] **Step 4: Run** `unity.ps1 test-play -Filter "PerPlayerLoot|DiveHaulNetwork|StateHash|DiveSpine|CellarContents"` (PASS), then `unity.ps1 gate` (PASS).
- [ ] **Step 5: Commit**: `git commit -m "Dive: host keeps a bag per player, pieces go to stock on pickup (plan 2 task 4)"`.

### Task 5: `PlayerBagSync`, the owner-only view on the player prefab

**Files:**
- Create: `Assets/_Project/Presentation/Cellars/PlayerBagSync.cs`
- Modify: `Assets/_Project/Editor/Builders/PrefabBuilder.cs` (`BuildPlayer`: `BuilderUtil.GetOrAdd<PlayerBagSync>(root)`), plus the regenerated player prefab from a `build` run
- Test: `PerPlayerLootNetworkTests.cs` (add cases)

**Interfaces:**
- Consumes: Task 4's `DiveHaulSync.ServerBagOf`, `ServerPick` and `ServerUnpick`.
- Produces:
  - `struct BagView : INetworkSerializable, IEquatable<BagView>`:
    - `FixedList128Bytes<ushort> Carried` (up to 60 ids) and `FixedList32Bytes<byte> Picks` (a carried index, or 255 = the satchel).
    - `int Gold, DiveGold, StashTotal, BankedThisDive, SlotCapacity`.
    - `bool BankOpen, SatchelWaiting, SatchelCarried`, `byte SatchelFloor`.
    - `uint ChestSeed`, `int ChestFloor`, `ulong OpenedChests`.
    - `static BagView From(PlayerBag bag)`.
  - On `PlayerBagSync : NetworkBehaviour`:
    - `public readonly NetworkVariable<BagView> Net = new(default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);`
    - `BagView View => Net.Value`, `static PlayerBagSync Local` (the owner's instance on this peer; set in `OnNetworkSpawn` when `IsOwner` and cleared in `OnNetworkDespawn`; reset in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`).
    - `void RequestPick(int index)` → `[Rpc(SendTo.Server)] RequestPickRpc(int index, RpcParams p = default)`, and `RequestUnpick` the same way. The host refuses when `p.Receive.SenderClientId != OwnerClientId`, then calls `DiveHaulSync.ServerPick`/`ServerUnpick`.
  - Host side: in `Update`, when `IsServer`, find the `DiveHaulSync` (cached; the scene search runs at most once a second, like `ResolveFloorSync`), build `BagView.From(ServerBagOf(OwnerClientId))`, and write it only when it is not `Equals` the current value. With no `DiveHaulSync`, write `default`.
  - Class authority comment: the host writes `Net` from its bag for this object's owner. Only the owner and the host can read it (decision 39: loot shown only to its owner). The owner asks for picks by RPC, and the host re-validates them in `DiveHaulSync.ServerPick`.

- [ ] **Step 1: Write the failing PlayMode tests**
  - `View_OnlyOwnerAndHostSeeIt_LateJoinerGetsOwn`:
    - The host adds 104 to A and 110 to B.
    - On client A: A's own `PlayerBagSync` shows `Carried == [104]`, and A's copy of B's `PlayerBagSync.Net.Value` equals `default`.
    - The host sees both.
    - Then a late joiner C (`PlayModeHarness.TestClient` + `JoinAsync`) gets 105 from the host. C's own view shows `[105]`, and C's copy of A's view is `default`.
    - `Catches:` the owner read filter and spawn sync.
  - `Pick_ByRpcFromTheOwner_HostValidatesCapacity`:
    - Set the floor to 2 (it has a bank) with host + 2 clients, so capacity is 2 each.
    - A carries 3 items. A's `RequestPick` with 0, 1 and then 2 leaves `Picks.Count == 2` on the host and in A's view.
    - Then `ServerBank` pushes on: A's `BankedThisDive` holds the 2 picked items.
  - `Rejoin_GetsTheSameBagBack`:
    - A carries 104, then leaves (`LeaveAndDestroy`) and rejoins with the same `PlayerId`.
    - The new client's view shows `[104]`, and `PlayerSlotOf` is unchanged.
    - If the direct-host harness has no roster (`Identity` null), install a `HostRoster` in the test or mark the case `Catches: only with a roster` and say so.
- [ ] **Step 2: Run them and see them fail.** Run `unity.ps1 test-play -Filter PerPlayerLoot`.
- [ ] **Step 3: Implement `PlayerBagSync`, add it in `PrefabBuilder.BuildPlayer`, then run `unity.ps1 build`.** Commit the regenerated player prefab and `.meta` files, and nothing else the build touched. If it changes other files, report them as churn.
- [ ] **Step 4: Run** `unity.ps1 test-play -Filter "PerPlayerLoot|DisconnectRejoin|StateHash"` (PASS), then `unity.ps1 gate` (PASS).
- [ ] **Step 5: Commit**: `git commit -m "Net: PlayerBagSync owner-only bag view on the player prefab (plan 2 task 5)"`.
- [ ] **Step 6: Risk review.** The controller sends Tasks 4-5 to `risk-reviewer` (networking).

### Task 6: Per-player chests; bag line on the HUD

**Files:**
- Modify: `Assets/_Project/Presentation/Cellars/CellarContentsSync.cs`, `Assets/_Project/UI/StirMeterHud.cs`
- Test: `PerPlayerLootNetworkTests.cs` (add), `CellarContentsNetworkTests.cs` (adjust the chest assertions), `StirMeterHudTests.cs` (bag line)

**Interfaces:**
- Consumes: `CellarLoot.RollFor` (Task 2), `PlayerBag.MarkChestOpened`/`ChestOpened` (Task 1), `DiveHaulSync.ServerBagOf`/`ServerAddItem`/`PlayerSlotOf` (Task 4), `PlayerBagSync.Local.View` (Task 5).
- Produces:
  - `ServerTryOpenChest(clientId, chestIndex)` keeps its range and wall checks.
    - The **key chest** (`fc.HoldsKey`) behaves as now: shared bit in `ContentsState.Chests`, `RoomKeyHeld`, once for the party.
    - A **loot chest** is refused when `ServerBagOf(clientId).ChestOpened(seed, floor, i)`. Otherwise:
      1. `item = CellarLoot.RollFor(seed, floor, i, PlayerSlotOf(clientId))`;
      2. `MarkChestOpened`;
      3. `ServerAddItem(clientId, item)`. A full bag still opens the chest; the item is lost and `LastLootItem` records it. Spec silent: *proposal*, keep it simple.
    - `ChestOpened` fires with `(i, item)`.
    - Loot chests never set the shared `ContentsState.Chests` bit.
  - Every peer draws a loot chest lowered when the local `PlayerBagSync.Local.View` has `ChestSeed/ChestFloor == State.Seed/Floor` and the bit is set. The key chest uses the shared bit. The owner `Interact` prompt skips chests that are already open for the local player.
  - Update the class authority comment: loot chests are per player (decision 39). The opened mask lives in each player's bag and rides on that player's owner-only `PlayerBagSync`. The key chest and keys stay in the shared state.
  - HUD: `StirMeterText.Bag(BagView v)` returns `"Bag: {Carried.Length} carried, {BankedThisDive} safe, {Gold + DiveGold} gold"`, shown while a `PlayerBagSync.Local` exists.

- [ ] **Step 1: Write the failing tests**
  - PlayMode `Chest_EachPlayerOpensOwnCopy_KeyChestStaysShared`:
    - Build a floor the way `CellarContentsNetworkTests` does, with host + 2 clients and `PlayersOverride` placing every player at the chest.
    - A opens loot chest i: A's bag gets `RollFor(seed, floor, i, slotA)`. A opens it again: refused.
    - B opens chest i: B's bag gets `RollFor(..., slotB)`, and A's bag is unchanged.
    - The key chest opened by A sets `RoomKeyHeld`. B's open of the key chest is refused, and neither bag holds `RoomKeyId`.
  - PlayMode `Chest_LoweredOnlyForThePlayerWhoOpenedIt`:
    - After A opens loot chest i through the real `RequestOpenChestRpc` path (or `ServerTryOpenChest` if the RPC needs a real walk), A's client shows it lowered and B's client shows it closed.
  - EditMode `StirMeterHudTests.Bag_LineShowsCarriedSafeAndGold`.
- [ ] **Step 2: Run them and see them fail.** Run `unity.ps1 test-play -Filter "PerPlayerLoot|CellarContents"`.
- [ ] **Step 3: Implement the interfaces above.**
- [ ] **Step 4: Run** `unity.ps1 test-play -Filter "PerPlayerLoot|CellarContents|DiveSpine|DiveWalkQa|Bot"` (PASS), then `unity.ps1 gate` (PASS).
- [ ] **Step 5: Commit**: `git commit -m "Dive: per-player loot chests, key chest shared, HUD bag line (plan 2 task 6)"`.
- [ ] **Step 6: Risk review.** The controller sends Task 6 to `risk-reviewer`.

### Task 7: Save round trip through the host flow; notes for plan 3

**Files:**
- Test: `Assets/_Project/Tests/PlayMode/PerPlayerLootNetworkTests.cs` (add one save case)
- Modify: `docs/PARKING_LOT.md`

**Interfaces:**
- Consumes everything above. `WorldSaveStore.PathOverrideForTests`, and the `WorldSaveFlow`/`HostWorld` capture used by existing save PlayMode tests (grep `PathOverrideForTests` for the setup to copy).

- [ ] **Step 1: Write the PlayMode test** `Save_BagsSurviveAWorldReload`:
  - With a roster so the bags are keyed, give A gold 5 (via `ServerAddGold` and then `ServerBankInTown`), a stash item 104, and a waiting satchel (fail on floor 2 while carrying 110).
  - Capture to the override path, reload the world, and start again.
  - A's bag shows `Gold 5`, `Stash {104:1}` and `Satchel.Waiting {2,[110]}`.
  - The real `world.json` is untouched.
- [ ] **Step 2: Run it.** Run `unity.ps1 test-play -Filter PerPlayerLoot`; expect PASS, or fix the capture/restore wiring from Task 3 until it does.
- [ ] **Step 3: Add the parking-lot entries** to `docs/PARKING_LOT.md`, each dated 2026-11-03:
  - per-player enemy drops (they plug in at `DiveHaulSync.ServerAddItem`);
  - placing the lost satchel on its floor and picking it up (plan 3);
  - saving the 3-player extra-slot owner (plan 3);
  - the bank room UI for picks and the decision 4 auto-fill;
  - retiring `DiveHaul`'s legacy loot and the world-wide `stock.loot`;
  - what happens to a full bag on a chest open.
- [ ] **Step 4: Run** `unity.ps1 check` (PASS; `SC MARKER` == HEAD).
- [ ] **Step 5: Commit**: `git commit -m "Dive: bags save round trip test, plan 3 notes (plan 2 task 7)"`.

**Manual 2-player walk** (for the handoff; host-only tests can't catch it): two PCs, one dive. Check:
- each player's chest gives a different item;
- the other player's chest stays closed on your screen;
- banking picks;
- a wipe leaves a satchel for each player;
- a rejoin keeps the bag.
