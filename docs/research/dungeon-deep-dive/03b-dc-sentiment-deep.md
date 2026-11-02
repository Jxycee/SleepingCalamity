# 03b - Dark Cloud sentiment, deep pass (wave 2)

Extends `03-dc-jrpg-crawlers.md` (not repeated here). Written 2026-11-02. Confidence tags: [V] verified from a page fetched this session, [S] search snippet only, [I] my inference.

## 0. Method and limits (read first)
- Effort actually done: about 28 WebSearch calls and about 30 WebFetch calls (about 20 returned usable text). The brief asked for 40 searches; the session's shared web-search budget ran out at that point ("limit used up"), so I stopped searching instead of working around it. Searches not run (do these in a follow-up): YouTube retrospective titles, Spheda/ridepod opinion, "what makes DC weapon system great", Pokopia vs Dark Cloud, PSNProfiles DC2 platinum, Polygon 2017 Hino interview text, Metacritic user pages.
- Blocked or empty this session: GameFAQs, Reddit (not attempted), RPGamer review pages (403), Cubed3 (bot wall), Giant Bomb (403), Glitchwave (403), HLTB (tool refuses), Backloggd review index (loads, no review text), Metacritic (not in results), HG101 (site mid-migration, empty), NeoGAF (403), ANN forum (403), Kotaku/Twinfinite (one 403, one worked), Slant (403), PSN store text reviews (not loaded).
- Honest headline: **per-dungeon memories for DC1 and DC2 barely exist in readable public text.** ResetEra's appreciation, "what was DC like", "very late", and "quick retrospective" threads (all fetched) name NO dungeon of DC1 or DC2 as good or bad, except the Owl Forest/Muska Lacka soundtrack tracks and "stuck around Muska Lacka". Nobody in them mentions Demon Shaft's content, Moon Sea, Gallery of Time, Zelmite Mine, Moon Flower Palace, Gundorada or ridepod floors by name. The names "Starlight Temple" and "Lunatic Wisdom" in the task do not match anything I could find; DC2's third dungeon is Starlight **Canyon** [V/S wiki-derived search results]. Treat dungeon-by-dungeon nostalgia as an evidence gap, and treat that gap itself as a finding: players remember the loop, the weapons and the town, not individual dungeons.

---------------------------------------------------------------------
## 1. Dark Cloud 1 and 2: new evidence

### 1.1 Developer statements (primary-ish)
- Hino, RPGFan interview 2003 [V https://www.rpgfan.com/feature/rpgfan-exclusive-interview-series-creators-talk-2003-akihiro-hino-dark-cloud-2/]: Level-5 was founded Oct 1998 with about a 2-year plan; the project began as a PS2 tech demo (flying carpet over water). Town-building/raising people was only "partially the aim" of DC1 because the studio was small. DC1 was not a complete version of the concept; DC2 was rebuilt from scratch (no code reuse, engine and tools changed midway). He said nothing on dungeons or random generation. He joked that players should take longer on the game when asked about its volume.
- Hino 2005 on Rogue Galaxy [V https://classic.rpgfan.com/news/2005/1872.html]: denied that its dungeons were randomly generated, so they are hand-built; pre-release, no design intent about length. A 2006 Q&A [V worthplaying] says only that Level-5 wanted "something different" after Dark Cloud.
- Level-5 on a sequel: Kusuda 2009 told Kotaku/Gematsu there was no plan but they would "seriously consider" it if user requests grew [V gematsu, kotaku]. Hino 2017 (Polygon, via PlayStationLifeStyle) said there were "a lot of requests" but the IP is co-owned or managed by Sony so Level-5 cannot act alone; past negotiations happened [V playstationlifestyle]. Wikipedia repeats the Sony co-ownership [V].
- Kotaku 2009 reader mail [V https://kotaku.com/sorry-i-still-havent-made-dark-cloud-3-despite-your-r-5967830]: requests were a sequel, a PSN re-release with trophies, a two-game collection; one young fan's list: more playable characters, darker tone, emotional plot, classic dungeon-based combat, more creative weapons and upgrades, level-ups, possibly online play.

### 1.2 DC1 dungeon criticism, extra sources
- GameSpot (via Wikipedia) [V]: by the third stage of each dungeon things turn monotonous because of reused textures and objects. AllGame: crawling repetitive. GamePro: fighting "monotonous". Metacritic 80 [V Wikipedia].
- RPGamer reader review, DC1 [V via search-fetch page that reviewed DC1; scores 83 overall, gameplay 86]: dungeon mode "overly repetitive and vexing", dungeon-specific unlock items differ per dungeon and were hard to figure out (mine-cart lubricant example), music tiring after hours, first dungeon dull, floors that restrict abilities or force one character "beyond annoying", fishing "irritating, dull, unnecessary", Georama restrictive (pieces must fit fixed slots, no mix and match), weapon creation "the most redeeming factor".
- HonestGamers DC1 reader 2/5 [V]: "only a few dungeons", they look alike; enjoyed weapon upgrading and town rebuilding. Staff 2.5/5 "dull". HonestGamers DC2 reader 2/5 [V]: DC1 was fun but unremarkable and Georama was the one distinct thing; staff DC2 3/5; critics average 8.8.
- Giant Bomb user review [S]: several random floors per dungeon, almost all the same. Backloggd [S]: two reviewers (game not named in snippet) call floor generation repetitive and the loop lacking dungeon variety.
- Cubed3 PS4 6/10 and Destructoid 8.5 [S via OpenCritic summary].
- Positive framing [S Giant Bomb]: "one of the more unique dungeon hacks"; RPGamer says the town building is "entertaining" next to the diving that makes up most of the game.

### 1.3 DC2 / Dark Chronicle: extra sources
- Game Critics (Brad Gallaway) [V https://gamecritics.com/brad-gallaway/dark-cloud-2-review/]: layouts feel segmented, not organic; dungeons "all alike except the decor"; combing cloned areas for keys and upgrade items becomes tedious by mid-game; the heavy reliance on dungeon crawling "should be reworked". Spheda "most nonessential"; Monica's collection of transformations "a big task all by itself"; Max's gun upgrades "worth the effort". Each floor holds a Geostone that unlocks building, so depth feeds Georama.
- RPGFan DC2 review [V https://www.rpgfan.com/review/dark-cloud-2-2/] (score 92): early dungeons 10-20 floors, later ones much longer, tedious at times but combat carries it; main irritation is the Red/Blue Seals that lock out Max or Monica until the floor's monsters are cleared (the DC2 echo of DC1 forced-character floors). Fishing slow, low reward. Spheda unlocks after certain floors. Ridepod customization makes Max combat "hard to tire of". Transformation: 12 badge families, 45 forms, less engaging than ridepod.
- RPGamer DC2 snippets [S]: more time on dungeons, less on minigames; Retroview lists "too-long dungeons". Game Revolution (via Wikipedia) [V]: repetitive toward the end due to "rigidly episodic flow". Famitsu 35/40, Metacritic 87, GameSpot 9, IGN 9, Eurogamer 9 [V Wikipedia]. Reception section does not single out dungeons.
- Almar's medals guides [S snippets]: transformation medals "frustrating"; first-tier monsters nearly useless; it can take many hours to build a monster strong enough for Moon Flower Palace medals; mimics in the Underground Channel's last floors make time medals luck; ridepod with fast legs is the efficient time-trial tool. The wiki notes Moon Flower Palace becomes unreachable after completion, making a trophy missable [S].
- ResetEra, extra detail [V appreciation thread]: ZangBa: ridepod, Spheda, fish contest = bloat, "Spheda most frustrating"; Einbroch: Spheda "greatest RPG minigame ever, deserves its own game"; Kurtikeya: only flaw is that you must clear dungeons first; Qwark: mech levels, fish tanks, photos bad, transformations weak; krae_man: 100-floor post-game dungeon reward is the best sword with nothing to use it on; Triggerhappytel: DC2 random dungeons charmless; ILikeFeet: dungeons too long at the end.
- ResetEra "very late" thread [V]: Ashes of Dreams, GenericBadBrie, Skulldead, TitanicFall all say dungeons are repetitive or stale; stati: DC2 more competent, DC1 more enjoyable, DC1 QoL gaps = forced character swaps, no skippable animations, steep upgrade costs; EarthPainting: durability and upgrade mechanics unintuitive and can cause grind; Qwark: always carry auto-repair powder; Skulldead: 100% unguided nearly impossible (missable photos); SixelAlexiS quit over photo requirements; Dice: recommends DC2 first.

### 1.4 Per-item memory table (what is remembered)
| Item | Good | Bad | Evidence level |
|---|---|---|---|
| DC1 back floors | Optional harder half; some enjoy "just one more floor" | Gated by per-dungeon special item that is unclear (RPGamer reader); forced-character floors | [V reader review] thin |
| DC1 Demon Shaft | Reward = best gear; marathon appeal | "100 floors of pure battle", no new reward, fatigue (RPGFan wave-1; krae_man; Isilia stopped) | [V] no one praises it by name |
| DC1 Moon Sea, Gallery of Time | none found | "Final dungeon and boss rough, extra grind" (ColonialHawk) | [V] one post |
| DC1 Divine Beast Cave, Wise Owl, Shipwreck, Sun and Moon | none found | Owl Forest only as music; first dungeon "dull" (RPGamer reader) | gap |
| DC2 Zelmite Mine, Moon Flower Palace, Gundorada, Starlight Canyon | none found as sentiment; wiki: elemental stones matter in Zelmite, ridepod weak vs strong enemies | Moon Flower transformation medals are hours of grind; Moon Flower missable | gap [S] |
| Ridepod | Customisation praised (RPGFan); good for time trials | "mech levels" bloat (Qwark, ZangBa); weak defence late | [V] mixed |
| Monster transformation | Some fun forms (RPGFan) | Weakest system; required only for medals; needs hours of levelling | [V]+[S] consistent negative |
| Spheda | "Greatest minigame" (Einbroch), praised (Qwark) | Most frustrating (ZangBa); "most nonessential" (Game Critics); gated by dungeon progress | [V] polarised |
| Time-trial medals | Seen as thoughtful goal per floor (some reviews) | RNG mimics, long dead ends; platinum notoriously rough | [V]+[S] |

---------------------------------------------------------------------
## 2. DC-like and adjacent games: dungeon reception (new)

- **Rogue Galaxy** (Level-5, hand-built dungeons): HonestGamers [V]: dungeons "tortuous", recycle structures, get exponentially longer; same review text noted (via snippet [S]) that despite having fixed layouts "you would not know it". German review [S]: soulless, too many similar rooms. PopMatters [S]: gigantically repetitive. Polish review [S]: level design closer to Dark Chronicle than to DQ VIII. Giant Bomb blog [S]: keeps DC's dungeon-delving focus without its characterisation. Lesson (I): hand-built did not cure sameness; a small kit repeated for 70 hours did.
- **Kingdom Hearts Re:Chain of Memories** [V RPGFan]: each world is "a series of rooms opened with map cards", exploration "pretty stale" vs KH1/2; rooms random; castle is 13 floors; the same white transition room used between floors. [S] card chosen shapes enemy strength and item quality; RNG-driven. Take: player choice of room type/reward is the interesting idea, but visuals per room were weak. Speedrunners exploit the RNG [S TASVideos].
- **Ever Oasis** [V Siliconera]: "Hieroglyph Labyrinths" are generated from slabs you place on a pedestal; slab type sets difficulty (gold slabs harder), same slabs rebuild a different layout; rooms colour-coded by door with colours that change meaning between labyrinths; mini-boss and boss; treasure room may include a decoy chest; endless-spawn room; single-use heal spots; exit spots let you leave keeping items, retiring forfeits the run's items. Reception [S]: NWR and Gameswelt call the dungeons the highlight; Checkpoint Gaming: swapping characters is the slowest part; Slant: dungeons "all-too-rare" next to a repetitive oasis; early dungeons easy. Take: player-chosen difficulty by input item and previewed rewards are well received; exit-with-loot vs retire-and-lose is the same bank-or-lose idea.
- **Rune Factory 4 / 5 / Azuma**: RF4 3DS (Game Informer, GamesRadar [S]): dungeons and bosses lacklustre, crawling sometimes a chore. RF5 [V Destructoid]: dungeon crawling "extremely basic", one-button combo combat, bad camera and auto-target, repetitive dungeons, trivial puzzles, bosses lack variety, recruited monsters break combat; dungeons are optional and can wait; a day takes 30+ minutes so a year is 60-70 hours. Steam post [S]: copy-paste dungeons with recoloured monsters. Azuma [V RPGSite]: village design "fantastic", dungeons "so boring", mostly corridors, the Blight puzzle repeated "a hundred times", second-round dungeons reuse environments with slightly harder enemies, final dungeon reuses the same Oni; combat lackluster; village placement needs almost no planning; bond building tedious (talk to everyone daily). Other Azuma reviews [S] criticise "boring dungeons" but praise combat/exploration. Take: even a beloved life-sim line is repeatedly criticised for dungeons; players want the dungeon to be as designed as the town.
- **Fantasy Life i** [S]: dungeons are small caves with Life-gated gathering and hidden recipes (8 cave locations); reviewers focus on the loop and the grind; only 4 of 14 Lives are combat. A Backloggd player wished for more dungeons. No readable direct DC comparison. A ResetEra poster (mnk) listed it as DC-adjacent for rescuing trapped NPCs and island building [V].
- **Azure Dreams** [V/S]: RPGamer staff 3.0/5 "another average random dungeon game", too easy unless careless [S]; Next Generation doubted random maps add replay value [S]; HG101 notes some find it tedious [S]; a Let's Play author [V lparchive] says death is "painless" because you keep monsters (levels reset to 1) and the game "wants me to win", floors have roughly constant monster/treasure counts regardless of size so small floors are denser, special floors (floor 17 spawns all three troll types; from 16 monsters disguise as objects). GBC port swapped random floors for a pool of premade designs and added a post-game basement, at the cost of less content outside the dungeon [S]. ResetEra poster PallasKitten calls it "probably the most similar" to DC [V]. Take: low-penalty death plus a persistent companion layer keeps people playing; per-floor density normalisation is a cheap idea.
- **Shiren 6** [V Siliconera, Seafoam]: main dungeon 32 floors randomly generated, run starts at level 1, dying costs carried items and money but bestiary/discoveries persist, warehouse lets you start runs with stored items, side stories easier and teach mechanics, secondary dungeons use modifiers, Behemoth gates spawn a hunter that chases until it vanishes; Siliconera 8/10: "failing will happen far too often", mistakes punishing but newcomer aids (dojo, manuals, online rescue). Steam: Very Positive (about 86%, 391-468 reviews) [S]. Sameness complaint appears only in a Gamepressure summary [S]; the two full reviews praise distinct biomes [V]. Take: hunter enemy, run-persistence of knowledge, modifier side dungeons.
- **Indie DC-likes 2023-2026** (see 3.3).

---------------------------------------------------------------------
## 3. What fans of Dark Cloud want from a successor

### 3.1 Evidence
- ResetEra "Haven't been games similar to Dark Cloud" [V https://www.resetera.com/threads/there-haven%E2%80%99t-been-games-similar-to-dark-cloud-have-there.1162521/]: Rainer516 wants to "explore dungeons, escape by the skin of my teeth", then return and build a town; found DQ Builders 2 closest but "not similar enough", Cult of the Lamb combat not fun; OGM_Madness values the puzzle of building so everyone is happy; The Unsent: Ni no Kuni II's town is overhead-only, "a glorified way to buy weapons", wants a walkable town; platypotamus: Littlewood is the closest on making townsfolk happy but has no dungeon crawling; Mochi and LuciaDMC want a remake or more of that feel, Mochi says the original is hard to revisit; cyress8: My Time at Evershine may be closest (dungeons, town, item creation); mnk: Fantasy Life i; PallasKitten: Azure Dreams, DQ Builders, ActRaiser; Chaos17 (other thread) says Soul Blazer/Terranigma are the lineage [V].
- ResetEra appreciation thread wishes [V]: Triggerhappytel wants authored levels not procedural; Clive says the formula would suit today's mid-budget roguelike market; ColonialHawk surprised no one tried random dungeons plus town building again; Aether: a Mana-remake-sized budget could work; DoshinGuy wants more ActRaiser-style community-building action RPGs; ILikeFeet wants an unbranded third game; gazoinks a spiritual successor; Evilisk wants more Georama.
- Other threads [V]: thewienke: a DC3 would be amazing but "needs a bit of modernization"; Mesoian: remaster with QoL for town building and photo hints; stati: skippable animations, no forced swaps, cheaper upgrades.
- Steam forum request [V]: a poster wants procedural dungeons with rising difficulty, gear crafting, and a return trip to sell loot and rebuild a town, darker tone; replies: Darkest Dungeon, Atelier, Dark Souls-likes - nobody names a true match, i.e. the niche is unfilled [I].
- "We want DC3" Steam group [V]: story/characters (Seda, Genie), console not handheld, remake pack; one member skeptical a new game could match the charm. Gadwin praises weapon-upgrade depth and Georama as what made the series great.
- Critic framing [S]: GameSpot-style fix suggestion: more dungeons with fewer floors each; Game Critics: rework the reliance on dungeon crawling.

### 3.2 "What fans want" list mapped to our design
| # | Want (evidence) | Our design status | Action (tag) |
|---|---|---|---|
| 1 | Dive, barely escape, bring loot home, rebuild (Rainer516, Steam poster) | Core loop plus bank-or-lose haul and stir meter | Keep; protect the tension of "barely escaped" with a visible escape route (cheap) |
| 2 | Town building with real decisions (OGM_Madness; Georama slot-fixed criticism; RF Azuma "no planning") | Pieces rebuild towns with scoring (Core town scoring) | Make placement choices matter (adjacency/happiness) instead of fixed slots (later/big; not dungeon work) |
| 3 | Walkable, explorable town you can see change (The Unsent) | 3D third-person towns | Keep (core) |
| 4 | Weapons that grow by use, creative variety (Kotaku mail, RPGamer reader "most redeeming", Gadwin) | Weapon growth by use | Keep; no permanent loss (see ranked list) (cheap rule) |
| 5 | Modern QoL: skippable scenes, no forced swaps, clear hints, cheaper upgrades (stati, Mesoian) | Four heroes, no forced single-character floors | Keep; add skippable cutscenes and explicit hints (cheap) |
| 6 | Dungeons that are designed, not random filler (Triggerhappytel; Game Critics; Azuma/RF5 critics) | Authored rooms, set-piece floors per band | Keep; make each room one idea; the room pool must be bigger than 12 by shipping (later) |
| 7 | Short-feeling dungeons: "fewer floors each" (critic fix), no 100-floor padding (krae_man, ILikeFeet) | ~12 floors, checkpoints | Keep; no endless post-game ladder (cheap) |
| 8 | Co-op/online (young fan letter 2009; Ever Oasis party-swap complaints) | 2-4 co-op core feature | Keep; fix swap friction by having each hero always present, not swapped (already) |
| 9 | Mid-budget scale, unbranded spiritual successor (Aether, ILikeFeet, gazoinks, Clive) | Original IP | Keep; positions us in an unfilled niche |
| 10 | Minigames that serve the loop, not bloat (ZangBa, RPGamer, Game Critics) | Parked "calm side activities" | Spend on dungeon variety first; deterministic, never-missable side goals (cheap rule) |
| 11 | Previewed difficulty/reward choice (Ever Oasis slabs, P3R shuffle time) | "Rewards shown on doors" stretch | Promote to Phase 1.5 (cheap) |
| 12 | Darker tone (one 2009 fan letter only) | Storybook tone | Single data point; ignore |
| 13 | Persistent progress that survives failure (Azure Dreams monsters keep levels; Shiren bestiary) | Failed dive loses only unbanked haul | Keep; consider "knowledge persists" items such as revealed room maps per band (later) |

### 3.3 Indie DC-likes and spiritual successors found (2023-2026)
- **Emberville** (Cygnus Cross) [V Steam page https://store.steampowered.com/app/2295170/Emberville/]: 2D isometric action RPG, rescue townsfolk who lend skills to rebuild homes, personal estate with furniture, tags include Procedural Generation, solo only; Early Access planned Jan 7 2027 per the store page (TechWiser says Oct 27 2026 - conflict, unresolved), demo available, about 18 months of Early Access; no user reviews yet; the page does not mention Dark Cloud. Dungeon layout rules not described. Closest direct competitor to our loop; watch the demo's feedback.
- **Aeruta** [V TechWiser]: action RPG with dungeon exploration and bakery management funded by loot; console release Dec 10 2026 (PC 2025). Partial match; its reception not checked.
- **Somewhen** (hopscorch, itch.io) [V]: names Dark Cloud and Dark Chronicle as inspirations; place homes and trees in the present to reshape the future with a time-travel train; migrate NPCs; invent weapons; dungeon design not described; devlog dated Jun 13 2026 "Switch to Godot"; 4.0/5 from 5 ratings; no generative AI.
- **Littlewood** [S/V]: town-building and farming, no dungeon crawling (platypotamus); demo originated on Kickstarter [S].
- **My Time at Evershine** (Pathea) [S]: governor/city-building successor to Portia/Sandrock, dungeons described, co-op mentioned, 2027 on PS5/Xbox/PC/Switch 2; not a verified DC link.
- **Pokopia** [S one Reddit title only]: cited as "Dark Cloud minus combat". Unverified.
- No Kickstarter "spiritual successor to Dark Cloud" with real traction was found; Operation Atla/"We want DC3" are fan groups. [I] the niche is open.

---------------------------------------------------------------------
## 4. Updated ranked complaint list (frequency evidence)

Counts are distinct sources this note plus wave 1 (about 55 sources total). "W1" = counted in 03-dc-jrpg-crawlers.md.

| Rank | Complaint | Count (approx) | Key evidence |
|---|---|---|---|
| 1 | Sameness / repeated kit, same dungeon pattern, reused textures, same music | about 24 | DC1: GameSpot, AllGame, RPGamer reader, HonestGamers reader, Giant Bomb user, Backloggd x2, RPGFan, Saveorquit, GameRevolution (W1), ResetEra x6 incl. Ashes of Dreams, GenericBadBrie, Skulldead, TitanicFall; DC2: Game Critics, RPGFan, RPGamer, Honestgamers; adjacent: Rogue Galaxy x4, RF5 x3, Azuma x2, Shiren (one summary), KH CoM, P3R x4 (W1) |
| 2 | Too long / padded, gets longer late | about 14 | RPGFan DC1+DC2, RPGamer DC2, Game Revolution DC2, ResetEra ILikeFeet, Isilia, krae_man, PallasKitten, ZangBa; Rogue Galaxy, RF5 days of 30 min |
| 3 | Weapon breaking / durability loses work | about 10 | Destructoid, RPGFan, Stacked (W1), ResetEra Y2Kev, Beelzebufo, EarthPainting, Evilisk, Qwark (carry repair powder) |
| 4 | Forced character or locked-out hero floors (DC1 forced floors; DC2 Red/Blue Seals) | about 7 | RPGamer reader, Chalgyr, RPGFan x2, stati; Ever Oasis swap friction |
| 5 | Side-systems bloat and RNG goals (fishing, medals, mimics, photos, transformation grind) | about 9 | ZangBa, No Depth, SixelAlexiS, Skulldead, Qwark, Almar guides, Game Critics (Spheda nonessential), RPGamer reader (fishing dull), RPGamer DC2 |
| 6 | Weak combat/targeting/camera making repetition worse | about 9 | ResetEra x4 (W1), Stacked, GamePro DC1, RF5 x2 (auto-target, one-button), Azuma |
| 7 | Attrition meters / softlock (thirst, repair, money) | about 7 | Destructoid, Stacked, ResetEra x4 (W1) |
| 8 | Trivial puzzles / "swap character to open door" / dungeons as corridors | about 6 | RPGFan, stati, RF5, Azuma ("copy pasted a hundred times") |
| 9 | Unclear gating (special item per dungeon, hard-to-find keys) | about 4 | RPGamer reader, Saveorquit, Chalgyr, Stacked |
| 10 | Post-game ladder with no reward | about 4 | RPGFan, krae_man, ZangBa, Isilia |
| 11 | Town/Georama too restrictive (fixed slots, no planning) | about 3 | RPGamer reader, HonestGamers reader, Azuma |
| 12 | File-destroying or harsh death | about 3 | Azure Dreams, Shiren (punishing), P3 variants |

Change vs wave 1: added rank 4 (DC2 seals confirm it repeats across both games), rank 8 (trivial/corridor dungeons also hit RF5 and Azuma), rank 11 (restrictive building). Sameness and length remain the top two with even more support; player-facing "which dungeon was bad" data is nearly nonexistent, so complaints are about the pattern, not any one level.

---------------------------------------------------------------------
## 5. Cross-cutting patterns (new)
1. Praise is for the loop and the weapons; blame is for the dungeons as content. Across ResetEra, reviewers and fan groups, no one names a favorite dungeon of DC1/DC2. A successor wins by making dungeon content memorable per room, not by matching the loop.
2. Hand-built did not fix sameness for Level-5's Rogue Galaxy or for Rune Factory/Azuma: repetition comes from a small kit repeated at length with the same enemies.
3. Optional, short, differently-ruled dungeons (Shiren side stories, Ever Oasis slabs, Stardew reward floors) get praise; one long mandatory ladder gets complaints.
4. Player-chosen difficulty tokens (Ever Oasis slabs, KH CoM cards, P3R doors) are liked more than invisible RNG.
5. Failure that preserves something (Azure Dreams monsters, Shiren discoveries, our unbanked-only loss) is accepted; failure that deletes a long investment is hated.
6. Mechanics gated behind a specific hero are universally disliked in both DC games; subset co-op makes this worse.
7. A town/loop that is separate from the dungeon boredom keeps people playing (Azuma village praised, dungeons panned).
8. Unfilled niche: forum users keep asking "any game like Dark Cloud?" and get tangents. Emberville and Somewhen are the only direct entries found, both 2D and EA/demo stage.

---------------------------------------------------------------------
## 6. For Sleeping Calamity (additions to wave-1 section 5)
1. **Make each hand-authored room memorable (fix #1).** Give every room in the pool one named idea (one hazard, one enemy formation, one terrain feature) and a one-line design card; target 20+ rooms before shipping a band rather than 12. Conflict: pool size is Phase 1 constrained; tag **later**, but do the "one idea per room" rule now (**cheap**).
2. **Room density normalisation (steal Azure Dreams).** Keep monsters and chests per room roughly constant per floor budget so small rooms feel tense and large ones not empty. **cheap**.
3. **Previewed reward and difficulty choice on doors (steal Ever Oasis slabs, P3R).** Already a stretch item; promote it. In co-op the host chooses; difficulty scales to players present. **cheap/later**.
4. **Exit-with-loot vs retire-and-lose (steal Ever Oasis exit/retire, Shiren warehouse).** Matches our bank-or-lose. Add a one-use "send piece home" item (**later**).
5. **Knowledge persists (steal Shiren bestiary, Azure Dreams pets).** Keep revealed floor shapes or enemy notes between dives so repeats feel like progress. **later**.
6. **Chasing hunter that eventually vanishes (steal Shiren Behemoth Gate).** Our tier-2 hunter at stir 70 could expire after a timer or when the party reaches a bank point, reducing frustration. **cheap**.
7. **No locked-out heroes (fix DC2 seals).** No room or door in the Hearthdeep may require one specific hero; hero abilities only open optional shortcuts. **cheap rule**.
8. **Skip the filler minigame trap.** Keep side activities to the town (calm), deterministic and unmissable; no fishing-with-RNG gating or photo missables. **cheap rule**.
9. **Optional modifier side-dungeons after the first (steal Shiren).** After the main dive, short rule-twist challenge dives teach mechanics and reward keepsakes. **later/big**.
10. **Walkable, readable town change.** Players explicitly miss seeing the town; make rebuilt pieces visible in the town immediately and reward placement thinking. **cheap/later**.
11. **Combat is the repetition lever (again).** Reviews of RF5/Azuma show weak combat turns the same room into a chore; room encounter shape and enemy variety matter more than room count. **cheap**.
12. **Competitor watch.** Play the Emberville demo and Somewhen when available; both aim at the same loop but in 2D, solo. Our differentiators are 3D, co-op, bank-or-lose tension.

Conflicts with current plan: promoting reward previews adds a UI task; "no heroes locked out" conflicts with parked "movement tools gating optional branches" only if those tools are hero-specific - make them shared items.

---------------------------------------------------------------------
## 7. Sources (P = primary/dev, S = secondary)
- RPGFan, Hino interview 2003 https://www.rpgfan.com/feature/rpgfan-exclusive-interview-series-creators-talk-2003-akihiro-hino-dark-cloud-2/ (P, fetched)
- Gematsu, Level-5 on DC3 2009 https://gematsu.com/2009/10/dark-cloud-3-will-happen-if-requested-enough-says-level-5 (P quote, fetched); Kotaku https://kotaku.com/sorry-i-still-havent-made-dark-cloud-3-despite-your-r-5967830 (S, fetched); PlayStationLifeStyle 2017 https://www.playstationlifestyle.net/2017/08/22/dark-cloud-revival/ (S citing Polygon, fetched)
- RPGFan classic news Rogue Galaxy Hino https://classic.rpgfan.com/news/2005/1872.html (P, fetched); Worthplaying Q&A https://worthplaying.com/article/2006/10/23/interviews/37215-rogue-galaxy-ps2-developer-qa/ (P, fetched)
- Wikipedia Dark Cloud / Dark Chronicle (S, fetched)
- Game Critics DC2 https://gamecritics.com/brad-gallaway/dark-cloud-2-review/ (S, fetched); RPGFan DC2 https://www.rpgfan.com/review/dark-cloud-2-2/ (S, fetched); RPGamer reader DC1 review (page reached through https://www.rpgfan.com/review/dark-cloud-2/ fetch, which served a DC1 review) (S); HonestGamers pages https://www.honestgamers.com/5615/playstation-2/dark-cloud-2/game.html and /110/playstation-2/dark-cloud/game.html (S, fetched); ComicBook retrospective https://comicbook.com/gaming/feature/dark-cloud-ps2-retrospective/ (S, fetched)
- ResetEra: https://www.resetera.com/threads/dark-cloud-series-appreciation-thread.388106/ ; https://www.resetera.com/threads/what-was-dark-cloud-like-both-back-then-and-how-it-holds-up-now.734784/ ; https://www.resetera.com/threads/very-late-but-are-dark-cloud-1-2-worth-trying-out-these-days.1324903/ ; https://www.resetera.com/threads/quick-retrospective-dark-cloud-2001.245254/ ; https://www.resetera.com/threads/there-haven%E2%80%99t-been-games-similar-to-dark-cloud-have-there.1162521/ ; https://www.resetera.com/threads/looking-for-a-town-builder-management-rpg.124053/page-2 (all player sentiment, fetched)
- Steam: https://steamcommunity.com/groups/OperationAtla/comments ; https://steamcommunity.com/discussions/forum/0/2793871682812682686/ ; https://steamcommunity.com/app/200710/discussions/0/810921274010214996/ (player, fetched); Emberville https://store.steampowered.com/app/2295170/Emberville/ (P store page, fetched)
- Somewhen https://hopscorch.itch.io/somewhen (P, fetched); TechWiser upcoming list https://techwiser.com/upcoming-rpg-games/ (S, fetched)
- Honestgamers Rogue Galaxy https://www.honestgamers.com/5597/playstation-2/rogue-galaxy/review.html (S, fetched); RPGFan Re:CoM https://www.rpgfan.com/review/kingdom-hearts-rechain-of-memories/ (S, fetched)
- Siliconera Ever Oasis https://www.siliconera.com/ever-oasis-randomly-generated-hieroglyph-labyrinths/ (S, fetched); Siliconera Shiren 6 https://www.siliconera.com/review-shiren-the-wanderer-6-is-packed-with-challenging-dungeons/ (S, fetched); Seafoam Gaming Shiren 6 https://seafoamgaming.com/2024/02/23/shiren-the-wanderer-the-mystery-dungeon-of-serpentcoil-island-nintendo-switch-review/ (S, fetched)
- RPGSite Azuma https://www.rpgsite.net/review/17576-rune-factory-guardians-of-azuma-review (S, fetched); Destructoid RF5 https://destructoid.com/reviews/rune-factory-5-review-destructoid (S, fetched)
- LP Archive Azure Dreams https://lparchive.org/Azure-Dreams/Update%2011 (player, fetched)
- Search-snippet only (S): Almar's medals/transformation pages, Giant Bomb user review, Backloggd users, Game Informer/GamesRadar RF4, RPGamer Azure Dreams retroview, TASVideos, Gamepressure Shiren summary, PSN Store rating 4.81 from 8,573 ratings (DC2 PS4 listing).

## 8. Open questions
- Per-dungeon DC1/DC2 sentiment (Divine Beast Cave to Gallery of Time; Zelmite Mine, Moon Flower Palace, Gundorada): not found in readable sources. Needs Reddit, GameFAQs boards, YouTube comments, or a PC session with broader access.
- YouTube retrospectives, Metacritic user text, HLTB reviews, PSN review text, Push Square DC re-release coverage: not retrieved (search budget exhausted; several sites blocked).
- "Starlight Temple" and "Lunatic Wisdom" are not recognised DC2 dungeon names in anything found; confirm the intended names.
- Emberville release date conflict (Steam page Jan 7 2027 vs TechWiser Oct 27 2026).
- Aeruta, Pokopia, Evershine dungeon design and reception unverified.
- Whether the DC1 "Demon Shaft among my favourite places" quote exists remains unsourced.
