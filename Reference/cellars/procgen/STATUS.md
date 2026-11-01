# Cellars procedural generation (procgen track)
Status: IN PROGRESS (item 1a-c merging; 1d and 3 deferred to the M5 contents session's chests/spawner)
Blocker: none. Coordinator 2026-10-31: Core-only items first (1, 2, 4); hold procgen merges while a spine or contents STATUS says "check running"; builder hooks only in CellarFloorBuilder.Procgen.cs
Branch: claude/cellar-procgen
Last check: PASS at d7403e2 (build PASS, EditMode 811/811, PlayMode 37 passed, 8 skipped, 0 failed) before the partial move; tip eb4efc0 needs scene regen + check
Next: see Handoff below
Archive: ready (variety session handed off; successor continues)

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

## Handoff (2026-10-31, variety session to successor)
- Merged to main: nothing from procgen yet.
- `claude/cellar-procgen` tip eb4efc0: item 1 a-c (route arc roles, special room, door hints; greybox in `CellarFloorBuilder.Procgen.cs`, one call line + `partial` in the main builder). Reviewed: approve (reviewer, Sonnet). Main incl. Town polish 2 merged; the two scenes were taken from main and are NOT regenerated yet.
- Merge order (coordinator): M5 spine, then M5 contents, then procgen item 1, then floor art. When contents is on main: merge origin/main (take main's .unity on conflict), regenerate scenes with the -nographics `build`, commit, one `check`, then `sc-merge`. After item 1, hold procgen merges while a spine/contents STATUS says "check running".
- `claude/procgen-item2` c1354d5a (based on cellar-procgen before eb4efc0): layout presets + far-dead-end exit, EditMode 829/829 in its worktree, no check yet. Review (Sonnet reviewer): APPROVE with fixes to do before merge:
  - should-fix: three tests got `[Timeout(900000)]` (generation ~5x slower: 400-attempt loop + a CellarRoomGraph per attempt in ExitAdjacentToEntry). Measure attempts/fallback, then cut seed counts (20000 -> 5000) or speed the adjacency check; timeout about 300 s.
  - add a test that the Wing loop's endpoints stay in branch Y (or the entry); Wing with m == 1 attaches the loop to the entry-spine link: force m >= 2 or document.
  - rename stale "Goldens_AreUnchanged" / "unchanged by the variety layers" names and comments (CellarFloorGeneratorTests:15, CellarHealingTests:141).
  - PlayMode CellarFloorNetworkTests golden was edited by value and has never run: the check proves it.
  Merge after item 1: rebase/merge onto main, fix the above (Sonnet implementer), regenerate scenes, check, merge.
- `claude/procgen-item4` f1efaa60 (based on cellar-procgen before eb4efc0): chunk regions, 6 rooms / 8 regions (1x1, 2x1, 1x3; the pool is too dense for 2x2), 54.8% non-open picks, worst variant 59.1%, 61.5% of visits differ from the room's most common combination; EditMode 828/828, no check. Review: APPROVE with:
  - should-fix: a heal point (CellarHealing, M5 file) can land inside a blocking/cover chunk cell. Fix on our side: `CellarRoomChunks` skips variants whose solid cells cover the floor's heal cell (CellarHealing exposes it), plus a seeds test that no heal point sits on a solid chunk cell. Don't edit CellarHealing.cs.
  - nits: special-room pillar can stand on a chunk block (greybox; art pass picks a free cell); `Chunks_DoNotMoveLayoutOrDressingHashes` is near-tautological (reword or drop).
  Merge after item 2: merge item 2 in, re-pin ChunksHash goldens, rerun the spread asserts (variant share < 60%, different > 40%), fix the above, check, merge.
- If a branch above is missing or unpushed, look in `C:\Dev\SleepingCalamity\.claude\worktrees\agent-*` for its worktree (`git -C <wt> log -1`).
- Then: item 5 (vignettes 12-20, state-conditioned odds; Core + greybox). Items 1d (loot mix) and 3 (roster subset, twists) wait on the M5 contents session (owns chests, loot roll, spawner).
- Don't touch: `CellarFloorBuilder.cs` beyond the one call line, user tuning constants, the pity save (M6), real art.

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
