# 05 - Co-op dungeons (1-4 players), scaling, and dungeon modifier systems

Confidence tags: [V] = verified from a fetched/search-returned source this session; [S] = secondary/community source (fan wiki, forum, listicle), treat as approximate; [I] = my inference or prior knowledge, not verified this session.
Written 2026-11-02. All text is in my own words.

---------------------------------------------------------------------
## 1. Per game / topic
---------------------------------------------------------------------

### 1.1 FFXIV Deep Dungeons (Palace of the Dead, Heaven-on-High, Eureka Orthos, Pilgrim's Traverse)
Structure [V consolegameswiki]:
- Roguelite, procedural floors, 1-4 players, no role restrictions (solo is allowed; solo clears without a wipe earn their own achievements). Entry has a matchmaker for strangers plus premade parties.
- Floors come in sets of 10; a set's boss ends it. PotD: 200 floors, levels 1-60, 60-minute timer, no unsync. HoH 100 floors (60-70). Eureka Orthos 100 floors (81-90). Pilgrim's Traverse 100 floors (91-100), can start at floor 1/21/31/51/71, and is the one where matchmaking works on ALL floors (the older three block matchmade parties on deep floors because of difficulty).
- Two save slots per entry. Progress of a 10-floor set is kept only once all ten are finished; running out of time or a full-party KO before that loses the set. Fixed-party saves only work with the same party. [V]
- Per-floor goal: kill enough enemies to light the Cairn of Passage, then ALL party members must stand in its glow to leave; it refuses to work if anyone is dead or in combat. A separate Cairn of Return lights after kills and revives fallen members (important for parties without a resurrection job). [V]
- Pomanders: shared party consumables from golden coffers, max 3 of each type held. Immediate-effect ones (Sight reveals map/traps, Safety deletes traps, Strength +30% damage/healing 8 min, Steel -40% damage taken 8 min, Serenity/Purity remove floor effects/ailments, Raising revives, Witching polymorphs enemies, Rage/Lust/Resolution transform you for burst or boss floors) and next-floor ones (Affluence +50% coffers, Flight halves the kill count needed for the cairn, Alteration turns an enemy into a mimic/mandragora). [V]
- Traps: hidden, max one per room, none in start rooms; five types (enfeeble, silence-like, landmine at roughly 80% current HP so it never kills alone, lure = spawns 3 enemies, toad = -30% max HP). Wall-hugging avoids most. [V]
- Floor enchantments: up to one per group per floor, 3 groups (stat changes like haste or HP penalty; item/knockback/sprint/autoheal penalties; gloom). Higher floors more likely to carry one. [V]
- Aetherpool: dungeon-only permanent gear growth (arm and armour each cap +99), raised from silver coffers (can fail), never lost by saving/resetting; synced down when over a floor's cap. Out-of-dungeon gear is irrelevant. Patch 7.31 raised minimum gains (+1 to +5, bosses +10) because grinding felt bad. Patch 7.5: a party wipe does not add to the KO count until floor 100. [V]
- Coffers can be mimics (scales with depth). Accursed Hoard granted to all members on exit (shared payout, not split). [V]
Reception [S/I]: Deep Dungeons are the "co-op-or-solo roguelite" that FFXIV players use for leveling and for relaxed grinding; they are praised for the pomander layer (a shared, party-visible resource) and criticised for long runs (50 floors is hours), save-slot punishments, and for solo being much harder on the healer-less setup. I did not pull a sourced thread this session.
Take: the Cairn of Passage "kill N then everyone gather" is the closest published analogue to our key-carrier + gate, and the "everybody stands in the glow" rule is a built-in anti-leave-behind. Pomanders = shared consumable pool = fits our shared haul.

### 1.2 WoW Delves (2024, The War Within)
- 1-5 players (solo or any group), no timer, 11 tiers, AI companion Brann who can be set to damage/heal (tank added in season 2). [V Blizzard/Wowhead-sourced summaries]
- Higher tiers raise enemy health/damage and add traps/challenges (like M+ key levels); loot gear scales with tier, Great Vault rewards cap at tier 8+. [V warcrafttavern]
- Death rule: above tier 3 the party shares 5 revives (soulstones count); each death adds a stack of a debuff and fewer revives left lowers loot quality; respawn at last checkpoint. Failing = out of revives. [V warcrafttavern]
- "Bountiful" delves: one per zone, rotating, better loot, key-gated chest (craft from shards or buy). [V]
- Companion balance history: damage/heal hotfixed +125% across all tiers after he was found weak (a bug had him absorbing enemy buffs); in March 2025 Blizzard said healer-player + tank-Brann was too strong and nerfed tank Brann. Companion was announced to leave in Midnight (replaced by a new character). [S sportskeeda / mein-mmo / icy-veins]
- Reception [S]: mixed opinions on companion usefulness; the key win was "solo-viable endgame without queueing". Complaint pattern: companion either feels useless or is so strong it trivialises content; designers kept re-tuning.
Take: shared revive pool across party (not per player) is directly relevant; "revives left" as a visible group counter that also scales loot quality.

### 1.3 WoW Mythic+ affixes (rotating modifiers)
- Weekly rotating affixes on top of fixed dungeons were the variety engine since Legion. Long-standing player complaints [S, Blizzard forum threads]: Bolstering (enemies buff each other on death; punishes killing order and AoE), Sanguine, Bursting, Raging, Necrotic (tank-queue strain), Quaking (random interrupt-driven damage in tight corridors), and the unavoidable Fortified/Tyrannical pair. Common theme: affixes that punish one role (tank) or create unpredictable failures ("key bricked") were hated; ones that add a readable extra mechanic were tolerated.
- 2024 War Within rework [S, forum/Wowhead; no primary blog fetched]: Sanguine, Bolstering, Raging, Bursting retired. Stated aim: no "push weeks" (some weeks being far easier than others) and a consistent challenge about the dungeon itself. Fortified/Tyrannical became milestone affixes (alternating weekly from a certain key level, both at +10); low keys dropped affixes entirely; the exact breakpoints changed between seasons, so I would not quote a number. Blizzard's argument reported: applying bonuses unevenly to players feels exclusionary.
Take: modifiers should be readable, predictable per floor, shown before entering, never class/role-punishing, and never random mid-fight.

### 1.4 WoW Torghast (Shadowlands roguelite tower)
- Run structure: floors with randomly offered "anima powers" (temporary build-ups), soul-free objectives, boss; challenge wings (Twisting Corridors) added shops 6 times. [S wiki]
- Complaints [S]: powers random and often dull/placeholder-feeling, shop thin, too few meaningful rewards, difficulty/scaling too high (Blizzard cut it sharply: one 2020/21 patch removed timers from the Torment challenge and mein-mmo reported "massive nerfs"), bugs with exploit-able broker anima cells. Players wanted lasting reward for the run, not just a temp build. [Could not retrieve the primary patch note details; reasons are the community's.]
Take: a roguelite layer inside a dungeon fails if (a) rewards don't carry over to the hub game, (b) the temp-power menu is mostly flat numbers, (c) scaling is not tuned to power on entry.

### 1.5 Diablo III / IV dungeons
- D4 beta/launch: players called dungeons repetitive (same-y layouts, back-tracking objectives); director Joe Shely pushed back, saying you move through many dungeons while leveling and Nightmare Dungeon keys add variety through affixes. [V NME, SI.com]
- Post-launch: sigils teleport players straight in; door slowdowns removed; patch 1.1.1 removed three Nightmare affixes seen as frustrating rather than healthy challenge; later seasons added timed Pit of the Artificer (Season 4 PTR / 5) and Infernal Hordes (wave mode; confirmed permanent). [V/S sportskeeda, wccftech, icy-veins]
- Party scaling: sources conflict. Dungeon enemy health appears to scale with party size, level follows the highest party member, loot is instanced per player. [S, unofficial; marked unverified.]
- I did not verify the "carry animus / find keys / kill all" objectives against a developer statement; do not cite that as dev-confirmed.
Take: variety came from player-chosen affix keys and mode swaps (Pit, Hordes), not from more objective types; the same dungeon geometry can stay if risk/reward changes. Frustrating affixes were removed rather than rebalanced.

### 1.6 Remnant 2 (co-op up to 3, procedural worlds from authored pieces)
- Worlds are built from authored zone/dungeon pieces with swapped events/bosses. Co-op scaling per Dexerto: enemy level follows a "power level" with a +3 cap above the lower party member's (the article is internally inconsistent on host vs lowest). Solo uses a weighted average of weapon/archetype levels. [V Dexerto/Prima, loosely]
- Reception [S]: co-op praised as the main draw; solo called harder; late boss over-scaling in co-op on Veteran reported on Steam; difficulty spikes at end. Player count vs HP formula not found.
Take: capping the gap between strongest and weakest player's power is the key anti-"carry" device; we have gear levels so it matters.

### 1.7 Minecraft Dungeons (co-op 4)
- Mob count and difficulty scale with party size. Revive a downed friend within 30 s or you lose a life each; loot (armour, weapons, artifacts, emeralds) is per player, arrows/consumables shared. [S fextralife, review]
- Takeaway: personal drops removed loot arguments; a revive window creates a visible clock.

### 1.8 Deep Rock Galactic
- Difficulty = Hazard level x player count. Per the wiki tables [V wiki.gg, tables read as given]: e.g. Hazard 3 enemy damage multiplier solo/2/3/4 = 1.20/1.30/1.40/1.50; Hazard 5 = 2.80/3.00/3.20/3.40. A separate "enemy count" row (resistance table) at Hazard 5 solo/2/3/4 = 0.85/0.85/1.25/1.50; at Hazard 3 = 0.50/0.50/0.80/1.10. The page notes only a small spawn increase between solo and 2 players "to compensate for the loss of the solo robot companion (Bosco)". Revive health drops with hazard (0.60 at H1 to 0.10 at H5). Resupply cost is a flat 80 nitra; I found no player-count scaling for it. [V partly; the table labels are ambiguous, treat the shape (count steps up 3p and 4p, damage slowly up) as reliable, not the digits.]
- Lesson: scale enemy COUNT in steps, damage by small increments, and give solo a helper that makes 1p and 2p roughly equal.

### 1.9 Left 4 Dead AI Director (primary: Michael Booth, Valve GDC 2009 slides; text extracted from the PDF) [V]
- Design goals: require cooperation (treat the team as "the player", make cooperating the only winning strategy), replayability through dramatic anticipation, structured unpredictability, adaptive pacing, procedurally populated environment.
- Explicit rule: do NOT enforce cooperation with invisible leashes, teleporting stragglers closer, or out-of-bounds damage; players rebel against heavy-handed enforcement. Instead use enemies that punish lone wolves (grab/pounce specials), limited shared resources, and helpless-teammate states that invite rescue.
- Pacing algorithm: estimate each player's "emotional intensity" as one float; raise it by damage taken (proportional), by incapacitation, by being pulled off a ledge, and by nearby enemy deaths (inversely with distance); decay it toward zero over time but NOT while enemies are actively engaging the player. Director tracks the max over all four players. Phases: Build Up (full threat population until intensity crosses a peak threshold) -> Sustain Peak (3-5 s more) -> Peak Fade (minimal population until intensity leaves the peak range, so the current fight plays out) -> Relax (minimal threats 30-45 s or until the team has travelled far enough toward the next safe room). Boss encounters are exempt from adaptive pacing.
- Population layers: wanderers, mobs of 20-30, specials, bosses; only the area around the team is populated.
- Principle quoted in slides: low probability + high drama = memorable; set up many possible moments knowing few will happen in one run.

### 1.10 Monster Hunter (HP scaling by players)
- Wilds community estimate [S, single breakdown, observation-based]: monster HP 100% solo, 163% two players, 200% three, 234% four. World's scaling reported ~2.2x for multiplayer with disputes. Rise: game readjusts when a player leaves. [S]
- Shape: sublinear in players (each extra player adds less than a full player's worth), which keeps solo playable and avoids 4x HP sponges. Rise/Wilds re-adjust live when somebody leaves, which is exactly our "any subset" requirement.

### 1.11 Elden Ring Nightreign
- Solo or trio; duo omitted at launch (FromSoftware said it was overlooked; being looked at). Enemy health pools scale to the number of players. Solo has no revives, so a self-revive is hidden on the map. [V VGC/hitmarker interviews w/ director Ishizaki; HP formula not public]
- Interesting for us: the "solo gets a self-revive pickup" pattern.

### 1.12 Others, briefly
- Lethal Company [I]: scaling loot quotas and a shared quota across players, spectating dead players that can still help via cams/doors. Not researched this session.
- Gauntlet / Castle Crashers / Trine [I, partly via Game Developer article]: Castle Crashers revive requires a live ally to reach and stand still (works because room fights are controlled); Trine shares a pool of revives/checkpoints (not researched here).
- It Takes Two / Split Fiction (Hazelight) [V partly]: built for exactly two players from day one, no solo mode; both heroes get different abilities that must be combined; designers keep each puzzle short and varied. No solo fallback - the opposite of our need.
- Portal 2 co-op: no usable source returned; known from general knowledge that every co-op test needs both players and a clear visual state. [I]
- Destiny dungeon/raid roles (callouts, mechanic roles for 3/6 players) [I, not researched]: they work with voice; not a model for our no-voice play.
- Path of Exile map mods [V partly]: player-chosen modifiers via items, designers promised mods that never make a map easier; most mods are drawbacks balanced by extra reward. The thread's critique: variety, not risk/reward, is the real purpose. 
- Phantasy Star Online [S]: 4-player online runs through Forest/Caves/Mines/Ruins, door/switch puzzles that need pillars activated across areas, enclosed boss arenas. No verified "room lock until enemies dead" source returned; my recollection that PSO sealed doors until a room's enemies were cleared is [I].
- Lost Ark, Divinity, BG3: not relevant enough to research.

### 1.13 Co-op death/revive design (Game Developer "Designing death in co-op roguelikes") [V]
- Revive rules reshape the game. Spelunky: ghost + coffin, one per level = long waits. Enter the Gungeon: one revive item, waits can be long. Isaac: partner shares the main player's health, quick revives but a weaker partner drags the stronger one. Rampage Knights: ghosts revive after 10 kills, full-health revives let teams trade deaths endlessly. Castle Crashers/Lost Castle: needs a live ally to reach you; in Lost Castle, enemies make it near-impossible. Author's own solution: clear the room to revive, paid mid-battle revives, a 20 s (60 s boss) countdown, escape items, and an easy-mode "healer" hero so newcomers get support.
- Also found: forum evidence that indefinite downed state (33 Immortals) and AFK teammates are friction; StarCraft II posters want AFK players not to cause permanent loss. [S]

---------------------------------------------------------------------
## 2. Cross-cutting patterns
---------------------------------------------------------------------
1. Scaling is almost never a clean multiple. HP scaling is sublinear (MH Wilds ~1.6/2.0/2.3x), enemy COUNT steps up with players (DRG), damage nudged slightly, and the smallest group is helped by a companion (DRG robot, Brann) or a self-revive (Nightreign). [V/S]
2. Rewards go to individuals (Minecraft Dungeons, D4 instanced) or to a group payout (FFXIV Accursed Hoard to all members), never "first to click". Shared consumables (pomanders, revives left) are group pools. [V/S]
3. Revives: a visible timer or shared pool beats indefinite waiting; "clear the room to revive" and cairn-style revives remove dead time without free respawns. [V]
4. Cooperation enforcement works through in-world pressure (enemy design, shared resource, gather-to-leave points), not leashes. Valve says explicitly no teleporting stragglers or invisible walls. FFXIV gather-to-exit and WoW checkpoints do enforce togetherness but only at transition points. [V]
5. Variety in repeated dungeons comes from modifiers chosen by or shown to the player (PoE maps, D4 sigils/NMD, M+ affixes, Delve tiers, floor enchantments) more than from more layouts. The modifiers that survive are readable and not role-punishing; hated ones were removed (D4 1.1.1, M+ TWW). [V/S]
6. Pacing: spikes of pressure then breathing rooms (L4D). A stress metric driven by damage/incapacitation, decaying with time but held during active combat. [V]
7. Roguelite temp-power layers need meaningful choices and some permanent carryover (Torghast failed; aetherpool and pomanders work). [S]
8. Solo companion tuning is a recurring maintenance burden (Brann nerfed/buffed three times). [S]

---------------------------------------------------------------------
## 3. For Sleeping Calamity (explicit answers)
---------------------------------------------------------------------
Context: any subset of up to 4 recorded players present; difficulty scales to players present; host-authoritative; shared haul held by host; stir meter 0-100.

### 3.1 Scaling (adopt) - cheap
- Scale by players PRESENT (alive and connected counts at floor start; re-evaluate at room entry, not mid-fight). Matches MH Rise re-adjust-on-leave. [V/S]
- Use enemy COUNT and enemy HP both, sublinear. Proposed table (inference, to be tuned in playtest, base = 1 player):
  - Enemy count per room: 1p 1.00, 2p 1.45, 3p 1.85, 4p 2.2 (round with a min of +1 enemy at 2p; the count should step, not drip).
  - Enemy HP multiplier per enemy: 1p 1.00, 2p 1.15, 3p 1.30, 4p 1.45 (elites and boss use HP more than count: boss HP 1/1.7/2.2/2.6, MH-like).
  - Enemy damage: flat across player counts (a 4-player party shouldn't be one-shot more than a solo one); instead raise the number of attackers.
  - Mix of both keeps fights from becoming damage sponges (HP only) or bullet-sponge-free zergs (count only). Evidence: DRG count steps and MH HP curve. [V/S]
- Solo and duo help: no AI companion in Phase 1; instead give 1p a start-of-floor free "calm" charge (a one-time self-revive on every dive, Nightreign model) and cut solo enemy count 10-15% (DRG Bosco compensation). **cheap**
- Stir meter scaling: already per player count. Rise per unbanked piece should be per-party not per-player so a 4-player party isn't punished 4x for a shared haul. Keep. Also make stir gain from deaths/revives bounded (see 3.3).
- Power-gap cap (Remnant 2): the floor's enemy level uses the party's average, and a player's contribution is clamped to avg +/- one rank so a high-rank host doesn't make the floor unplayable for the low-rank friend and vice versa. **cheap** (formula) / **later** (tuning).

### 3.2 Revive and downed state - cheap/later
- Downed state, not instant death: downed player can crawl slowly or call a ping; ally reaches and holds interact ~2.5 s to revive (Castle Crashers/L4D model, "helpless demands cooperation"). Bleed-out timer 25 s (60 s at boss floors) after which the player is "fallen" until the room is cleared or a healing point is used (Son of a Witch model: clear-the-room revive removes dead time, 20/60 s timers). [V article]
- Fallen players spectate with a free "ghost cam" and can place pings; revive at the next room entry at 30% HP (clear-the-room revive). This guarantees nobody sits out longer than one room. **cheap**
- Shared revive pool per dive (WoW Delves 5 shared): start a dive with revive tokens = players present (min 2). Each "bleed-out to fallen" does NOT spend one, only a healing-point/room-clear revive that happens while the room is unclear spends one. Visible counter in HUD. **later** (HUD)
- Solo: no revive item pool; one self-revive per dive (see 3.1). **cheap**

### 3.3 Death and penalty - cheap
- Never wipe the group for one player's death. Party fail only when every present player is fallen at the same time (FFXIV full-KO, Delves "out of revives").
- A wipe does not delete the shared haul unless the 90 s deep-closure timer expires; wipe = back to last bank/checkpoint with unbanked haul lost (current design). Keep it; the FFXIV 7.5 note (wipe doesn't add to a counter until floor 100) shows softening KO penalties was needed. [V]
- Per-player death effects: bounded. Falling does not reduce your weapon growth or your personal items; the only cost is time and one stir tick (cap 3 per floor) so a clumsy friend can't push everyone into the stir tiers. **cheap**
- AFK / griefing in friend groups: no kicking (friends), but a host-only "pause for player" and "AFK = auto-ghost after 45 s of no input while others are in combat" so they stop counting for scaling count? Counting rule: AFK players DO count for scaling only after 60 s so a sleepy friend doesn't make fights trivially small mid-fight. Griefing mitigations: no friendly fire on players; no ability to block exits (player-player collision off in doorways); shared haul can't be dropped by clients (host holds it). **cheap**

### 3.4 Splitting up vs leashing (room locks) - cheap
- L4D's lesson is no invisible leash/teleport; we agree. But our combat rooms already have a fight/clear structure, so use a physical door lock on room entry (arena doors), unlocking on clear: gives everyone in the room the same fight, makes stragglers wait at the door rather than get picked off.
- Late-joiner rule: the room locks when the first player crosses the threshold AND all living players are within ~one corridor-length; otherwise the first entrant triggers a 6 s "wait" ring at the door (visible, cancelable). Prevents split-up soloing the harder 4p-scaled room. If a player is >1 room behind, they stay out until clear; if they are "fallen", they are not required. **cheap**
- Teleport-to-party: a dive-hub style "Rejoin" at the nearest checkpoint or healing point only, not a free hop (so floor traversal still matters). A lagging or reconnecting player respawns at the party's last cleared room edge. **later**
- Gather-to-exit (FFXIV Cairn model): the exit gate asks all living players inside its ring for ~3 s, fallen players are ignored; stops a speedrunner from leaving friends and from losing the key carrier. **cheap**
- Key carrier: if the key-carrier enemy drops the key, any player picks it up; the carried key is shown to all; if the carrier is fallen, key drops at the body (shared haul style). **cheap**

### 3.5 Loot ownership - cheap
- Shared haul held by host (design) stays for pieces/town progress. For chest loot (rank-filtered): chest contents are rolled per-player (instanced like Minecraft Dungeons/D4: each player opens the chest and sees their own roll) so nobody races to click. Town pieces are shared (one copy goes to the haul). **cheap**
- Weapon growth XP is individual. Revival or reconnect doesn't reset it. Banked haul is shared; a failed dive loses the unbanked haul but never an individual's weapon progress.
- Reward shown on doors (planned stretch) tells whether a room's reward is personal (per-player) or shared. **later**

### 3.6 Puzzles and "hearth marks" - later
- Co-op-only secrets (parked): always include a solo route (opposite of It Takes Two) such as a heavy lever with a timed alternate, or spend the one-time self-revive resource. Evidence: the FFXIV pomander pattern (Flight halves a requirement) shows consumable alternatives. Make N-player puzzles scale to present count: "N players push" where N = min(2, present). **later**

### 3.7 Camera and communication - later
- Shared camera is a trap with 4 players in 4 m cells: stay per-player third-person cameras (our design) and use a small "party strip" HUD with offscreen arrows. Avoid auto-zoom-out.
- Pings (no voice): three types (here/danger/need help) with a short ground marker; a context ping on a downed ally; chest/door "I've got it" ping; an automatic L4D-style shout line (characters call "behind you", "revive!"), which Valve cites as improving situational awareness. [V] **cheap/later**

### 3.8 Pacing - director-lite (steal) - later
- Adopt the L4D shape: per-room "intensity" float from damage taken and downs, held while enemies engage, decays otherwise; the stir meter already adds a macro layer. The micro layer uses the same data to modulate the NEXT room's spawn count within +/-1 (not mid-fight) and place a healing point earlier when the party is bleeding. Add guaranteed breathing: after a fight at peak, no wandering spawn for 30-45 s equivalents (a quiet corridor). Boss and set-piece floors exempt, same as L4D. **later**

### 3.9 Affix / modifier ideas that fit the stir meter
Rules from the evidence: modifiers must be (a) shown on the door/floor card before entry, (b) readable in the room, (c) not role- or hero-punishing, (d) never random mid-fight, (e) one per floor at most and tied to a stated reward (PoE risk/reward) or a stir tradeoff, (f) removed if they stall play (D4 1.1.1, M+ TWW).
1. **Stirred Floor** (player opt-in): tick stir +10 up front, doubles chest rolls on that floor. Direct risk/reward on the existing meter, similar to PoE map mods and Delve bountiful tier. **cheap**
2. **Cold Hearth**: no healing point on this floor, +1 revive token on bank. Opposite of "healing points on ~half of floors" - now a labelled variant. **cheap**
3. **Dark Floor** (planned rare dark floor): enemies see farther than you; lamps are shared consumables (like pomander of sight). **later**
4. **Heavy Sleepers** (stealth floor): roaming hunter wakes at lower stir, fewer enemies in rooms. For small parties; scale count down. **later**
5. **Echoing Halls**: every 3rd kill spawns one more trickle enemy but drops a small stir reduction. Converts combat into a pace choice. **later**
6. **Crowded Rooms** (4-player only): +1 enemy tier at 4p. Avoid as fixed affix; instead make it Stir-driven (already at stir 40).
7. **Hearth Blessing** (shared pomander equivalent): on entering a floor choose one of 3 team buffs (Strength +damage, Steel -damage taken, Sight reveals traps/key carrier) from a shared pool of up to 3 stored charges, found in chests. Directly lifts FFXIV pomander design; the shared pool matches host-held haul. **later**
8. **Mirror Room**: rule-twist gauntlet (parked idea). Requires no new systems except a rule flag per room.
Avoid: percentage-of-max-HP traps (FFXIV's landmine is deliberately capped at ~80% so it never kills - copy that cap), role-specific affixes, anything where "wrong" kill order bricks a run (Bolstering), random mid-fight quakes.

### 3.10 Conflicts with current design
- Stir meter "per unbanked piece, scaled by player count": if scaled UP with count, 4p dives accelerate; I recommend scaling meter increments by sqrt(players) rather than linearly. [I]
- "Each room linked to a random earlier one" and loops: room locks add forced-fight tempo; keep dead ends unlocked (no fight lock) so exploring never traps people.
- Gate key on one enemy: with 4 players the carrier can be lost in chaos; show the carrier highlight (ping marker) in the HUD for all.
- "Checkpoint after floor 2" is party-wide; a returning subset must agree at the checkpoint (host decides). Fine.

---------------------------------------------------------------------
## 4. Sources
---------------------------------------------------------------------
Primary:
- Valve, "Replayable Cooperative Game Design: Left 4 Dead", Michael Booth, GDC 2009: https://cdn.fastly.steamstatic.com/apps/valve/2009/GDC2009_ReplayableCooperativeGameDesign_Left4Dead.pdf
- Game Developer, "Designing Death in Co-op Roguelikes": https://www.gamedeveloper.com/design/designing-death-in-co-op-roguelikes
- Blizzard Delves announcement: https://worldofwarcraft.blizzard.com/news/24104270
- Elden Ring Nightreign director interviews (via VGC): https://videogameschronicle.com/news/elden-ring-nightreign-can-be-played-in-3-player-co-op-or-single-player-but-not-as-a-duo
- Blizzard forum (affix system updates TWW): https://eu.forums.blizzard.com/en/wow/t/affix-system-updates-in-the-war-within/520314 (search snippet only; 404 on fetch)
Secondary:
- FFXIV Console Games Wiki, Deep Dungeons: https://ffxiv.consolegameswiki.com/wiki/Deep_Dungeons
- FFXIV Console Games Wiki, Palace of the Dead: https://ffxiv.consolegameswiki.com/wiki/Palace_of_the_Dead
- Icy Veins PotD guide (403 on fetch; snippet only): https://www.icy-veins.com/ffxiv/palace-of-the-dead-deep-dungeon-guide
- Warcraft Tavern Delves guide: https://www.warcrafttavern.com/wow/guides/delves/
- Sportskeeda Brann tank nerf: https://www.sportskeeda.com/mmo/wow-delves-too-easy-healer-tank-brann-combo-blizzard-explains-nerf
- Torghast notes: https://godless-internets.org/2021/01/12/torghast-tower-of-the-damned , https://mein-mmo.de/en/wow-blizzard-has-heard-you-torghast-will-be-massively-nerfed,631100 , https://www.wowhead.com/news=316329/torghast-tower-of-the-damned-patch-notes-for-shadowlands-build-34615-removal-of-
- Mythic+ affixes: https://www.wowhead.com/news/343512 , https://mein-mmo.de/en/wow-throws-mythic-overboard-again-now-its-finally-good,1143509 , https://us.forums.blizzard.com/en/wow/t/mythic-affix-design/1213758
- Diablo 4: https://www.nme.com/news/diablo-4-director-pushes-back-on-claims-of-repetitive-dungeons-3417417 , https://videogames.si.com/news/diablo-4-repetitive-dungeon-design , https://www.sportskeeda.com/mmo/diablo-4-patch-1-1-1-set-remove-three-nightmare-dungeon-affixes , https://www.icy-veins.com/d4/news/infernal-hordes-permanent-feature/
- Remnant 2 scaling: https://www.dexerto.com/gaming/remnant-2-enemy-level-scaling-explained-2248477
- Minecraft Dungeons: https://minecraftdungeons.wiki.fextralife.com/Multiplayer
- Deep Rock Galactic scaling: https://deeprockgalactic.wiki.gg/wiki/Hazard_Level
- Monster Hunter scaling (community): https://www.gfinityesports.com/article/monster-hunter-wilds-monsters-are-dropping-too-fast-capcom-needs-to-buff-their-health , https://monsterhunterrise.wiki.fextralife.com/Multiplayer
- PoE map design: https://pathofexile.com/forum/view-thread/55194 , https://www.pathofexile.com/forum/view-thread/958575
- PSO: https://phantasystar.fandom.com/wiki/Forest_(Phantasy_Star_Online)
- It Takes Two: https://en.wikipedia.org/wiki/It_Takes_Two_(video_game)

---------------------------------------------------------------------
## 5. Open questions / unverified
---------------------------------------------------------------------
- Exact Delve party-scaling formula for 1-5 players and Brann's contribution; not found.
- Primary Blizzard post for the TWW affix rework; only secondary summaries.
- Diablo IV party health scaling and "carry/key/kill all" objective removals; no developer-confirmed source; no Season 12 confirmation.
- Exact DRG resupply/nitra scaling with player count; table digits ambiguous.
- Torghast launch-day review text and reasons for specific nerfs.
- Lethal Company, Gauntlet, Trine, Destiny, Portal 2 co-op: no sourced notes (marked [I] above).
- PSO room-lock behaviour; FFXIV Pilgrim's Traverse reception; Eureka Orthos pomander/aetherpool changes.
- All proposed numeric tables in section 3 are my inference and need playtest tuning.
