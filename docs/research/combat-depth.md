# Combat depth: how action RPGs stop combat becoming button mashing

Written 2026-10-30 after the user's verdict on the M3 arena ("it mostly feels like mindless button mashing right now"). In our own words: mechanics and
numbers only. Source tiers as in the other research files: **[decomp]** our reading of a community decompilation, **[rea]** our own reverse-engineering
notes, **[doc]** developer interview or official text, **[wiki]** fan wiki, **[forum]** player discussion, **[general]** widely known genre practice with
no number found this session. Where a number was not found it says so; our proposals are P9 Placeholders, not borrowed values.

## Why ours mashes today (the diagnosis)
Read from the code on `main` (`8e8eefb`):
- `EnemyModel.ApplyHit` sets `Staggered = !armoured` for every non-elite hit, and small-enemy attacks have no armour window. **Every hit flinches every
  slime, rat-king and broom, and a flinch cancels the attack in progress.** Hitting an enemy during its wind-up is therefore the safest thing to do: the
  tell we built in the enemy-polish pass is erased by pressing attack.
- Enemy life is low against the heirlooms: a rat-king (90 HP, DEF 10) takes about 80 damage from one axe combo (20 + 22 + 38) and 37 from one dagger
  combo, so it dies inside one or two uninterrupted chains.
- The dagger's parry restarts on every press (`HeroCombat` sets `_guardStart` whenever the guard goes up), so tapping RMB repeatedly also parries.
- Rage (+30% attack after 7 hits) exists but never matters, because the enemy never gets to act.
- Nothing pays the player more for hitting at the right moment than at any moment.

Every game below solves the same problem the same way at heart: **an enemy must be able to keep acting while being hit, and the player must earn the
moments when it cannot.**

## What the games do

### Dark Cloud 1 and 2 (our own lineage)
- **DC2 poise accumulator [decomp, ChronicleTwo `monster.cpp`].** Each hit adds the attack's `stagger` value to the monster's counter; the counter clears
  60 steps after the last hit; the monster flinches only when the counter reaches its table threshold. A threshold of 0 means every hit flinches; a table
  flag makes a monster immune; a knockdown hit bypasses the count and clears it. The hero has the same rule with a fixed threshold of 2.
- **DC2 monster guard [decomp].** On each damaging hit a monster guards if its `guard_rate` beats a d100 roll (not while dizzy, not against guard-ignoring
  attacks, a script can force it). A guarded hit is scaled down by the attack's guard factor.
- **DC2 hit invulnerability [decomp].** A monster just hit ignores new hits for the attack's stun time, which caps how fast damage can be poured in.
- **DC2 rage [decomp, already in `dc2-combat-weapons.md`].** Each hit lowers a counter; at zero a non-boss hits 1.3x for 900 steps. Rage is DC2's answer
  to "hit it many times": the monster gets more dangerous the longer the fight drags.
- **DC1 guard windows [rea, `dc1-rea.md`].** Monster scripts open timed guard windows (`SET_GUARD_FRAME`, `GUARD_SEARCH`); a hit inside one deals almost no
  damage (the weapon still wears). DC1 flinch is 50 ticks on the first row measured and cancels the monster's attack (`combat-timings.md`), but DC1 places
  monsters far apart (at most 3 near each other) so you rarely face more than one or two at once.

### Kingdom Hearts 1 and 2 (Critical and Proud)
- **Revenge [wiki, `kh-combat-camera.md`].** A hidden per-enemy gauge filled by each hit (opener 1, finisher 3, big spells 4-6); at the cap the enemy breaks
  out with a counter. Not only bosses: strong regular enemies have it too (Dusks, Reapers and Sorcerers are listed among enemies with revenge actions
  [wiki]). This is the direct "punish the mash" tool: the third uninterrupted combo on a Dusk gets you hit.
- **Super armour per animation window [doc].** KH's attack data switches armour on and off per frame range, so many enemies cannot be interrupted once an
  attack is committed.
- **Critical / Proud [wiki].** Mostly double incoming damage. No source this session says revenge values change on Critical. Our KH1 notes already flag
  "difficulty as only a damage multiplier" as something to avoid.
- **Reaction commands [doc].** An enemy's own attack opens a counter window: the reward for reading it is a strong, safe hit.

### Zelda (Ocarina of Time 3D / 2026, Breath of the Wild, Tears of the Kingdom)
- **One at a time [doc, Iwata Asks: OoT 3D].** The original team found that several enemies swarm the player; Z-targeting flags one opponent and tells the
  others to wait, so a group fight is a series of one-on-one duels. Our strongest source for "fewer, tougher, one duel at a time".
- **Blocking enemies [wiki].** Lizalfos and Stalfos block and counter; a shield block makes a Lizalfos lose its footing briefly, opening it. You win by
  waiting for the opening, not by swinging into the guard.
- **Flurry rush and perfect guard [wiki, forum].** A dodge made at the last moment before a hit opens a slowed flurry of free hits; a shield raised at the
  last moment deflects. Guides agree the window is tight and early dodges are the usual mistake. **No frame numbers found.** The slow-time part cannot be
  copied into host-authoritative co-op; the idea we can use is "a last-moment dodge opens the attacker".

### Hades (I and II)
- **Armour [wiki].** Elite foes carry an armour bar on top of life; while it holds they do not stagger and ignore many control effects; breaking it stuns
  them briefly and they flinch normally afterwards. Excess damage past the armour is lost. Players complain the game never explains that armour stops
  stagger: **the state must be visible.**
- **Fewer but meaner encounters [general].** Chambers mix a few dangerous foes with fodder; the fodder dies to mashing, the elite does not.

### God of War (2018)
- **Stun meter [doc/forum].** A third bar under the enemy's life fills as you land blows (fists, wall slams and the boy's arrows fill it much faster than
  the axe); when full, a grab prompt executes or heavily damages the enemy. Weapon choice matters because each tool fills a different bar.
- **Shielded and armoured enemies [general].** Some enemies must be opened by a specific tool (a guard break, a thrown axe, a runic) before normal hits work.
- GDC 2019 talks on the combat redesign (Sheth, McDonald) exist; slides were not read this session.

### Devil May Cry 5
- **Super armour states [wiki].** Some enemies (the Hell Antenora variant of the Hell Caina family) gain a visible armour state in which normal attacks do not
  stagger or knock them down. Players on the hardest mode complain about larger health bars *and* harder stagger together: armour should replace health
  padding, not stack with it.

### Monster Hunter, Souls-likes, Sekiro, NieR: Automata
- **Commitment [general].** Monster Hunter's attacks cannot be cancelled into a dodge for most of their length; heavy weapons hit hard but leave you open,
  so you swing only into openings the monster gives (after its own big attack, when it is tripped or stunned). Monsters themselves rarely flinch: flinch
  and trip come from accumulated part damage. No numbers gathered this session.
- **Stamina [general].** Souls games charge stamina per swing and per roll; an empty bar leaves you unable to block or roll. It works, but it gates the
  player rather than making enemies answer the mash, and Dark Cloud has none. Ranked low.
- **Poise [general].** Souls enemies (and heroes) have a hidden poise value; light hits do not interrupt a heavy enemy's swing, so trading hits with it
  loses.
- **Sekiro posture [wiki, forum].** A posture bar that fills from deflects much more than from hits and drains when the enemy is left alone; full posture
  opens a deathblow. A well-timed deflect builds enemy posture and some of your own back; a mistimed one costs chip damage later in the game. A deflect cannot
  be mashed out. No regeneration numbers found.
- **NieR: Automata [forum].** A perfect evade grants a counter; players report the counter window is tighter than the evade window. No frame data found.

### Design writing on mashing
- **Critical Points [blog, 2023 and 2024].** Mashing is a real skill when a game rewards it; to discourage it cap how much repeated presses can do, or lock an
  action out for a period after a press (a primed window, then a lockout). Smash Bros is the cited example of a game that shuts mashing down.
- Fighting games name the two tools we want: **counter hits** (interrupting an attack at the right time pays extra) and **clashing** (attacks that trade).

## The mechanics, ranked for Sleeping Calamity
Ranked by how much each fixes "mindless mashing" for what it costs us (Core code, replication, art, risk to what already works).

| # | Mechanic | What it does against mashing | Who does it | Cost to us |
|---|---|---|---|---|
| 1 | **Poise threshold with a reset window** (flinch only after enough stagger in a short time) | A string of light hits no longer locks an enemy; heavy hits and finishers matter | DC2, Souls, MH, Hades armour | **Small.** Core counter per enemy (like `RageTracker`), stagger value per attack as data, host-only. Must scale with party size like revenge. |
| 2 | **Hyper armour once committed** (from the glint to the end of the strike) | Hitting into a committed attack trades: you take the hit. Reading the tell before the glint is rewarded, swinging blind is punished | KH, DMC, Souls | **Small.** The tell clock is already replicated, so every client can predict it; a "clank, no flinch" cosmetic. |
| 3 | **Openings with a damage bonus** (after the enemy's strike, after a parry, after a poise break) | Gives the player a reason to wait: the same swing is worth more at the right moment | KH reaction commands, Zelda, MH, fighting-game counter hits | **Small-medium.** Host reads the enemy's phase; a distinct spark and sound so it is learnt. |
| 4 | **Post-stagger steadiness** (cannot be flinched again for a moment after a flinch) | Stops poise-break, flinch, poise-break stunlocks; one flinch per exchange | DC2 knockdown reset, Souls poise regen, Hades armour regen | **Tiny** once 1 exists. |
| 5 | **Fewer, tougher enemies; one duel at a time** | Each enemy has enough life to show several attacks; the others wait | OoT, DC1 spacing, KH hovering | **Small.** `ArenaWaves` data, HP data, attack-token count. |
| 6 | **A visible enemy guard that ripostes** (blocked hits build to a counter) | Hammering a guard gets you hit; flank or break it | DC1 guard windows, DC2 guard, Lizalfos, KH Large Body | **Medium.** One new enemy attack (must pass the tell rules), a guard state shown on the body. |
| 7 | **Revenge on regular tough enemies** (a counter after too many uninterrupted hits) | Caps any combo that ignores the enemy | KH Dusks | **Medium.** Overlaps 2 and 6; one new attack per enemy kind. |
| 8 | **Perfect dodge reward** (a last-moment roll opens the attacker) | Rewards reading the glint with offence, not only safety | BotW/TotK flurry rush, NieR, Bayonetta | **Medium, networking.** Victim-judged like the parry, one ServerRpc; window must be far tighter than the 0.4 s safe roll or roll-mashing earns it. No slow-time in co-op. |
| 9 | **Parry lockout** (a re-raised guard does not parry for a moment) | Stops parry mashing | Sekiro, Critical Points' lockout | **Tiny.** One constant in `GuardMath`, owner side. |
| 10 | **Rage from chip hits** (only hits outside openings count toward rage) | Makes the mash itself dangerous, reuses DC2 rage | DC2 | **Tiny** once 3 exists. |
| 11 | **Stun / posture meter with an execution** | A long-run reward for good play | GoW, Sekiro | **Medium-large** (UI, execution animation). Later, maybe for bosses. |
| 12 | **Stamina** | Gates the player's mashing directly | Souls | **Medium**, and off-genre for Dark Cloud. Not recommended. |

## Gaps
- No frame windows found for BotW flurry rush, TotK perfect guard, NieR perfect evade, Sekiro deflect or posture regeneration; our windows are proposals.
- DC2's per-monster stagger thresholds and per-attack stagger values live in data tables we did not extract (`Damage_Param_Table`, `BASE_MONSTER_TBL`).
- GoW 2018 GDC slides and Monster Hunter motion-value / commitment data not read.
- Whether KH2 Critical changes revenge caps: not found.

## Sources
- ChronicleTwo (DC2 decompilation, `C:\Dev\_research\ChronicleTwo`): `ps2/src/monster.cpp` hit handling (guard roll, stagger accumulator, rage),
  `ps2/src/actionchara.cpp` (hero stagger threshold 2, 60-step reset).
- Our notes: `docs/research/dc1-rea.md`, `dc2-combat-weapons.md`, `combat-timings.md`, `kh-combat-camera.md`, `kh1-sweep.md`, `kh2-sweep.md`.
- Iwata Asks, Ocarina of Time 3D, original development staff: https://iwataasks.nintendo.com/interviews/3ds/zelda-ocarina-of-time/1/2
- KH wiki: https://www.khwiki.com/Revenge_Value , https://www.khwiki.com/Revenge_Action , https://www.khwiki.com/Forum:Notes_41_(Revenge_Value)
- Zelda wikis: https://zeldawiki.wiki/wiki/Lizalfos , https://zeldawiki.wiki/wiki/Flurry_Rush , https://zeldawiki.wiki/wiki/Perfect_Dodge
- Hades armour: https://primagames.com/gaming/how-to-break-enemy-armor-in-hades-2 , https://www.noobfeed.com/blogs/5209/hades-ii-learning-how-armor-works
- God of War stun: https://blog.playstation.com/?p=370399 , https://gdconf.com/news/see-how-god-wars-combat-evolved-match-its-new-camera-system-gdc
- DMC5: https://devilmaycry.fandom.com/wiki/File:Hell_Antenora.jpg , https://steamcommunity.com/app/601150/discussions/0/1771511442697149588
- Sekiro: https://sekiroshadowsdietwice.wiki.fextralife.com/Deflection , https://samurai-gamers.com/sekiro-shadows-die-twice/posture-system/
- NieR: https://steamcommunity.com/app/524220/discussions/0/135511913385581507
- Critical Points: https://critpoints.net/2024/03/22/no-more-than-mashing , https://critpoints.net/2023/05/16/mashing-rote-memory-vs-system-mastery/
