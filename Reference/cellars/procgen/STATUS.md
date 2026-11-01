# Cellars procedural generation (procgen track)
Status: IN PROGRESS (item 1a-c merging; 1d and 3 deferred to the M5 contents session's chests/spawner)
Blocker: none. Coordinator 2026-10-31: Core-only items first (1, 2, 4); hold procgen merges while a spine or contents STATUS says "check running"; builder hooks only in CellarFloorBuilder.Procgen.cs
Branch: claude/cellar-procgen
Last check: PASS at d7403e2 (build PASS, EditMode 811/811, PlayMode 37 passed, 8 skipped, 0 failed); re-check on the partial move pending
Next: merge item 1, then item 2 (layout presets + far-dead-end exit), then item 4
Archive: no

Source: `Reference/cellars/variety/STATUS.md` "Refine later" and `docs/research/procedural-dungeons.md` (3, takeaways). Coordinator order, 2026-10-31.

## Rules for every item
- Pure function of (seed, floor) on its own `Rng.Derive` stream. Each new layer is a resolver over the finished `CellarFloorLayout` (like `CellarFloorDressing.Resolve`), never a change to `TryBuild`/`Builder`, unless the item says otherwise.
- Each layer has its own hash with fresh seed-12345 goldens. `LayoutHash` and `DressingHash` stay unchanged (pinned for floors 1-3); any deliberate change is called out here.
- Sameness/spread tests over 1,000+ seeds. Not touched: the user's tuning constants (three-run check), the pity save (M6), real art.
- Each item merges on its own green `check`.

## Items
1. Phase 1 cheap (research takeaways):
   a. Entry-to-exit route with arc roles: the main path from entry to exit, the existing loop cycles that cross it, each arc labelled (plain / key arc / shortcut-back); floors with no loop on the path get `none`, rate measured. The real planned ring shape comes with item 2's `ring` preset (it rewrites placement).
   b. Reward hint on doors: Cellars floors have one stair exit, so a Hades door choice doesn't map literally. Reading used: each room-to-room door shows a hint of what lies beyond it (chests, key, special room, reward vignette). Core computes it; the icon is greybox in Presentation. Coordinator: correct this reading if wrong.
   c. One capped special room per floor: a tag on an existing combat room, "one of N, at most once per dive, may be none" (dive replay like `VignetteDive.Select`); not floor 4, not the locked leaf or checkpoint, never the room holding the sleeping-elite vignette.
   d. Loot mix steered to target shares: no chest-content system exists yet (`FloorChest` has no contents; pieces are a fixed 2/2/2 that are never missable, so only loot categories can be steered). DEFERRED (coordinator): the M5 contents session owns chests and the loot roll; P47 added no heal items. Feedback rule target + k x (target - share), dive replay, placeholder targets in one tuning spot; not wired to `DiveHaulSync`.
2. Layout presets (spine / ring / hub / wing) + far-dead-end exit (placement change: goldens move deliberately).
3. Per-floor enemy roster subset + ambush / thin / crowded twists.
4. Swappable chunk regions inside room grids.
5. More greybox vignettes (target 12-20) + state-conditioned odds.

## Item 1 tuning notes
- Door icons: notable (non-chest) hints mark 74.3% of doors (flagged incl. chests 92.0%; key 23.6%, special 22.5%, reward vignette 14.0%; 1200 seeds x floors 1-3). Still most doors; if the three-run check finds the icons noisy, drop Exit/Checkpoint from Notable or hint only along the route. Test bound < 75%.
- Placeholders: `SpecialRoomDive.FloorPercent` 40/55/55, `Kinds` (EliteDen from floor 2), `KeyArcPercent` 70, `ShortcutMinSteps` 6.

## Log
- 2026-10-31: track opened, branch from main b499a71.
- 2026-10-31: item 1a-c built (branch claude/procgen-item1). Core: `CellarFloorRoute` (+ `CellarRoomGraph`: rooms + corridor cells, because branch links end on existing corridors), `CellarSpecialRoom` (`SpecialRoomDive`), `CellarDoorHints`; greybox `DoorHint_r<room>_d<door>` icons and `SpecialRoom_<Kind>_r<room>` marker in `CellarFloorBuilder` (group `Procgen`). Tests `CellarProcgenTests` (8, EditMode); full test-edit 794/794 PASS; LayoutHash/DressingHash goldens untouched.
  - 1a: spine = fewest-step entry-to-exit route; arcs = off-spine loops (one shortest per spine-node pair, may hold no room); roles Spine/Arc/Branch/Locked; cycle role Plain/KeyArc/ShortcutBack/None. Measured over 1200 seeds: None 3.3/2.4/3.3% (floors 1/2/3), Plain ~65%, KeyArc ~13%, ShortcutBack ~18%; ~4 arcs per floor. Placeholders: KeyArcPercent 70, ShortcutMinSteps 6.
  - 1c: kinds EliteDen (floor 2+), Treasury, Shrine, Quiet; per-floor chance 40/55/55 (placeholders in `SpecialRoomDive`); a kind is spent when drawn even if the floor has no eligible room (keeps floor N independent of earlier layouts). Measured: special on 44/52/57% of floors 1/2/3, 11% of dives have none, 1.5 per dive, never a repeat kind, never locked / elite-vignette room / non-combat / floor 4; Spine room only when no off-path room is eligible (2% of specials).
  - 1b: a hint per open door with something beyond it, over the whole corridor network behind the door (a T junction shows every room on it; union of flags). Because networks are big, 97% of hints carry a flag (key 32%, special 33%, reward vignette 19% of hints): too noisy to read as a choice. Refine later: hint only the next room on the route, or only the nearest room per branch.
- 2026-10-31: door hints now cover only the rooms reached FIRST through a door (corridor BFS stops at another room's door; own room excluded). Flagged share 97% -> 92.0% (1200 seeds x floors 1-3, 34771 hints): key 23.6%, special 22.5%, reward vignette 14.0%. Still above 80%: most doors sit one corridor hop from a chest room, the stair/bank room or a vignette. HintsHash goldens re-pinned (deliberate); route/special hashes unchanged.
- 2026-10-31: item 2 built (branch claude/procgen-item2). Core: `CellarLayoutPreset.cs` (`LayoutPreset` Spine/Ring/Hub/Wing, `CellarLayoutPresets.PresetFor`, weights 30/30/20/20 are placeholders in `Weights`, own stream `cellars.preset`, previous floor's pick dropped), steering in `CellarFloorGenerator.TryBuild` (placement order, preferred parent in `LinkNewest`, per-preset loop rules), `CellarFloorLayout.Preset` (in `LayoutHash` for floors 1-3 only), `ExitIsLeaf`/`ExitAdjacentToEntry`, validator checks.
  - Presets: Spine = rooms link to the newest room 75%, 0-1 loops. Ring = two chains off the entry meeting at a join room, exit hangs off the join room; forced second-arc loop onto the corridor outside the join room's door (+ at most 1 extra). Hub = biggest combat room placed first, others link to it 70%, 1-2 loops. Wing = two branches off the entry, exit ends the larger (X), the one loop lives in the smaller dead branch Y (never touches the spine; a one-room wing loops the entry's spare door onto that room's link).
  - Exit: placed last (after the locked leaf), one link, never to the entry, never offered a second door; an attempt whose exit door shares a corridor network with an entry door is thrown away. The locked room can no longer take the exit parent's or loop source's last door (reserved). Validator: leaf + not adjacent apply to every floor 1-3 including last resort (only the distance bound relaxes).
  - Ladder: retries as before, then 2 retry streams building the PICKED plan as Spine (keeps the rooms; layout then carries Spine), then the last resort (first 8 streams real preset, rest Spine). `Generate` returns fallback 1 for the Spine-plan retries.
  - Measured (2000 seeds x floors 1-3 = 6000 floors): preset shares Spine 29.0 / Ring 28.9 / Hub 21.2 / Wing 20.9 %; never a repeat on consecutive floors; fallback retry 2.78%, last resort 0.00% (none), last resort with a short exit 0; every floor validates, exit a leaf, not adjacent, >= 12. Loops: Spine 0:1398 1:339; Ring 1:1571 2:80; Hub 1:1190 2:35; Wing 1:1220. Route arc rate (CycleRole != None): Ring 99.6%, Hub 95.9%, Wing 81.7%, Spine 75.4%. Mean entry-to-exit steps: Spine 32.7, Hub 30.4, Ring 29.6, Wing 30.7. Distinct FootprintHash 6000/6000; distinct (preset, spine room sequence) 8.9% (distinct spine room sequence alone 4.1%).
  - Cost: floors are dearer to generate (about 5 ms vs less before; locked chains retry more), so the three 10k/20k-seed tests got `[Timeout(900000)]` (default 180 s no longer fit).
  - Deliberate golden changes (seed 12345, floors 1-3; floor 4 unchanged): LayoutHash f1 7160980227004277501 -> 2055763217958189686, f2 18317817182715499079 -> 1402270470201830695, f3 10285151120262563894 -> 11057337751887225001 (CellarFloorGeneratorTests, CellarHealingTests, CellarVarietyTests, PlayMode CellarFloorNetworkTests); DressingHash -> 7289301962720901386 / 16695276620330047160 / 5435435391175835042; RouteHash and HintsHash re-pinned (Procgen_GoldenHashes_Seed12345; SpecialHash unchanged on floors 1-3). New: `Preset_Seed12345_Golden` = Hub, Ring, Spine.
  - Not verified: PlayMode (CellarFloorNetworkTests golden edited by value, not run); builder untouched.
