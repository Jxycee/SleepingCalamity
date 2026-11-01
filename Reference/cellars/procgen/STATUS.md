# Cellars procedural generation (procgen track)
Status: IN PROGRESS (item 1a-c built, 1d waits)
Blocker: item 1d (loot mix) waits on P47 healing's item ids (asked local M5 session)
Branch: claude/cellar-procgen
Last check: none
Next: item 1d after P47 ids; then item 2
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
   d. Loot mix steered to target shares: no chest-content system exists yet (`FloorChest` has no contents; pieces are a fixed 2/2/2 that are never missable, so only loot categories can be steered). Waits on P47 healing ids. Feedback rule target + k x (target - share), dive replay, placeholder targets in one tuning spot; not wired to `DiveHaulSync`.
2. Layout presets (spine / ring / hub / wing) + far-dead-end exit (placement change: goldens move deliberately).
3. Per-floor enemy roster subset + ambush / thin / crowded twists.
4. Swappable chunk regions inside room grids.
5. More greybox vignettes (target 12-20) + state-conditioned odds.

## Log
- 2026-10-31: track opened, branch from main b499a71.
- 2026-10-31: item 1a-c built (branch claude/procgen-item1). Core: `CellarFloorRoute` (+ `CellarRoomGraph`: rooms + corridor cells, because branch links end on existing corridors), `CellarSpecialRoom` (`SpecialRoomDive`), `CellarDoorHints`; greybox `DoorHint_r<room>_d<door>` icons and `SpecialRoom_<Kind>_r<room>` marker in `CellarFloorBuilder` (group `Procgen`). Tests `CellarProcgenTests` (8, EditMode); full test-edit 794/794 PASS; LayoutHash/DressingHash goldens untouched.
  - 1a: spine = fewest-step entry-to-exit route; arcs = off-spine loops (one shortest per spine-node pair, may hold no room); roles Spine/Arc/Branch/Locked; cycle role Plain/KeyArc/ShortcutBack/None. Measured over 1200 seeds: None 3.3/2.4/3.3% (floors 1/2/3), Plain ~65%, KeyArc ~13%, ShortcutBack ~18%; ~4 arcs per floor. Placeholders: KeyArcPercent 70, ShortcutMinSteps 6.
  - 1c: kinds EliteDen (floor 2+), Treasury, Shrine, Quiet; per-floor chance 40/55/55 (placeholders in `SpecialRoomDive`); a kind is spent when drawn even if the floor has no eligible room (keeps floor N independent of earlier layouts). Measured: special on 44/52/57% of floors 1/2/3, 11% of dives have none, 1.5 per dive, never a repeat kind, never locked / elite-vignette room / non-combat / floor 4; Spine room only when no off-path room is eligible (2% of specials).
  - 1b: a hint per open door with something beyond it, over the whole corridor network behind the door (a T junction shows every room on it; union of flags). Because networks are big, 97% of hints carry a flag (key 32%, special 33%, reward vignette 19% of hints): too noisy to read as a choice. Refine later: hint only the next room on the route, or only the nearest room per branch.
