# 10 - Red team: Hearthdeep dungeon design (2026-11-03)

Reviewed: `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` (the spec, read fully), `docs/research/dungeon-design-deep-dive.md`, notes 00, 02, 03, 03b, 04b, 05, 07, 07b (grep and line ranges), `docs/design/dark-cloud-lessons.md`, STORY 5, 6 and 8.3-8.4, PHASE1 1, 2, 6, 7.2, 10 and the change log (A16), and the code headers of `Core/Cellars/StirMeter.cs`, `StirTuning.cs`, `DiveState.cs` and `CellarFloorLayout.cs`. No web was used; everything below comes from the repo plus reasoning. Each finding is tagged [E] (evidence in the repo's research) or [R] (reasoning).

"Settled" means the finding touches a section 2 user decision. In those cases I name the smallest change that keeps the user's intent.

---

## 1. Top 10 findings, ranked by severity

### F1. Time is now the only real stake, and the design makes players pay it. This re-creates DC's backtracking and length complaints. [E+R] (settled: decisions 3, 11, 13; section 4.1)
**The hole.** These rules combine:
- every dive starts at the entrance, with no floor select (4.1);
- every dive generates fresh floors (dec. 11);
- pieces and people are safe the moment they're picked up (dec. 10);
- weapon growth is kept on failure (4.3);
- failure costs only unbanked loot plus half the dive's gold (dec. 7);
- the shortcut to the bank opens only after the boss (dec. 13).

Together they remove almost every stake *except time*, then charge a lot of time:
- A second-half wipe on Undercroft floor 7, 8 or 9, or at the Bell-Warden, sends the party back to floor 1. That means four generated floors plus the floor-5 set piece again: about 35-45 min at the research's own budget of 4-7 min per floor plus 8-10 min per set piece ([E] synthesis 4.2.14).
- In the later 12-16-floor dungeons (section 8), losing to a boss costs 60-90+ min of replay.

This is DC complaint #2, "too long, padded, gets longer late" (about 14 sources, [E] 03b section 4). It also reverses the answer `dark-cloud-lessons.md` already gave:
- "return dives start at the deepest reached checkpoint";
- "Tension comes from carrying loot, not from losing time".

It is also harsher than DC1 itself, which let you restart from any cleared floor ([E] 03 1.1, Chalgyr and GameRevolution).

**It also makes the bank choice coercive.** Choosing "go home" doesn't cost loot. It costs a full replay of the front half on the next dive. So players push on to avoid the replay, not because they weighed the risk. Success criterion 3 ("a choice players argue about") turns into "we can't afford to go home".

**Smallest fix (keeps "one bank, no other exits, a real push-or-go-home").** Once a party has *reached* the bank in some dive, a later dive may start at the bank:
- with an empty haul, stir at 0 and a fresh seed;
- the bank's slots and choice apply as normal.

Failure still costs the loot and half the gold; it just stops costing the replay. This moves the shortcut in decision 13 from "boss beaten" to "bank reached". The post-boss shortcut becomes the *town-to-bank walk* (a cosmetic and convenience link) instead of the first access.

If the user wants decision 13 exactly as written, the minimum is a **boss-door rule**: a wipe *at the boss* puts the next dive at the boss door, with the full failure penalty. **cheap** (rule plus DiveRoute). Touches dec. 13 and 4.1.

### F2. The stir and collapse maths was built for 4 floors and one checkpoint. It breaks on 10-16-floor single dives. [E code + R] (proposal 4.5, dec. 19)
**What the code does.** `StirTuning.Default` is +10 per floor cleared. The floor term resets only on a town bank or a failed dive (`StirMeter.cs` header). Deep-closes latches at 100.
**What follows.** With the proposed square-root player factor (x2 at four players):
- a 4-player party reaches 100 at the end of floor 5, the bank;
- a solo player reaches it on floor 10, the boss;
- in a 14-floor dungeon, solo hits 100 around floor 10, that is, mid-second-half.

**The unanswered case.** The collapse timer is "path time to the nearest bank point or boss stair x1.5". From floors 6-13 the bank is *behind* the party, and there's no going back up. The boss stair is floors away. The spec never says what reaching *this* floor's stair does while the deep is closing. Read literally, stir 100 in the second half is a guaranteed fail.

**Two smaller problems.**
- The spec doesn't say whether the halfway bank lowers stir. The current code's bank press does lower it, but the spec only says "the hunter leaves".
- Pieces left the inputs, so stir in the front half is now almost purely a floor counter. That's a clock, not a decision.

**Fix (decide before coding `StirMeter` v2):**
- stir is a **per-half ramp** that the bank resets to 0 (or to a floor value);
- the deep closing targets the *current floor's stair*, and reaching it drops stir to the hunter tier;
- per-floor increments come from the half length, so each half ramps 0 to 100 at about its last floor for a solo player, and the square-root factor pulls 4-player parties earlier by design, not by accident;
- keep "valuables carried" as the input players control.

**cheap** (numbers and one rule). Not settled; decision 19 names only the inputs.

### F3. Phase 1 ends exactly where the central decision begins, so M7 can't test it. [R] (settled: dec. 17)
Decision 17 ends Phase 1 at the bank. Push on leads nowhere, because floors 6+ aren't built. And PHASE1 P4 says only the four heirlooms exist in Phase 1, so there are almost no bankable weapons. What sits in the slots is attachments, if they count as "larger items", and maybe bombs and potions, which don't exist yet.

So the spec's own open question 1 ("is the tension enough... check in the M7 walk") and success criterion 3 can't be measured. Phase 1 would sign off on the dive rules without ever exercising "push on".

**Smallest fix (keeps the user's story and art end point at the baker):**
- add **2 greybox floors past the bank**, reusing the water rooms with the bubbling variant, plus a stub end (a "boss door" that simply ends the dive and brings the haul home);
- add **one bankable item class that matters**: a non-heirloom weapon drop, or attachments explicitly counted as bankable.

**cheap-to-medium.** Touches dec. 17's scope line, not its intent (the refinement pass still follows).

### F4. The first-hour hook depends on a decision the rules forbid, and its first wall costs 40 minutes. [R] (settled: dec. 3)
**The contradiction.** Section 5's hook, step 4, is "the push decision on floor 4 while carrying loot". Under decision 3 there's no exit before floor 5, so floor 4 offers no decision at all.
**The cost.** The first save point arrives after about 35-45 min: floor 1 authored, 2-4 generated, then the floor-5 oven maze and a 600-HP super-armoured Dough Warden (PHASE1 6.6). Healing is 3-5 loaves (dec. 20), because healing points become optional rooms. A new player who wipes on the Warden replays floors 1-4 on new layouts. That happens in the session that "must hook".

**Fixes (pick one; neither adds an exit):**
- **(a)** the game's first dive can't fail: a wipe revives the party at the floor's start, once, as the dream "spitting them back";
- **(b)** a general **set-piece retry**: a wipe inside a set piece restarts that set piece once per dive, at the full gold penalty.

Also rewrite hook step 4 as "carrying loot into the oven maze". **cheap.**

### F5. There's no suspend in the second half, but dives of 80-130 minutes need one. [E+R] (settled: dec. 12)
Halfway is the only save-and-quit point. A quit anywhere else counts as abandoning, which is a failure (4.6).

**Session-length estimate (from the research's floor budget):**
- the Undercroft's second half, floors 6-10, takes 40-45 min;
- a later dungeon's second half (floors 7-16) takes 50-70 min;
- a full later-dungeon dive from the entrance takes 100-130 min.

Research puts co-op group sessions at 30-90 min ([E] synthesis 4.2.14). Returnal's loudest complaint was no mid-run save-and-quit; Housemarque patched in a suspend ([E] 02 line 79). In co-op, the host leaving fails the dive for everyone. The host is also always the creator (PHASE1 A16).

**Smallest fix (keeps "can't be used to cheese"):** **save-and-quit at any stair**. The stair is already a gather ring between floors, so nobody can quit mid-fight. It's a one-shot suspend that's deleted on load. A mid-floor quit stays an abandon. This reuses the crash saver's per-floor snapshot (4.6). It also shrinks the job of the "real crash or force-close" heuristic, which then only has to judge mid-floor exits.

The heuristic itself treats a hang that the user ends in Task Manager as an abandon, because a hang leaves no crash evidence. Hangs are a common kind of "crash" for players. **cheap** (the snapshot exists). Touches dec. 12 and dec. 21.

### F6. Who owns loot is undefined, and per-player chest rolls contradict the shared haul. [R] (proposal 4.2 and 4.7)
The spec says three things that don't fit together:
- 4.7: chest loot is rolled per player (instanced);
- 4.2: each player assigns slots "from the shared haul";
- PHASE1 7.2: the haul is shared and held by the host.

Weapons are effectively per hero: an axe matters only to Tobin's player. So a "shared" weapon pool is a fiction that will turn into a slot-ownership argument the design didn't intend.

Nothing says where a banked weapon *goes*: a per-player inventory keyed by persistent player id, or a world stash. Nothing says what happens to it when its player is absent next session (dec. "any subset"). This decides the bank UI, the world-save schema, and what the crash snapshot must hold.

**Decide now:**
- per-player inventory sections in the world save, keyed by persistent id (A34 already moved weapons there);
- plus a world-shared stash for usables and large items;
- a player's slots bank only their own items or shared items, never another player's personal item.

**cheap** (decision), save work is **later** (risk-reviewer).

### F7. One host flag per gimmick creates co-op griefing, softlock paths and a model that doesn't survive dungeon 2. [R] (approved: dec. 22, table 3.2)
**Co-op.** A floor-wide water level means one player at a valve floods or drains the room another player is fighting in, or cuts off a downed ally. That's the Water Temple in co-op.
**Softlocks.** The validator proves only static reachability (connected, exits reachable, key reachable: PHASE1 M5). Gimmick states make reachability depend on state, for example:
- the key carrier dies in a flooded channel;
- a two-valve seal combined with a room that exists only at low tide.

The validator needs to prove that a state sequence reaches the stair from every reachable state, and that the key or objective is never stranded.
**The model breaks by dungeon 2.** "One host-owned state, changed by a single interaction" already fails for later dungeons in the table:
- tides run on a *clock*;
- the Glow Seams have *two* states (pressure and carts);
- the Orrery rotates *per room*;
- wax is *per area*.

**Fixes:**
- **(a)** gimmick state is a **small typed per-region state vector**, seeded per room so a late joiner can rebuild one room (synthesis section 6), with a dungeon-level driver that's either an interaction or a clock;
- **(b)** controls act on their *own region*, with a visible pipe or flue run;
- **(c)** add state-aware reachability to the Soak tier.

Decision 22 approved the *gimmick table*; this changes only the implementation model. **cheap** to decide now, **big** if it's decided after 40 rooms exist.

### F8. Rescuing the baker removes free bread, so the first reward reads as a nerf. It also brings back DC's attrition softlock. [R] (settled: dec. 8)
Decision 8: before the baker's rescue, every dive starts with 3-5 loaves. After it, "a player who runs out gathers resources for the baker".

So the first rescue in the game *takes away* free healing. A broke player with no bread also has to farm before they can dive safely: DC complaint #7, attrition meters and running out of money (about 7 sources, [E] 03b section 4). The research's rule was "the hearth always gives a minimal kit; never softlock" ([E] 03 #4).

**Smallest fix within decision 8's wording:** the baker keeps a **free daily or per-dive ration** (the same 3-5 loaves) and *sells or bakes more and better bread*. Gathering buys extra, never the minimum. **cheap.**

### F9. "Longer later dungeons" and "variants are longer" turn the cheap way to add length (more generated floors) into the main source of sameness. [E+R] (settled: dec. 1, dec. 15)
**What the spec asks for.** Section 8 sets later dungeons at 12-16 floors, and dec. 15 makes variants "longer". Generated floors are the cheapest content to add, so length will be bought in generated floors.
**Why that's a problem.**
- The research says floors don't carry replay: room shufflers get about 20 h before "too few rooms" ([E] 02 line 120), and sameness is perceptual ([E] 07b Compton).
- "Gets longer late" is the literal DC complaint, and Rogue Galaxy's dungeons "get exponentially longer toward the end" ([E] 03 1.3).

The Hearthdeep, fully authored and the most expensive dungeon, comes last, when the budget is thinnest.

**Smallest fix keeping "long game, long dungeons":** fix the **dive length by time, not floors**. Every dungeon's entrance-to-bank half targets about 35-45 min. Later dungeons get *richer* floors: more gimmick states, events, a mid-half set piece. Variants are *different*: new rules, remixed rosters, an alternate path, never just more floors. The game stays long through six dungeons, their returns and the town. **cheap** (a rule).

### F10. Absent players come back weak: the "Isaac baby partner" problem through subset play. [R] (settled: "any subset")
Weapons grow by use, per hero. In a 4-recorded-player world where any subset plays, a player who misses three sessions comes back with a weapon far behind the dungeon. Co-op scaling counts players, not power. The research named the Remnant 2 power-gap clamp ([E] 05 line 118) and "no weak co-op partners" ([E] synthesis 2), but the spec has no catch-up rule.

**Fix:**
- a **catch-up floor**: weapons below the party's median gain growth at about 2x until they reach it;
- or growth from boss kills and set pieces credited world-wide to every recorded hero.

**cheap** (Core rule), and it matters before 3-4-player playtests.

**Near misses (real, but below the top 10):**
- **Repeated authored floors.** Undercroft floors 5, 8 and 9 replay their puzzle every dive. Add per-world "solved" flags so a beaten set piece collapses to a short traversal with its rescue already done.
- **Split objectives.** The two-valve seal and hold-the-pump split a 4-player party into pairs, each facing rooms scaled for 4 players.
- **Tight rooms.** The smallest room is 2x3 cells (8 x 12 m), against a 5 m camera boom, 4 heroes and 8 enemies. Size the room minimum from the 4-player case, not from solo.
- **Full-clear pressure.** "Pieces left" counters with no time input to stir mean optimal play is a full clear of every floor, which blows the floor-time budget. Either add a light time or room term to stir, or carry over the "pieces on this floor" glint after about 60 s.
- **Missing amendment.** Section 10 doesn't amend PHASE1 P23 (scaling 1.7/2.3/2.8 plus one enemy per room), which conflicts with the 4.7 table.
- **STORY contradiction.** STORY already contradicts itself on where the Gleaners first appear: Saltwake in section 6, the Undercroft's Tithe Roots in 8.4. Spec 3.1 puts them in every dungeon. Fix it in the STORY amendment.
- **The 3-player slot.** The random extra slot plus a save field is the most complicated answer to "4 slots for 3 players". A shared 4th "party slot" that the host assigns does the same with no RNG among friends (dec. 5; low priority, mention only).

---
## 2. Scorecards

### 2a. Common complaints about procedurally generated dungeons (built from notes 02, 04b, 05, 07, 07b and 09)
| Complaint | Status | Where | Fix |
|---|---|---|---|
| Sameness, perceptual | **half** | Within a floor it's strongly answered: named bundle, objective never repeated twice in a row, modifier, gimmick A/B, elite. Structurally it's made worse: entrance restarts mean the front-half pool is seen 2-4x more often than the back half (F1). The Undercroft heat half has only 2 generated floors (6-7) but plans 20+ rooms. | F1 restart anchor. Measure room-seen counts per *campaign*, not per dive (extend the Soak fingerprint, synthesis section 7). |
| Padding and length | **re-created** | 4.1 entrance starts; section 8 "longer"; dec. 15 variants "longer" | F1, F9 (time budget per half) |
| Unfair RNG | answered, with small leftovers | Key carrier telegraphed, modifiers stated, per-player chests. The 3-player random slot is the leftover. | Party slot (near misses) |
| Softlocks | **half** | Static validator only; state-dependent gimmicks; key carrier in water; tide-only rooms | F7(c) state-aware reachability Soak |
| Backtracking and re-clearing | **re-created** | No floor select; set pieces replayed; boss loss means a full replay | F1, "solved" set-piece flags |
| Objective repetition | half | 4 types per dungeon, Undercroft-specific. With 3 generated floors per half you meet each type about 4x within 5 dives. | One new objective per later dungeon from its gimmick, and events with choices (synthesis 4.3.2) as a 5th "objective" |
| Loss feels unfair | **half** | Loot and gold loss is mild, but the hidden cost (time) is large, and it lands hardest on boss wipes | F1, F4 |
| Co-op friction | **half** | Good: exits-only locks, gather ring, per-player loot, no N-player requirement. Missing: global gimmick toggles (F7), split objectives, host quit fails everyone, no mid-dive join while dives run 80-130 min (F5), ownership (F6), catch-up (F10), the creator must be present (A16) | F5, F6, F7, F10; make mid-dive join required before 3-4 player tuning |
| Scaling | half | Sublinear table is good; the stir maths breaks (F2); P23 isn't amended | F2, amend P23 |
| 3D camera and readability | **not addressed** | Heavy atmosphere (steam, flour dust, fog), water hiding floor hazards, rooms 8 m wide, 12 bodies | Readability rule: telegraphs and hazards must read through fog and water (outline or rim, decals above the water line); size the room minimum for 4 players |
| Content exhaustion around 20 h | half | Six dungeons spread it out; weapons, events and the town carry it. But per-dungeon pools are hit hard by restarts, and there's no unlock-driven pool growth (02 line 151) | Pool growth tied to the world; the modding route (section 4) |
| Session length | **re-created** | Second half can't be suspended (F5) | Stair suspend |
| First-hour hook | **at risk** | Section 5 hook step 4 contradicts dec. 3; first save at 35-45 min | F4 |
| One-way pressure, dread | answered | Shakeable hunter; stir rewards | Keep |
| Missables | answered | Counters; deterministic side goals | Keep |

### 2b. Dark Cloud complaints (`dark-cloud-lessons.md` plus 03/03b ranked)
| # | DC complaint | Status | Where | Fix |
|---|---|---|---|---|
| 1 | Sameness, same pattern, same music | half | Strong per-dungeon identity (gimmick, tool, rule, music, set pieces). The structure over-exposes the front half. | F1, F9 |
| 2 | Too long, padded, longer late | **re-created** | Entrance starts; 12-16 floors later; longer variants | F1, F9 |
| 3 | Weapon breaking | answered | P7 no wear; growth kept on failure (4.3) | Keep |
| 4 | Forced hero floors, seals | answered | 7 "no required hero ability"; tools are shared items | Keep |
| 5 | Side-system bloat, RNG goals | answered | Deterministic side goals; food and water as a calm craft | Keep cooking optional |
| 6 | Weak combat makes repetition worse | out of scope | M3 | — |
| 7 | Attrition, softlock (money, supplies) | **re-created after the baker** | dec. 8 | F8 |
| 8 | Trivial puzzles, corridor dungeons | answered on paper | Gimmick and cross-room dependency, room cards. Risk: once solved, the "two-valve seal" becomes a walking errand. | Vary seal placement and add a twist per dungeon |
| 9 | Unclear gating, hard-to-find keys | answered | Key telegraph; sigil locks shown early | Keep; the journal lists seen sigils |
| 10 | Post-game ladder | half | "Longer, harder" variants smell like it | F9: variants differ, not lengthen |
| 11 | Restrictive town building | n/a (town) | — | — |
| 12 | Harsh death, lost files | half | Crash saver; a hang killed by the user counts as an abandon | F5 |
| L | "Long dungeons, few checkpoints; one death costs a lot" (lessons table) | **re-created** | one bank, entrance starts | F1 |
| L | "Backtracking and re-clearing is a slog" (lessons answer: floors stay cleared, return dives start at the checkpoint) | **reversed** | dec. 11 plus 4.1 | F1; update `dark-cloud-lessons.md` either way so the two docs agree |
| L | Camera in tight spaces | not addressed in the dungeon spec | room minimum | Near misses |
| L | Missables | answered | Counters | Keep |

---

## 3. Growth limits, and what to decide now

| Area | Where it stops scaling | Rewrite risk | Decide now |
|---|---|---|---|
| **Room format** | Single level, 14x14 grid of 4 m cells, a 4-bit door mask and identical connectors (PHASE1 7.2, `CellarFloorLayout.GridSize = 14`). The "feels 3D" win (balconies, pits), tides, mine drops and wax levels all need height. So do connector types (arch, gate, crawl, secret: synthesis 4.4). | **High.** Every room authored before the format grows has to be migrated. The plan implies about 200. | Room data v2 before room 13: per-cell level or height, connector type per door, gimmick-state layers (A/B or N), tool-lock sockets, enemy affordance tags (water, open floor, ceiling), a schema version. Make grid size a per-dungeon data value. Pilot one multi-level room. |
| **Gimmick state** | One global host flag (3.2) | High for dungeons 2-5 | Per-region typed state vector, interaction- or clock-driven; state-aware validator (F7) |
| **Floor graph and dive** | `DiveState.MaxFloor = 4`, checkpoint at 2, heart at 4 are constants. A linear graph. Stir floor mask is an int (fine to 31 floors). | Medium (the spec already lists the code changes) | Dive length and bank position become per-dungeon data. Allow a branch (an alternate path with a toll, synthesis 4.4.3) in the graph model now, even if unused. |
| **Stir** | Built for 4 floors (F2) | Low if fixed now | Per-half ramp |
| **Tools both ways** | Every earlier dungeon needs alcove sockets for every later tool. Adding a 5th tool later means revisiting every earlier pool. | Medium | Freeze the tool list as abstract lock verbs now (4: sluice/bellows, blast, smoke, star), with placeholder art. Section 3.3 item 1 depends on this. |
| **Cross-dungeon enemies** (variants, dream bleeds) | An eel needs water; a minecart enemy needs track | Medium | Habitat tags on enemies, affordance tags on rooms; variants pick only compatible pairs. Test the matrix in Soak. |
| **Save** | New sections the spec implies: Atla assignments x6 dungeons, the 3-player rotation, per-player inventories (F6), solved set-piece flags, seen sigils and tool locks, variant unlocks, the suspended dive snapshot, the root network, residents' travel | Medium (save migration) | One world-save schema plan with per-dungeon sections and version fields, reviewed once by risk-reviewer, before Phase 1 writes the bank fields. The snapshot must hold every input to generation (party HP if state-aware odds read it, 4.3.5). |
| **Lighting** | Realtime only (synthesis section 6). The heat half means many flickering fire lights; the water-first Undercroft needs reflections, and URP has no built-in SSR. | Medium | Per-room-prefab baked reflection probes (rooms are fixed geometry, so baking per room works), a shadowed-light budget per room in the room card, and GPU Resident Drawer from the start. Prove one flooded room's look early, because water comes first now. |
| **Networking** | Host-only save on the creator's PC (A16), so "any subset" must include the creator. No migration. Mid-dive join is "later" (4.6). | Medium | Make mid-dive join and rejoin a requirement before the 3-4 player playtests (dives of 80-130 min). Write a per-client mirror of each client's own gains (4.3.10 in the synthesis) at stair snapshots. |
| **Moving and rotating rooms** (Orrery, carts) | Owner-authoritative movement on moving parents | Low (the `sc-netcode` parenting-rider pattern exists) | Prototype in greybox when that dungeon's design starts, not before |

### Production estimate (what the plan implies; [R], rounded)
| Content | Per dungeon | x6 (5 generated plus the Hearthdeep) |
|---|---|---|
| Combat rooms (20+ per gimmick half, each with A/B states and 2-3 dressings) | Undercroft 40; later 30-40 | **about 170-200** |
| Authored floors (floor 1, 2 set pieces, the pre-boss floor, boss) | 4-5 | about 22 plus the Hearthdeep's 6-8 = **about 30** |
| Enemy species (3-4 per dungeon, plus Gleaner types and echo variants) | 4-5 | **about 25-30**, each with tiers, telegraphs and networked AI |
| Bosses and set-piece minibosses | 2-3 | **about 14-16** |
| Art kits at style-bible quality (the Cellars corner alone is milestone M9) | 1 | **6** (plus 6 towns) |
| Music (2 halves, 2 set pieces, boss, with stems) | 5 | **about 30** |
| Shared tools | 1 | 4 |

That is roughly DC2-sized dungeon content, a 3-4-year load for a 20-person studio. For this team, dungeon content (above all the art kits, not room geometry) is the binding constraint. **Decide now:** cost one dungeon at the end of the Undercroft the way M10 costs characters (log hours and usage per room, set piece, enemy and kit piece). Then set the per-dungeon budget from that number, not from the floor count.

---

## 4. Winning-position plan (ranked by impact)

1. **Make failure cost loot, not time (F1, F4, F5), before the dive rules are coded.** It's the single change that decides whether the loop reads as Dark Cloud fixed or Dark Cloud repeated.
   *First step:* one-page amendment for the user: bank-reached start, set-piece retry, stair suspend. Then build order step 1 implements those, not "start at the entrance".
2. **Make the core decision testable in Phase 1, and measure it.** A studio wouldn't sign off a loop it hasn't played (F3).
   *First step:* add "2 greybox floors past the bank plus a stub end, and one bankable item class" to the Phase 1 build order.
   *Metrics for M7, logged by the dev stats overlay (S5):* bank go-home vs push-on rate by party size, fail floor, minutes per floor, pieces per dive, room-seen counts, how many items sit in slots and how valuable they are.
   *Human test:* Compton's "name one memorable thing per floor".
3. **Freeze the content contracts before scaling content.** Studios build kits and grammar once, then produce. Covers room data v2, the gimmick state model, tool lock verbs and enemy habitat tags (section 3).
   *First step:* a short spec "room data v2" plus one multi-level, two-state pilot room through the builder, before any of the 8+ new Phase 1 rooms.
4. **Run content as an economy.**
   - Reuse: the dream-bleed rule already makes reuse into story. Make it a quota: about 30% of each later dungeon's rooms are digested echoes of earlier geometry with the new kit's dressing and gimmick layer.
   - Data-driven encounters: the 08 templates as data.
   - The objective/modifier/event layer is the cheapest variety.
   - Hades' warning applies: modular rooms can cost more than more rooms ([E] 07b), so pilot it.
   *First step:* a per-dungeon budget sheet filled from Undercroft actuals (section 3).
5. **A vertical slice that's also the demo.** Undercroft floors 1-5 plus the bank plus 2 push-on floors, the bakery rebuilt in view, at final art for one corner. That's the hook, the decision, and the "home changes" payoff in 45-60 min, which is what a Steam Next Fest demo needs.
   *Market:* the open niche is "a game like Dark Cloud" ([E] 03b 5.8). Emberville and Aeruta are 2D and solo. Lead with "3D co-op Dark Cloud successor: dive with friends, rebuild your town", show the bank argument in the trailer, and keep a wishlist page live from the demo on.
   *First step:* define the slice's done list as a PHASE1 amendment (after F3).
6. **Early access: yes, by chapter, but only once the save schema is versioned and the Undercroft plus Saltwake are complete.** Chaptered towns are natural EA drops, and co-op dive games build communities in EA (DRG, Lethal Company: [E] 04b context). The risk is story spoilers and save breaks.
   *First step:* make save migration tests part of `check`. They exist in pattern ("bad section keeps the rest"); add a version-upgrade test.
7. **Modding as the content-exhaustion answer.** No anti-cheat, plus rooms already authored as data grids, makes community rooms a real option: Workshop room packs, shared seeds, a weekly seed with a modifier set (synthesis 4.3.12).
   *First step:* keep the room format runtime-loadable and documented (contract item 3). Don't build mod tooling yet.
8. **Co-op robustness ahead of 3-4 player tuning.** Mid-dive join and rejoin, host-quit handling (stair suspend for everyone), a client-side mirror of each client's gains, and catch-up for absent players (F10).
   *First step:* risk-reviewer pass on a "co-op dive lifecycle" one-pager covering join, leave, host quit, suspend, crash, resume with a different subset.
   *Later:* Unity Relay bills by usage. Keep the transport swappable (Steam sockets) in case Relay costs bite at launch. Networking already sits behind the assembly boundary.
9. **A playtest cadence studios actually use.**
   - Every milestone: an internal 2-player walk plus one outside group of 2-4 players who haven't seen the game.
   - Watch them, don't explain.
   - Record the three metrics from move 2.
   - Every other milestone: the fixed-camera "which two floors are the same" screenshot test.
   *First step:* recruit two outside co-op groups for M7.
10. **Difficulty assists early** (synthesis 4.3.11): per-player assists only affect that player; world assists are host-set and shown to everyone. It's cheap, it widens the co-op friend-group audience, and it softens F4 for new players.

---

## 5. Strong parts of the design (don't touch)
- **One demon, six dungeons, one grammar.** The shared DNA plus a per-dungeon gimmick, tool and dive rule is the right answer to "nobody remembers a DC dungeon". Each later dive rule (tide clock, heat stir, sweetness lure, turning floor) is a real systemic hook, not a reskin.
- **Tools work both ways across dungeons, and the dream bleeds.** Reuse that tells the story is the best content economics in the spec.
- **Pieces safe on pickup, Atla-style depth assignment, counters, nothing missable.** Clean, and it ends the DC piece-loss frustration. It's only the combination with entrance starts that hurts (F1).
- **Banking rules.** Bank slots, with the save written after they're set, and keys are never bankable. Tidy and resistant to reloading.
- **The hard design rules** (section 7): no required hero, no required N-player action, every secret has a tell, side goals are deterministic, a gimmick changes with a single interaction.
- **Co-op rules** (4.7): exits-only locks, the gather ring that ignores fallen players, per-player chest loot, sublinear scaling, solo self-revive.
- **Higher stir also raises rewards.** That turns pressure into temptation, and the hunter can be shaken off.
- **The floor recipe order:** bundle, objective, layout, dead-end jobs, one modifier, elite, secret, card, map. It's the right big-decision set (Compton).
- **The music plan, and the boss theme that isn't a remix.**
- **The return ceremony and "the town changes the dive".**
- **The crash saver's idea** (snapshots at floor starts): reuse it for the stair suspend (F5).

---

## 6. Confidence
- **Evidence-backed:**
  - F1's complaint basis: DC #2, the dark-cloud-lessons reversal, DC1 letting you restart from a cleared floor (03, 03b);
  - F5's suspend precedent (02, Returnal);
  - F9's 20 h room-shuffler ceiling and "longer late" (02, 03);
  - F10's power-gap precedent (05);
  - F8's attrition complaint (03b);
  - F2's numbers, read from `StirTuning.Default` and the `StirMeter` header.
- **Reasoning (should be playtested or reviewed):**
  - the replay-time estimates, which rest on the research's 4-7 min per floor budget, not on measured play;
  - F3, which depends on whether attachments count as bankable;
  - F6 ownership (the spec may intend something unwritten);
  - F7's co-op griefing and softlock paths (no generator for state exists yet);
  - the production estimate (an order of magnitude only);
  - market and EA advice (no fresh market data; the web budget was spent).
- **Not checked:** the full `CellarRoom` door model, the M7 S3 rejoin implementation, and what Phase 1 actually banks today (`DiveHaul` loot kinds).
