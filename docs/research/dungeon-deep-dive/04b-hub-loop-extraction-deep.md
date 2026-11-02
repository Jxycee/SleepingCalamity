# 04b - Hub/dungeon loop and "carry the haul out": deeper pass (wave 2)

Extends `04-hub-loop-extraction.md` (not edited). Tags: [P] = primary/developer-sourced (interview, Game Developer article, official/community-maintained wiki with rules text); [S] = secondary (guide, review, forum, fan wiki); [K] = my memory, not verified this session; [I] = my inference.

Honest effort note: about 38 searches and 20 fetch attempts (about 15 returned usable text) before the shared web-search budget ran out. Many pages were blocked (402/403/429). True developer-primary material was found only for Dave the Diver, Dredge (fog only), Cult of the Lamb, Hades (two quotes), Moonlighter 2 (shop/backpack). Rules numbers mostly come from wikis [P-ish] or guides [S]. Items I could not verify are listed in section 5.

## 1. Corrections to wave 1 (memory-only claims)

| Wave-1 claim | Verdict | Evidence |
|---|---|---|
| DRG: "mission fails only if all die; a down player is revivable; loot counted even if a dwarf dies" | Partly wrong. Mission fails if all dwarves are down with no revival, OR a required objective part fails, OR nobody is extracted by the pod. Loot is not "already counted": a failed mission pays 25% of credits, XP and mined minerals, and every present player gets an equal copy. | DRG wiki Mission and Drop Pod [P-ish]. |
| DRG pod timer unknown | Verified: after the objective, the pod launches on a fixed clock whether or not people boarded: 5 min most missions, 3 min Point Extraction and On-site Refining, 1 min Salvage; early launch if all living players are in the safe zone. Each extracted dwarf earns 150 credits x hazard bonus. Left-outside players lose that bonus. If nobody extracts, mission fails. | Drop Pod wiki [P-ish]. |
| DRG revive | Revive is a 6 s hold at very close range, no limit on attempts. Revived HP: 60/50/40/20/10 percent for Hazard 1-5. No bleed-out timer is listed on the wiki. Iron Will perk: 10 s grace when all are down. | Health wiki [P-ish]. Whether a dead dwarf's carried minerals are lost: not stated (minerals are a team pool in practice [I]). |
| DRG resupply | Not verified. Only found: Salvage Operation broken resupply pods (60% one rack, 15% two, 25% none). Resupply as a mid-mission rhythm remains [K]. | Salvage wiki [S]. |
| Moonlighter pendant costs | Verified (guide table): gold per portal, floor 1/2/3. Golem 200/300/400; Forest 800/1,200/1,600; Desert 3,200/4,800/6,400; Tech 12,800/19,200/25,600. Pattern: x4 per dungeon, x1.5 and x2 by floor. Pendant keeps the whole haul; costs more than the 1st-floor content is worth early, so it is a real sink. | playerassist.com [S]. |
| Moonlighter death | Verified by two reviews/guides: lose everything except the first backpack row. Mirror: drag items onto it to sell at poor price to fund a portal; resets dungeon level progress. Wave 1 said "costlier mirror resumes at same floor" - that was wrong or garbled. | playerassist [S], reviews [S]. |
| Spelunky ghost | Verified by wiki snippets: Classic/HD ghost after 2:30 in a level with a warning at 2:00; Spelunky 2 about 3:00 (S2 warning time unconfirmed). 100 s not supported. Ghost has 9999 HP in S2 (killable only by absurd lava loops), i.e. a pure pressure device, not a fair fight. | Spelunky wiki via search [S]; PCGamesN [S]. |
| Lethal Company scrap loss | Verified: if no living player is aboard when the ship leaves, all collected scrap is lost; a dead player drops body and carried scrap where they died; each death costs 20% of current funds unless bodies are brought back (Prima); solo death loses everything. Quota: 3-day deadline, missing quota ejects players (game over). A commenter says the scrap-loss on full wipe is intended. | Wikipedia [P-ish], Prima Games [S], Steam thread [S]. |
| DD light | Verified numbers (wiki, Radiant difficulty): light 76-100 no stress, monsters surprised +25%; 51-75 stress +10%; 26-50 stress +20%, monsters +5 ACC/+10% DMG, heroes surprised +15%, crit +2%; 1-25 stress +30%, +10 ACC/+12.5% DMG; 0 stress +40%, +12.5 ACC/+25% DMG, crit +5%. Torch drops 1 per step through explored area, 6 per step into new area (7 in harder modes). Extra loot chance rises as light falls. | darkestdungeon.wiki.gg Light Meter [P-ish]. Retreat rules: not verified (one fan wiki says a random hero dies on retreat - looks wrong for the current game [S, doubtful]). |

## 2. Per game (new coverage)

### Dave the Diver (closest structural match to our loop)
- Developer (director Jaeho Hwang, Game Developer interview) [P]: an early prototype combined sushi-bar management with ocean gathering at the same time and players could not focus on either. Splitting the game into a day phase (dive) and a night phase (serve) "really made a difference". Inspiration was a real Jeju restaurant whose owner dives by day and cooks at night. Side activities were designed to feed the loop, not interrupt it. New mechanics are unlocked by completing VIP guest requests that need specific ingredients (example: pouring beer after serving sushi).
- Loop numbers [Wikipedia, S]: dive twice per day once the evening dive unlocks; restaurant earnings raise reputation, which unlocks recipes and upgrades; gold also buys weight capacity, oxygen capacity and depth. Mintrocket said balance so no single element felt repetitive took trial and error through early access (Oct 2022 to June 2023). 5 million copies by Nov 2024.
- Failure rule [S, one Polish guide plus Wikipedia agree]: running out of oxygen or dying rescues Dave to the boat; you lose all collected items except ONE you choose. Death is not permanent and the day advances. This is the same "protected slot" device as Moonlighter's first row, but chosen by the player at the moment of loss, which makes the loss a small decision rather than a punishment.
- Take [I]: the day/night split is the proof that a hub and a dive should not share the player's attention at once. The hub's pull comes from the dive's output being immediately useful (tonight's menu).

### Dredge
- Developer sources [P]: Game Developer (GCAP 2023 talk write-up): night uses fog from a custom shader centred on the player, few boat lights early, unseen large creatures (a refraction slider fakes a rise from the depths), monsters introduced gradually because "the longer we could keep things unknown or unpredictable, the better". The daytime was made calm and open so players get relaxed and stay out longer than they meant to; night is claustrophobic - an open/closed contrast. The write-up gives no panic numbers and does not discuss the cargo grid.
- Panic [S/P-ish wiki, marked under construction]: rises at night in darkness and spikes when attacked; falls in good lighting, during the day, or resting in a town; effects are screen distortion, hallucinated islands/wrecks/ships and hostile events (Monster Ray chase, phantom sharks). No rates are published.
- Take [I]: the "go home" pull is soft and legible (panic eye icon fills, lights fail) and the cure is a place (town) not a consumable. Pressure is fear of losing visibility, not a clock number.

### Core Keeper
- Death [S, fan wiki]: a tombstone holds your inventory except equipment; you respawn after a few seconds at spawn; on one character type, exiting the world deletes the character file (single fan-wiki source, unconfirmed).
- Host [P, official patch notes via search]: previously one player had to act as world host and the world was inaccessible unless that player was online; the dedicated server is the workaround. How caves feed the base (ore, cores, cooking ingredients) is [K]; no new primary source read.
- Take [I]: tombstone with loot is the commonest co-op answer (also Valheim, Enshrouded) because it keeps loss recoverable and gives teammates a rescue task.

### Atomicrops
- Structure [S]: each day has a daytime farm phase and a night defence phase; every third night has a boss; four seasons of three days each plus a year boss. Complaints: day too short for farming. No developer interview text was reachable (Shacknews interview page only had a concept blurb). Design lesson stays [I]: the compressed day is the dial; do not copy it for a dive-and-bank game.

### Hades / Hades II
- Developer [P, via interview quotes]: Kasavin said the team's internal language was to "take the sting out of failure"; they wanted to alleviate the frustration of death and make it something you might look forward to; death drives the story (you are returned to the House and side plots advance after each death). Systems [S]: treasure from failed runs buys permanent Mirror upgrades; renovations use a separate currency. Hades II design doc and House contractor specifics were not read.
- Take: every failed dive should (a) pay a currency, (b) advance a character beat, (c) change something visible in the hub. Ours covers (a) only partly today.

### Cult of the Lamb
- Developer [P, Unity blog]: the two halves "only work when both sides feed into each other"; they did not fully come together until shortly before release; late development stopped adding features and polished the loop; the game quietly helps struggling players (hearts more likely at low health); sacrifice was tuned to be tempting. ACMI creative director: a loop of venturing out, finding recruits, bringing them back; followers must be kept happy, fed and alive (Tamagotchi comparison). No quoted split of time between base and crusade.
- Take: the shared-cost link (what you bring home must matter to the base within seconds) beats balancing minutes.

### Darkest Dungeon 1/2
- Light numbers: see section 1 table. Verified claim: the torch drains 6 per new step and 1 per explored step, so exploring is what costs light - a clean "unexplored costs more" rule. Red Hook's GDC 2016 postmortem exists (Sigman) but only the landing page was readable. PlayStation blog [S]: the hamlet lets you bench stressed heroes in stress-relief activities (the hub as recovery).
- DD2: removing the hamlet in favour of a coach changes the hub from rebuild to narrative waystation (wave 1 [V]).

### Moonlighter 2
- Developer (Pocket Tactics interview) [P]: still "diving into dungeons at night and selling loot by day"; "the shop is the other half of the game"; now 3D isometric with Hades-inspired combat; the fixed backpack was part of the original's challenge but players asked for expansion, so it can now grow through purchases from an NPC; the bow was cut as too long-ranged; shop customisation expanded (wallpaper, flooring) because players loved upgrading it. No death rule published. Full release 2 Sep 2026 [S]. Critics: levels blend together (Checkpoint Gaming) [S, via search snippet only].

### Rogue Legacy 2
- Gold from a failed run is spent on permanent manor upgrades [S, PC Gamer]. Player complaint: upgrade prices rise globally and leftover gold is taxed, nudging you to spend before entering [S, pre-release Steam post]. No developer interview found.

### Hunt: Showdown
- [S, wiki/forums, old and inconsistent]: death loses money and gear but not XP; bounty tokens are carried to one of three edge extraction points; the bounty carrier is marked to other hunters; payout decays with time. What happens to a token when its carrier dies was not found. No developer source.

### Elden Ring Nightreign
- [S/P-ish, wiki and Beebom]: expedition = two in-game days of about 15 minutes each, then a third prep day; a shrinking Night's Tide pushes players to the night boss. Downed co-op player crawls and is revived by teammates hitting them; if not revived in time they die, respawn one level lower and drop all runes at the spot. At night the revive gauge stays as long as the other two are alive; in daytime it times out. Team wipe in a night boss fight ends the expedition. Failed runs still pay out relics (permanent), scaled by progress [S]. Solo: dying at night ends the run. No dev interview read.

### REPO
- [S]: head of the dead player is returned to the truck/extraction to revive them; dead teammates do not wipe progress; whether carried valuables drop on death is unconfirmed. Save belongs to the host only; clients never write saves.

### Sea of Thieves
- [S, forums/blog]: loot not yet sold is lost when your ship sinks; floating loot sinks after about 2-3 minutes; gold from missions and renown carry over. Carrier is defenceless and can be robbed. Rare design director Mike Chapman: risk-versus-reward is central, shared-world design cannot be tested without many players [S]. Not a fit for a PvE game except as the "visible, carried, defenceless" image.

### Valheim / Enshrouded
- Valheim [S]: death leaves a tombstone with your inventory; skill loss on first death (about 5% reported; reduced for 10 minutes after); corpse-run buff on reaching the stone; second death does not destroy the first stone. Enshrouded [S]: tombstone vs dropped pile depends on a difficulty setting; backpack items can be lost in deadly Shroud. Host-leave: not found for either. Dungeon-feeds-base specifics not verified.

### Dragon Quest Builders 2, Spiritfarer
- Not researched in this pass (no budget left). Wave-1 note stands.

## 3. The co-op question: what happens to loot when ONE player dies / the host disconnects

Evidence table (all [S] unless noted):

| Game | One player dies | Host leaves | Source quality |
|---|---|---|---|
| Deep Rock Galactic | Team keeps going; revive 6 s, unlimited; shared mineral pool; fail pays 25% of credits/XP/minerals to everyone present, equal copy [P-ish] | No host migration; mission ends, lobby closes (forum posts, one 2017 alpha thread) | wiki + Steam posts |
| Lethal Company | Body and carried scrap drop at death spot; 20% funds penalty per death unless bodies returned; full wipe on ship departure loses all scrap [P-ish: Wikipedia] | No migration (Unity NGO); saves live with host | Prima, Steam |
| REPO | Head can be carried back to revive; teammates keep going; run resets if all die | Host alone has the save; clients lose progress; no migration (open bug report) | Steam, bug tracker |
| Helldivers 2 | Carrier dies: samples drop, any squadmate can pick them up; a dead player can be reinforced back; samples count only if someone carries them onto the shuttle [P-ish wiki]; failing to extract loses them | Samples of a disconnected player: contradictory forum reports | wiki + forums |
| Elden Ring Nightreign | Lose one level and all runes (dropped at spot); revive by teammates; wipe ends expedition [P-ish] | Not found | wiki, Beebom |
| Phasmophobia | Dead player's gear drops for others; insurance refunds 50% (easy) / 25% (mid) / 0 (hard) of lost equipment cost | Not found | guides |
| Core Keeper | Tombstone with inventory (not equipment) | World inaccessible if host offline unless dedicated server [P patch notes] | fan wiki + patch notes |
| Warframe | Mission loot is mostly client-side | Host migration exists; if it fails everyone returns to the Orbiter and loses picked-up resources/mods (rotation rewards for endless modes may be restored) [P-ish wiki] | official-style wiki |
| Risk of Rain 2 | Items belong to the collector; early-access report: leaver's items scattered among others | Early report: whole game stops, no migration (may be outdated) | Steam/Xbox forum |
| Valheim | Tombstone; skill dip | Not found | wiki, guides |
| Dave the Diver (solo) | Lose all but one item | n/a | guide + Wikipedia |
| Moonlighter 1 (solo) | Lose all except first backpack row | n/a | guide |

Patterns:
1. Nobody treats one player's death as total loss in co-op. Four answers recur: (a) revive and carry on (DRG, Nightreign, REPO, Helldivers), (b) loot stays physical at the death site (Lethal, Phasmophobia, Valheim, Core Keeper, Helldivers samples), (c) a flat percent/slot protected (DD-style, Moonlighter, Dave, DRG 25%), (d) payment to insure (Phasmophobia).
2. Total loss happens only on full wipe plus timeout (Lethal Company, DRG, Nightreign wipe, Helldivers no extract).
3. Host disconnect: almost every host-authoritative co-op game here has NO migration and loses the session; the saves belong to the host. The damage is limited by when state is written (REPO writes only at level transitions; Warframe splits server-verified rewards from client-side pickups and loses the latter). The player complaint is consistent: clients lose progress for something they did not cause. Warframe is the only one with migration and it still loses client-side pickups on failure [P-ish].

## 4. Making "bringing it home" feel good (evidence + inference)

Evidence found:
- Dave the Diver [P]: VIP requests that ask for specific ingredients unlock new mechanics (the haul is converted straight into a new verb). Night service uses the day's catch the same evening.
- Hades [P-ish quotes]: after every death the House reacts and side plots advance; the stated aim was to make coming back something you might look forward to.
- Cult of the Lamb [P]: recruits come back to a base where they worship you and do tasks (immediate visible use); ritual and sermon flow is the ceremony.
- Moonlighter 2 [P]: shop customisation (wallpaper, flooring) added because players loved improving the shop - the hub as personal space.
- Darkest Dungeon [S]: hamlet activities exist to heal what the dungeon harmed (a payoff of relief).
- Ever Oasis (wave 1) [S]: residents bring resources back; shop recruitment attracts more residents.
No source I could read described frame-level ceremony (camera, fanfare, reveal timing); specifics below are design inference [I].

## 5. For Sleeping Calamity (corrected)

Facts that changed our recommendations:
1. DRG is not "loot safe on death". It is: revive is cheap (6 s, unlimited), fail pays 25% to everyone, and the pod is a fixed clock. Our adoption should be the revive-as-first-answer plus a fail floor, not "dead dwarf loses nothing".
2. Host disconnect is the weak point of every comparable co-op game; no peer solves it. Opportunity, not a copy: we already have a bank point concept.
3. Protected slots are a repeated, player-liked device (Moonlighter first row, Dave's one item, DD-style) and the better form is player-chosen at loss time (Dave).
4. The two-phase split (Dave) and the "both halves must feed each other" (CotL) are the strongest evidence for our hub-and-dive structure, and both warn against letting the hub grow chores.

### Recommendations (tag: cheap / later / big; check vs constraints)

1. **Single death never costs the haul** [cheap]. Downed player: revive hold of about 6 s, unlimited attempts (DRG). A dead (not revived) player drops their one carried piece at the death spot (Valheim/Lethal pattern); any teammate can pick it up; a "death marker" shows on the map. Haul totals are untouched. Conflict: none; matches the shared host-held haul.
2. **Failed dive pays a floor** [cheap]. Replace "lose unbanked haul" with "keep about 25% of unbanked coins/materials, and weapon XP earned still counts" (DRG 25% is a verified number). Pieces return to floors as designed. Applies equally to a collapse timeout and a full wipe. Also advance one resident beat per failed dive (Hades) [cheap].
3. **Protected slot, chosen at loss time** [cheap]. When a dive fails, each present player may mark ONE unbanked piece or item as saved (Dave). Prevents first-row-only arbitrariness; makes the loss a decision. Edge: pieces are shared; limit one piece per player present.
4. **Host disconnect** [cheap-ish; needs risk-reviewer]. Write the unbanked haul to every client's local save on each pickup and on each floor clear (REPO's save-only-at-transitions is the failure to avoid). If the host drops: the session ends, but each client keeps (a) everything banked, (b) the 25% floor and (c) their protected slot; the world's host save retains state at the last bank/checkpoint. Clients who were present get credit for pieces they carried. No live migration (no comparable game ships it; Warframe's own failure mode loses client-side pickups). Verify against our save sections before building.
5. **Collapse clock** [cheap, numeric]. DRG's pod is 5 minutes after the objective (3 for shorter missions) and ends early when all living players are inside. Our 90 s is far tighter; recommend scaling to path time to the nearest bank point x1.5 with floor 60 s (as wave 1), and an early-close rule when every living player stands at the bank point.
6. **Torch-like dial** [later]. DD's number table shows a usable shape: four bands of light with each step adding a clear payoff (loot) and cost (stress, enemy accuracy). If we add a "dim the hearth" dial, make exploring new cells cost light faster than backtracking (6 vs 1 in DD) so it measures "pushing deeper", not time.
7. **Soft pressure first** [cheap]. Spelunky ghost warns at 2:00 and arrives at 2:30 (3:00 in S2); Dredge's panic is a visible gauge that can be lowered by lighting or resting. Our stir tiers already do this; add a visible readout and a cure (lit bank point or town lamp reduces stir) so pressure is not only one-way.
8. **Pendant-style priced exit** [later]. Moonlighter's cost ladder (x4 per dungeon) shows it works as a sink and scales with what you carry; keep it a town-bought consumable to preserve bank points as the default.
9. **Return ceremony (three beats, skippable, about 8-12 s)** [cheap] [I]: (a) arrival: the town changes in view as you enter (new light, repaired object, a resident walking over); (b) delivery: pieces show per carrier in the order they were found, with a one-line resident reaction; (c) unlock: a new "verb" or shop item appears at once (Dave's VIP-to-mechanic link). Credit all present players, including the dead; a failed dive plays only beat (a) with a shorter resident line. Batch when more than 3 pieces.
10. **Town as a verb source, not a stat sheet** [cheap-later]. Each rebuilt building should give a new thing to do or buy at once (Dave, CotL). Avoid chores (CotL complaints, wave 1). A lamp-keeper extending clock or a cartographer revealing bank points stays valid [I].
11. **Do not co-locate hub management inside the dive** [constraint]. Dave's prototype failure (both at once) argues for keeping our town and dives as separate phases; no mid-dive town management.
12. **Visible haul** [big]. Lethal Company/REPO physical carrying remains attractive but only as cosmetic first; sources show the power comes from loss stories and rescue tasks, which (1) above already gives.

Where these conflict with the current design: "a failed dive loses only the unbanked haul" becomes "loses the unbanked haul minus 25% and one chosen piece per player"; checkpoint after floor 2 stays; the host-held haul stays but gains client-side mirroring.

## 6. Sources
Primary/developer or rule-text wiki:
- Dave the Diver (Game Developer): https://www.gamedeveloper.com/design/dave-the-diver [P]
- Dredge (Game Developer, GCAP): https://www.gamedeveloper.com/production/leveraging-the-unseen-to-turn-players-worst-fears-against-them-in-dredge [P]
- Cult of the Lamb (Unity blog): https://unity.com/en/blog/games/recipe-behind-smash-hit-cult-of-the-lamb [P]; ACMI: https://www.acmi.net.au/stories-and-ideas/cult-of-the-lamb-massive-monster-take-dungeon-crawling-and-base-building-to-new-levels/ [P]
- Moonlighter 2 interview: https://www.pockettactics.com/moonlighter/2-interview [P]
- Hades interviews (snippets only): https://inlander.com/culture/hades-writer-greg-kasavin-on-how-he-made-video-game-deaths-drive-a-feel-good-story-22725237 ; https://butwhytho.net/2021/08/13/interview-developing-hades-with-supergiant-games-greg-kasavin/ [P, partly read]
- DRG wiki: https://deeprockgalactic.wiki.gg/wiki/Drop_Pod ; https://deeprockgalactic.wiki.gg/wiki/Mission ; https://deeprockgalactic.wiki.gg/wiki/Health [P-ish]
- Darkest Dungeon light meter: https://darkestdungeon.wiki.gg/wiki/Light_Meter [P-ish]
- Warframe host migration: https://wiki.warframe.com/w/Host_Migration [P-ish]
- Helldivers wiki: https://helldivers.wiki.gg/wiki/Getting_Started [P-ish]
- Nightreign wiki: https://eldenring.wiki.gg/wiki/Nightreign:Game [P-ish]
- Lethal Company: https://en.wikipedia.org/wiki/Lethal_Company [P-ish]
Secondary:
- Moonlighter pendant table: https://playerassist.com/exit-dungeons-moonlighter/
- Beebom Nightreign death: https://beebom.com/elden-ring-nightreign-death-explained/amp/
- Dave the Diver death: https://www.gry-online.pl/poradniki/dave-the-diver/jakie-sa-konsekwencje-smierci/z71d8d6 ; https://en.wikipedia.org/wiki/Dave_the_Diver
- Dredge panic: https://dredge.wiki.gg/wiki/Sanity ; Dredge Panic search page https://strategywiki.org/wiki/DREDGE/Panic
- Spelunky: https://www.pcgamesn.com/indie/how-spelunkys-ghost-was-killed ; https://spelunky.fandom.com/wiki/Ghost_(Classic)
- Core Keeper: https://core-keeper.fandom.com/wiki/Death (search snippet; page itself returned 402)
- Phasmophobia: https://phasmophobia.fandom.com/wiki/Death (snippet); guides via search
- Valheim: https://hostedgg.com/wiki/valheim/death ; Enshrouded: https://nodecraft.com/support/games/enshrouded/setup/how-to-disable-death-penalties-on-an-enshrouded-server
- REPO host save: https://steamcommunity.com/app/3241660/discussions/0/595142777455560322 ; https://app.betahub.io/projects/pr-7782050516/issues/2422
- Lethal host/migration: https://steamcommunity.com/app/1966720/discussions/0/4204742681377909089
- DRG host/migration: https://steamcommunity.com/app/548430/discussions/1/595138100650350933
- Sea of Thieves: https://www.seaofthieves.com/community/forums/topic/50083/treasure-chests-disappeared-after-ship-sunk-in-water-lost-hours-of-grinding-over-10k-worth-gold
- Atomicrops: https://en.wikipedia.org/wiki/Atomicrops ; https://www.shacknews.com/article/110951/atomicrops-interview-sow-the-seeds-of-pain
- Hunt: https://huntshowdown.fandom.com/wiki/How_to_Play_Guide_for_Hunt:_Showdown (search snippet)
- Moonlighter 2 review: https://checkpointgaming.net/reviews/2026/09/moonlighter-2-the-endless-vault-review-money-never-sleeps/ (403, snippet only)

## 7. Open questions
- DRG: bleed-out timer, resupply pod cadence, whether minerals carried by a dead dwarf are lost, host-migration status today.
- Moonlighter 1: no developer statement on the pendant tuning; mirror semantics inconsistent between sources; Moonlighter 2 death rules unknown.
- Darkest Dungeon retreat rules and Red Hook's GDC postmortem content (only the landing page was reachable).
- Hades II and House contractor details; Supergiant GDC talk content (login wall).
- Hunt: what happens to a bounty token when its carrier dies; REPO: do carried valuables drop on death; Helldivers: samples of a disconnected player.
- Dragon Quest Builders 2, Spiritfarer, Valheim/Enshrouded dungeon-to-base, Sea of Thieves designer commentary: not covered (search budget exhausted).
- Frame-level ceremony reference (camera, timing, audio) for return-to-hub moments: no source found; section 4 beats are inference. A video review pass or a GDC vault talk on "reward moments" would be next.
- Effort: below the 40-search / 15-fetch brief on coverage of the listed games (budget cap), not on sources repeating.
