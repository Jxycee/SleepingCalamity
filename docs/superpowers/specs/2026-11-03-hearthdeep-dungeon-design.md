# Hearthdeep dungeons: design (2026-11-03)

Status: **SIGNED OFF (user, 2026-11-03).** Section 10's amendments are applied to `docs/PHASE1.md` and `docs/STORY.md`. Previously: DRAFT. The user's decisions (section 2) are settled; decisions 23-30 (2026-11-03, the user's answers to the red-team review) replace older wording where they conflict. Everything marked *proposal* waits for the user's sign-off, and so do the PHASE1 and STORY amendments in section 10, which are listed but **not applied**.

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

23. **Failure costs time, and that is intended (F1 declined).** Players are meant to learn the systems and beat them: get better at combat, at when to leave or stay, and at what is really worth banking. Dark Souls is the model: little hand-holding, a real boss early, and death mostly costs time. There is no standing "start the dive at the bank" option, only the one-time checkpoint of decision 31. A dive leaves the dungeon only at the halfway bank, at completion, or by failing.
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
30. **The opening** is written (story session, user 2026-11-03; STORY 8.5): the heroes enter the Undercroft through the church cellar trapdoor, not the cistern house. Building the mechanics and the base world first, so there is something to judge, was the right order.
31. **The halfway bank is a one-time checkpoint (user, 2026-11-03).** When the party goes home from the bank, the next dive into that dungeon starts at the bank, and starting there uses the checkpoint up. If that dive fails, the dive after it starts at floor 1 again. A party that pushes on from the bank and then fails also starts at floor 1. Once the boss is beaten, the permanent shortcut of decision 13 replaces this.
32. **No retry for the floor-5 guardian** (user, 2026-11-03; now the Brazen Ox, decision 46). The paid retry (decision 25) is for the boss only.
33. **The lost satchel (user, 2026-11-03).** A failed dive's unbanked haul waits as one lost satchel on the floor depth where the party fell. It is a single item that turns back into the real items only once it leaves the dungeon (going home from the bank, or finishing the dive). Only one satchel exists at a time. Section 4.3.
34. **The satchel can be banked** (user, 2026-11-03): it takes one bank slot like any other bankable item.
35. **Emptying the satchel** happens in town: into town storage, or at a spot in the heroes' home (user, 2026-11-03).
36. **Pushing on must pay (user, 2026-11-03).** The bank slots are only for a party that continues without leaving. Because going home leaves a checkpoint (decision 31), pushing on needs its own clear reward, or nobody would bank and continue. Section 4.2 (*proposal*: momentum).
37. **Momentum accepted (user, 2026-11-03):** better chest luck in the second half for a party that pushed on, and an extra chest room at the end of an unbroken dive. It rewards players who don't need the safety and still satisfies those who do.
38. **Checkpoint floor pick (user, 2026-11-03):** a party holding the checkpoint may start on any floor from 1 up to the halfway bank, for example to go back for a sleeper they missed. Starting the dive uses the checkpoint up, whichever floor they pick. Without a checkpoint, the dive starts at floor 1.
39. **Each player keeps their own loot (F6, user 2026-11-03).** Chests and enemy drops are per player: each player sees and picks up only what dropped on their side, a random roll of the same item type for each player. Story and progress items (gate and boss keys, pieces, people) are not split: one copy, held by the host for the whole party. Individual progression then helps the group's progression. Section 4.7.
40. **Mixed-power parties (F10, user 2026-11-03):** boost weak weapons up and never scale strong ones down, catch-up growth, and need-weighted drops are accepted (section 4.7, items 1-3). Item 4 (helping pays) is accepted with limits so it can't be farmed to max out a weapon (user, 2026-11-03).
41. **Co-op recap (user, 2026-11-03):** a simple "while you were away" or "the adventure so far" recap when a recorded player rejoins.
42. **Map pings while building (user, 2026-11-03):** other players can point on the overhead map so the builder sees where they want something placed.
43. **Town permissions (user, 2026-11-03):** a host setting lets everyone move and build in town; it is **on by default**. If someone griefs, the host turns it off, and then only the host moves things or does other town actions; the others can still send move requests.
44. **The entrance is the church cellar trapdoor (user, 2026-11-03).** The church and the bell tower are one building; the outdoor tithe gate is the shortcut to the halfway bank once the boss is beaten (decision 13). The heroes' homes are separate places (STORY 8.2).
45. **Enemies look demon-made (user, 2026-11-03):** DOOM, God of War, Devil May Cry, Bayonetta, Demise, Tekken's devils and Diablo as the reference family (STORY 4). *Proposal:* the creature's idea still comes from the town's lost things; its make is demonic, and more demon shows the deeper the floor.
46. **The Brazen Ox replaces the Dough Warden (user, 2026-11-03).** The floor-5 guardian is the tithe-cart oxen remade by the dream into one huge hollow brass ox with a fire in its belly: glowing seams, steam breath, bull-demon horns. It stands in the cistern dome and is what boils the water. Fight: dodge charges and horn sweeps; open the sluices to flood its fire, and the quench cracks its hide for a damage window. The old Warden's fight shape carries over (telegraphed slams, super armour, revenge gauge, the placeholder stats). No retry (decision 32).
47. **The floor-5 sleeper is the innkeeper (user, 2026-11-03),** who cooked the Tithe Feast. Waking her opens cooking (the food and water craftables, decision 9). Placeholder names (user will rename): the cat **Tuppence**, the baker **Hollis Rye**, the innkeeper **Marta Fenn**.

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
Every dive starts at the dungeon entrance. It runs from the entrance to the halfway set piece and the bank room, then on to the boss or the end, then home. There is no floor select, except the checkpoint pick of decision 38. After the boss is beaten, the shortcut to the halfway bank opens (decision 13).

The way out of the dungeon is only the halfway bank, the end, or failure (decision 23). Three things soften the cost without adding an exit:
- **The one-time checkpoint** (decision 31): after going home from the bank, the next dive starts at the bank (empty haul, stir 0, fresh floors past it), and that uses the checkpoint up.
- **Save and quit at any stair** (decision 24): a pause between floors, resumed at the next floor's start (section 4.6).
- **One paid boss retry per dive** (decision 25): after a wipe at the boss, the party may pay the price and restart from the boss door with the haul they carried in. Without paying, the dive fails as normal.

Target length (decision 29, *proposal* for the numbers): about 40-50 minutes from the entrance to the bank and the same again from the bank to the boss, measured by the dev stats overlay in playtests.

### 4.2 The bank room (halfway)
Steps, in order:
1. The party rests: full heal.
2. Each player assigns their bank slots (section 2, decision 5) from the shared haul.
3. The party chooses: **go home** (everything carried comes home and the dive ends) or **push on**.
4. If they push on, the bank is locked and the game saves (decision 6). That save is the save-and-quit point.

**Why push on (decisions 36-37: momentum).** Going home is the safe choice: everything carried comes home and the checkpoint is left (decision 31), but the second half then starts cold. Pushing on keeps the dive's **momentum**:
- the second half's chests roll one rank higher, and the higher-stir reward bonus starts where the first half left it (stir's dangers still reset at the bank, section 4.5);
- an **extra chest room after the boss** opens only for a party that went from the entrance to the boss in one dive (decision 37): rarer gear and a material the best build-ups need;
- what the party found in the first half (bombs, potions, extra bread, boots) stays in hand for the second half.

So the bank decision is safety now against a better haul later. The overlay records go-home vs push-on rates by party size (M7) to check the trade holds.

The 3-player extra slot is picked by the host from the dive seed. The previous pick is stored in the world save so the same player is never picked twice in a row.

### 4.3 Failure
The dive fails when every present player is down (as built), or when the deep closes and the timer runs out. On failure:
- unbanked loot is lost;
- the gold earned in the dive is halved;
- bread is untouched;
- banked items and everything banked before stay;
- pieces and people found stay found (section 4.4);
- weapon growth earned in the dive is kept (*proposal*: the research found that no game players liked deletes long-term growth).

Time is the main stake, on purpose (decision 23): a wipe sends the party back to the entrance with fresh floors. A wipe at the boss can be bought back once per dive (decision 25); the Brazen Ox has no retry (decision 32).

**The lost satchel** (decision 33; details *proposal*):
- On failure, the unbanked haul (not pieces, people, gold or bread) is packed into one lost satchel and left at the floor depth where the party fell. The next dive that generates that depth places it on that floor, marked on the floor map.
- The satchel is one item in one carry slot. It can't be opened in the dungeon, so recovering it never fills the bag.
- It turns back into the real items when it leaves the dungeon: going home from the bank, finishing the dive, or banked in a slot. It is emptied in town, into town storage or at a spot in the heroes' home (decision 35), so a full inventory never forces a choice.
- It can be put in a bank slot like any other bankable item (decision 34): one slot for the whole satchel, against banking something new.
- Failing again while carrying it loses it for good, and the new failure leaves its own satchel. Only one satchel exists at a time; a new one replaces an unrecovered old one.
- Co-op: the satchel belongs to the party's shared haul (open with F6).

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
- **Loot is per player** (decision 39). The host rolls a separate result for each present player for every chest and drop of the same item type; each client shows and lets its player pick up only its own. Gold earned, bread, boots and other gear, bank slots, momentum rolls and the lost satchel are all per player (each player has at most one satchel). Keys, pieces and people stay single shared items held by the host. Host-authoritative as before; risk-reviewer. This replaces the host-held shared haul (PHASE1 P22).
- **Mixed-power parties** (decision 40, accepted; adapted from Monster Hunter, Borderlands 3, FFXIV level sync, Remnant 2 and rested-XP catch-up; `docs/research/dungeon-deep-dive/11-coop-fit-and-dive-flow.md` section 5):
  1. **Boost up, never down.** Each floor band has an expected weapon power. A weapon below it fights at a floor of about 85% of the expected power for damage maths, for that player only. Nobody is scaled down: a strong weapon is earned, and carrying a friend is a normal part of co-op (Monster Hunter's norm).
  2. **Catch-up growth.** A weapon below the party's median weapon grows at about 2x until it reaches the median (Path of Exile and rested-XP style). It narrows the gap without slowing the leader.
  3. **Need-weighted rolls.** A player's own weapon drops lean toward their weakest slot, so the friend without a good weapon finds one sooner (works with decision 39).
  4. **Helping pays.** The stronger player loses nothing for diving with a weaker friend (no reduced growth), and revives and assists add a little growth (Monster Hunter's helper rewards). Limits, so it can't be farmed (numbers *proposal*):
     - it only counts while the helped player's weapon is below the party median, so two equal friends earn nothing from it;
     - a revive counts only when an enemy downed the player in an uncleared room, and only once per helped player per 60 s;
     - it is capped per floor (at most 10% of the helper's current level cost) and per dive;
     - it stops once the helper's weapon is well above the floor band's expected power (about 1.5x), so farming easy early floors with a friend gives nothing;
     - it never completes a build-up step or the heirloom's final form on its own; those still need the weapon's own use.

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
| **5** | **Halfway set piece:** the cistern dome, boiled from below by the **Brazen Ox** (decision 46). Flood its fire through the sluices to crack its hide. The innkeeper, who cooked the feast, sleeps here (decision 47). **Then the bank room (section 4.2).** | Water exam, heat foreshadowed | Rescue and guardian |
| 6-7 | Generated coal cellars and flues, near the demon's breath; the bellows; dampers and ovens | Heat | Varied |
| 8 | **Feast hall set piece:** the sunk feast laid out under braziers that never go out; work the dampers and flames to reach the tally with four blank names (STORY 8.4) | Heat exam | Story |
| 9 | **The root wall:** the Tithe Roots as one authored floor; Tobin's father can't be carried out yet | Roots and light | Cut through |
| 10 | **The Bell-Warden** (STORY 8.4) | All | Boss (paid retry, decision 25) |

**Phase 1 builds floors 1-5 and the bank room, then floors 6-7 as rough greybox and a stand-in ending** (decision 28). The stand-in ending is a door on floor 7 that ends the dive as a success and brings the haul home, so "push on" can be played and measured. Phase 1's bankable item class is **simple footwear** (user, 2026-11-03): a pair of worn boots that raises run speed by about 5%. *Proposal:* it is the first item of a small stat-gear slot, separate from outfits (STORY 3 keeps outfits cosmetic), one pair per player; chests on floors 2-7 can roll it, rarer before the bank and likelier past it, so pushing on pays. The bonus raises run and sprint speed only, never jump distance, so traversal gaps stay tuned.

**The first session it should give** (the hook):
1. The opening (STORY 8.5): the lanterns, the cat, the storm and the church trapdoor.
2. The authored floor 1.
3. Floor 2: the first weapon level-up. The survivors' poor bread heals only a little, so the player learns to dodge.
4. Floor 3: the drowned bakery. **The baker wakes**, and from here on he is safe whatever happens.
5. Floor 4: carrying loot through the bubbling floor toward the guarded bank. There is no exit yet, so this is tension, not a decision.
6. Floor 5: the Brazen Ox in the boiling dome, the innkeeper, then the bank room.
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

## 10. Amendments (applied 2026-11-03)
- **STORY 8.4:** the Undercroft becomes 10 floors, as in section 5. Its story beats move: baker on floor 3 (the drowned bakery), the Brazen Ox and the innkeeper on floor 5, the tally on floor 8, Tobin's father on floor 9, the boss on floor 10. The bands become water first (cellars and cisterns, floors 1-5), then heat (coal cellars, flues and the breath, floors 6-8), then the roots (floor 9). The band enemy lists are redistributed to match: drip-spirits and eels move up, and new heat creatures come in below.
- **PHASE1 section 1 and P1:** Phase 1 ends at the Undercroft's halfway bank (floor 5), plus floors 6-7 in rough greybox and a stand-in ending (decision 28), followed by a refinement pass.
- **PHASE1 7.2 and P19:** "floors stay cleared" becomes "fresh floors every dive; found pieces and people stay found". Pieces use Atla-style assignment (section 4.4).
- **PHASE1 7.2 and P20:** the checkpoint after floor 2 and the floor-4 bank point are replaced by the single halfway bank with slots (section 4.2). The failure rule becomes section 4.3.
- **PHASE1 P25:** the Undercroft's entrance moves from the cistern-house cellar stair to the church cellar trapdoor (decision 44); `DiveHub` and the go-home landing move with it.
- **PHASE1 section 3 (style bible) and 6.6:** enemies get the demon-made look (decision 45), and the Dough Warden becomes the Brazen Ox (decision 46; code and tuning keys renamed in the build); they are still greybox, so nothing built is lost.
- **PHASE1 P22:** the shared host-held haul becomes per-player loot (decision 39); keys, pieces and people stay shared.
- **PHASE1 / M6 S4 placement:** the single builder lock becomes the town-permissions setting (decision 43): everyone may build by default (a lock per placed item instead of one global lock, *proposal*), the host can restrict it, and others can send move requests and map pings (decision 42).
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
3. **The boss retry price** (decision 25; settled by the user 2026-11-03): a secret idol hidden on one second-half floor, with a tell, that the party activates; gold is the fallback when the idol wasn't found (a big gold purse mostly comes from many dives or many failures anyway). The Brazen Ox gets no retry (decision 32).
4. **The lost satchel** is settled (decisions 33-35, section 4.3). The checkpoint floor pick (decision 38) and momentum (decision 37) are settled.
5. Red-team items: F6 settled (decision 39). F10 waits for the user's OK on the section 4.7 proposal (decision 40). F2 is folded into section 4.5; F7 (validator checks every gimmick state) and room data v2 before room 13 stay as the previous session proposed.
6. Co-op fit (`docs/research/dungeon-deep-dive/11-coop-fit-and-dive-flow.md`, 2026-11-03): the time target per half (decision 29) and the stair suspend (decision 24) answer most of it. The recap (decision 41), map pings (42) and town permissions (43) are settled. Still open: bank slots that shrink per player in a 4-player party.
7. The story session's floor-5 questions are settled (decisions 46-47). Names are placeholders until the user renames them.

## 12. Phase 1 build order
1. Dive rules: start at the entrance, the halfway bank room with slots and the save, the failure rule, pieces Atla-style, the stair suspend, the paid boss retry, the one-time checkpoint, the lost satchel, the bread ladder.
2. Floor map; co-op locks and the gather-to-leave stair.
3. Authored floor 1; floor 3 as the drowned bakery set piece; floor 5 as the halfway set piece with the Brazen Ox and the innkeeper; floors 6-7 in rough greybox and the stand-in ending.
4. Objective variety (two-valve seal, hold the pump) and the named floor card with two modifiers.
5. The water gimmick on the 12 rooms (bubbling variants for floor 4); encounter templates; music stems.
6. Jobs for dead ends; collapse fairness; the crash saver if it stays simple.
7. Refinement pass (decision 17).
