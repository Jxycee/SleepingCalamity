# 11 - Does co-op fit, and is the dive flow solid? (2026-11-03)

Follow-up to notes 00-10 and the draft spec (`docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md`). The previous session ran out of web search budget before it checked market and co-op fit. This pass covers those gaps (about 15 searches plus one page fetch). The notes are in our own words; anything a source says but nobody has measured is marked [opinion].

The user's question: "I want the dive flow super solid, fun and engaging. I'd love multiplayer, but some aspects make me think it may not be the best fit."

## 1. New evidence

### Co-op progress models in shipped games
- **Host-only progress is the most-hated model.** Redfall, Far Cry 5 / New Dawn, Dying Light 2 and Hammerwatch II all drew complaints that guests replay content or can't continue without the host. Endless Dungeon changed after launch to share hero unlocks with every player. *Our world rule (a shared world, up to 4 recorded players, any subset can play) avoids this whole class of complaint.*
- **Shared world progress has the opposite problem: "my friends played it without me."** Enshrouded tracks quests on the server, so a quest a friend finishes counts for everyone. Players said they missed parts of the game when friends progressed while they were away. The developers kept the shared model (they built it for one group sharing a world and wanted newcomers to catch up by playing) but are adding an option to replay every quest personally and a quest log showing server vs personal progress. *This is the risk our "any subset" rule carries: an absent player comes back to a rescued baker and a rebuilt bakery they never saw.* The red team's F10 covered weapon power. This adds **story and moment FOMO** (fear of missing out) on top of it.
- **Valheim and Grounded:** skills are per player and the world is shared. The usual advice is to agree house rules when the world starts. Grounded's "shared worlds" restore progress whoever hosts next, which is the model we already have.
- **Remnant 2:** story progress belongs to the host, but gear follows the player. Players rotate hosts to run each other's campaigns. *Our per-player weapon growth matches the part players like.*
- **Monster Hunter Wilds:** a single-player story with co-op added on top. Each player must watch the story scenes before they can join, and cutscenes during a mission break the group. One guide calls story co-op one of the game's weakest parts [opinion]. Plain hunts, where anyone joins an in-progress quest, are where its co-op shines. *Lesson: story beats and co-op fight each other unless every present player gets the beat at the same moment.*

### Shared loot
- **No Rest for the Wicked:** a shared loot pool made the early game feel punishing, as if the world's loot were split by the number of players. Defenders say shared loot is what makes co-op feel like co-op [opinion]. Several communities ask for a per-session choice.
- **Borderlands 3** moved to per-player loot and kept shared loot as an option. **Brut@l** kept loot unshared and made handing items over a deliberate part of play.
- *Fit with the spec:* chest loot rolled per player (spec 4.7) and pieces going to town stock are both on the good side. The open point is bank slots: with 4 players each gets 1 slot (spec dec. 5), so a party of four takes home the same 4 items as a pair. That reads exactly like the No Rest complaint ("loot divided by players"). See section 3, item 4.

### Friend-group co-op hits (Lethal Company, R.E.P.O.)
- Both are short-round co-op extraction games. The level gets more dangerous the longer you stay, and players carry loot out together. Players credit the co-op chaos and physical comedy (carrying fragile things together) for the fun, along with team roles that form on their own [opinion]. Lethal Company sold about 10 million copies. Neither has meaningful long-term upgrades.
- *Lesson:* carrying the haul together, the pressure to stay or leave, and short rounds are what make friends come back. Their rounds are minutes long. Ours are hours.

### Session length and finishing together
- **Typical session:** surveys put the most common gaming session at about 1-2 hours (Rakuten Insight APAC, Jan 2025). An older UK study found about 2 hours, mostly between 6 and 10 pm. No source split this out by co-op or weeknights.
- **Completion:** about 23% of Baldur's Gate 3 owners on Steam unlocked its ending achievement. No source measured how often friend groups drop a co-op game because of scheduling. The link between co-op drop-off and session length is still reasoning, not data.

### Dive flow
- **Dark Cloud 2:** contemporary reviews say players kept exploring floors they didn't need because the combat was fun and every floor held a geostone the town needed. RPGamer's retroview lists "too-long dungeons" as its main con. This repeats note 03b.
- **Dungeons of Hinterberg (2024)** pairs hand-made dungeons, each built on its own idea, with a calm town and social life. It holds Metacritic 82 and OpenCritic 87% recommend. Reviewers praise the dungeons most and fault the simple combat and puzzles and the uneven pacing. *This supports spec 3.2: one gimmick per dungeon is what reviewers remember.*
- **Returnal:** a critique argues it leans too hard on risk and too little on reward, and that roguelite structure clashes with its skill-based play [opinion]. *Supports the spec's rule that higher stir also raises rewards.*
- **Extraction design:** games in this space make the way back itself the danger (earlier areas turn hostile), not just a countdown. Blight: Survival calls its design "extraction-lite" and says the aim is the feeling of risk, not the genre checklist.

## 2. Verdict: does co-op fit?

**Yes for the dive, with friction in three places.** The dive is the most co-op-friendly part of the game: combat with downs and revives, carrying the haul home together, and the halfway bank argument. It's the same pattern behind the friend-group hits. The four-hero cast also maps one hero to each player. The world rule removes the worst co-op complaint in the genre (host-only progress).

The friction is not about co-op itself. It comes from three choices that suit solo play and strain co-op:

1. **Dive length versus a shared evening.** The spec's dives run about 80-130 minutes (red team F5). The only save-and-quit point is the halfway bank, and there's no other exit. Typical sessions are 1-2 hours, and a co-op session needs every player free at the same time. A solo player can stop when they like; a group has to plan the evening around one dive. **This is the biggest fit risk.** It also makes F1 (a late wipe costs about 40 minutes of replay) much worse in co-op, because the whole group pays it.
2. **Story moments with any subset.** The baker waking up, the bakery rebuilt in view, and the tally on floor 8 are the game's best beats. With any subset allowed to play, some recorded players will miss them. That's Enshrouded's complaint, made worse because our beats are emotional, not just quest ticks.
3. **Town time is solo-shaped.** Placement has one builder lock (M6 S4), so the other players stand and watch. Calm side activities are still parked. Between dives, three of four players may have nothing to do.

Puzzles (valves, sluices, dampers) are a smaller fourth point. If one player turns the valve and three watch, co-op stalls. The spec already leans the right way (hold the pump, a single interaction per gimmick, no required N-player action).

## 3. Recommendations (for the user to decide; none applied)
1. **Size each half of a dive to one evening.** Aim for about 40-50 minutes per half (entrance to bank, bank to boss) and a dive under 2 hours, rather than 80-130 minutes. Back it with **F5** (suspend at any stair) and **F1** (once the bank has been reached, a dive may start there). With those, a co-op group can always stop at a natural point and never replays half a dungeon after a wipe. If only one of the red-team answers is taken, take these two.
2. **Every present player gets the beat; absent players get a recap.** Set-piece rescues and the return ceremony already credit every present player (spec 9). Add a short "while you were away" recap for absent recorded players on their next join: the rescue line, the building rising, and their weapon catch-up (F10). Optionally, let a player replay a set piece they missed as a no-stakes memory. That's Enshrouded's answer, taken before launch instead of after.
3. **Give town time a co-op shape.** Placement needs one builder, but the others need something parallel to do: carrying parts, helping a resident meet a wish, a calm side activity (cooking from PARKING_LOT is the obvious first one), or the smith. Not Phase 1 code; note it for the town design session.
4. **Bank slots should not shrink per player as the party grows.** At 4 players with 1 slot each, a full party takes home the same 4 items as a pair. That's the "loot divided by players" complaint. Either keep 2 per player at every size, or accept 1 each but make the per-player chest rolls (spec 4.7) rich enough that nobody feels short-changed. Decision 5 is settled; this note raises the risk for the user to weigh in the M7 playtest, not as a change.
5. **Keep co-op, and make solo first-class.** Solo has to be fun with no compromise: the self-revive (spec 4.7), solo enemy counts, and every puzzle solvable alone. Then co-op is a bonus, not a requirement. Most players who buy a co-op game play much of it alone.

## 4. Sources
- [Game Informer: Redfall progress tied to host](https://gameinformer.com/2022/06/15/redfall-campaign-progress-tied-to-host-in-co-op)
- [Stevivor: Saints Row co-op progress](https://stevivor.com/features/in-depth/saints-row-co-op-drop-in-drop-out-and-progress/)
- [Hammerwatch II co-op review](https://www.taptap.io/es/post/6150690)
- [Endless Dungeon co-op meta changes](https://tryhardguides.com/endless-dungeon-reveals-future-changes-to-co-op-meta-progression/)
- [Enshrouded: On quest progression (developer post)](https://enshrouded.com/en-US/news/on-quest-progression)
- [Two Average Gamers: Valheim as a 2-player game](https://www.twoaveragegamers.com/is-valheim-actually-a-good-2-player-game-or-just-co-op-shaped/)
- [Shacknews: Grounded shared worlds](https://shacknews.com/article/131883/grounded-shared-worlds)
- [Shacknews: Remnant 2 co-op](https://www.shacknews.com/article/136331/remnant-2-co-op)
- [RPG Site: Monster Hunter Wilds story co-op](https://www.rpgsite.net/guide/16929-monster-hunter-wilds-online-multiplayer-guide-how-to-play-through-the-story-with-friends-squads-links-lobbies-and-more)
- [NME: Monster Hunter Wilds co-op story](https://www.nme.com/guides/gaming-guides/monster-hunter-wilds-co-op-story-3842422)
- [No Rest for the Wicked forum: shared loot early game](https://forum.norestforthewicked.com/t/early-game-feels-punishing-to-play-with-friends-with-the-current-loot-system/21932)
- [PlayStation Blog: Brut@l co-op](https://blog.playstation.com/2016/05/18/how-to-survive-dungeons-together-in-brutal-on-ps4/)
- [IIT TechNews: R.E.P.O. vs Lethal Company](https://technews.iit.edu/2025/04/03/r-e-p-o-versus-lethal-company/)
- [VGChartz: Lethal Company, a rare co-op horror success](https://www.vgchartz.com/article/459129/lethal-company-a-rare-co-op-horror-success/)
- [Notebookcheck: Baldur's Gate 3 completion rate](https://www.notebookcheck.net/Baldur-s-Gate-3-Despite-over-15-million-copies-sold-only-23-of-players-actually-played-it-to-completion.1126313.0.html)
- [Statista: APAC session duration](https://statista.com/statistics/1612407/apac-online-gaming-session-duration-by-country)
- [Stornoway Gazette: typical session length study](https://www.stornowaygazette.co.uk/read-this/gamers-will-spend-the-equivalent-of-three-years-of-their-adult-life-playing-online-splurging-almost-ps16000-in-the-process-2894773)
- [RPGamer: Dark Cloud 2 retroview](https://rpgamer.com/review/dark-cloud-2-review/)
- [RPGamer archive: Dark Chronicle review](https://archive.rpgamer.com/games/darkcloud/darkchr/reviews/darkchrrdrev1.html)
- [OpenCritic: Dungeons of Hinterberg](https://opencritic.com/game/16996/dungeons-of-hinterberg/reviews)
- [TechRadar: Dungeons of Hinterberg review](https://techradar.com/gaming/dungeons-of-hinterberg-review)
- [Superjump: Returnal and roguelite risk](https://www.superjumpmagazine.com/returnal-cant-handle-the-risks-of-roguelite-design)
- [Giant Bomb: ROGUEFLITE Extraction](https://giantbomb.com/wiki/Games/ROGUEFLITE_Extraction)
- [Blight: Survival risk and reward wiki](https://thegameswiki.com/blight-survival/wiki/risk-and-reward)

Blocked or thin: a plain DuckDuckGo request from the shell returns a bot challenge (HTTP 202), so shell search doesn't work here; use the WebSearch tool. No source measured co-op drop-off by session length.
