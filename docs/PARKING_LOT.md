# Parking lot

Ideas worth keeping that aren't in the approved Phase 1 plan. Each line gives the idea, its source, and the date. Promote an idea into a plan deliberately; never build one silently.

## From the Kingdom Hearts sweeps (2026-10-30)
**Promoted into Phase 1 on 2026-10-30 (user):** growth verbs, tech bonus, hero abilities from weapon milestones, spell pips, and boss parts. See the PHASE1 change log. The rest stays parked.
Sources: `docs/research/kh1-sweep.md` and `docs/research/kh2-sweep.md`.

### Weapon growth (fits M4)
- **Growth verbs per heirloom** (KH2 Drive Forms): each weapon also levels from its own style of play.
  - Axe: finishers and guard breaks.
  - Dagger: hits and parries.
  - Staff: multi-hits.
  - Bow: weak-point shots.
- **Tech bonus** (KH1): a parry, deflect, weak-part finisher or element match pays extra weapon experience, with a small pop. Teaches the enemy answers. Fits M3 and M4.
- **Hero abilities from weapon milestones** (KH2 form levels feeding base Sora): a weaker copy of the ability stays when the hero switches weapons.

### Combat
- **Spell pips refilled by landing hits** (KH1 MP): for Wren's staff and other active skills. Start at 3; weapon growth raises the cap.
- **Boss parts with their own HP** (KH1 Guard Armor): breaking a part drops healing. In co-op, players naturally split the parts between them. Candidate for the floor-4 Dough Warden.

### Dungeons and exploration
- **Co-op "hearth marks"** (KH1 Trinity Marks): two or more players push, lift or brace together to open secrets and shortcuts.
  - Each colour unlocks with the story.
  - Every mark also has a solo route, and nothing can be missed.
- **Movement tools unlocked by progress** (KH2 High Jump and Glide, the Cavern of Remembrance):
  - Optional room-pool branches are tagged with the tool they need, and gates carry colour-coded hints.
  - One player who has the tool opens a shortcut for the whole party.

### Progression and rewards
- **Permanent capacity from story beats and optional superbosses** (KH2 bonus levels, Absent Silhouettes): an extra slot, a gauge bar or max HP, each listed in the journal.
- **Rule-twist challenge gauntlets built from existing rooms** (KH2 Coliseum cups): no healing, one weapon, a shared damage budget.
- **Keepsakes returned to named residents** (KH1 Dalmatians): found three to a chest, each return pays a milestone reward. Ties into rebuilding home.

### Things to avoid (from both sweeps)
- Hidden random penalties, such as KH2's Anti Points.
- Irreversible early build choices, or hidden permanent choices.
- Features only a full group can trigger.
- Team attacks or summons that take a player out of play.
- Prompts that reward mashing.
- Enemies that respawn after leaving a room.
- A forced travel mini-game.
- A hard mode that only multiplies damage.

## Tooling (2026-10-30)
- ~~Cloud `.csproj` generation~~ built 2026-10-30 (`sc_cloud_compile.py --csproj`).

## Art (2026-10-30)
- 2026-10-30: B11 cellar lantern, optional polish (relaxed review 28.5/40 said a player would notice): stronger wear from the three-quarter view (soot streaks above the vents, orange rust on the base rim), more roughness/specular variation on the iron body, and a slightly warmer iron so it catches the gold rim light. Not needed before the dungeon-corner captures.
- 2026-10-30: B6 plaza ground, next gains past 28/40 (sc_cobbles.py, sc_flag_ring.py): a per-instance tone or hue jitter in the cobble shader (Unity instance colour, or 4 more baked tone variants) for stronger stone-to-stone contrast at the wide camera; a wet/dark band of setts in the dish ring after rain; one or two adopted moss rocks (Poly Haven rock_moss_set_01/02, CC0) at the facade's left foot; crisper granite arrises on the setts (smaller voxel, 2.5 mm) once the PC bake shows whether the 1024 proof reads soft.
- 2026-10-30: B7 props, optional polish (self-review 29.5/40 after 4 rounds; target 31): a shallow water fill in Bucket_A, faded stencil marks on one sack, a darker inner burlap face on Sack_C's cuff, the dirt splash a touch stronger at the cobble line, and real straw strands instead of the flat straw bed under the produce.
- 2026-10-31 (ground + foliage wave 2): Hollowmere's low cameras (HM_Ruins, the lake shore) see a bare horizon and a cyan-green sky band. They need a far tree line / hedgerow: real tree-line or hedge cards (a B9 atlas row of crown masses) or the orchard tree as instanced far trees. `HollowmereFoliage.FarHedge` (ring at 125-131 m, lake gap) is ready but not called: the scaled ivy cards it uses read as giant ivy strands.
- 2026-11-01 (road2): under the LowSun preset the R3_02 house face and the Lower Gate bank slab in Cap_HM_RoadOut go saturated orange (low-sun key on warm plaster/earth); a cooler, less saturated low-sun key or a value cap on those surfaces.
- 2026-11-01 (visual review, user): gameplay outside Hollowmere's walls, for after Phase 1: gathering spots (synthesis materials), fishing spots (fishing opens in Saltwake), small finds and journal scraps, a viewpoint, resident wish targets that use the land ("where I can see the lake"). The ring around the town is walkable but closed (Nier Replicant style). See `Reference/qa/visual/TOWN_VISUAL_REVIEW.md` part C.
- 2026-11-01 (user): finding a new town may reveal a survivor (someone who lived through the Long Night because of something or someone). Hollowmere itself has no people until a rebuild unlocks a resident.
- 2026-11-02 (review of the day's merges, `Reference/qa/review-2026-11-02.md`): low items for later. F3 fixed (claude/review-lows). F4 is routed to the dive-flow session's stair follow-up branch, not parked. F5 fixed (claude/review-lows). F7: `StairHandoverTests` should destroy `TestHubGround` in teardown.

## Town life (2026-11-03)
- 2026-11-03 (user): awake residents travel between towns (shops, friends, the day's task), then home. Seamless: a service is never closed because its owner is out (the Stardew annoyance); most travel happens while the party dives. Design notes in `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` section 9.
- 2026-11-03 (user): food and water as craftables with simple buffs and a cooking animation, separate from bread (basic healing); cooking opens when the innkeeper is rescued on Undercroft floor 5 (spec decision 47).

## Per-player loot, for plan 3 (2026-11-03)
From `docs/superpowers/plans/2026-11-03-per-player-loot.md` (decisions and review amendments). Plan 3 owns floor content, the bank room and `DiveStir`.
- 2026-11-03: **Per-player enemy drops (DONE 2026-11-14, claude/drops-and-hash).** Was: no enemy drops loot (only healing orbs and the shared gate key). The bag API takes any source; a drop plugs in at `DiveHaulSync.ServerAddItem(clientId, itemId)`.
- 2026-11-03: **CLOSED 2026-11-06 (stale-cleanup): the lost satchel is placed and picked up** (`CellarContentsSync.cs:942` checks `Satchel.ShouldPlaceOn` and calls `ServerPickUpSatchel`; `LostSatchel.cs:42`; `DiveHaulSync.cs:460`). Was: place the lost satchel on its floor and pick it up (plan 3, floor content). Today the satchel is stored and saved only (`PlayerBag.Satchel.Waiting`); `SatchelSlot.ShouldPlaceOn(floor)` and `PickUp` are ready in Core.
- 2026-11-03: **DONE (already persisted as `dive.extraSlotOwner`; bad-value test added 2026-11-14).** Save the 3-player extra-slot owner (plan 3). `DiveHaulSync._previousExtraOwner` lives in host memory for the session only, so a restart can give the extra bank slot to the same player twice in a row.
- 2026-11-03: **Bank room UI for picks, and the decision-4 auto-fill.** Picks work by RPC (`PlayerBagSync.RequestPick`) but no screen drives them. The party bank action fills a player's empty slots with their highest-ranked bankable items so a player with no UI still banks something; plan 3's bank room may drop that auto-fill.
- 2026-11-03: **CLOSED 2026-11-06 (stale-cleanup): `DiveHaul` is deleted** in `469155da` ("Retire the old dive"; no `class DiveHaul` left in `Assets/_Project`). Was: retire `DiveHaul`'s legacy loot and the world-wide `stock.loot` / `dive.bankedLoot`.** They stay only so old saves keep their shared loot as town storage; no new loot enters them. Plan 3 deletes them with `DiveHaul`.
- 2026-11-03: **A chest opened with a full bag loses the item.** The chest still counts as opened for that player and the roll is reported (`LastLootItem`), but nothing lands in the bag. Decide on a drop-at-feet, a swap prompt, or refusing the open.
- 2026-11-03: **DONE 2026-11-14 (bag-bank-robustness, `BankSlots.Allocate`).** Bank visit under capacity when the party shrinks mid-floor.** Visits are sized when the floor is entered (or a late joiner's pick): 4 players dropping to 2 leaves each with 1 slot, so the party banks fewer items than the 4-slot total for 2 players.
- 2026-11-03: **DONE 2026-11-14 (already re-resolved on roster key change; pinned by test).** Sticky `PlayerBagSync._bagId`. If a stray bag existed under another id before the client's roster key arrived, the owner-only view stays bound to it. Re-resolve the id when the roster key changes.
- 2026-11-03: **DONE 2026-11-14 (parts salvaged: `WorldSaveStore` ElementMemberSections).** One mistyped value inside a bag entry loses that player's whole bag** (a hand-edited file only: that entry is dropped, never the file). Could keep the parts that parse.
- 2026-11-03: **DONE 2026-11-14 (restored carried satchel takes a slot; else stays waiting).** The satchel is not counted against `PlayerBag.MaxCarried`.** A player carrying a full bag plus a picked-up satchel can hold more than the cap.
- 2026-11-03: **Save-and-quit at a stair needs its own bag save rule** (spec decision 24). Today a mid-dive save writes every bag as a failed dive at the current floor, which is wrong for a player who quit between floors on purpose.
- 2026-11-03: **CLOSED 2026-11-06 (stale-cleanup): valuables now feed stir** (`DiveStir.cs:13,34`, `PerUnbankedValuable = 5`; `DiveHaulSync.cs:210` sets `UnbankedValuables`), so the 70/100 thresholds are reachable; `DiveStirTests` cover them. Was: floors-only stir cannot reach the hunter (70) or deep-closes (100) thresholds.** Pieces no longer feed stir, so floors alone cap it at 52.5 in the live 4-floor dive. Valuables-driven stir is plan 3's `DiveStir`; PlayMode tests that need the high thresholds pass a `StirTuning` with a high `PerFloorCleared`.
- 2026-11-03: **DONE 2026-11-14.** A late joiner's bank visit can lift the party's slots above `BankSlots.TotalSlots`.** `EnsureVisit` sizes the joiner's visit for the party as it now stands and leaves the others' visits as they were (the spec is silent).
- 2026-11-05: gold update: chest purses now reach per-player gold (`CellarLoot.GoldPurse`); enemy-kill gold, stash view and town emptying still open. Design note with placeholder numbers: `docs/superpowers/specs/2026-11-05-queue-triage-notes.md` section C1.
- 2026-11-03: PARTLY CLOSED 2026-11-06 (stale-cleanup): gold sources now exist for chest purses (`CellarContentsSync.cs:1005,1021`) and room-clear payouts (`CellarContentsSync.cs:1047`, `DiveHaulSync.cs:200`). STILL OPEN: enemy-kill gold (no kill caller of `ServerAddGold` found), a stash view (`BagView.StashTotal` is synced, `PlayerBagSync.cs:47`, nothing shows it) and emptying the satchel in town. Original: per-player gold has no source yet: nothing outside tests calls DiveHaulSync.ServerAddGold. Plan 3 needs gold sources (enemy kills, chests), a stash/satchel view (BagView.StashTotal is synced, nothing shows it) and emptying the satchel in town (decision 35).
- 2026-11-03: CLOSED 2026-11-06 (stale-cleanup): `DiveStir` replaced `StirMeter`; valuables carried can fall (`UnbankedValuables` setter refreshes, `DiveStir.cs:23`) and `OnPushOn` resets the latches per half (`DiveStir.cs:71-80`). Original: floors-only stir never falls during a dive (StirMeter re-arms only below threshold, which now happens only at ResetAll), so deep-closes cannot fire a second time in one dive; CellarContentsNetworkTests works around it with a fresh ServerBegin. Plan 3's DiveStir settles it.
- 2026-11-03: PHASE1 PART CLOSED 2026-11-06 (stale-cleanup): PHASE1 7.2 carries the current stir rule (`DiveStir.cs`) and a supersede note on the shared haul. STILL OPEN (kept docs-only to skip a full check): the two comments `AssetPaths.cs:18` ("shared haul summary") and `PrefabBuilder.cs:38` ("host owns the shared haul") should say per-player bags; fix when those files are next touched. Test names with 'Haul' left as they are. Original: stale 'shared haul' wording left in comments (DiveHaulSync.cs ~428, DiveHaul.cs:1, DiveState.cs:1, AssetPaths.cs:18, PrefabBuilder.cs:38, DiveWalkQaTests.cs:242, DisconnectRejoinTests.cs:223) and test names (Chest_ClientRpcOpensIt_LootInHaul_..., Case1_...CarryingPieces...); fix when those files are next touched. PHASE1.md 7.2 stir formula still has '+5 per unbanked piece' (section is marked as built in M5).
- 2026-11-03: HUD 'N home' undercounts pieces after a reload (restored pieces are _home, which BankedPieceCount does not count); Summary.BankedPieces comment is wrong.
- 2026-11-03: an absent (disconnected) player's bag follows the party through push-on, go-home and fail (DisconnectRejoinTests case 1 pins it); spec silent.
- 2026-11-03: DONE 2026-11-14 (host bag save test added): test gap: the host's own bag is never saved and reloaded in a test (Save_BagsSurviveAWorldReload covers a client's bag); the code path is the same.

- 2026-11-14 (bag-bank-robustness review, open): (1) after a partial-party resume, a returning player keeps saved picks while the present players grew slots, so the bank can reach 5 picks vs `TotalSlots` 4 (`BankSlots.cs:39-66`, `DiveSession.cs:107-121`); no item lost, user picks whether TotalSlots or "never take a pick" wins. (2) per-part salvage covers only `bags.players`, not `suspend.bags` records. (3) stale "drop from the end" comments: `DiveHaulSync.cs:15,584`, `SuspendSave.cs:282`, `PlayerBag.cs:244`, `DiveHaulNetworkTests.cs:231,268`. (4) 3-player extra-slot owner can be recorded though they lost the slot (`BankSlots.cs:52-64`). (5) five new tests also pass on old code (`BankRoomRulesTests.cs:155,188`, `WorldSaveStoreTests.cs:755`, `PerPlayerLootNetworkTests.cs:808,957`).

## Triage 2026-11-04 (against merges since Nov 1)
- Per-player loot section above: all items stay open and are owned by plan 3 (`docs/superpowers/plans/` roadmap row 3). Nothing closed by the plan 2 merge `ee9435ef`; 2 stale "shared haul" comments remain (`AssetPaths.cs:18`, `PrefabBuilder.cs:38`); see the 2026-11-03 entry above.
- 2026-11-01 far tree line / hedgerow for the low Hollowmere cameras: the backdrop form merged in `894b46df` (visual review A12) may have covered it. Re-check on the next Hollowmere capture round before building anything.
- 2026-11-02 F7 (`StairHandoverTests` leaves `TestHubGround`, line 230): still open, a one-line teardown fix for the next test cleanup.
- Re-filed nowhere else: Art, Tooling and Town life items are unaffected by the Nov 1-4 merges (Hollowmere polish, junctions, rooms v2, story bible).

- 2026-11-06 art direction (user): props may lean toward a Hobbit / Lord of the Rings / classic-fantasy film look where it fits (hand-made timber, round doors, carved wood, lived-in clutter). Props only, not people. Fold into the style bible (docs/PHASE1.md 3) at the next art pass.

## Character and asset tools to trial later (2026-11-17, user)
- **MetahumanMaker** (github.com/TREE-Ind/MetahumanMaker, MIT Blender add-on): more character heads than the single Tobin preset. Revisit when we come back to character models. Assessment: `docs/research/metahumanmaker-unity-assessment.md` (fit/sculpt/skin bakes work in Blender; any rigged Unity asset needs UE 5.8 + MetaHuman Creator, then a GLB with sampled facial morphs, no RigLogic; licence for Unity use looks OK under the UE EULA since 5.6 but is unverified against Epic's own pages; photoreal, so heads need correcting toward our style). Pilot when revisited: one Opus local session, one Hunyuan head end to end to a GLB beside Tobin, stop at the first failed gate.
- **Patch** (patch.gg/blender, cloud Blender, 500 free credits): test run on a few assets after the Run 7 T list finishes; complements the Megascans additions. No rush. A Haiku batch trial was suggested earlier (CURRENT, 2026-11-17).
