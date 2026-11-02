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
- 2026-11-02 (review of the day's merges, `Reference/qa/review-2026-11-02.md`): low items for later. F3: `CoopDiveFastTests` should assert `run.MaxFloor >= 1` and `run.Seconds >= 90` (it can pass with the host bot stuck in the hub). F4 is routed to the dive-flow session's stair follow-up branch, not parked. F5: the dev overlay's per-room clear times should be keyed by (floor, room), with the first mask on a floor as the baseline. F7: `StairHandoverTests` should destroy `TestHubGround` in teardown.
