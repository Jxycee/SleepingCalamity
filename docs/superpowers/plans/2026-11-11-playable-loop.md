# Playable loop milestone (PL)

Written 2026-11-11 by Fable Run 1 (`2026-11-11-fable-reset-plan.md` section 4). Status: **proposal, waiting for the user's read.** Sections 1-2 say where the loop stands, section 3 is the work in four Sonnet sessions plus a reserve, section 4 is the first session's brief and the queue changes, section 5 is the walk that closes the milestone, section 6 lists what the user decides.

Citations are `path:line` under `Assets/_Project/` unless they start with `docs/`. They come from Haiku scans of `main` at `bb41d8be`; the lines marked (checked) were re-read for this plan. An implementer confirms a line with the LSP tool before editing.

## 0. Summary

**The loop is already wired. What is missing is the way in, the way out, and proof that a person can do it.**

The brief for this plan assumed the glue was thin because Gameplay, UI and App are small. The glue sits in Presentation instead: `Presentation/Cellars/DiveDirector.cs` (1,134 lines), `DiveHaulSync.cs` (820), `Presentation/Town/PlacementMode.cs` (346) and `TownSync.cs` drive `DiveSession`, `TownState`, `TownStock` and the save from real prompts and real buttons. An automated host-plus-client run of town, dive, go home, place the bakery, loaves, save and reload passes (`Tests/PlayMode/SquareLoopWalkQaTests.cs:240`).

So this milestone builds no new game system. It does four things:
1. **Proves the loop by player input only**, from floor 1, and measures how long the first building takes (session PL-1).
2. **Tells the player what to do next and what a dive brought home** (PL-2).
3. **Replaces the dev panel with a real front door**: title, worlds, host lobby with the join code, join, session ended (PL-3).
4. **Gives a way out and closes the two-player seams**: pause menu, floor pick, the build and the walk script (PL-4).

Two player actions exist only on dev keys, which a release build turns off: picking the floor of a return dive (G) and eating a loaf (H). Both get a real button (PL-4 and PL-2).

**Done when** a person with a release build, no dev panel, no dev key and no command-line flag can: make a world, walk Hollowmere, dive, come home (by the bank, by the end, or by falling), place at least the bakery frame with a piece they found, leave through a menu, come back and find it standing. Solo, and with two players joined by code where the second player places a part. Finishing the whole bakery with the baker and getting loaves is the stretch goal, not the bar (decision D3).

## 1. The loop as screens and transitions

| # | Screen or step | What drives it today | State |
|---|---|---|---|
| 0 | Boot | `Bootstrap` scene, `AppBootstrap.Compose` builds the services and hands to `Lobby` (`App/AppBootstrap.cs:87-185`) | Works |
| 1 | Title | Nothing. `Lobby` holds a camera and a sun (`Editor/Builders/SceneBuilder.cs:57-72`). The only UI is the IMGUI `DevSessionPanel` (`App/DevSessionPanel.cs:61-110`, checked), which also shows in release builds | **Missing** |
| 2 | World list, new world, delete | Rules and storage exist: `WorldSaveFlow` opens the selected, else newest, else a new world (`App/WorldSaveFlow.cs:128-160`), 10 per kind (`WorldSummary.MaxPerList`), solo needs exactly 1 player and multiplayer 2-4 (`Core/World/WorldPlayers.cs:46-47, 72-89`). UI only in `DevSessionPanel.DrawWorldPicker` (`:118-155`) | Logic works, **screen missing** |
| 3 | Solo start | `session.StartLocalAsync(false)`; `SceneFlow` starts a solo host at once (`App/SceneFlow.cs:109-112`, checked) and loads Hollowmere through NGO scene loading (`:121-140`) | Works |
| 4 | Host lobby (multiplayer) | `HostOnlineAsync` (Relay) gives a join code, shown only in the dev panel. The only caller of `SceneFlow.TryStartSession` is the dev panel's Start button (`DevSessionPanel.cs:98`; gate `Runtime/Net/HostWorld.cs:306-330`). A refused start publishes a toast **key**, and no table turns keys into words | Logic works, **screen missing** |
| 5 | Join by code | `JoinOnlineAsync(code)` (`Networking/UgsNetworkSession.cs:94`), approval through `ConnectionApprover.cs:128-170` and `HostWorld.Admit` (4 recorded players, solo worlds refuse). No waiting screen, refusals are keys | Logic works, **screen missing** |
| 6 | Town (Hollowmere) | Spawn and late-join spawn in `App/ArenaPlayerSpawner.cs:47-94`. Prompts appear only within 3.5 m of the trapdoor (`DiveDirector.cs:45, 818-829`) and 4 m of the build point (`PlacementMode.cs:20-21, 98-121`). Nothing says where to go | Works, **no guidance** |
| 7 | Dive start | Interact at the trapdoor sends `RequestStartDive` (`DiveDirector.cs:863`), host checks in `ServerTryStartDive` and `ServerStartDive` (`:273-283, 347-353`). One press takes the whole party. The floor pick for a return dive is on the dev key G (`:765, 825-830`), so a release build always starts on floor 1 even with a held checkpoint, while the prompt still says "G changes the floor" (`:771`) | Works; **floor pick dev-only, checkpoint unusable in release** |
| 8 | Floors 1-7 | Floor card (`UI/FloorCardHud.cs`), stir meter and pieces left (`UI/StirMeterHud.cs`), combat HUD (`Presentation/Combat/CombatHud.cs`), stair (`DiveDirector.cs:363-371`), chests (`CellarContentsSync.cs:1039-1069`). A piece is picked up by walking onto it (`CellarContentsSync.cs:973`) and goes to the town stock at once (`DiveHaulSync.cs:331`, `DiveDirector.cs:493`). Loaves are granted at the start (`DiveDirector.cs:207-210`) but the only way to eat one is the dev key H (`Runtime/Input/DevKey.cs:12`, `DiveDirector.cs:1033-1039`); `HeroVitals.RequestEatLoaf` (`Gameplay/Combat/HeroVitals.cs:108`) has no player input | Works; **no button to eat a loaf** |
| 9 | Bank room (floor 5) | Interact on the bank cell (`DiveDirector.cs:846-851`), IMGUI bank screen with real buttons: up/down, Jump picks, Primary pushes on, Secondary goes home (`:882-887`; `Core/Input/InputContextRouter.cs:60-72`). Two players confirm inside 10 s (`DiveHaulSync.cs:13-14`) | Works (queue item :55 is stale) |
| 10 | Return to town | Go home (`DiveDirector.cs:396-403`), finish at floor 7's end cell (`:406-416`), or fall: all heroes down fails the dive (`:538-553`), and solo that means one death (`docs/PHASE1.md:349`, checked). Every ending teleports the party to the home point (`:442-470, 622-643`); no scene change. **No result is shown**: the only summary is the dev overlay on F4 (`Presentation/Cellars/DiveStatsOverlay.cs:146`), and the end reason never reaches a client (`DiveEnded` is a host-only event, `:73, 468`; clients only see `NetActive` go false, `:459`) | Works, **no result screen** |
| 11 | Place a building | `PlacementMode`: Interact at the build point takes the builder lock, top-down view, pick a lot, Jump turns, Primary places frame, then parts, then the resident, Secondary picks up, Pause leaves and saves (`PlacementMode.cs:108-206`, panel `:320-343`). Host checks every request (`TownSync.cs:240-298`), a client can build. Refusals are plain words (`Core/Town/Placement/PlacementText.cs:38-57`). The finished bakery gives loaves (`DiveDirector.cs:209`) | Works (IMGUI) |
| 12 | Save | On every world change, every 10 s when dirty, on session start and end, and on quit (`WorldSaveFlow.cs:144, 178-203, 237-242`; `AppBootstrap.cs:198-202`). Sections: weapons, stock, town, residents, dive, players, bags, suspend (`Core/Save/WorldSave.cs:21-37`, written at `Runtime/Net/HostWorld.cs:200-205`) | Works |
| 13 | Leave or quit | `LeaveAsync` is only on the dev panel (`DevSessionPanel.cs:101`), and the cursor is locked during play (`Runtime/Input/PlayerInputAdapter.cs:308`, checked), so a player cannot click it. The Pause button does nothing outside placement (only reader: `PlacementMode.cs:165`, checked). Going offline loads `Lobby` without a word (`SceneFlow.cs:115-117`); the end reason is a toast key (`:142-147`) | **Missing** (Alt+F4 only) |

**Two players, the differences.** Start needs 2 present (row 4). A second player may also join after the start (`ArenaPlayerSpawner.cs:68-70`). One press at the trapdoor moves both. In the bank room the second press confirms. One builder at a time holds the lock, and the other player sees the ghost and the wishes live. A downed hero has 20 s and is revived by a 3 s hold (`Core/Combat/EnemyRules.cs:139-197`, `Gameplay/Combat/HeroVitals.cs:13-30`). A client that loses the host lands in `Lobby` (row 13).

**What the passing tests do not prove.**
- `SquareLoopWalkQaTests` starts the dive at the bank checkpoint and hands itself the pieces with `ServerAddPiece` (`:270-275`, checked). It calls RPCs, not buttons, and its client declines scene loads (`:3, :11-12`). It proves what the dive hands the town, not that a player can find a piece and carry out the loop.
- `DiveBot` (`Tests/PlayMode/Bot/DiveBot.cs:21`) plays a solo dive through `FakeInputService`, but stops at the dive. There is no test of `PlacementMode` input, of the menus, or of leaving and coming back by input.
- No person has walked the loop. The walk script in `docs/superpowers/handoff/USER_TODO.md:27-60` still describes the old four-floor dive, `docs/playtest/NOTES.md` has no entry, and the two-player Relay walk has waited for a second player since 2026-10-30.

## 2. The gaps, smallest first

| # | Gap | Why it stops a person | Size | Session |
|---|---|---|---|---|
| G1 | No input-only proof of the whole loop from floor 1 | Every other gap is a guess until a bot plays it with buttons | M (tests only) | PL-1 |
| G2 | The first building can be far away | The four first-half pieces each sit on floor 2 or 4, drawn per world (`Core/Town/Placement/PieceCatalog.cs:31-33`, checked; `Core/Cellars/PieceDepths.cs:33-51`). Floor 1 has none. In about half of all worlds the frame is on floor 4, and then a first dive must pass floor 3's set piece, where the bot without healing dies on every seed (`docs/superpowers/handoff/QUEUE.md:20`), and there is no bread before the bakery stands | S (Core, after D1) | PL-1 |
| G3 | Stale queue items hide the real state | `QUEUE.md:55` (bank keys) has real input already; `QUEUE.md:32` (solo unwinnable) predates four fixes and the bot now reaches floor 3 | S (docs + one measurement) | PL-1 |
| G4 | No guidance | A new player does not know the trapdoor exists, that pieces came home, or where the build point is | M (Core + one HUD line + two beacons) | PL-2 |
| G5 | No dive result | A fall, a go-home and a finish all look the same: you are suddenly in town | M (Core + card + one replicated end reason) | PL-2 |
| G5b | No button to eat a loaf | The bakery's reward cannot be used in a release build, and a solo player has no healing of their own | S (one input action; the request RPC exists) | PL-2 |
| G6 | Keys instead of words | Start refusals, join refusals, end reasons and save warnings reach the player as `world.library_full` | S (text table + test) | PL-3 |
| G7 | No front door | Rows 1, 2, 4, 5 exist only as the dev panel | L (UI stack + five screens) | PL-3 |
| G8 | No way out | Rows 13: no pause menu, no leave, no quit | M | PL-4 |
| G9 | Return-dive floor pick is a dev key | Spec decision 38 lets the party pick a floor up to the bank; in a release build the checkpoint earned at the bank does nothing | S-M (net) | PL-4 |
| G10 | Relay has never carried two real players, and the walk script is stale | The milestone ends in a walk | S (build + docs) | PL-4 |

**Left out on purpose** (each stays where it is in the queue or the parking lot): floors 8-10 and the Bell-Warden, water gimmick states, the deferred floor modifiers, 3-4 player smoke and the state-hash bar, the bag-rule and gold decisions, real pausing in solo (`GameScope` and `IPauseState` stay unattached), the unused `Core/Rooms` folder and `WishEvaluator.Score`, moving the in-game HUD off IMGUI, all art. None of them stops the loop.

## 3. The sessions

Four sessions and a reserve. Each is one Sonnet 5.5 medium session on its own branch and worktree, delegating to `implementer`, with the reviews named below, merged by the usual rule (checked tip, merge commit, `sc-merge`). PL-2 and PL-3 touch different files and can run at the same time after PL-1 (one on the PC, one in the cloud, because of the single PlayMode port). PL-4 needs PL-3's UI stack.

```
PL-1 loop proof  ->  PL-2 guide + result   \
                 ->  PL-3 front door        -> PL-4 way out, seams, build -> user walk -> PL-5 walk fixes (reserve)
```

### PL-1 Loop proof (first batch)
Goal: play the whole loop with buttons in a test, find every break, and measure the first building. Mostly test code; fixes of 30 lines or fewer land here, larger ones are written up for PL-2 to PL-4.

| Task | What | Files | Acceptance tests |
|---|---|---|---|
| 1.1 | **Input tests for the town side** (fast tier, PlayMode). Drive `PlacementMode` and the trapdoor through `FakeInputService` in the real Hollowmere scene, solo host | new `Tests/PlayMode/PlacementModeInputTests.cs`, new `Tests/PlayMode/DiveEntryInputTests.cs`; read `Tests/PlayMode/Fakes/FakeInputService.cs`, `Tests/PlayMode/PlayModeHarness.cs:130-166`, `Tests/PlayMode/HollowmereHubTests.cs` | `PlacementModeInputTests`: `InteractAtBuildPoint_TakesLock_EntersPlacement`, `Primary_PlacesFrame_WhenFrameInStock`, `Primary_WithoutFrame_SaysNoFrameInStock`, `Jump_TurnsGhost_BlockedTurnIsRefused`, `Primary_PlacesPartsInOrder_ThenResident`, `Secondary_PicksUpLastPlaced`, `Pause_LeavesPlacement_ReleasesLock_WorldSaved`, `WalkingAway_LeavesPlacement`. `DiveEntryInputTests`: `InteractAtTrapdoor_StartsDive_Solo`, `OutOfRange_NoPrompt_NoStart`, `ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint` (pins today's behaviour; PL-4 task 4.2 replaces it), `WalkOverPiece_PieceReachesTownStock` (a real pickup on a generated floor, no `ServerAddPiece`) |
| 1.2 | **Loop bot** (`Category("Bot")`, `SC_BOT=1`). From the Hollowmere spawn: press Interact at the trapdoor, let `DiveBot` play, come home by any ending, press Interact at the build point, place what is in stock, Pause out, leave the session, start the same world again, check the town. The bot may teleport between town points; every interaction is a button press | new `Tests/PlayMode/Bot/LoopBot.cs`, new `Tests/PlayMode/Bot/LoopBotTests.cs`; reuse `Tests/PlayMode/Bot/DiveBot.cs:77-100`, `DiveBotTests.cs:27-79` | `SoloLoop_Tough_InputOnly_FrameHome_Placed_SurvivesReload` (seeds 7, 12345, 2026; `Tough` on so it tests wiring, not difficulty; must pass). `SoloLoop_Measure` (same seeds, `Tough` off, up to 4 dives, never fails: writes dives needed, minutes, deaths per floor and the floor each piece was found on to `Reference/sessions/pl1-loop-proof/RESULTS.md`) |
| 1.3 | **Two-player loop by input**, as far as one process allows: both heroes on their own fake input, wired per hero as the co-op bot does (`Tests/PlayMode/Bot/CoopDiveRun.cs:103-128`), starting on floor 1 with real pickups. The in-process client declines scene loads, so its `PlacementMode` may not exist: then the client places through `TownSync` requests as `SquareLoopWalkQaTests` does, and the report says so; the two-process run is the user's walk | extend `LoopBotTests.cs`; read `Tests/PlayMode/SquareLoopWalkQaTests.cs:57-170`, `Tests/PlayMode/Bot/CoopDiveRun.cs` | `PairLoop_Tough_BothByInput_FrameHome_ClientPlacesPart_SurvivesReload` (seed 7) |
| 1.4 | **First building distance** (EditMode). Count where the frame lands over world seeds. If the user said yes to D1: put the frame (id 100) on floor 2 for every new world, leave saved worlds alone (the assignment is saved in the dive section, field `pieceDepths`, `Runtime/Save/WorldSaveStore.cs:70, 88`, stamped at `Runtime/Net/HostWorld.cs:135-140`) | `Core/Town/Placement/PieceCatalog.cs:31-41`, `Core/Cellars/PieceDepths.cs:33-51`, new cases in its EditMode test file | `PieceDepthsTests.FrameFloor_Share_Over1000Seeds` (prints the share; `[Category("Soak")]`). With D1: `Frame_IsOnFloor2_ForEveryNewWorld` (200 seeds), `SavedAssignment_IsKept_OnLoad`, `OtherPieces_StillSpreadOverFloors2And4` |
| 1.5 | **Queue truth.** Close `QUEUE.md:55` with the evidence in row 9 above if 1.1 and the existing `BankScreenInputTests` agree. Close or re-scope `QUEUE.md:32` from the 1.2 numbers. List every break found, with a size, in the session's STATUS for PL-2 to PL-4 | `docs/superpowers/handoff/QUEUE.md` (in the merge commit) | none |

Reviews: `reviewer` once for the batch (tests and Core). `risk-reviewer` only if 1.4 changes `PieceDepths` (it decides what a new world saves), and only on that diff. No branch review (tests, Core, docs).
Checks: `gate` on push; `check` on the tip before merge; the bot runs with `SC_BOT=1` are extra and do not gate the merge; `soak` once if 1.4 touches generation input.

### PL-2 Guide and dive result
Goal: the game says what to do next, and says what a dive brought home.

| Task | What | Files | Acceptance tests |
|---|---|---|---|
| 2.1 | **`LoopGuide`** in Core: a pure function from a small snapshot (in a dive or not, floor, pieces left on the floor, what is in stock, what is placed, baker found / placed, service open) to one `GuideStep` with its numbers. Words in a `GuideText` table | new `Core/Game/LoopGuide.cs`, `Core/Game/GuideText.cs`, new `Tests/EditMode/LoopGuideTests.cs` | `NewWorld_PointsAtTheStair`, `InDive_PiecesOnFloor_SaysHowManyLeft`, `InDive_NoneLeft_PointsAtTheStairDown`, `HomeWithFrame_PointsAtBuildPoint`, `FramePlaced_PartsInStock_SaysPlaceParts`, `FramePlaced_NothingInStock_SaysDiveForMore`, `BakerInStock_BuildingDone_SaysPlaceTheBaker`, `ServiceOpen_SaysDiveWithLoaves`, `EveryStep_HasWords` |
| 2.2 | **Guide line and beacons.** One objective line on screen, fed from state every peer already has (`TownSync.View`, `DiveHaulSync.Current`). A light column at the trapdoor and at the build point while the guide points there; Hollowmere has none today (the trapdoor is a slab and a ring inside the church, about 26 m from the spawn), and the dive floors' stair column is the pattern to copy (`Presentation/Cellars/CellarFloorBuilder.Dive.cs:64`) | new `UI/LoopGuideHud.cs` (IMGUI, same self-spawn pattern as `UI/FloorCardHud.cs:78-84`), `Editor/Builders/HollowmereHub.cs:52-56, 81-82, 113-115` (two marker objects), new `Tests/PlayMode/LoopGuideHudTests.cs` | `Town_NoPieces_GuideAtStair_BeaconOn`, `AfterPieceHome_GuideAtBuildPoint`, `Client_ShowsTheSameStepAsHost`, `InPlacement_GuideHidden` |
| 2.3 | **Dive result.** Core `DiveResult` (ending, floors cleared, pieces and people found this dive, items banked, items lost as the satchel, gold kept) built by the host when the dive ends, one per player. A card shows it until Interact or 10 s, then the guide line takes over. Each player's own numbers are already on their client (`Presentation/Cellars/PlayerBagSync.cs:17-26, 115`: an owner-read `BagView` with carried, gold, banked this dive, satchel), so each peer snapshots its own bag just before the end. The end reason is not replicated, so add one host-written value beside `NetActive` (reason plus a dive counter, so a repeat of the same ending still shows) and nothing else | new `Core/Cellars/DiveResult.cs`, `Presentation/Cellars/DiveDirector.cs:452-470` (`EndDive`), `Presentation/Cellars/PlayerBagSync.cs` (read side), new `UI/DiveResultCard.cs`, new `Tests/EditMode/DiveResultTests.cs`, new `Tests/PlayMode/DiveResultNetworkTests.cs` | EditMode: `WentHome_BankedItemsCount_NothingLost`, `Fell_UnbankedLost_GoldHalved_PiecesStayFound`, `Finished_EverythingCarriedComesHome`, `PeopleFound_AreListed`. PlayMode: `EachPlayer_GetsTheirOwnResult_HostAndClient`, `Fail_ShowsTheLostSatchel`, `Card_Dismisses_OnInteract`, `LateJoiner_GetsNoCard` |
| 2.4 | `QUEUE.md:51`: the "N home" count after a reload, since 2.3 reads the same numbers | `UI/StirMeterHud.cs`, its Core text class | `StirMeterTextTests.HomeCount_AfterReload_CountsBankedPieces` |
| 2.5 | **A button to eat a loaf.** A new `UseItem` action in the Player map (suggested: Q, gamepad D-pad up; pick free bindings), carried through `PlayerInputFrame` and `PlayerIntent` in the Free and LockOn contexts, calling the existing `HeroVitals.RequestEatLoaf`. The dev key H stays for tests. The combat HUD's help line names the button | `Runtime/Input/SleepingCalamityInput.inputactions`, `Runtime/Input/PlayerInputAdapter.cs:190, 329-344`, `Core/Input/PlayerInputFrame.cs`, `Core/Input/PlayerIntent.cs`, `Core/Input/InputContextRouter.cs:43-104`, `Presentation/Cellars/DiveDirector.cs:1033-1039`, `Presentation/Combat/CombatHud.cs:167` | EditMode `InputContextRouterTests.UseItem_PassesInFreeAndLockOn_NotInPlacementBankOrMenu`. PlayMode `LoafInputTests`: `UseItem_EatsALoaf_HealsAndCountsDown`, `UseItem_NoLoaves_DoesNothing`, `UseItem_AtFullHealth_KeepsTheLoaf` (or today's rule, whichever `RequestEatLoaf` enforces), `Client_UseItem_HostApplies` |

Reviews: `reviewer` for 2.1, 2.2, 2.4, 2.5. `risk-reviewer` once on 2.3 (a new replicated value and owner-only data). `branch-reviewer` before merge (networking plus Core, UI, Presentation and a builder wired together).
Authority: guide and card are local views on every peer; the result's numbers are the host's.

### PL-3 Front door
Goal: a release build opens on a real menu and the dev panel is gone from it.

**UI stack (decision D2).** Menus use UI Toolkit; the in-game HUD stays IMGUI for this milestone. Reasons: menus need a text field, lists, focus and gamepad navigation, which IMGUI does not give; `.uxml` and `.uss` are text files, so no prefab YAML; the Input System asset already has a UI map and `InputContext.Menu` routes to it (`Runtime/Input/SleepingCalamityInput.inputactions:53`, `Core/Input/InputContextRouter.cs:28`). The risk is UI Toolkit under `-nographics` test runs, so all behaviour lives in plain C# models that EditMode tests cover, and task 3.0 is a spike with a fallback.

| Task | What | Files | Acceptance tests |
|---|---|---|---|
| 3.0 | **Spike, one implementer run.** A `UIDocument` and `PanelSettings` on the App object (built by `SceneBuilder`), one button. Prove: keyboard, mouse and gamepad activate it in the editor; a `-nographics` PlayMode test finds it by name and clicks it. If either fails, build the views below in IMGUI on the same models and park UI Toolkit | `Editor/Builders/SceneBuilder.cs:40-72`, new `UI/Menus/` folder, `UI` asmdef | `MenuStackSmokeTests.Document_Binds_ButtonClick_ReachesModel` |
| 3.1 | **`UiText`**: words for every toast key, `WorldStartText` verdict, `EndReasonText` reason and join refusal | new `Core/Game/UiText.cs`; read `Core/World/WorldStartText.cs`, `Core/Net/EndReasonText.cs`, `App/WorldSaveFlow.cs:135, 147` | `UiTextTests.EveryStartVerdict_HasWords`, `EveryEndReason_HasWords`, `EveryPublishedToastKey_HasWords` (greps the keys from a list kept beside the table) |
| 3.2 | **`FrontDoorModel`** (plain C#, UI assembly): Title, Worlds (solo or multiplayer), HostLobby, JoinEntry, Connecting, WaitingForHost, Ended. It calls only what the dev panel calls today: `WorldSaveFlow.Select` and the library listing, `INetworkSession.StartLocalAsync / HostOnlineAsync / JoinOnlineAsync / LeaveAsync`, `SceneFlow.TryStartSession`, `HostRoster` | new `UI/Menus/FrontDoorModel.cs`; read `App/DevSessionPanel.cs:61-178` as the reference for every call | `FrontDoorModelTests` with a fake session: `Title_PlaySolo_NoWorlds_CreatesAndStarts`, `Worlds_Full_NewWorldDisabled_WithWords`, `Worlds_Delete_AsksFirst`, `Host_ShowsJoinCode_WhenConnected`, `Host_OnePlayer_StartDisabled_SaysNeedsTwo`, `Host_TwoPlayers_Start_CallsTryStartSession`, `Join_EmptyCode_Disabled`, `Join_Refused_ShowsWords_BackToJoinEntry`, `Client_Connected_NotStarted_WaitingForHost`, `SessionEnded_ShowsReason_ThenTitle`, `SoloWorld_NotOfferedForHosting` |
| 3.3 | **Views** for those states. The host lobby shows the code large with a copy button and the players' names (`Runtime/Net/HostRoster.cs:16-37`); Start is greyed out with the reason. The roster is not replicated, so the guest's waiting screen says only that they are connected and waiting; showing names to guests is parked. `Lobby` keeps its plain backdrop | new `UI/Menus/*.uxml`, `*.uss`, `FrontDoorView.cs`; `Editor/Builders/SceneBuilder.cs` | covered by 3.5 |
| 3.4 | **Wire it.** The App object gets the front door. `DevSessionPanel` draws only when `Debug.isDebugBuild` or a new `-dev-panel` flag is set. Going offline shows Ended with the reason instead of a silent `Lobby` | `App/AppBootstrap.cs:87-185`, `App/SceneFlow.cs:100-147`, `App/DevSessionPanel.cs:61`, `Core/Net/DevFlags.cs:41-57`, `Tests/EditMode/DevFlagsTests.cs` | `DevFlagsTests.DevPanel_Flag_Parses`; 3.5 |
| 3.5 | **Flow tests** (PlayMode, direct transport standing in for Relay) | new `Tests/PlayMode/FrontDoorFlowTests.cs`; harness `Tests/PlayMode/PlayModeHarness.cs:130-166` | `Solo_NewWorld_Play_LandsInHollowmere`, `Solo_Continue_OpensTheSelectedWorld`, `Host_TwoPlayers_Start_BothInHollowmere`, `Host_Alone_CannotStart`, `Client_WaitsUntilHostStarts`, `HostLeaves_ClientSeesEnded_ThenTitle`, `ReleaseBuildPath_DevPanelNotDrawn` |

Reviews: `reviewer` for 3.0-3.3. `risk-reviewer` once after 3.4 (session start, join, leave, world selection: networking and save). `branch-reviewer` before merge (App, UI, Networking and builders wired together).
Authority: the front door is local on every peer; it only asks. The host's start gate and join approval stay where they are.

### PL-4 Way out, two-player seams, the build
Goal: a player can leave, a returning party can pick a floor, and the walk build exists.

| Task | What | Files | Acceptance tests |
|---|---|---|---|
| 4.1 | **Pause menu** on PL-3's stack: Resume, Controls (the bindings read from the action asset), Leave to title, Quit. Opens on Pause in the Free and LockOn contexts and sets the Menu context, which frees the cursor. It never stops time (two players share the world; solo pausing is parked). Host leaving asks first: it ends the session for everyone, and the world is saved | new `UI/Menus/PauseMenuModel.cs`, `PauseMenuView.cs`; `Core/Input/InputContext.cs:11`, `Runtime/Input/PlayerInputAdapter.cs:190, 308` | `PauseMenuModelTests`: `Pause_InFree_Opens`, `Pause_InPlacement_LeavesPlacementInstead`, `Pause_InBankScreen_DoesNotOpen`, `HostLeave_AsksFirst`. `PauseMenuFlowTests` (PlayMode): `Open_FreesCursor_HeroStopsTakingInput`, `Leave_AsHost_WorldSaved_SessionEnds`, `Leave_AsClient_HostKeepsPlaying_ClientAtTitle`, `Downed_CanStillOpen` |
| 4.2 | **Return-dive floor pick** without a dev key. With a held checkpoint the pick defaults to the bank floor, not floor 1; at the trapdoor prompt a real button cycles the allowed floors (1 to the bank) and Interact starts. The prompt names the real button instead of G. The host checks the pick against the held checkpoint | `Presentation/Cellars/DiveDirector.cs:273-283, 347-353, 765-771, 818-830, 863`, `Core/Input/InputContextRouter.cs` | `DiveEntryInputTests`: `ReturnDive_DefaultsToTheBankFloor` (replaces PL-1's pin), `FloorPick_Button_CyclesAllowedFloors`, `Prompt_NamesTheRealButton`, `FloorPick_NoCheckpoint_OnlyFloor1`, `FloorPick_ClientPick_HostValidates`, `FloorPick_AboveBank_Refused` |
| 4.3 | **Breaks from PL-1's list** that need networking or more than 30 lines (unknown until PL-1 reports; cap: the three that most block the walk, the rest go to the queue) | from PL-1's STATUS | a failing test first for each |
| 4.4 | **Build and walk script.** Build the release player, confirm `unity.ps1 exec` reports it correctly (`QUEUE.md:65`), replace `USER_TODO.md` items 3b and 3c with section 5 of this plan, add the one-PC Relay rehearsal | `Editor/Dev/PlayerBuild.cs:13-19`, `docs/superpowers/handoff/USER_TODO.md:27-60` | the build runs to the title screen (one screenshot in the session folder) |

Reviews: `risk-reviewer` once for 4.1's leave paths and 4.2 together. `branch-reviewer` before merge.
Authority: the pause menu is local; leaving goes through `INetworkSession.LeaveAsync`; the floor pick is asked by a client and decided by the host.

### PL-5 Walk fixes (reserve)
One session after the user's solo walk and two-player walk, for what they found. Anything that does not fit one session goes to the queue. If the walks find nothing blocking, this session is not started.

## 4. First session brief and queue changes

### Brief for PL-1 (paste into `start_session`, model `claude-sonnet-5-5`, effort `medium`, own worktree; or into a `sessions/requests/` file for the cloud)

```
Title: PL-1 Loop proof
Status file: Reference/sessions/pl1-loop-proof/STATUS.md

Goal: Prove with player input only that town -> dive -> town -> place a building -> leave -> reload works
from floor 1, solo and host+client, and measure how many dives the first building takes. This is the first
session of docs/superpowers/plans/2026-11-11-playable-loop.md (read sections 0, 1 and "PL-1" in section 3;
nothing else in it is yours). The loop is wired already; your job is to find where it breaks for a person.

Branch: claude/pl1-loop-proof, cut from origin/main, in its own worktree. Run `git lfs checkout` first.
Delegate code to `implementer` (effort high: PlayMode), reads to `scanner`. Two implementer runs:
run A = tasks 1.1 + 1.4, run B = tasks 1.2 + 1.3. One `reviewer` pass at the end.

Files and symbols (confirm lines with the LSP tool before editing):
- Tests/PlayMode/Fakes/FakeInputService.cs (how tests feed buttons)
- Tests/PlayMode/PlayModeHarness.cs:130 StartDirectHostWithScenes, :166 StartClients, :336 TestClient
- Tests/PlayMode/Bot/DiveBot.cs:77 (ctor), :100 Tick, :49 Tough; Tests/PlayMode/Bot/DiveBotTests.cs:27-79
- Tests/PlayMode/SquareLoopWalkQaTests.cs:57-170 (session, town and save helpers to reuse), :240 (the walk)
- Tests/PlayMode/Bot/CoopDiveRun.cs:103 (installs the fake), :123-128 Wire (one fake per hero)
- Tests/PlayMode/HollowmereHubTests.cs (loading the real Hollowmere in a test)
- Presentation/Town/PlacementMode.cs:108-206 (prompt, lock, inputs), :320-343 (panel)
- Presentation/Cellars/DiveDirector.cs:818-863 (hub prompt and RequestStartDive), :273-283 and :347-353
  (start checks), :452-470 (EndDive), :765 and :825-830 (PickFloor, dev key G)
- Presentation/Cellars/CellarContentsSync.cs:973 (a piece is picked up by walking onto it)
- Core/Town/Placement/PieceCatalog.cs:31-41 (Specs), Core/Cellars/PieceDepths.cs:33-51 (Assign)
- docs/superpowers/handoff/QUEUE.md:32, :55

Behaviour (tests are the deliverable; change game code only for a break of 30 lines or fewer):
1.1 New fast-tier PlayMode fixtures PlacementModeInputTests and DiveEntryInputTests, solo host in the real
    Hollowmere scene, every action through FakeInputService. Names and cases are in the plan's PL-1 table.
    `ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint` pins today's release behaviour (PL-4 changes it).
    The fake implements IDevInput too: keep every dev key off in these fixtures.
1.2 New LoopBot (Tests/PlayMode/Bot/LoopBot.cs) wrapping DiveBot: trapdoor, dive, home by any ending,
    build point, place what is in stock, Pause out, leave, start the same world, check the town. It may
    teleport between town points; it may not call a Server* method or an RPC directly.
    `SoloLoop_Tough_InputOnly_FrameHome_Placed_SurvivesReload` (seeds 7, 12345, 2026) must pass.
    `SoloLoop_Measure` (Tough off, up to 4 dives) never fails and writes RESULTS.md.
1.3 `PairLoop_Tough_BothByInput_FrameHome_ClientPlacesPart_SurvivesReload` (seed 7): each hero on its own
    fake input (CoopDiveRun.Wire), starting on floor 1 with real pickups. If the in-process client has no
    PlacementMode (it declines scene loads), it places through TownSync requests; say so in the report.
1.4 EditMode `PieceDepthsTests.FrameFloor_Share_Over1000Seeds` (Category Soak, prints the share).
    ONLY if the coordinator's brief says decision D1 = yes: put piece 100 on floor 2 for every new world,
    with `Frame_IsOnFloor2_ForEveryNewWorld`, `SavedAssignment_IsKept_OnLoad`,
    `OtherPieces_StillSpreadOverFloors2And4`; that diff also gets `risk-reviewer`.
1.5 In the merge commit: tick QUEUE.md:55 if 1.1 and BankScreenInputTests show real input; tick or
    re-scope QUEUE.md:32 from the measured numbers.
A test that fails because of a GAME bug stays failing with [Ignore("PL break: <one line>")] only if the
fix is over 30 lines; list each one in STATUS with file:line, a size and whether it is networking.

Tests that must fail first: none are expected to; an unexpected failure is the finding.

Out of scope: menus, HUD, guide, result screen, pause (PL-2 to PL-4). Difficulty tuning. Any change to
RPCs, NetworkVariables or save sections. CURRENT.md. Art.

Check: PC `powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>\Tools\unity.ps1 gate -OutDir <scratchpad>`
on each push; `check` on the tip before merge (SC MARKER must equal HEAD). Bot runs:
`$env:SC_BOT=1` then `test-play` (extra, not a merge gate; PlayMode waits for the machine lock).
Cloud: `bash Tools/cloud/unity.sh <same> --out <dir>`.

Report (STATUS + board profile, not a message): tests added with pass/fail counts, the check line,
RESULTS.md numbers (dives to first frame, minutes, deaths by floor, floor of each piece), the list of
breaks with sizes, and what the client side could not be driven to do.
Done when: merged to main, QUEUE ticks in the merge commit, STATUS says `Archive: ready`.
```

### Queue changes (for the coordinator to apply in the commit that launches PL-1)

Add a new first section, **"Ready: playable loop (critical path)"**:
- `[ ]` **M** PL-1 Loop proof: input-only loop tests and loop bot, first-building measurement (`plans/2026-11-11-playable-loop.md` section 3) [U]
- `[ ]` **M** PL-2 Guide and dive result: `LoopGuide`, objective line and beacons, dive result card, a real button to eat a loaf (after PL-1) [U][net]
- `[ ]` **L** PL-3 Front door: UI Toolkit spike, `UiText`, `FrontDoorModel`, five screens, dev panel off in release (after PL-1) [U][net]
- `[ ]` **M** PL-4 Way out and seams: pause menu, return-dive floor pick with a real button, PL-1 breaks, release build and walk script (after PL-3) [U][net]
- `[ ]` **M** PL-5 Walk fixes (reserve; only after the user's walks)

Fold into those and retire as separate items:
- `QUEUE.md:32` BUGS item 1, solo unwinnable: becomes PL-1 task 1.2 (`SoloLoop_Measure`) and is closed or re-scoped from its numbers.
- `QUEUE.md:55` dev-only bank keys: closed by PL-1 task 1.5 (real input exists, row 9).
- `QUEUE.md:51` HUD "N home": becomes PL-2 task 2.4.
- `QUEUE.md:65` verify the exec fix on the next player build: becomes PL-4 task 4.4.
- `QUEUE.md:67` `sc-walk` names helpers that do not exist: rewrite the skill around `LoopBot` and `FakeInputService` after PL-1 (S, tools), instead of porting `WalkDriver`.

Change in "Waiting on the user":
- Replace "Playtest `playtest-2026-11-06`" with "Playable-loop walk on the PL-4 build (plan section 5)". The 11-06 build still opens on the dev panel and predates all of this; it is not worth the user's time now.
- Add decisions D1-D5 (section 6).
- `USER_TODO.md` items 3b and 3c are stale (old four-floor dive); PL-4 replaces them.

Push down, not out: the dive-build items (`QUEUE.md:16-29`), M7 readiness (`:58-60`) and per-player loot follow-ups (`:52-54`) stay, below the new section.

Docs found stale during this run (S, docs, ride along with any PL merge): `docs/PHASE1.md:255-278, 326-331, 347` still describe the floor-4 Dough Warden, the shared haul and the 90 s timer; the roadmap header says four plans for five rows; `CURRENT.md:10` says "Then plan 3" under a done plan 3 and `:15` cites an older check tip than `:7`.

## 5. The walk that proves the milestone

On the PL-4 release build. No dev panel, no dev keys, no flags. If a step needs any of them, the milestone is not done. Write surprises in `docs/playtest/NOTES.md`.

**Solo, about 30 minutes.**
1. Start the game. A title screen, not a grey box. Choose to play alone, make a new world.
2. You stand in Hollowmere. A line on screen tells you to find the stair, and you can see where it is.
3. At the trapdoor a prompt appears. Press Interact. You are on floor 1 with a floor card.
4. Play down. On floor 2 (decision D1) you find the bakery frame; the pieces-left count drops.
5. Come home either way: fall, or reach the bank room on floor 5 and choose to go home. A card says what came home and what was lost.
6. The line now points at the bakery lot. Walk there, press Interact, place the frame on a lot that reads green. Try a spot that blocks a lane: refused, in words.
7. Press Pause to leave placement. Press Pause again: a menu. Read the controls. Leave to the title.
8. Continue the same world. The frame stands. Quit from the menu.
9. Stretch: dive again for the oven, door and sign, wake the baker on floor 3, finish the bakery, place the baker, and start the next dive with 3 loaves. Take a hit and eat one with the button the HUD names. After going home from the bank, the trapdoor offers the bank floor.

**Two players, about 40 minutes.** A second person on their own PC; before that, a ten-minute rehearsal on one PC with the editor and the build side by side on two profiles, which is the first time Relay carries two players.
1. Host: play with friends, new multiplayer world. The lobby shows a join code and one name. Start is greyed out and says it needs two players.
2. Guest: join, type the code. Guest sees "waiting for the host", host sees two names. Host starts. Both stand in Hollowmere.
3. Either player presses Interact at the trapdoor: both are on floor 1. Each finds a piece. One falls; the other revives them with a hold.
4. Reach the bank room. One chooses to go home, the other confirms. Each sees their own result card.
5. Host places the frame while the guest watches the ghost move. Host leaves placement; guest takes over and places a part. Guest tries a lane-blocking spot: refused.
6. Guest opens the pause menu and leaves. Host keeps playing. Guest joins again with the same code and is back in town.
7. Host leaves from the pause menu after the warning. Guest sees "the host ended the session" in words and lands on the title.
8. Host continues the world alone? Not allowed: a multiplayer world needs two. The lobby says so. Guest joins, host starts: the frame, the part, both players' weapon growth and bags are as they were.

**Automated counterpart** (must be green on the PL-4 tip): `LoopBotTests` (PL-1), `LoopGuideHudTests` and `DiveResultNetworkTests` (PL-2), `FrontDoorFlowTests` (PL-3), `PauseMenuFlowTests` and `DiveEntryInputTests` (PL-4), and the existing `SquareLoopWalkQaTests`.

## 6. Decisions for the user

Each has a default; the sessions proceed on the defaults unless told otherwise, except D1, which changes a settled rule and waits for a yes.

- **D1. The bakery frame is always on floor 2 in a new world.** Today the four first-half pieces each land on floor 2 or 4 (plan 3 decision 10). If the frame is on floor 4, the first building sits behind floor 3's set piece with no bread yet. With the frame on floor 2, every first dive that clears one floor brings home something to place, and the loop closes in about ten minutes. Saved worlds keep what they have. *Recommended: yes.*
- **D2. Menus on UI Toolkit, in-game HUD stays IMGUI for now.** With an IMGUI fallback if the spike fails. *Recommended: yes.*
- **D3. The bar is "frame placed and saved"; the finished bakery with loaves is the stretch.** Otherwise the milestone waits on dive difficulty, which Run 5 (balance) owns. *Recommended: yes.*
- **D4. Floor 3 enemy mix 20/50/30 to 30/50/20** (already proposed, `QUEUE.md:20`, waiting on you). It decides whether a solo player without bread can reach the baker. *Recommended: yes, and let Run 5 tune it properly.*
- **D5. One press at the trapdoor takes the whole party.** That is how it works today; the spec asks for gathering at stairs. *Recommended: keep for this milestone and judge it in the two-player walk.*
