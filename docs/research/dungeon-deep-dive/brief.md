# Shared context for every dungeon research agent (Sleeping Calamity)

## The game
Sleeping Calamity: an original action RPG in the Dark Cloud tradition (Unity, 3D, third-person, KH/DC-style real-time combat). Dive into dungeons (the Hearthdeep, one region under each of six towns), carry pieces back to rebuild ruined towns, weapons grow by use, calm side activities between dives. Solo, or co-op for 2-4 players (host-authoritative), difficulty scales to the players present. Four heroes with distinct abilities. Art: storybook stylised realism. We are NOT one game: we take parts and ideas from many games and make our own thing. Dark Cloud-likes are rare, so fixing what players disliked about DC1/DC2 dungeons is an opportunity.

## The current dungeon design (Phase 1, Undercroft Cellars floors 1-4)
- Each dungeon ~12 floors in 3 themed bands of 4 + a boss floor; each band ends in a fixed hand-authored set-piece floor (floor 4 = an oven maze where a rescued resident sleeps, guarded by an elite).
- Generated floors: 14x14 grid of 4 m cells, 4-6 hand-authored rooms from a pool (12 combat rooms, entry, exit, checkpoint; 3 sub-themes per band, ~70% from the floor's sub-theme), corridors matched by 4-bit mask, each room linked to a random earlier one, 1-3 loops, 1-3 dead ends. Seed from host so clients build the same floor.
- Floor loop: one enemy carries the gate key to the exit; 50% of floors have a locked room whose key is in a chest; 6-8 chests with rank-filtered loot; town pieces spread across floors with a "pieces left" counter; floors stay cleared; healing points on ~half of floors.
- Checkpoint after floor 2; banking: the shared haul (held by the host) is only safe once banked; a failed dive loses only the unbanked haul and lost pieces return to their floors.
- Stir meter 0-100 (rises per floor cleared and per unbanked piece, scaled by player count): at 40 one more enemy per room; at 70 a roaming tier-2 hunter chases the party; at 100 the deep closes: 90 s to reach a bank point or the dive fails.
- Planned stretch: optional dressing sockets with fill chance, placement jitter, per-floor atmosphere (rare dark floor), vignettes with pity counters, layout presets (spine/ring/hub/wing), chunk regions inside rooms, roster subsets per floor, rewards shown on doors.
- Parked ideas: co-op "hearth marks" (2+ players push/brace together to open secrets, always with a solo route), movement tools gating optional branches, rule-twist challenge gauntlets, keepsakes returned to residents.

## Already researched in the repo (do NOT redo; only cite briefly if needed)
Procedural generation internals of Brogue, Spelunky Classic, Diablo 1 (DevilutionX), Pokemon Mystery Dungeon floor gen, Hades room/reward rules, Unexplored graph rewriting, Enter the Gungeon flows, Isaac's room-count formula and 174 room templates, Cogmind, DunGen, Dead Cells. DC1 and DC2 internals from decomp (generators, chests, keys, monsters, Georama). Genre landscape of Rune Factory 5/Azuma, Fantasy Life i, DQ Builders 2, Ni no Kuni II, Atelier Ryza 3.

## Rules for your work
- Research with WebSearch (use "standard" mode first, "extended" when thin) and WebFetch. Go deep: keep going until new sources mostly repeat what you already have. No artificial limit on number of sources. Prefer primary sources (developer talks, postmortems, interviews, design docs, wikis with numbers, source code) and real player sentiment (Reddit threads, GameFAQs boards, Steam reviews, review aggregators) over listicles.
- IP rule: write in your own words. Mechanics, structures and numbers only; never paste text, never copy story/names into our design. No ROMs/ISOs/decompiled output.
- Write your notes progressively (append as you go so nothing is lost) to the file path given in your task. Structure:
  1. Per game/topic: how its dungeons are structured (mechanics, numbers), what players loved, what they complained about (with evidence: link + one-line gist), and what the developers said about it if found.
  2. Cross-cutting patterns you noticed.
  3. "For Sleeping Calamity": concrete ideas mapped to our design above (keep/steal/fix/avoid), each tagged **cheap** (Phase 1 sized), **later**, or **big**, and each checked against our constraints: real-time 3D third-person, co-op 2-4 with any subset of players, procedural floors from authored rooms, DC-style dive-and-bank loop, storybook tone. Note where an idea conflicts with our current design and why.
  4. Sources list with URLs, marked primary/secondary.
  5. Open questions / things you could not verify.
- Mark confidence: what you verified from a source vs. what is your inference.
- Final reply to the controller: a SHORT summary (under 400 words): the 8-12 strongest findings and the file path. The file is the real output.

## Wave 2 additions (read carefully)
- Wave 1 agents stopped too early (~20-30 searches). Minimum effort for wave 2: at least 40 WebSearch calls and 15 successful WebFetch reads before you write the summary, and keep going past that while new sources still add facts. Stop only when sources repeat.
- reddit.com is blocked by the network proxy; GameFAQs, GameSpot and some fandom pages often 403. Do NOT try to get around blocks (no alternate front-ends, no scraping tricks, no API tricks). When a page is blocked, find the same facts in another public source: ResetEra, Backloggd, Metacritic, Steam store reviews, developer blogs, Game Developer (gamasutra) articles, Wikipedia, publisher sites, interviews, academic papers, official patch notes, other wikis.
- Read the wave-1 note file named in your task first, so you extend it instead of repeating it. Write to a NEW file (given in your task); do not edit wave-1 files.
