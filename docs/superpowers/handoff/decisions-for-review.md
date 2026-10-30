# Decisions for review

Claude made these calls while working on its own (user, 2026-10-29: "Any design decisions please note and list at the end. We will go through them."). Each one is written so it can be changed easily.

- **Placeholder** means only a simple swap is needed, such as a name or a number.
- **Call** means a design choice the user should confirm.

Change a line in place, or mark it OK.

## Story (from the locked STORY.md, items Claude proposed)
| # | Kind | Decision | Where |
|---|---|---|---|
| S1 | Placeholder | The Bargain was made about **60 years** before the game, and the Long Night happened **10 years** before. | STORY 2 |
| S2 | Call | A few survivors outside the towns (a hermit, a trader, a shepherd) can be met on the road. Claude kept these when the story was locked. | STORY 2 |
| S3 | Call | The four heroes' unclaimed status is explained on Undercroft floor 8, and it is why the Gleaners hunt them. | STORY 2, 8.4 |
| S4 | Call | Each heirloom's legendary form is earned in the chapter where its original owner comes home. | STORY 3 |
| S5 | Placeholder | The Tithe-Keeper is named **Elder Corwin Vane**, and his servants are called **the Gleaners**. | STORY 4 |
| S6 | Call | The final fight has four stages (waking, the fight in the open with the towns helping, inside the maw, the end). The more of the Vale is rebuilt, the more help the player gets in stage 2. | STORY 7 |
| S7 | Placeholder | The Undercroft has 12 floors in 3 bands plus a boss floor. The Phase 1 plan may change these counts. | STORY 8.4 |
| S8 | Placeholder | Supporting names: the Tithe Feast, the Long Night, the Gleaners, the Bell-Warden, and the dungeon names. | STORY 6, 8 |

## Setup (session 1)
| # | Kind | Decision |
|---|---|---|
| T1 | Call | Packages left out for now: unity-mcp, the Tripo bridge, VFX Graph, terrain-tools and `com.unity.pipeline`. Each can be added back when the plan needs it. |
| T2 | Call | Kept the Data assembly and skipped FishGame's Editor.TestMenu. |
| T3 | Call | Turned the REA MCP server back on in `.claude/settings.json`; FishGame had it denied. |
| T4 | Call | Research files now live in fixed folders outside the repo instead of the per-session scratchpad: `C:\Dev\_research\` and `C:\Dev\_private\ps2\`. |
| T5 | Call | Did not port the `fun-formula` memory (user: skip). Ported `reverse-engineering-tools` as a reference. |

## Autonomous run (night of 2026-10-29)
Claude adds lines here as it works.

| # | Kind | Decision |
|---|---|---|
| A1 | Call | Proposed URP starting stack:<br>• Forward+ with the GPU Resident Drawer.<br>• 4 shadow cascades, about 100 m.<br>• Adaptive Probe Volumes for the towns. The dungeons are generated at runtime, so they can't use baked GI.<br>• SSAO.<br>• The Neutral tonemapper plus a grade (compare ACES before locking).<br>• The free MIT CristianQiu volumetric-light package for god rays.<br>To be tested on the beauty corner. Source: `docs/research/urp-stylised-realism.md`. |
| A2 | Flag | Early press describes the OoT 2026 remake's mood as darker and more grounded than our brief, which reads it as saturated daylight. The brief stays as the user's picture; worth checking against the official screens once it launches (5 Nov 2026). |
| A3 | Call | No reference photo shows the teal-and-gold dark look: cellar and cistern photos are lamp-orange. That look is to come from the grade and fog in the style bible, not from the textures. The OoT-2026 shift in `docs/research/style-targets.md` is a proposal and hasn't been measured. |
| A4 | Call | The reference library came from Wikimedia Commons, because Openverse was down. Licences were read from the Commons file metadata and only one landing page was checked by hand. All 88 are CC0, public domain or CC BY. |
| A5 | Call | Kingdom Hearts research stopped at the docs and decomps. Hitstop, i-frame and camera-distance numbers will be tuned by feel in the greybox rather than dug out of the disc data. |
| A6 | Call | The Phase 1 plan is drafted in `docs/PHASE1.md`. It is not locked. Its 51 decisions (P1-P51) are listed in its section 11. Look at these first:<br>• **P10:** hit authority. The attacker's client judges hits for latency; the host still owns HP, AI and rewards. This is a deliberate exception to the host-authoritative rule in CLAUDE.md.<br>• **P1/P2:** Phase 1 ends at Undercroft floor 4.<br>• **P29:** the character pipeline (MPFB2 base, Tripo parts, Mixamo animation).<br>• **P45:** renaming, outfits and the solo town jobs are moved to after Phase 1.<br>• **P7 and P49:** no weapon wear yet, and a dive can fail when the deep closes. |
| A7 | Call | World rules (M1): every session of a multiplayer world needs at least 2 of its recorded players present. Any 2 to 4 of them can play. A multiplayer world can't be started with only 1 player, because solo play has its own solo world. This reads the user's "requires 2+ players to start it" as applying to every session, not just to creating the world. |
| A8 | Call | Asset skills rewritten. The FishGame tools tied to the old style are marked legacy and listed in `sc-asset-craft` 0b: preview, tint, paint pass, score, census, concept match, and adopt's full conversion. Fixing `sc_asset_audit.py` is added to M8: it crashes on kit pieces that have a second UV map and mask-blend vertex colour. |
| A9 | Call | Saves (M1 risk review): an older build keeps a newer build's unknown save sections instead of deleting them. A damaged players list never turns a multiplayer world back into a new one, because the world's kind is stored separately from the list. |
| A10 | Call | A world uses one identity kind, set when it is created. Worlds made with the dev direct-connect identity are separate from online (Unity account) worlds. |
| A11 | **Decided (user, 2026-10-30): (b), weighted toward OoT, slightly brighter, moody; PHASE1 3.1 updated** | We measured 65 official OoT 2026 frames (private, study only; numbers in `docs/research/style-targets.md`). Its daytime is darker and lower-contrast than our approved style bible (PHASE1 3.1):<br>• **Value:** town p50 0.44 and p90 0.78, against our 0.48-0.55 and 0.90-0.94.<br>• **Colour:** plaster is tan (`#b7a680`), not near-white, and shadows are slate-teal (`#2f3a44`).<br>The measurements come from trailer and press grades. PHASE1 is unchanged. Choose one:<br>(a) keep our brighter, cleaner bands, which fit "saturated daylight" and Dark Cloud;<br>(b) move the day bands halfway toward the measured OoT values;<br>(c) match OoT.<br>Claude leans to (b): keep the saturation on foliage, sky and accents, drop it on stone, plaster and roofs, and add teal shadows. Decide before the M9 corner captures. |
| A12 | Call | Reference frames: 65 OoT, 17 DC1, 27 DC2 and 13 genre frames, all in gitignored `Reference/private/`, with sources listed in `Reference/private/SOURCES.md`. Good Dark Cloud in-game town and dungeon shots are scarce online (MobyGames and GameSpot block scraping). PCSX2 captures from your own discs would fill the gap; that needs your BIOS. One CC BY Saltwake image with no named creator was dropped. |
| A13 | Placeholder | "Up to 10 worlds" is read as 10 solo plus 10 multiplayer, one tab each. If you meant 10 in total, it's a one-number change. |
| A14 | Call | If players drop out mid-session and only 1 is left, the session carries on and difficulty scales down. The 2-player rule applies only when a session starts. |
| A15 | Call | Kingdom Hearts ideas added alongside your three picks, because they work with them: hero abilities unlocked by weapon milestones (kept one tier weaker on other weapons), and spell pips that refill by landing hits (the staff). |
| A16 | **Decided (user, 2026-10-30): the creator always hosts** | A multiplayer world's save lives on the host's PC. A group without that host can't play it unless the save travels: by cloud save, or by handing it to another recorded player. Choose cloud save later, a manual hand-off, or "the creator always hosts". |
| A17 | Call | World cap: 10 per tab, counted separately for dev direct-connect and online worlds (the multiplayer tab can hold 10 of each). Deleting a world sets the file aside rather than erasing it. |
| A18 | Placeholder | M4 weapon numbers are placeholders to tune in M3/M7:<br>• **Attachments:** +5 fire, +5 pest, +2 ATK.<br>• **Build-up targets:** Cinder Axe 30, Warden Staff 20, Bane Dagger 15, Ember Bow 18.<br>• **Slots:** 3/3/2/2.<br>• **Style experience:** 1-3 per event, capped at 6 per enemy.<br>• **Tech bonus:** +50%.<br>• **Abilities:** unlock at weapon level 3 or a build-up.<br>• **Spell pips:** 3 to start, 1 per 4 hits.<br>The names are placeholders too. |
| A19 | Call | M4 rules:<br>• Fitted attachments do nothing until the next level-up absorbs them.<br>• A re-route keeps the hero abilities it unlocked.<br>• No weapon level cap yet.<br>• Tech bonuses don't stack with each other, but they do stack with the finisher bonus. |
| A20 | Call | A re-route gives back any attachment that was absorbed after the build-up, to the bag. The stats return exactly as they were and no item is lost, keeping "nothing is irreversible" (P14). |
| A21 | Call | Scene builders now renumber Unity's scene file IDs, so rebuilding a scene leaves git clean. There's a guard for prefab instances and in-scene network objects. |
