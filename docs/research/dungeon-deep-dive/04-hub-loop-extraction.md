# 04 - Hub/dungeon feedback loop and "carry the haul out" tension

Confidence tags: [V] = seen in a fetched/searched source this session; [K] = my prior knowledge, not re-verified; [I] = my inference.
Search tooling returned mostly reviews, wikis and forum posts; no developer postmortems surfaced for most of these games. Treat design-intent claims as community-reported.

## 1. Per game

### Moonlighter 1 (and 2)
- Structure: shop by day, gated dungeons by night; each dungeon is a few floors plus boss; loot fills a backpack grid; the shop sells it and the money buys town upgrades/gear. [K+V]
- Return mechanics: the Merchant Pendant is a paid escape (gold cost scales with dungeon and floor; a guide table shows e.g. 200-400 in the first dungeon, five figures in the last). [V, secondary guide]. A costlier mirror item lets you resume at the same floor. [V]
- Death: you lose almost everything except the first inventory row ("pockets"). [V] Players' sentiment is split: some say the pendant makes death soft (no permadeath feel); others like it as a learning tool; one reviewer flagged certain combat elements causing unfair deaths. [V, Steam thread + reviews]
- Take: the pendant turns "retreat" into a priced, visible decision, and it scales with how much you could carry. A "pockets" row is a cheap partial-banking device.
- Moonlighter 2: no strong verified data found. [unverified]

### Darkest Dungeon 1 / 2
- DD1: light meter is described by players as the real difficulty dial; lower light buffs enemies and improves loot odds, but many posters say the payoff is not worth it [V, Steam threads]. Retreat is called the hardest decision; failing a mission adds stress; recovery from losses is named by some as the biggest criticism [V]. Hamlet is rebuilt between runs with currency from dungeons [K].
- DD2: home base replaced by a stagecoach moving inn to inn; hope flame is the run-wide resource; run is one long roguelike path with a fixed core of four [V]. Mixed reception (Metacritic user 6.1 noted) [V]; some critics say removing retreat flattens risk vs reward [V, one critic]. Losses are cushioned by meta progress ("losses feel like less waste") [V].
- Take: the dial that the player can move mid-run (light) is a good push-your-luck knob only if the reward is clearly worth it; players distrust dials whose upside is hidden or small.

### Hades / Rogue Legacy 2
- Hades: Mirror of Night turns each death into an investment; House renovations use a separate currency (gems) so progress comes from any run [V, guides]. Community also argues meta progression can mask lack of real improvement [V, one thread]. Key idea: failure always pays something.
- Rogue Legacy 2: gold is converted to permanent manor upgrades; the ferryman takes leftover gold at the start of the next run, so you are pushed to spend before entering [V]. Nothing was verified about a keep-gold option.
- Take: our "failed dive loses only unbanked haul" already matches the always-pays-something principle. Consider small guaranteed trickle even from a failed dive (see section 3).

### Cult of the Lamb
- Crusade runs average ~10 min (Inverse) to 15-20 min (4Players) [V]; time split roughly evenly with base management by some reviewers, 75% management by others [V]. Common complaints: management shallow and repetitive, follower chores tedious, traits don't matter [V]. Praise: the blend and "factory" resource stress [V].
- Take: keep the town loop light on chores; town change should be visible not a spreadsheet.

### Ever Oasis
- Dungeons hold specific resources; residents bring back items; recruiting residents opens shops that attract more residents; reviewers call it a rewarding forward loop [V]. Main complaint: too many trips back to town to swap party members for puzzles; the warp gate that returns you to where you left off softens it [V].
- Take: never make going home mandatory for dungeon logic. Fast town trip + return-to-same-floor warp is the fix, and it is relevant to our checkpoint after floor 2.

### Core Keeper
- Co-op base-plus-caves; drop-in/out co-op (2-8); PC Gamer 86; atmosphere and cozy base praised, material hunting and grinding criticised; Steam ~92% positive [V].
- Take: base-building in co-op works when everyone contributes to one shared thing. Our town rebuild is a shared goal; pieces carried by anyone should credit the town for everyone.

### Dredge, Graveyard Keeper dungeon, Atomicrops, Children of Morta
- Dredge: search returned nothing usable. [unverified]. From memory [K]: daytime is safe, night raises panic and hazards, cargo is a Tetris-like grid so each catch displaces another; the pressure to head home comes from the clock plus a full hold.
- Others: not researched; no claims.

### Deep Rock Galactic
- Drop pod extraction at the end of mission; carry minerals/objectives to the pod; waiting at the pod is a timed final defence. [K; search only confirmed pod role/timer mentions] [V weak]
- Why it works [K+I]: the extraction point is known and visible from the start, the haul is a shared pool, and loot is already counted even if a dwarf dies (a down player is revivable; mission fails only if all die). Team always ends the mission together.

### Lethal Company
- Quota sold at end of deadline (3 days) and rises each cycle; day clock 8:00 to midnight; ship autopilot leaves at midnight; monsters spawn partly by time elapsed [V, wiki/guides]. If nobody is alive aboard when the ship leaves, all collected scrap is lost; the developer reasoning (quoted on Steam, secondhand) is that keeping scrap through deaths would make the quota too easy [V, secondary]. Players treat carrying one high-value item back as a gamble [V, guide].
- Key traits [K]: carried scrap is physical, visible, heavy and slows you; teammates are the ones who recover it; loss story is shared and funny.

### REPO
- Valuables are physics objects; rough handling, walls, monsters and teammates damage them and reduce value; damage threshold per item; good teamwork damages less [V, guides; no developer source]. Huge Steam success early 2025 [V, PCGamesN headline].
- Take: "value drops when you are careless" is a continuous, legible, readable risk, not a binary loss.

### Extraction shooters (Tarkov, Dark and Darker, Hunt)
- Fear of loss is both the tension engine and the exhausting part; sources say the same mechanic is praised and disliked [V]. Tarkov's dev quote: made for satisfaction, not fun [V, Insider Gaming]. Dark and Darker is seen as softer than Hunt/Tarkov; a low-stakes scav-style run lets people recover [V]. A review argues removing gear loss weakens tension (MW2 DMZ) [V].
- Take: losing carried items is acceptable when you can see it coming and the next run is cheap to start; stressful when loss streaks stack and the cost to start again is high. Ours: only unbanked haul is at risk and nothing you wear is, which is already on the soft side.

### Spelunky ghost
- Soft timer: in Classic the ghost appears after about 2.5 minutes in a level (warning at 2:00); Spelunky 2 is about 3 minutes [V, wiki/Steam]. The 100 s number floating around is not supported [V: not found]. Community split: some say it discourages exploration and called it jeopardising enjoyment; most defend it as the risk-reward spine. A mod exists to toggle it off [V].
- Take: a timer that is a nuisance first (a chaser you can outrun/handle) reads fairer than an instant fail.

### Other timers
- Risk of Rain difficulty-over-time, Don't Starve nights, Nightreign circle: not researched this pass; from memory [K] they work because the clock is global and visible, and the player chooses when to stop.

## 2. Cross-cutting patterns
1. Visible, shared, physical haul beats an abstract counter: Lethal Company and REPO carry loot in hands; DRG carries at the pod. Loss stories are memorable and social.
2. Partial protection is universal: Moonlighter's pocket row, DRG team-survival, Hades gems. Almost nobody loses 100% of everything on every failure.
3. Cost-priced escape (pendant) turns a fail state into a decision; the price scales with the amount at risk.
4. Clocks feel fair when: visible, start soft (a nuisance), warn early, and give a clear destination. They feel punishing when instant-fail or invisible.
5. Hub change must be visible within seconds of returning; renovations/residents arriving.
6. Frequent forced town trips annoy (Ever Oasis). Return-to-where-you-were fixes it.
7. Co-op loss feels worst when one person's death or absence takes away everybody's work; best when the group shares the loss and a teammate can save it.

## 3. For Sleeping Calamity

### Is bank/stir/collapse good push-your-luck?
Mostly yes. The shared haul + stir meter + 90 s collapse is close to a Lethal Company / DRG hybrid. Strengths: stir has three readable tiers (40 extra enemy, 70 hunter, 100 collapse) so the escalation is a ramp not a cliff; unbanked pieces raise stir so greed is visibly priced; pieces return to floors if lost so loss is not permanent. [I]

Frustration risks and fixes:
1. **Host-held haul** (conflict risk: network, host disconnect). If the host drops, the whole party's unbanked haul vanishes and clients feel punished for the host's connection. Fix: [cheap-ish] snapshot unbanked haul into each client's local record on every pickup, host handoff or reconstruct at rejoin; or bank automatically on clean disconnect. Needs risk-reviewer.
2. **A death losing the haul.** Do NOT adopt Lethal Company's all-dead-means-total-loss as the base rule. Adopt DRG: a downed player is revivable; the haul is lost only on full wipe or collapse timeout. [cheap] Verify our death rules; a single dead player should drop nothing, or at most their own carried piece.
3. **90 s collapse** may be tight on large floors. It is fair only if (a) a bank point is guaranteed within roughly 60 s of any spot (checkpoint/exit visible on the minimap, path lit), (b) the collapse is telegraphed (rumble at stir 85+, UI clock), (c) it triggers failure of the *dive* but not instant defeat: try "collapse closes the way back; you may still reach a bank point before time ends, and the haul lost is only what is unbanked". Better: make 90 s scale with distance to the nearest bank point at trigger time (e.g. path-time x 1.5, floor 60 s). [cheap, numeric]
4. **Co-op disagreement**: one player wants to push, others want to leave. Show a shared "stir" and give any one player a "call the retreat" ping that starts a 10 s regroup at the exit. [cheap]
5. **Loss streaks**: pity rule. If a dive fails with 0 banked, award a small consolation (a few coins/materials or one weapon-XP trickle), like Hades gems. [cheap]

### Better variants to consider
| Idea | Tag | Notes / conflicts |
|---|---|---|
| Partial banking: carry-limit "pockets" slot(s) that always survive (Moonlighter first row) | cheap | One protected piece per player; small, keeps loss from being total. |
| Priced return item (pendant): spend coins/consumable to leave anywhere, cost scales with unbanked haul value | cheap-later | Gives the player an escape before collapse; coin sink. Conflicts mildly with "bank points only"; solve by making it a consumable that must be bought in town. |
| Insurance: pay in town before a dive to protect N pieces | later | Gold sink; adds a prep decision (DD provisioning analogue). |
| Visible physical carry: pieces appear on the carrier's back (slows slightly, can be passed/thrown/dropped) | big | Lethal-Company-style stories; needs netcode ownership transfer, animation. Keep optional: host-held counter stays the truth and the visual is cosmetic first (cheap version). |
| Value loss on hit (REPO) | later | Probably wrong for a combat game: damage is constant and would feel punishing. Alternative: a fragile "brittle" piece variant that must be carried not fought with. |
| Risk shown up front | cheap | Door rewards already planned; add stir forecast ("+8 stir if you take this piece out"). |
| Shared revive to save haul | cheap | See above. |
| Soft-chaser first, hard-fail last | cheap | Our tier-2 hunter is already the Spelunky ghost analogue; make it escapable (door, stairs break line) before the 100 stage. |
| Torch/light dial (DD) | later | An optional "dim the hearth" toggle: more loot, harder enemies, cosmetic mood. Players distrust dials with weak upside, so make the payoff clear. |

### Pieces arriving home
- Immediacy: on return, the first thing seen is the town changed (a building, a lamp lit, a resident arrives) within a few seconds, ideally in the plaza cutscene-lite. Ever Oasis and Hades both reward quick visible change. [I] **cheap**
- Ceremony: a short brief ritual per piece (the piece flies/places itself, resident reaction line), skippable, batched when there are many. Avoid per-piece long cutscenes; Cult of the Lamb's chore feel is a warning. **cheap**
- Credit all present players, including ones who died carrying nothing, to avoid co-op resentment. **cheap**

### How the town changes the next dive
- Tangible, small and readable: a rebuilt shop adds a purchasable return item or heal items; a rebuilt gate cuts a floor shortcut/checkpoint; a lamp-keeper extends the collapse timer by a few seconds; a cartographer reveals bank point on the minimap. All **cheap-to-later**, all give "the town helped" rather than flat stats. [I]
- Avoid mandatory trips (Ever Oasis): allow a one-way warp back to the checkpoint floor after banking. Already fits checkpoint after floor 2. **cheap**
- Keep failure productive: a failed dive still advances residents' dialogue/keepsake progress. **cheap**

## 4. Sources
Secondary unless noted (no primary developer sources found):
- Moonlighter: https://en.wikipedia.org/wiki/Moonlighter_(video_game) ; https://playerassist.com/?p=217352 ; https://steamcommunity.com/app/606150/discussions/0/1696043263506496423
- Lethal Company: https://lethal-company.fandom.com/wiki/Profit_Quota ; https://steamcommunity.com/app/1966720/discussions/0/4032472470670005882
- REPO: https://repo-2025horror.fandom.com/wiki/Valuables ; https://www.pcgamesn.com/repo/steam-success
- Darkest Dungeon: https://steamcommunity.com/app/262060/discussions/0/1727575977558067839 ; https://rpgsite.net/review/6682-darkest-dungeon-review ; DD2: https://www.nintendoworldreport.com/review/67884/darkest-dungeon-ii-switch-review ; https://www.nme.com/features/darkest-dungeon-2-how-redhook-studios-are-reinventing-their-roguelike-classic-3085987
- Hades: https://mechanicsofmagic.com/2025/11/04/loops-and-arcs-in-hades/ ; https://www.brainbound.blog/hades-repeated-gameplay-loop-guide
- Rogue Legacy 2: https://godisageek.com/reviews/rogue-legacy-2-review ; https://roguelegacy.wiki.gg/wiki/Gold
- Cult of the Lamb: https://inverse.com/gaming/cult-of-the-lamb-review ; https://windowscentral.com/gaming/pc-gaming/cult-of-the-lamb-review ; https://indiecator.org/2022/09/08/cult-of-the-lamb-is-good-but/
- Ever Oasis: https://www.rpgfan.com/review/ever-oasis/ ; https://www.nintendolife.com/reviews/3ds/ever_oasis ; https://checkpointgaming.net/reviews/2017/06/ever-oasis-review/
- Core Keeper: https://pcgamer.com/games/survival-crafting/core-keeper-review
- Extraction shooters: https://insider-gaming.com/tarkov-battlestate-satisfaction-not-fun/ ; https://www.mmobomb.com/i-don-t-get-appeal-of-extraction-shooters-why-they-re-so-popular
- Spelunky: https://www.pcgamesn.com/indie/how-spelunkys-ghost-was-killed ; https://steamcommunity.com/app/418530/discussions/0/4462460605171360862
- DRG: https://deeprockgalactic.wiki.gg/wiki/Drop_Pod

## 5. Open questions
- No primary dev sources for Moonlighter pendant tuning, Lethal Company scrap-loss rationale (only a quoted forum post), REPO damage design, or DRG pod timer. Worth a GDC/interview pass.
- Dredge, Atomicrops, Children of Morta, Graveyard Keeper, Hunt, Terraria, Peak, Content Warning, Abiotic Factor, Nightreign, Risk of Rain, Don't Starve, Left 4 Dead were not researched (search unproductive or out of time); claims on them are memory only.
- Exact Moonlighter 2 and DD2 mechanics not verified.
- Our own death/revive rules and host-disconnect behaviour need checking against code before applying fixes 1-2.
