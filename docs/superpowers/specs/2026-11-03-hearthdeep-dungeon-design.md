# Hearthdeep dungeons: design (2026-11-03)

Status: **DRAFT.** The user's decisions (section 2) are settled. Everything marked *proposal* waits for the user's sign-off, and so do the PHASE1 and STORY amendments in section 10, which are listed but **not applied**.

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
8. **Bread** is the most basic healing item. Nothing happens to it when a dive fails. Until the baker is rescued, every dive starts with a set amount. After that, a player who runs out gathers resources for the baker.
9. **Food and water** are craftables, separate from bread, that give simple buffs, with a cooking animation (a calm town activity).
10. **Pieces and people work as in Dark Cloud:** spread across a dungeon's floors and safe the moment they're found (section 4.4).
11. **Every dive generates fresh floors** (a new seed).
12. **The halfway point is the save-and-quit point** for a dive.
13. **A shortcut from town to the halfway bank** opens once the dungeon's boss is beaten.
14. **A crash saver** (QoL, skip if it gets complicated): after a real crash, the party resumes at the start of the floor they were on and keeps their gold. It isn't meant as a cheese route.
15. **Reasons to return to old dungeons** (optional content, never required):
    - local materials, and bounties later on;
    - locks seen before their keys;
    - **variants as the dungeon's hard mode**: longer, with harder enemies and enemies from later dungeons once the player has met them there; variants unlock somewhat later in the game, not right away.
16. **Residents who travel between towns** once they're awake (to shops, to see friends, for the day's tasks), then home again. It must be seamless and must never get in the player's way: no closed shop because its owner is out (the Stardew annoyance). Town-side, section 9.
17. **Phase 1 ends at the halfway point** (the baker's rescue and the first bank). The end of Phase 1 includes a pass to refine everything built so far, so Phase 2 is smooth.

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
| Undercroft (Hollowmere) | Heat: dampers, ovens, flour dust; then water level in its lower half | Bellows | The baseline rules |
| Drowned Arcade (Saltwake) | Tides that rise and fall on a cycle | Sluice hook | The tide is a second clock; some rooms exist only at low tide |
| Glow Seams | Pressure and minecart routing | Blasting charge | Heat raises stir faster |
| Hive Choir | Wax that hardens and melts; sweetness | Smoker | Sweetness lures enemies toward whoever carries the haul |
| Orrery | Rotating rooms | Star key | The floor turns between dives |
| Hearthdeep | All of the above | none | Fully authored, no generated floors (STORY 6) |

Each dungeon also gets 3-4 enemy species born from what its town lost, its own music, and its own set-piece endings.

### 3.3 How the dungeons connect
1. **Tools work both ways across dungeons.**
   - Forward: a dungeon's tool opens optional branches in every later dungeon.
   - Backward: earlier dungeons hold sealed alcoves stamped with a later dungeon's sigil. You see the lock in chapter 1 and get the key in a later chapter (decision 15).
2. **The dream bleeds.** Later dungeons contain digested echoes of earlier ones (Hollowmere barrels rusting in the Arcade's brine, a rat-king grown gills). This is asset and enemy reuse that tells the story.
3. **Variants (hard mode)** unlock later in the game. A variant is longer, has harder enemies, and mixes in enemies from dungeons the player has already reached (decision 15).
4. **Local materials and bounties.** Each dungeon yields materials that only it produces, which later towns' residents and weapon build-ups want. Bounties come later.
5. *Proposal:* **the roots join everything.** Late in the game, root passages link the dungeons' halfway banks as a fast-travel network that ends in the Hearthdeep.

## 4. Dive rules
### 4.1 Shape of a dive
Every dive starts at the dungeon entrance. It runs from the entrance to the halfway set piece and the bank room, then on to the boss or the end, then home. There is no floor select. After the boss is beaten, the shortcut to the halfway bank opens (decision 13).

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

### 4.4 Pieces and people (Dark Cloud style)
- When the world is created, each of a dungeon's pieces and people is assigned to a fixed floor, or to a random floor in the first or second half of the dungeon. The assignment is stored in the world save, the way DC1 does it (`dc1-rea.md`, Atla).
- Set-piece floors hold only their own story rescue.
- Floors are generated fresh each dive, but a piece's floor depth stays the same. It is placed somewhere on whatever floor is generated at that depth.
- A piece or person is **safe the moment it's picked up**. It goes straight to the town's stock and is not part of the at-risk haul.
- Each floor and the dungeon show "pieces left" and "people left" counters. Nothing can be missed.
- Key residents come home through the set-piece rescues (the baker at floor 5).
- *Open:* our decomp notes don't record DC1's rule on dying with an Atla in hand. "Safe on pickup" is the rule we're adopting, whatever DC1 did.

### 4.5 Stir
Pieces no longer feed stir, because they are safe once found. *Proposal* for stir's inputs:
- floors cleared this dive;
- unbanked valuables carried (weapons and large items);
- player count growing as the square root of players, not linearly.

Higher stir also **raises rewards** (better chest rolls, bonus materials), so pushing on pays. Its dangers stay: extra enemies, the hunter, the deep closing.

Collapse timer (*proposal*): path time to the nearest bank point or boss stair, times 1.5, with a 60 s minimum, and closing early once every living player is there. The hunter can be shaken off, and it leaves when the party banks.

### 4.6 Crash saver (*proposal*, QoL, risk-reviewer)
- Each floor start writes a dive snapshot to the host's save: floor, seed, haul, dive gold, stir, bank state, and the players present.
- A menu quit writes a clean-exit marker.
  - A **menu "abandon dive"** counts as a failure.
  - Quitting at the halfway bank is the normal save-and-quit.
- If the game starts and finds a dive snapshot with no clean-exit marker, it offers **"resume at the start of floor N"**: gold kept, haul as it was at that floor's start.
- Force-closing the game looks like a crash, and that's accepted (the project adds no anti-cheat). Menu quitting can't be used to cheese.
- Co-op: the host's snapshot is the dive's snapshot. Resuming after a host crash rebuilds the floor from the seed with whichever recorded players join. A client who crashed rejoins the running dive; mid-dive join is later work.

### 4.7 Co-op rules (*proposal*, from the research)
- **Scaling** by the players present: enemy count 1.0 / 1.45 / 1.85 / 2.2, enemy HP 1.0 / 1.15 / 1.3 / 1.45, damage flat, bosses mostly HP. Solo gets one self-revive per dive.
- **Locks:** combat rooms lock their exits only, with a short "wait for the party" ring at the door. Downed or disconnected players aren't needed for the clear. Dead ends never lock.
- **Leaving a floor:** the stair works as a gather-to-leave ring that ignores fallen players.
- **Pings:** a small set of party marks.
- **Chest loot** is rolled per player, so nobody races to open chests.

## 5. Hollowmere's dungeon: the Undercroft (10 floors)
*Proposal*: the layout that fits decision 2 and keeps STORY's beats.

| Floor | What | Gimmick | Objective |
|---|---|---|---|
| 1 | **Authored teaching floor:** bellows, dampers, the key carrier; the oven glow seen through a grate; a sealed alcove with a sigil the player doesn't know yet | Heat | Key carrier (taught) |
| 2-4 | Generated cellars (larders, wine vaults, coal cellars) | Heat | A different objective each floor (section 6) |
| **5** | **Oven maze set piece:** route the heat through dampers to the sleeping baker; the Dough Warden. **Then the bank room (section 4.2).** | Heat exam | Rescue |
| 6-7 | Generated cisterns; water level from one wheel per floor | Water | Varied |
| 8 | **Feast hall set piece:** drain it in stages; the tally with four blank names (STORY 8.4) | Water exam | Story |
| 9 | **The root wall:** the Tithe Roots as one authored floor; Tobin's father can't be carried out yet | Roots and light | Cut through |
| 10 | **The Bell-Warden** (STORY 8.4) | All | Boss |

**Phase 1 builds floors 1-5 and the bank room** (decision 17).

**The first session it should give** (the hook):
1. Wren's list.
2. The authored floor 1.
3. The first weapon level-up on floors 2-3.
4. The push decision on floor 4 while carrying loot.
5. The oven maze.
6. The baker wakes.
7. Go home, or push on.
8. Back in town, the bakery is rebuilt in view and sells bread.

## 6. Floor recipe (generated floors; *proposal*)
Built in this order from the host seed:

1. **Bundle.** Sub-theme, light, roster subset and hazard flavour. Most of this exists; what's new is giving the bundle a name (for example "The Smoking Larder").
2. **Objective.** Never the same type twice in a row:
   - key carrier (exists), now telegraphed;
   - **damper seal:** two dampers in different rooms open the stair (the link between rooms that Chamber Dungeons lacked);
   - **hold the hearth:** defend an oven while it heats;
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
- **Gimmick states:** each pool room has state A and state B (dampers open or shut, water up or down), so one room plays two ways.
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
Each later dungeon is longer than the Undercroft (for example 12-16 floors plus a boss), with the same halfway bank and its own gimmick, tool and dive rule (section 3.2). Each is planned around its gimmick from the first design pass.

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
- **STORY 8.4:** the Undercroft becomes 10 floors, as in section 5. Its story beats move: baker on floor 5, the tally on floor 8, Tobin's father on floor 9, the boss on floor 10.
- **PHASE1 section 1 and P1:** Phase 1 ends at Undercroft floor 5 (the baker's rescue) and the bank room, followed by a refinement pass.
- **PHASE1 7.2 and P19:** "floors stay cleared" becomes "fresh floors every dive; found pieces and people stay found". Pieces use Atla-style assignment (section 4.4).
- **PHASE1 7.2 and P20:** the checkpoint after floor 2 and the floor-4 bank point are replaced by the single halfway bank with slots (section 4.2). The failure rule becomes section 4.3.
- **PHASE1 P21 and P49:** stir inputs and the collapse timer as in section 4.5.
- **PHASE1 P47 and P48:** healing is bread first. Healing points become an elective dead-end room. The bank room heals fully.
- **Code** (later, after sign-off):
  - `DiveHaul` (pieces leave the haul);
  - `StirMeter` and `StirTuning` (inputs, square-root players);
  - `DiveState` and `DiveRoute` (one bank, start at the entrance);
  - new bank slots and the world-save field for the 3-player rotation;
  - generator (an authored floor 1, floor 5 as the set piece).

## 11. Open questions
1. Stir inputs now that pieces are safe (section 4.5): is "unbanked valuables carried" right?
2. Is the tension enough now that only loot and half the dive gold are at risk? Check in the M7 walk.
3. The crash saver as written (section 4.6), or skip it.
4. How much bread a dive starts with before the baker is rescued.
5. The gimmick and tool table (section 3.2) and the root network (3.3 item 5).

## 12. Phase 1 build order (after sign-off)
1. Dive rules: start at the entrance, the halfway bank room with slots and the save, the failure rule, pieces Atla-style.
2. Floor map; co-op locks and the gather-to-leave stair.
3. Authored floor 1; floor 5 rebuilt as the damper-puzzle set piece.
4. Objective variety (damper seal, hold the hearth) and the named floor card with two modifiers.
5. The heat gimmick on the 12 rooms; encounter templates; music stems.
6. Jobs for dead ends; collapse fairness; the crash saver if it stays simple.
7. Refinement pass (decision 17).
