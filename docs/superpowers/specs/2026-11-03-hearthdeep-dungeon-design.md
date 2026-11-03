# Hearthdeep dungeons: design (2026-11-03)

Status: **DRAFT.** The user's decisions (section 2) are settled; decisions 23-30 (2026-11-03, the user's answers to the red-team review) replace older wording where they conflict. Everything marked *proposal* waits for the user's sign-off, and so do the PHASE1 and STORY amendments in section 10, which are listed but **not applied**.

Research behind this: `docs/research/dungeon-design-deep-dive.md` and its notes in `docs/research/dungeon-deep-dive/`. Design session in the cloud, 2026-11-02 to 11-03.

## 1. Intent
Dark Cloud players remember the loop, the weapons and the town, and remember no dungeon by name. Their dungeons read as one repeated pattern. Our dungeons should be the part players remember. Each town's dungeon should feel unique and separate, yet clearly the work of one sleeping demon. A dive should be a real push-or-go-home decision, not a series of checkpoints.

Success:
- a new player is hooked inside the first dungeon's first session;
- each dungeon is known by its own idea (its gimmick), not its tileset;
- the halfway bank is a choice players argue about in co-op.

## 2. User decisions (2026-11-02 / 11-03; settled)
1. **Long game, long dungeons.** This is not a short-run game. Dungeons can be long.
2. **Hollowmere's dungeon is one dungeon** that gets the game moving: 8-10 floors plus a boss floor, shorter than every later dungeon.
3. **One bank, halfway through the dive.** At the bank the party may go home with everything they carry; otherwise they push on. **There are no other exits**: no retreat outside the bank, no escape item. The risk is continuing or not.
4. **Bankable items:** weapons, usables such as bombs and potions, and other larger items. **Never bankable:** boss keys, gate keys and anything else that drives dungeon progress. Gold and bread are not bankable and don't count toward the limit.
5. **Bank slots by party size:**

   | Players | Slots |
   |---|---|
   | 1 | 3 |
   | 2 | 2 each (4) |
   | 3 | 4 in total: 1 each, plus one extra slot given to a randomly picked player, never the same player twice in a row |
   | 4 | 1 each (4) |

   Banking is a grace, not a necessity. Larger parties already have more revives.
6. **Saving at the bank happens after the bank items are set,** so the choice can't be changed by reloading.
7. **Gold:** a failed dive halves the gold earned in that dive; the gold you already had doesn't move.
8. **Bread** is the most basic healing item. Nothing happens to it when a dive fails. Until the baker is rescued, every dive starts with a set amount. After that, a player who runs out gathers resources for the baker. *(Refined by decision 27: the bread ladder.)*
9. **Food and water** are craftables, separate from bread, that give simple buffs, with a cooking animation (a calm town activity).
10. **Pieces and people work as in Dark Cloud:** spread across a dungeon's floors and safe the moment they're found (section 4.4).
11. **Every dive generates fresh floors** (a new seed).
12. **The halfway point is the save-and-quit point** for a dive. *(Widened by decision 24: save and quit at every floor.)*
13. **A shortcut from town to the halfway bank** opens once the dungeon's boss is beaten.
14. **A crash saver** (QoL, skip if it gets complicated): after a real crash, the party resumes at the start of the floor they were on and keeps their gold. It isn't meant as a cheese route.
15. **Reasons to return to old dungeons** (optional content, never required):
    - local materials, and bounties later on;
    - locks seen before their keys;
    - **variants as the dungeon's hard mode**: longer, with harder enemies and enemies from later dungeons once the player has met them there; variants unlock somewhat later in the game, not right away.
16. **Residents who travel between towns** once they're awake (to shops, to see friends, for the day's tasks), then home again. It must be seamless and must never get in the player's way: no closed shop because its owner is out (the Stardew annoyance). Town-side, section 9.
17. **Phase 1 ends at the halfway point** (the first bank; scope widened by decision 28). The end of Phase 1 includes a pass to refine everything built so far, so Phase 2 is smooth.
18. **Water first, heat second (2026-11-03).** The Undercroft's floors 1-5 use water; the later floors use heat. The water starts bubbling on floors 4 and 5, foreshadowing the heat below.
19. **Stir inputs** as in section 4.5: floors cleared plus unbanked valuables carried, square-root player scaling, rewards rising with stir.
20. **Bread at the start of a dive** before the baker is rescued: 3 to 5 loaves, to be tuned. *(These are the survivors' poor bread; decision 27.)*
21. **Keep the crash saver**, with a cheap check that tells a real crash from a force-close (section 4.6).
22. **The gimmick and tool table** (section 3.2) and **the root network** (3.3 item 5) are approved.

**Answers to the red-team review (user, 2026-11-03, later):**

23. **Failure costs time, and that is intended (F1 declined).** Players are meant to learn the systems and beat them: get better at combat, at when to leave or stay, and at what is really worth banking. Dark Souls is the model: little hand-holding, a real boss early, and death mostly costs time. There is no "start the dive at the bank" option. A dive leaves the dungeon only at the halfway bank, at completion, or by failing.
24. **Save and quit at every floor is standard (F5 accepted).** At each stair the party can save and quit; loading resumes at the start of the next floor. It is a pause, not an exit: the dive continues as it was. Section 4.6.
25. **A second try at the boss, for a price.** One retry per dive from the boss door. It must cost something: an item, some gold, or a secret idol the party found and activated in the dungeon. Which cost (or which mix) is open (section 11).
26. **The baker is rescued earlier** than the halfway floor, so baking and crafting open early. A party that wipes on floor 5 then feels the relief that the baker and every piece already found stay found. Section 5 places him on floor 3 (*proposal*).
27. **The bread ladder (F8 answered).** Section 4.8:
    - before the baker: 3-5 free loaves of the survivors' poorly baked bread, the weakest heal in the game, so players learn combat before leaning on heals;
    - after the baker: 3-5 free loaves per dive of his bread at the default heal value;
    - gathered materials let the baker bake more, carried in stacks of up to 10;
    - upgrading the baker upgrades the bread, so bread stays viable late in the game;
    - bread is never the best healing: potions and healing places (hot springs, fairy lights) do more, but a basic potion is never better than the best bread.
28. **Phase 1 scope (F3 accepted):** build to the halfway bank, plus two rough floors past it and a stand-in dungeon ending, so the push-on choice can be played.
29. **Dive length is set by time, not floors (F9 accepted).** With the right scaling, later dungeons don't need more floors; extra floors would just be waves of fodder. A dungeon gets longer only when a larger puzzle or set piece needs the room. Later dungeons and variants get richer floors, not more of them.
30. **The opening five minutes** get a story session with the user (pending). Building the mechanics and the base world first, so there is something to judge, was the right order.

## 3. The Hearthdeep: one demon, six dungeons
Each town's dungeon is a region of Mawrrow's dream digesting that town (STORY 2, 6).

### 3.1 Shared DNA (in every dungeon)
- **The breath:** red glow rising from below, stronger the deeper you go.
- **Stir:** the same meter and the same push-or-bank decision.
- **The Gleaners:** the demon's servants, the only enemies in every dungeon, released as stir rises.
- **The dream's remakes:** each dungeon is built from its town's own things turned into nightmares.
- **One grammar:** floor card, map, objective families, the halfway bank, set-piece exams. Players learn it once and read every new dungeon in it.

### 3.2 Unique per dungeon (*proposal*; gimmick and tool names are placeholders)
| Dungeon | Gimmick (one host-owned state, changed by a single interaction) | Shared tool it gives | Its own dive rule |
|---|---|---|---|
| Undercroft (Hollowmere) | Water first: still water levels set by valves and sluices (floors 1-5, bubbling on 4-5); then heat: ovens, flues, dampers, flour dust (floors 6-9) | Sluice key (first half), bellows (second half) | The baseline rules |
| Drowned Arcade (Saltwake) | Tides that rise and fall on a cycle | Tide bell (placeholder) | The tide is a second clock; some rooms exist only at low tide |
| Glow Seams | Pressure and minecart routing | Blasting charge | Heat raises stir faster |
| Hive Choir | Wax that hardens and melts; sweetness | Smoker | Sweetness lures enemies toward whoever carries the haul |
| Orrery | Rotating rooms | Star key | The floor turns between dives |
| Hearthdeep | All of the above | none | Fully authored, no generated floors (STORY 6) |

Each dungeon also gets 3-4 enemy species born from what its town lost, its own music, and its own set-piece endings.

Two cautions follow from putting water first in the Undercroft:
- **Keep it apart from Saltwake.** Hollowmere's water is still, set by valves (plumbing). Saltwake's is tidal, moving on a cycle (time). That's why the sluice tool moved to Hollowmere and Saltwake's tool is now a placeholder.
- **No teal in the Cellars** (PHASE1 P34). Flooded cellar floors read warm under lamplight; teal stays for the Cisterns' deeper water (`sc-water-look`).

### 3.3 How the dungeons connect
1. **Tools work both ways across dungeons.**
   - Forward: a dungeon's tool opens optional branches in every later dungeon.
   - Backward: earlier dungeons hold sealed alcoves stamped with a later dungeon's sigil. You see the lock in chapter 1 and get the key in a later chapter (decision 15).
2. **The dream bleeds.** Later dungeons contain digested echoes of earlier ones (Hollowmere barrels rusting in the Arcade's brine, a rat-king grown gills). This is asset and enemy reuse that tells the story.
3. **Variants (hard mode)** unlock later in the game. A variant is different, not longer (decision 29): new rules, an alternate path, harder enemies, and mixes in enemies from dungeons the player has already reached (decision 15).
4. **Local materials and bounties.** Each dungeon yields materials that only it produces, which later towns' residents and weapon build-ups want. Bounties come later.
5. *Proposal:* **the roots join everything.** Late in the game, root passages link the dungeons' halfway banks as a fast-travel network that ends in the Hearthdeep.

## 4. Dive rules
### 4.1 Shape of a dive
Every dive starts at the dungeon entrance. It runs from the entrance to the halfway set piece and the bank room, then on to the boss or the end, then home. There is no floor select. After the boss is beaten, the shortcut to the halfway bank opens (decision 13).

The way out of the dungeon is only the halfway bank, the end, or failure (decision 23). Two things soften the cost without adding an exit:
- **Save and quit at any stair** (decision 24): a pause between floors, resumed at the next floor's start (section 4.6).
- **One paid boss retry per dive** (decision 25): after a wipe at the boss, the party may pay the price and restart from the boss door with the haul they carried in. Without paying, the dive fails as normal.

Target length (decision 29, *proposal* for the numbers): about 40-50 minutes from the entrance to the bank and the same again from the bank to the boss, measured by the dev stats overlay in playtests.

### 4.2 The bank room (halfway)
Steps, in order:
1. The party rests: full heal.
2. Each player assigns their bank slots (section 2, decision 5) from the shared haul.
3. The party chooses: **go home** (everything carried comes home and the dive ends) or **push on**.
4. If they push on, the bank is locked and the game saves (decision 6). That save is the save-and-quit point.

The 3-player extra slot is picked by the host from the dive seed. The previous pick is stored in the world save so the same player is never picked twice in a row.

### 4.3 Failure
The dive fails when every present player is down (as built), or when the deep closes and the timer runs out. On failure:
- unbanked loot is lost;
- the gold earned in the dive is halved;
- bread is untouched;
- banked items and everything banked before stay;
- pieces and people found stay found (section 4.4);
- weapon growth earned in the dive is kept (*proposal*: the research found that no game players liked deletes long-term growth).

Time is the main stake, on purpose (decision 23): a wipe sends the party back to the entrance with fresh floors. A wipe at the boss can be bought back once per dive (decision 25).

### 4.4 Pieces and people (Dark Cloud style)
- When the world is created, each of a dungeon's pieces and people is assigned to a fixed floor, or to a random floor in the first or second half of the dungeon. The assignment is stored in the world save, the way DC1 does it (`dc1-rea.md`, Atla).
- Set-piece floors hold only their own story rescue.
- Floors are generated fresh each dive, but a piece's floor depth stays the same. It is placed somewhere on whatever floor is generated at that depth.
- A piece or person is **safe the moment it's picked up**. It goes straight to the town's stock and is not part of the at-risk haul.
- Each floor and the dungeon show "pieces left" and "people left" counters. Nothing can be missed.
- Key residents come home through the set-piece rescues (the baker at floor 3).
- *Open:* our decomp notes don't record DC1's rule on dying with an Atla in hand. "Safe on pickup" is the rule we're adopting, whatever DC1 did.

### 4.5 Stir
Pieces no longer feed stir, because they are safe once found. *Proposal* for stir's inputs:
- floors cleared this dive;
- unbanked valuables carried (weapons and large items);
- player count growing as the square root of players, not linearly.

Higher stir also **raises rewards** (better chest rolls, bonus materials), so pushing on pays. Its dangers stay: extra enemies, the hunter, the deep closing.

Stir over a long dive (red-team F2, folded in as *proposal*): stir builds up separately in each half and drops to 0 when the party pushes on from the bank, so the 10-floor dive doesn't hit 100 on its first half. The collapse timer aims at the current floor's stair, not a bank several floors away.

Collapse timer (*proposal*): path time to the current floor's stair (or the bank or boss door when that is on this floor), times 1.5, with a 60 s minimum, and closing early once every living player is there. The hunter can be shaken off, and it leaves when the party banks.

### 4.6 Stair suspend and crash saver (*proposal*, risk-reviewer)
**Stair suspend (decision 24).** At any stair the party may choose "save and quit". The game writes the next floor's start snapshot and marks it as a suspend. Loading resumes the dive at that floor's start, with the same seed, haul, dive gold, stir and bank state; the suspend is used up on load. Quitting anywhere other than a stair or the bank is an abandon (a failed dive), so quitting can't undo a bad fight. Co-op: only the host can suspend; any subset of the recorded players can resume it.

**Crash saver.**
- Each floor start writes a dive snapshot to the host's save: floor, seed, haul, dive gold, stir, bank state, and the players present.
- A menu quit writes a clean-exit marker.
  - A **menu "abandon dive"** counts as a failure.
  - Quitting at the halfway bank is the normal save-and-quit.
- If the game starts and finds a dive snapshot with no clean-exit marker, it offers **"resume at the start of floor N"**: gold kept, haul as it was at that floor's start.
- **Telling a crash from a force-close** (to verify in a Windows build):
  1. Write the clean-exit marker in `Application.quitting`. That callback runs for a menu quit and also for closing the window or Alt-F4, which send a normal close request. All of those count as a quit: during a dive (outside the halfway bank), that is an abandon.
  2. A real crash, or a process kill from Task Manager's Details tab, skips `Application.quitting`. To tell those two apart, the next launch checks whether the Unity player left crash evidence newer than the snapshot: a crash report folder or a crash marker in `Player-prev.log`. Crash evidence means resume; no evidence means the run is treated as abandoned.
  3. A plain process kill can't be told from a crash with certainty. Treating it as abandoned is the fair default, since a real crash leaves evidence. Never block a resume because of a doubtful check, and log the decision for support.
- Menu quitting can't be used to cheese.
- Co-op: the host's snapshot is the dive's snapshot. Resuming after a host crash rebuilds the floor from the seed with whichever recorded players join. A client who crashed rejoins the running dive; mid-dive join is later work.

### 4.7 Co-op rules (*proposal*, from the research)
- **Scaling** by the players present: enemy count 1.0 / 1.45 / 1.85 / 2.2, enemy HP 1.0 / 1.15 / 1.3 / 1.45, damage flat, bosses mostly HP. Solo gets one self-revive per dive.
- **Locks:** combat rooms lock their exits only, with a short "wait for the party" ring at the door. Downed or disconnected players aren't needed for the clear. Dead ends never lock.
- **Leaving a floor:** the stair works as a gather-to-leave ring that ignores fallen players.
- **Pings:** a small set of party marks.
- **Chest loot** is rolled per player, so nobody races to open chests.

### 4.8 Bread and healing (decision 27)
Bread is the free heal you can always count on and can upgrade. It is never the strongest heal.

| Tier | Where it comes from | Free per dive | Heal (*proposal*, against today's 30 HP loaf) |
|---|---|---|---|
| Survivors' bread (poorly baked) | the heroes' own baking, before the baker | 3-5 (tune) | about half a loaf (15 HP), the weakest heal in the game |
| Baker's bread | the baker, once rescued and housed with wishes met | 3-5 (tune) | the default loaf (30 HP) |
| Better bread | each baker upgrade | 3-5 | rises per upgrade |

- **Extra bread:** gathered materials let the baker bake more. A player carries a stack of up to 10.
- **Order of healing strength:** a basic potion is about equal to the best bread and never above it. Higher potions and healing places (hot springs, fairy lights, mending springs) are stronger than bread at the same point in the game.
- Bread is not bankable, doesn't count toward bank slots, and survives a failed dive (decisions 4 and 8).
- Bread found inside the dive keeps working as built (`DiveLoafLedger`).

## 5. Hollowmere's dungeon: the Undercroft (10 floors)
*Proposal*: the layout that fits decision 2 and keeps STORY's beats.

| Floor | What | Gimmick | Objective |
|---|---|---|---|
| 1 | **Authored teaching floor:** the sluice key, a valve that raises and lowers a flooded cellar, the key carrier; a warm red glow seen through a drain grate; a sealed alcove with a sigil the player doesn't know yet | Water | Key carrier (taught) |
| 2 | Generated larders and cold rooms | Water | Varied (section 6) |
| **3** | **The drowned bakery set piece (decision 26):** the taken bakery, flooded and cold, its ovens drowned. Route the water to reach the sleeping baker. A short set piece with a lighter guard than a boss. **The baker wakes** and is safe from here on (section 4.4). | Water exam | Rescue |
| 4 | Generated wine vaults and upper cistern channels; **the water starts bubbling**: steam vents, warm pools that hurt | Water, bubbling | Varied |
| **5** | **Halfway set piece:** the cistern dome, boiling from below. The **Dough Warden** (grown from the bakery's stolen dough, risen in the heat) guards it. A second sleeper to rescue (*proposal*: the innkeeper, ring 1; for the story session). **Then the bank room (section 4.2).** | Water exam, heat foreshadowed | Rescue and guardian |
| 6-7 | Generated coal cellars and flues, near the demon's breath; the bellows; dampers and ovens | Heat | Varied |
| 8 | **Feast hall set piece:** the sunk feast laid out under braziers that never go out; work the dampers and flames to reach the tally with four blank names (STORY 8.4) | Heat exam | Story |
| 9 | **The root wall:** the Tithe Roots as one authored floor; Tobin's father can't be carried out yet | Roots and light | Cut through |
| 10 | **The Bell-Warden** (STORY 8.4) | All | Boss (paid retry, decision 25) |

**Phase 1 builds floors 1-5 and the bank room, then floors 6-7 as rough greybox and a stand-in ending** (decision 28). The stand-in ending is a door on floor 7 that ends the dive as a success and brings the haul home, so "push on" can be played and measured. Phase 1 also needs one bankable item class that matters (a non-heirloom weapon drop, or attachments counted as bankable; *open*).

**The first session it should give** (the hook):
1. The opening in Hollowmere (story session pending, decision 30) and Wren's list.
2. The authored floor 1.
3. Floor 2: the first weapon level-up. The survivors' poor bread heals only a little, so the player learns to dodge.
4. Floor 3: the drowned bakery. **The baker wakes**, and from here on he is safe whatever happens.
5. Floor 4: carrying loot through the bubbling floor toward the guarded bank. There is no exit yet, so this is tension, not a decision.
6. Floor 5: the Dough Warden in the boiling dome, a second sleeper, then the bank room.
7. Go home, or push on into the two rough heat floors.
8. Back in town, the bakery is rebuilt in view, and the baker's bread is better than anything the heroes could bake.

## 6. Floor recipe (generated floors; *proposal*)
Built in this order from the host seed:

1. **Bundle.** Sub-theme, light, roster subset and hazard flavour. Most of this exists; what's new is giving the bundle a name (for example "The Smoking Larder").
2. **Objective.** Never the same type twice in a row:
   - key carrier (exists), now telegraphed;
   - **two-valve seal** (water half) or **damper seal** (heat half): two controls in different rooms open the stair (the link between rooms that Chamber Dungeons lacked);
   - **hold the pump** (water half) or **hold the hearth** (heat half): defend a point while it works;
   - **swarm:** clear every room.
3. **Layout preset** (exists), then the rooms are placed.
4. **Jobs for dead ends, farthest first:** vault (the locked room), mending spring (likelier when the party is hurt), challenge hall (offered when the party is healthy), stir altar. These give the existing special-room kinds real effects.
5. **Modifier.** At most one per floor, always with a stated reward (for example "Stirred: +10 stir, double chests"). Some floors are guaranteed to have none.
6. **Encounters** built from templates. One elite with one affix per floor, from the second half on.
7. **One secret:** an empty grid pocket touching two or more rooms, marked by a crack and a glint.
8. **Floor card** on arrival: the floor's name, objective, modifier and reward, and the pieces and people left.
9. **Floor map:** fog of war by cell, door lock colours, a state per room, shared in co-op.

## 7. Rooms and encounters (*proposal*)
- **Room card:** one idea, a teach/use/twist beat, a landmark, a loop around a central obstacle, and a cover foothold inside the door.
- **Gimmick states:** each pool room has state A and state B (water up or down in the first half; dampers open or shut in the second), so one room plays two ways.
- **Pool size:** 12 combat rooms are enough for Phase 1. Grow to 20+ per gimmick half before a dungeon ships, adding dressing variants of existing geometry first.
- **Encounters:**
  - one enemy type for a breather, two for a normal fight, three for a complex one;
  - at most 8 live enemies with four players;
  - waves at most two deep;
  - templates in `docs/research/dungeon-deep-dive/08-encounters-arenas-bosses-audio.md` 7.1.
- **Music:** one piece per gimmick half, one per set piece, and a boss theme that isn't a remix. Combat stems come in and drop out; a drone layer rises with stir.
- **Rules from the research:**
  - no required hero ability;
  - no required N-player action;
  - every secret has a tell;
  - side goals are deterministic and never missable;
  - a gimmick changes with a single interaction.

## 8. Later dungeons
Each later dungeon targets the same time per half as the Undercroft (decision 29). Later dungeons get richer floors (more gimmick states, floor events, a set piece in each half), not more floors, and grow only when a larger puzzle needs room. Each has the same halfway bank and its own gimmick, tool and dive rule (section 3.2). Each is planned around its gimmick from the first design pass.

## 9. Town-side notes (not dungeon work; recorded here so they aren't lost)
- **Return ceremony** (*proposal*): three skippable beats of 8-12 s in total:
  1. the town changes in view;
  2. pieces and people delivered, with one resident line;
  3. a new verb or shop item appears.

  Every present player gets credit.
- **The town changes the dive** (*proposal*): the baker's bread, a lamp-keeper adding collapse seconds, a cartographer marking bank points, a smith mending weapons.
- **Residents who travel** (decision 16; *proposal* for making it seamless):
  - **A service is never closed because of a schedule.** When an owner is out, an apprentice, a ledger counter or a bell at the counter serves the player.
  - Most travel happens while the party is diving. Back in town, the player sees what changed (gossip, things bought, a visit paid).
  - Each resident's routine is short: home, one task place, home.

## 10. Proposed amendments (not applied; for sign-off)
- **STORY 8.4:** the Undercroft becomes 10 floors, as in section 5. Its story beats move: baker on floor 3 (the drowned bakery), the Dough Warden and a second sleeper on floor 5, the tally on floor 8, Tobin's father on floor 9, the boss on floor 10. The bands become water first (cellars and cisterns, floors 1-5), then heat (coal cellars, flues and the breath, floors 6-8), then the roots (floor 9). The band enemy lists are redistributed to match: drip-spirits and eels move up, and new heat creatures come in below.
- **PHASE1 section 1 and P1:** Phase 1 ends at the Undercroft's halfway bank (floor 5), plus floors 6-7 in rough greybox and a stand-in ending (decision 28), followed by a refinement pass.
- **PHASE1 7.2 and P19:** "floors stay cleared" becomes "fresh floors every dive; found pieces and people stay found". Pieces use Atla-style assignment (section 4.4).
- **PHASE1 7.2 and P20:** the checkpoint after floor 2 and the floor-4 bank point are replaced by the single halfway bank with slots (section 4.2). The failure rule becomes section 4.3.
- **PHASE1 P21 and P49:** stir inputs and the collapse timer as in section 4.5.
- **PHASE1 P27, P47 and P48:** healing is bread first, as the ladder in section 4.8 (survivors' bread before the baker, the baker's bread after, stacks of 10, upgrades). Healing points become an elective dead-end room. The bank room heals fully.
- **Code** (later, after sign-off):
  - `DiveHaul` (pieces leave the haul);
  - `StirMeter` and `StirTuning` (inputs, square-root players);
  - `DiveState` and `DiveRoute` (one bank, start at the entrance);
  - new bank slots and the world-save field for the 3-player rotation;
  - generator (an authored floor 1, floors 3 and 5 as set pieces, floors 6-7 and the stand-in ending).

## 11. Open questions
1. Is the tension right with time as the main stake? Check in the M7 walk, with the overlay's dive and floor times.
2. Verify the crash check (section 4.6) in a Windows build.
3. **The boss retry price** (decision 25): an item, gold, or a secret idol activated in the dungeon. *Recommendation:* the idol, hidden on one second-half floor with a tell, because it rewards exploring and skill rather than a full wallet; gold as the fallback when the idol wasn't found. Also open: does the floor-5 guardian (the Dough Warden) get the same paid retry?
4. *Proposal, Dark Souls style:* when a dive fails, the lost haul waits as an echo on the floor depth where the party fell; the next dive that reaches that depth can pick it up once. It keeps "failure costs time" but gives the run back a goal. For the user to accept or drop.
5. Red-team items still open: F6 (one bag per player instead of the host-held shared haul), F10 (catch-up for absent players' weapons). F2 is folded into section 4.5; F7 (validator checks every gimmick state) and room data v2 before room 13 stay as the previous session proposed.
6. Co-op fit (`docs/research/dungeon-deep-dive/11-coop-fit-and-dive-flow.md`, 2026-11-03): the time target per half (decision 29) and the stair suspend (decision 24) answer most of it. Still open: a recap for absent recorded players, co-op-shaped town time, and bank slots that shrink per player in a 4-player party.
7. The floor-5 second sleeper and the Warden's story reason: for the story session (decision 30).

## 12. Phase 1 build order (after sign-off)
1. Dive rules: start at the entrance, the halfway bank room with slots and the save, the failure rule, pieces Atla-style, the stair suspend, the paid boss retry, the bread ladder.
2. Floor map; co-op locks and the gather-to-leave stair.
3. Authored floor 1; floor 3 as the drowned bakery set piece; floor 5 as the halfway set piece with the Dough Warden; floors 6-7 in rough greybox and the stand-in ending.
4. Objective variety (two-valve seal, hold the pump) and the named floor card with two modifiers.
5. The water gimmick on the 12 rooms (bubbling variants for floor 4); encounter templates; music stems.
6. Jobs for dead ends; collapse fairness; the crash saver if it stays simple.
7. Refinement pass (decision 17).
