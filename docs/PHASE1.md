# Sleeping Calamity: Phase 1 plan

**Status: APPROVED (user, 2026-10-30).** The decisions P1-P51 were accepted with the plan; any later change is deliberate and goes in a change log at the end with the date and who asked. Every call is still tagged `[Pn]` in the text and listed in section 11.

Sources: `CLAUDE.md`, the handoff (`docs/superpowers/handoff/2026-10-29-new-project-handoff.md`; its "User decisions" are settled), `docs/STORY.md` (locked), `docs/design/dark-cloud-lessons.md`, `docs/research/*.md`, `docs/superpowers/handoff/decisions-for-review.md`. Numbers marked "DC1", "DC2" or "KH" come from those research notes; everything else is ours, as a starting point for tuning.

## 1. Scope: what Phase 1 delivers

Phase 1 proves the core loop in greybox and the art target at final quality, in the first area. It ends at **Undercroft floor 4, the baker's rescue**. That is the story's first "someone comes home" beat (STORY 8.4), and the bakery is in rebuild ring 1 (STORY 8.3). [P1]

**A. A networked greybox vertical loop, 2-player co-op from day one** (solo is the same code as a local host):
1. Pick a hero, move, camera, soft target and hard lock-on.
2. Combat with all four heirlooms: Tobin's axe and Pip's dagger and shield tuned deep; Wren's staff and Ash's bow and sword playable at base depth. [P3] Only the four heirlooms exist in Phase 1. [P4]
3. Three Cellars enemy types (jam-jar slime, rat-king, sweeping broom) in two tiers each, plus one floor-4 elite. [P5]
4. Weapon growth in Core: experience, level-ups, attachments, one build-up step per heirloom, and a visible weapon tree, with EditMode tests.
5. The Cellars band, floors 1-4: floors 1-3 generated from hand-authored room pools, floor 4 a fixed bakery set-piece. Gate keys, chests, a checkpoint, banking, and the stir meter.
6. Carrying the bakery's pieces and the baker home, placing them in a greybox Hollowmere square with a live wish preview, and the baker's wish check. The world save holds it all.

**B. One beauty corner of the Hollowmere square at final quality,** approved by the user on captures (section 3.7).

**C. One dungeon corner of the Cellars at final quality,** approved the same way.

**D. One hero, Tobin, through the full character pipeline** (section 5), shown in the beauty corner.

**Not in Phase 1** (named here so nothing is lost; move to `docs/PARKING_LOT.md` when it exists): the Cisterns and Tithe Roots bands, the Bell-Warden, residents other than the baker, the lanes and tower rings, weapons other than heirlooms, synthesis and spectrumizing, weapon wear [P7], fishing (opens in Saltwake), day and night, the journal, renaming, outfits, the solo town jobs of the three heroes not diving [P45], final art for the other three heroes, enemies and the baker, and dressing the whole square. Dressing the whole square at final quality is the vertical slice after Phase 1 (handoff 5). It reuses every kit and tileable that Phase 1 builds. [P2]

## 2. Milestones

There are two tracks plus the character test. Each milestone is "done" on the evidence listed. `check` means `Tools/unity.ps1 check` green on the merge commit. Walks follow `sc-walk`.

| # | Milestone | Done when | Runs alongside |
|---|---|---|---|
| **M1** | **Foundation port** (section 8): Core utilities, Runtime services, App flow, Networking, Editor builders and validation, test patterns. Plus the world rules: solo or multiplayer world, a multiplayer world needs 2+ players to start, 4 recorded players, any subset can play. Claude hands the user the Mixamo clip list for M2. | `check` green; EditMode tests for the world-player rules (a multiplayer world can't start with 1 player, a 5th player is refused, a solo world refuses joins); a PlayMode host+client connect test; a manual 2-player Relay join walk on the PC | M8, M10 (concept stage) |
| **M2** | **Networked hero, camera, lock-on** on a builder-made greybox test arena, with a mannequin hero [P32] and Mixamo locomotion (the user downloads the clips from the M1 list) | EditMode tests for target picking (cone, hysteresis, LOS grace, flick switch); a PlayMode 2-player test that both players move and lock independently; a short capture of free and lock cameras | M4, M8 |
| **M3** | **Combat greybox:** attack data with frame windows, the damage formula, combos, hitstop, dodge, guard, the revenge gauge, the four heirlooms, the three enemies and the elite with telegraphs, and the co-op hit flow (section 6.4) | Core EditMode tests for damage, combo, revenge and scaling; a PlayMode 2-player hit test; a 2-player walk with the network simulator at 150 ms RTT and 2% loss; **the user plays a 5-minute arena and says the feel is right** (or names what isn't) | M4, M5 room authoring, M9 |
| **M4** | **Weapon growth in Core:** experience, level-up, attachments absorbed at level-up, build-up with re-route, and a greybox weapon-tree UI | EditMode tests for every rule in 6.3, including "no irreversible choice" (build-up then re-route returns the same stats) | M2, M3 |
| **M5** | **Cellars floors 1-4:** a Core floor generator, the room pool as data, keys, chests, checkpoint, banking, the stir meter, and the floor-4 bakery set-piece | EditMode: the same seed gives the same layout; 10,000 seeds all valid (connected, exits reachable, key reachable); the repetition metric in 7.2 passes; PlayMode: two clients build an identical floor from the host's seed; a 2-player walk of floors 1-4 | M3 tuning, M9 |
| **M6** | **Hollowmere square loop:** a greybox square, carrying the haul home, placement mode, the baker's wishes and service, and the save | EditMode tests for wishes and placement (port of `Core/Village/Placement`); a save round trip plus a "bad section keeps the rest" test (`sc-netcode`); a solo and a 2-player walk: dive, rescue, bank, place, wish met | M9 |
| **M7** | **Loop integration and tuning:** the full loop in 2-player, then a 3-4 player smoke test with Multiplayer Play Mode [P44] | The user plays the whole loop in 2-player co-op (two PCs or Multiplayer Play Mode) and the tuning notes are applied; the 4-player smoke run shows no desync in the log; `check` green | M10 in-game step |
| **M8** | **Style bible into the tools:** port the `fg-aaa-prop` tools; rewrite the style sections of `sc-asset-*`, `sc-texture-paint`, `sc-foliage`, `sc-scene-look`, `sc-reference` and `.claude/rules/art-assets.md` from section 3; build a look-test scene with the URP stack; produce tileable and trim batches B1-B3 (section 4) | A reviewer reads the rewritten skills against section 3; look-test captures are measured with `sc_ref_palette.py` and fall inside the 3.1 bands; the B1-B3 contact sheets are reviewed | M1-M5 |
| **M9** | **Beauty corner and dungeon corner** (batches B4-B11), the approval gate in 3.7 | The captures are measured inside the bands; the batch reviews are done; **the user approves both corners on captures** | M3-M6 |
| **M10** | **Tobin through the character pipeline** (section 5) | Turntable and in-game captures in the beauty corner; idle, locomotion and axe combo play in-game on the mannequin's controller; **the user approves Tobin**; hours and usage are logged so the next three heroes can be costed | Concept stage from M1; rig and import after M2 |

Order: M1 first, because all code depends on it. M8 and the M10 concept stage start the same day. M4 is pure Core and can run beside M2 and M3. Room-pool authoring for M5 can start during M3. M6 needs M5's haul and M4's save shapes. M9 needs M8. M7 closes the phase. The OoT remake launches on 5 Nov 2026 (decisions A2): before the M9 captures, compare the targets against its official screens in `Reference/private/`. That is a check, not a blocker.

## 3. Style bible

The target: Dark Cloud's whimsical shapes and town and dungeon themes, rendered in the stylised realism of the 2026 Ocarina of Time remake. Concretely, that means real PBR materials, chunky storybook forms, clean saturated day colour, rich dark scenes, and heavy atmosphere. The numbers below start from `docs/research/style-targets.md`. They are graded on **in-engine captures**, measured with `Art/Blender/tools/sc_ref_palette.py` (k=7, Lab), the same tool that measured the references. [P33]

### 3.1 Palette and value

| | Day (Hollowmere square) | Cellars (Undercroft floors 1-4) |
|---|---|---|
| Value p10 / p50 / p90 (sRGB max channel) | 0.21-0.25 / 0.44-0.50 / 0.80-0.86 | 0.06-0.10 / 0.24-0.32 / 0.55-0.68 |
| Warmth (lights minus darks) | +0.12 to +0.20 | +0.25 to +0.35 |
| Masses | 3-4 readable masses: one pale (plaster, sky), one dark (timber, shadow), one warm mid (stone, roof) | Gold lamplight dominant, deep shadow, warm brick mid |
| Key hues | plaster warm tan-cream `#cbbd9c` (OoT `#b7a680`, a little lighter); roof red-terracotta `#9c4a34`; timber `#2e2520` to `#4e4844`; cobble `#817a7b` to `#b7b0aa`; foliage yellow-green `#6f8a34`; moss `#616e2e` / `#b6ab28` | lamp gold `#d6a24a`; lit stone `#c9aa76`; brick `#9d7d69` / `#bf7a38`; shadows deep warm umber to plum `#2a1c18` to `#3a2430` (no teal or green); ember accent `#c2412a` (the demon's breath) |
| Saturation vs photos | OoT-like, moody and slightly muted: no chroma boost on stone, plaster or roofs; +10-15% only on foliage, sky, painted wood, cloth and accents; shadows tinted slate-teal (`#2f3a44`) | Clean, rich firelight gold (saturated, not brown-muddy); clear value steps between lit stone, mid brick and shadow; no mass is pure black |

**The Cellars are warm firelight, clean, never muddy (user, 2026-10-30).** STORY 8.4 gives them warm lamplight; teal belongs to the Cisterns, so the Cellars carry no teal or blue-green tint at all. Gold firelight is the dominant light mass, shadows are deep warm umber to plum, and the red ember glow is a small accent. The user liked the first warm fire-glow pass but it read as a muddy, Skyrim-like dungeon: keep the warmth while making it cleaner and more storybook (more saturated gold, crisper value separation, less brown haze and fog, readable shapes). [P34]

### 3.2 Materials (PBR: albedo, normal, ORM; tidy, placed wear over clean base colour)
- **Town:** lime plaster (warm cream, faint trowel relief), adzed oak timber, granite cobble with warm lights, dressed sandstone (the fountain), red clay roof tile, wrought iron, painted wood (shutters, stalls: confident blues, greens and ochres), canvas and linen (awnings, banners: ochre, madder red), rope, moss, ivy, and grime/drip decals.
- **Cellars:** old brick (tan and orange), vault rubble stone, damp limewash, flagstone floor, oak barrel staves with iron hoops, glazed jars and crocks, coal and soot, roots, and ember glow (emissive in cracks).
- **Method:** each material is made once per area as a smart material: a CC0 scan (Poly Haven, ambientCG) plus procedural layers in the `aaa_mats2.py` style. Every kit piece shares it (handoff 3.5).

### 3.3 Texel density and budgets [P35]
| Class | Texel density | Triangle budget (LOD0) | Textures |
|---|---|---|---|
| Ground, walls up to 3 m, cellar walls and floors | 1024 px/m (tileables) | kit piece 0.5-5k | shared tileables and trims, 2K |
| Upper storeys, roofs, vault crowns | 512 px/m | as above | shared |
| Instanced sculpted pieces (cobbles, tiles, rubble, roots) | 1024 px/m, one baked set per family | 50-400 each | 1 x 2K per family |
| Hero props | 1024-2048 px/m | 30-60k | unique bake, up to 2 x 4K |
| A whole building (kit + heroes) | n/a | at most 75k | 2x4K + 4x2K + 2x1K (cottage budget) |
| Hero character | n/a | 60k | 1 x 4K body and outfit, 1 x 2K head and hair |
| Enemy (later) | n/a | 25k | 1 x 2K-4K |

### 3.4 Shape language [P36]
- Take dimensions from photos, then exaggerate. Timber sections are about 1.4x real (25 cm and up), arches and voussoirs about 1.3x, and roof overhangs and chimney stacks are bigger and chunkier.
- No dead-straight long lines on old work: beams and walls are 1-3 degrees off true, roof ridges sag 2-5 cm per metre, and nothing aged runs perfectly straight for more than 2 m.
- Every hard edge seen at play distance has a visible chamfer of at least 1.5 cm. Silhouettes are rounded and readable at 20 m.
- Make things the way the real object is made (planked, coopered, coursed, turned). Spalls get square arrises (handoff 4.3 lessons).
- Scale: doors 2.2 m against a 1.75 m hero; props about 10% oversized.

### 3.5 Light and atmosphere [P37]
Use the URP stack proposed in decisions A1: Forward+, GPU Resident Drawer, HDR, Neutral tonemapper plus grade (compare ACES once), SSAO (Depth Normals, radius about 0.35 m, low direct-lighting strength), bloom (threshold about 1.0, modest intensity, high scatter, no lens dirt), and the CristianQiu volumetric package for god rays with authored cone cards as the fallback.
- **Day:** one warm sun (about 5200 K) and a cool sky fill; 4 cascades, about 100 m, 4096, soft High; Adaptive Probe Volumes for the town; exponential haze matched to the horizon colour; drifting pollen and leaves.
- **Cellars:** generated floors can't use baked GI, so they are lit in realtime. Forward+ lamps, with shadows only on the nearest 2-4; a per-band ambient gradient; SSAO; emissive lamps and embers; dust motes in the lamp shafts. If the dungeon-corner captures read flat, the next test is additive-loaded room scenes, each with its own APV bake.

### 3.6 Character proportions [P31]
- About 6.5 heads tall: storybook, not chibi.
- Hands and feet about 1.15x real, with chunky boots, gauntlets and belts (OoT-2026 Link).
- Faces realistic in material, slightly stylised in form: eyes about 1.1x.
- Heights: Tobin 1.90 m, Ash 1.75 m, Wren 1.68 m, Pip 1.58 m.
- Each hero reads at 30 m from silhouette alone: Tobin broad, Pip small with a shield, Wren with the staff's height, Ash with the bow across the back.

### 3.7 The approval gate (beauty corner and dungeon corner)
- **Captures:** three fixed capture cameras per corner (wide, mid, close) at 1920x1080 [P39], built by an Editor builder so every round is identical. The town is captured at midday plus one low-sun shot.
- **Before showing the user:** the captures fall inside the 3.1 bands, and a reviewer has scored them with the rewritten `sc-asset-review` rubric against the reference row (the photos and paintings in `Reference/`, plus the private style frames for study).
- **The user approves on captures.** That is the only sign-off in the art track. If the user rejects, their notes become the next round.
- Once approved, the captures become the reference row that every later batch is reviewed against.

## 4. Asset list (square and Cellars band)

The tiers follow handoff 3.5: **T1** tileable or trim, **T2** modular kit (no unique bake), **T3** instanced sculpted piece (a few highs baked once, scattered), **T4** hero prop (unique bake, blind A/B). Methods: **Gen** is a procedural Blender generator (`sc-asset-craft`, rewritten); **Scan+** is a CC0 scan plus procedural layers; **Adopt** is a CC0 model brought in through `sc-asset-adopt`; **AI** is a Tripo model plus cleanup (`sc-ai-model-pipeline`). "Phase 1" means the item is needed for a corner; "Slice" means it is listed now and built in the vertical slice. [P38] [P40]

| Batch | Items | Tier | Method | When |
|---|---|---|---|---|
| B1 Town tileables | plaster, oak timber, cobble, sandstone, roof tile, painted wood, canvas, iron | T1 | Scan+ | Phase 1 |
| B2 Cellar tileables | old brick, vault stone, limewash, flagstone, barrel oak, soot and coal, root bark | T1 | Scan+ | Phase 1 |
| B3 Trims and decals | timber trim, stone moulding, iron trim, one trim sheet per area; moss, grime, drip and crack decals | T1 | Scan+ / Gen | Phase 1 |
| B4 Half-timber facade kit | wall panels (plain, window, door), posts, braces, jetty beams, sills, a roof module, a chimney, shutters, a door | T2 | Gen | Phase 1 (one facade); the rest Slice |
| B5 Cellar vault kit | barrel-vault segment, end wall, wine niche wall, arch, pillar, stair, floor tiles, coal chute | T2 | Gen | Phase 1 (one room); rest Slice |
| B6 Sculpted scatter | cobbles, flagstones, roof tiles, rubble, bricks, root clusters | T3 | Gen high, bake once | Phase 1 |
| B7 Prop family | barrels, crates, sacks, jars and crocks, buckets, a wine rack, larder shelves | T2/T3 | Gen | Phase 1 |
| B8 Market dressing | a market stall, awning, harvest banners, bunting, baskets, lanterns | T2 | Gen | Phase 1 (one stall); rest Slice |
| B9 Foliage | ivy, crack weeds, grass clumps, one orchard tree, moss tufts | T2/T3 | `sc-foliage` (rewritten) | Phase 1 |
| B10 Hero: dry fountain | stone basin, carved spout, bleached banner pole | T4 | Gen + unique bake + A/B | Phase 1 |
| B11 Hero: cellar lantern; breath grate | an iron lantern (light source) and a cracked floor grate venting red glow | T4 | Gen + unique bake + A/B | Phase 1 |
| Later | the other buildings (bakery, inn, smithy, schoolhouse, homes), the bell tower, the mill, the bakery oven maze (floor 4), the tithe gate | T2/T4 | Gen / AI for organic pieces | Slice |

**The beauty corner:** one half-timbered facade beside the dry fountain on cobbles, with one market stall, barrels, crates and sacks, a bleached banner, ivy and crack weeds, and haze with god rays.

**The dungeon corner:** one barrel-vault cellar room with a wine-niche wall, larder shelves of jars, the coal chute, the lantern, the breath grate, roots trickling in, and dust in the lamp shafts.

## 5. Characters

**Options weighed:**
- **Hand-built in Blender by agents.** Full control, but organic sculpting is the agents' weakest skill, and FishGame never got past capsules. It is fine for prop-like enemies (the broom, the jam jar), not for heroes.
- **An AI model plus cleanup (Tripo).** Fast and close to the look, but a single fused mesh bakes clothes into the body, which blocks STORY 3's customisable outfits. The topology also deforms poorly without a retopo.
- **Free base meshes.** **Bought meshes are out, because the project rule is free assets only** (`.claude/rules/art-assets.md`). The free option is MPFB2 (the MakeHuman Blender extension; outputs CC0, to confirm in `sc-asset-adopt`). It gives clean topology, a game rig and separate clothing layers, but a plain realistic look.

**Recommendation: a hybrid.** [P29]
1. Use an MPFB2 body for topology, the rig and weights, pushed to the 3.6 proportions with sliders and a sculpt pass.
2. Generate the head, hair and outfit as separate parts in Tripo from multi-view concept images, then retopo and clean them (`sc-ai-model-pipeline`, `texture-bleed-cleanup`).
3. Skin the parts by weight transfer from the body.
4. Outfits stay separate meshes on one skeleton, so customisation later is a swap.
5. If a part fails cleanup twice, model it in Blender over the body instead.

**Animation source: Mixamo** (free, royalty-free in games). The user signs in with their own Adobe account and downloads the clips; Claude supplies the list. Retarget through Unity Humanoid. Mixamo covers locomotion and its melee, sword-and-shield, great-sword, longbow and magic packs cover the four heirloom types (check each pack when downloading). Every combat clip gets our own retiming and frame-window data (6.1). Signature finishers are hand-keyed later, not in Phase 1.

**Test one hero first: Tobin.** [P30] Chapter 1 is his arc, and the axe is the hardest test of weight and hitstop. The test also proves the broad silhouette and the big hands that the proportions ask for.
- **Steps:** concept (front, back and sides from the user's chosen image tool, or Tripo text-to-image), then body, parts, rig, the Mixamo set, Unity import, then turntable and in-game captures (M10).
- **Greybox stand-in:** until M10, every hero uses one free mannequin with a per-hero colour and its heirloom. [P32]
- **The baker and the enemies** stay greybox in Phase 1.

## 6. Combat

### 6.1 Systems adapted from the research
- **Attacks are data with frame windows** (KH). Each attack clip carries these windows: hitbox on, combo-input open, finisher allowed, cancel, i-frames, turn-to-target (with a speed), and super armour. Core reads the flags, so tuning needs no code. Enemies use the same system.
- **Two targeting layers** (KH, DC2). A soft target is always on: a cone, range and height band, weighted by facing, and swings home and lunge toward it. The hard lock is a toggle with hysteresis; a right-stick flick switches to the nearest target in that direction; bosses and the elite never drop lock.
- **Counted combos** (KH): 2 hits plus a finisher, on the ground and in the air. The finisher gets +10% per prior hit and adds knockback and revenge.
- **Hitstop is local and cosmetic** (KH note). It slows the attacker's own animation and kicks their camera. Stacked hitstop is capped, and the host simulation never freezes.
- **The revenge gauge on the host** (KH) for the elite, and later for bosses. It is summed over every player's hits, with the cap scaled by player count.
- **Enemy tiers** (DC2 families): one model, stats scaled by tier. **Rage** (DC2): after N hits the enemy telegraphs and gains attack for a time.
- **Per-part multipliers for weak points** (DC1). One active element per weapon (DC1), easier to read than DC2's eight-attribute sum.

### 6.2 What we deliberately change (dark-cloud-lessons)
- **Damage uses a ratio, not subtraction** [P8]: `dmg = max(1, ceil(ATK x move x 100/(100+DEF) x (1 + element) x tag x part x crit))`. With subtraction, damage collapses or explodes between tiers and small hits round to zero (DC2 note). The ratio keeps tier gaps readable and every hit counts. The whole formula lives in one tested Core function, and every multiplier is a named data field (none of DC2's hidden script values or misnamed fields).
- **A real crit stat** instead of a coin-only crit. A separate species tag multiplier ("anti-pest") from element.
- **No weapon wear in Phase 1.** [P7] DC1's break rule is the root of the babysitting complaint. Revisit after the M7 playtest: if the loop wants a field-repair beat, the gentle DC1 curve `1.5 - 0.01 x END` applied to output (never to existence) is the starting point.
- **No hunger or thirst. No "starting weapon earns nothing"** (DC1).
- **Enemies need different answers** (6.5), not mashing.

### 6.3 Weapon growth (Core, M4) [P15]
- **Experience (ABS) per kill** is the species value: slime 4, rat-king 8, broom 6, elite 40. A finisher kill pays x1.25.
- **Co-op credit:** every player who hit the enemy in its last 10 s gets the full value, on the weapon they hit with. No last-hit competition. [P12]
- **Level cost:** `40 x (1 + 0.5 x L)` (DC2's curve as a float, without the integer quirks). Level-up is automatic: +2 ATK per level to level 5, then +1.
- **Attachments** (three Cellars drops: Ember = fire, Pest-bane = anti-pest tag, Heartwood = +2 ATK) sit in 2-3 slots. They are free to remove, and are **absorbed permanently at the next level-up, with a preview** (DC1's best mechanic, made visible). [P13]
- **Build-up:** one target per heirloom in Phase 1. It opens when ATK and the target's element or tag are at or above 90% of the target's base (DC2). The weapon tree shows each threshold as a bar.
- **On build-up:** level returns to 0, ABS keeps its fill ratio, and the weapon gains +10% of the target's base stats (DC2).
- **Re-route:** a build-up can be undone at the heroes' home workbench, returning the stats exactly as they were. Nothing is irreversible. [P14]

### 6.4 Co-op authority and latency [P10] [P11]
- **Player movement is owner-authoritative**, the proven FishGame pattern: the owner moves its CharacterController, and NetworkTransform with AuthorityMode Owner replicates it. The host never moves players.
- **Hits are decided on the client that sees the moment that matters:**
  - **Player hits on enemies:** the attacker's client detects them during its hitbox window, plays the spark and hitstop at once, and sends a hit intent. The host applies the damage, reaction, revenge and rewards.
  - **Enemy hits on players:** the victim's client judges them against its own dodge and guard state, so a visible dodge is never hit.
  - **The host owns** all HP, enemy AI, enemy movement, revenge, the stir meter, the haul and rewards.
  - This fits the no-anti-cheat rule. The host re-checks only game rules (target alive, attack exists), as `sc-netcode` asks.
- **Proof:** the M3 walk at 150 ms RTT and 2% loss with the Multiplayer Tools network simulator. Melee must feel instant for the attacker, and damage numbers must land within one round trip.

### 6.5 Starting numbers (tune in M3 and M7) [P9]
| Item | Start value |
|---|---|
| Hero | HP 100, DEF 10; incoming damage uses the same ratio, minimum 1 |
| Heirloom ATK | axe 22, staff 14 (long reach, sweeps), dagger 10 (fast, with a shield guard), bow 12 (aimed shot) and sword 12 |
| Move multipliers | hit 1 x1.0, hit 2 x1.1, finisher x1.6 (plus 10% per prior hit); charge x2.0 after a 0.6 s hold |
| Crit | 5% base, x1.5 |
| Soft target | cone of 60 degrees half-angle, 6 m, height band ±2 m |
| Hard lock | acquire 10 m, release 13 m, 1.0 s LOS grace (KH hysteresis advice) |
| Camera | free boom 5.0 m, lock boom 6.5 m, pivot 1.5 m, FOV 50; Cinemachine 3 orbital follow, Deoccluder, target group when locked |
| Hitstop | 4 frames (60 Hz) on normal hits, 10 on finishers; capped at 12 per 0.5 s |
| Dodge | 0.5 s roll with i-frames on frames 5-18 of 30; owner window leniency 100 ms |
| Guard [P46] | dagger and shield only: frontal 120 degrees, guarded damage x0.2; a parry window of 6 frames |
| Healing in the dive [P47] | the checkpoint room and the floor-4 bank point heal fully; about half of floors 1-3 hold a healing point (DC2: 51%) with a 30 s recharge |
| Revenge (elite) | opener 1, finisher 3, charge 4; cap 12 x (1 + 0.5 x (players - 1)) |
| Rage | after 7 hits, +30% attack for 15 s, telegraphed (DC2) |
| Enemy tier 2 (floors 3-4) | life x3, attack x2.3, defence x3.3 (DC2 family ratios) |
| Co-op scaling [P23] | enemy life x1.0 / 1.7 / 2.3 / 2.8 for 1-4 players, plus 1 extra enemy per room per extra player; attack unchanged |
| Downed (co-op) [P24] | 20 s downed; an ally revives with a 3 s hold; all players down ends the dive |

### 6.6 Enemies (Cellars, M3) [P5] [P6]
| Enemy | Tier 1 HP / ATK / DEF | Role | The answer |
|---|---|---|---|
| Jam-jar slime | 40 / 12 / 5 | Hopping swarm, lobs slow globs | Wide sweeps (staff, axe) |
| Rat-king | 90 / 18 / 10 | Fast lunges, scatters and regroups, telegraphed spin | Lock-on and speed (dagger), guard |
| Sweeping broom | 60 / 15 / 15 | Bristle front blocks 80% of frontal damage; knock-down sweeps | Flank, dodge behind, or the axe's guard-breaking finisher |
| **Dough Warden** (floor 4 elite) | 600 / 30 / 20 | Big telegraphed slams, super armour, revenge gauge | Dodge timing, co-op flanking |

Placeholder combat sounds use CC0 packs (Kenney and similar, licence recorded), because hit sound is half of combat feel. [P42]

## 7. Maps

### 7.1 Hollowmere square (greybox in M6) [P25] [P26] [P27] [P28]
- **Built by an Editor builder from a layout file** (no hand-edited scenes).
- **The square:** about 40 x 30 m around the dry fountain on the hilltop above the lake. Lanes leave it as stubs, and the bell tower and the sealed tithe gate show on its north side.
- **The heroes' home** (the cistern house, the bell-loft annex) sits on the square's edge. Its cellar stair is the way into the Undercroft.
- **The build zone** covers the square's empty plots. Placement is free on a 0.5 m snap grid with 15-degree rotation, with an undo, a free camera, and a live wish preview (lessons). There are no fixed lots.
- **Wishes are deterministic predicates,** extending FishGame's `PlacementWish` kinds (AtLot, NearPoint, FarFrom) with Facing, NextTo and InArea, after DC1's vocabulary. Wishes carry equal weight (DC1) and are shown in plain words with a live tick.
- **The bakery** comes home as 4 pieces (frame, oven, door, sign) plus the baker. It counts only when all are placed (DC1 completion gate).
- **The baker's wishes:** "near the fountain, so folk pass my door" (within 12 m), "door facing the square", and "not right against the inn" (8 m or more from it). Meeting all three opens his service: healing loaves for the next dive. [P48]

### 7.2 Undercroft Cellars floors 1-4 (greybox in M5) [P16] [P17] [P18] [P19] [P20] [P21] [P22]
- **Two layers, after DC2.** A hand-authored floor graph decides what each floor is. Phase 1's graph is linear: floor 1, 2, 3, 4.
  - A Core generator builds floors 1-3 from a seed: 32-bit seed plus floor id, sent by the host, so every client builds the same floor. The grid is 14 x 14 cells of 4 m.
  - Each floor has 4-6 rooms from the pool. Each room's rectangle plus a one-cell margin must be clear. Each new room links to a random earlier one, then 1-3 extra corridors make loops, and 1-3 side dead ends are added. Corridor tiles are matched by a 4-bit neighbour mask.
  - Floor 4 is a fixed layout: the taken bakery's oven maze, with the baker asleep at its heart and the Dough Warden on guard.
- **Rooms are authored as data grids** (cells, doors, dressing sockets, spawn points) and built into greybox prefabs by an Editor builder. The art pass later swaps kit pieces into the same sockets.
- **The Phase 1 pool:**
  - 12 combat rooms (2x3 to 5x4 cells), 1 entry, 1 stair-exit and 1 checkpoint room;
  - 6 corridor pieces (straight, corner, T, cross, dead end, door);
  - a sub-theme per floor (floor 1 larders, floor 2 wine vaults, floor 3 coal cellars), with about 70% of each floor's rooms drawn from its own sub-theme;
  - each room has 2-3 dressing variants per socket.
- **Repetition metric (EditMode, over 1,000 seeds):** no room appears twice on one floor; two consecutive floors share at most 2 rooms; floors 1-3 show at least 3 distinct layout silhouettes. M7 adds the real check: the user plays floors 1-4 three times and says whether they felt samey.
- **Floor loop, after DC1 and DC2:**
  - One random eligible enemy carries the gate key to the exit.
  - 50% of floors get a locked room whose key is in a chest elsewhere. Any player can carry it.
  - Each floor has 6-8 chests with rank-filtered loot (DC2's triangular roll).
  - The bakery pieces are spread across floors 1-3, with the baker on floor 4. Each floor shows a "pieces left" counter.
  - Floors stay cleared once cleared.
- **Checkpoint and banking:**
  - The checkpoint room is after floor 2. Floor 4's rescue is a second bank point and leads home.
  - **The haul is shared by the party and held by the host** (not per-player bags).
  - Banking at a checkpoint or in town makes it safe. A failed dive loses only the unbanked haul, and lost pieces return to their floors, so nothing is missable.
  - Return dives may start from the checkpoint.
- **The stir meter** [P49] runs from 0 to 100 and resets when the haul is banked in town:
  - +10 per floor cleared and +5 per unbanked piece carried, x(1 + 0.25 x (players - 1));
  - at 40, one enemy more per room;
  - at 70, a roaming tier-2 rat-king hunts the party;
  - at 100, the deep closes: 90 s to reach the next bank point, or the dive fails.

## 8. FishGame port (copy, never move) [P43]
| When | What | Note |
|---|---|---|
| M1 | Core utilities: `Core/Random`, `Hashing`, `Versioning`, `Net`, `Input`, `Diagnostics`, and the save framework (`WorldSave` store, versioning, the clean-per-section pattern) | Not FishGame's world sections; new sections for players, haul, weapons, placements |
| M1 | `Runtime/Save`, `Settings`, `Input`, `Services`, `Audio`, `Dev`; `App` (bootstrap, scene flow, save flow, dev startup); `Networking` (UGS bootstrap, Relay and Lobby, local session, connection approval, transport) | Connection approval enforces the 4-recorded-players rule |
| M1 | `Editor/Builders` (BuilderUtil, Prefab, Scene, NetworkPrefabs, ProjectSettings, MaterialLibrary), `Editor/Validation`, `CompileLogHook.cs`; PlayMode co-op test patterns | Strip the Millpond, Harbour and Fishing builders |
| M2 | `Gameplay/Player` locomotion and intent pattern (owner movement, `PlayerActions` intents), `InputContextRouter` | Reworked for combat input |
| M6 | `Core/Village/Placement` (PlacementWish, placements, gifts), `Villagers` wish lists | Extended with free placement and new wish kinds |
| M8 | `fg-aaa-prop` tools from branch `claude/aaa-prop-pipeline` (`aaa_mats2.py`, `props.py`, `compose.py`, `ab_sheet.py`, `stand_ab.py`, `imgdiff.py`, `snap.py`); cottage helpers `ck_lib.py`, `fetch_textures.py` | Renamed with the `sc_` prefix |
| Later | `Core/Fishing`, `Fishing/` (Saltwake), `Core/World` (clock, weather), `Core/Journal` | Not Phase 1 |

## 9. Batch production and usage

**Per batch:**
- **Brief:** a Sonnet agent gets a brief (`sc-delegate`) naming the family, the 3.x rules, the budget and the reference row.
- **Build:** it writes or extends one generator per family and runs all the items headless in one Blender session.
- **Bake:** joined highs go into one Cycles bake per batch (the cottage lesson: 1 h down to 4.5 min).
- **Review:** the agent renders one small JPEG contact sheet with the reference row. The controller (Opus) scores the sheet itself and names the worst three, because the pinned `reviewer` agent is Sonnet. A Sonnet agent fixes those three, and the sheet is re-scored once. [P50]
- **Heroes and new families** (B10, B11, the first B4 facade) also get the two-reviewer blind A/B, with `imgdiff.py` run first.
- Separate families run in separate agents with their own scratch folders, at most 3 at once, once the user gives the go-ahead (CLAUDE.md: multi-agent work needs it). [P51] A fix to a tileable flows to every piece that uses it.

**Rough usage per batch** (tokens; recalibrate after B1) [P41]:
| Step | Model | Approx. |
|---|---|---|
| Generator and render run | Sonnet | 150-250k in, 20-40k out |
| Contact-sheet review | Opus | 30-50k |
| Fix round (worst three) | Sonnet | 80-120k |
| Blind A/B per round (heroes only, 2 reviewers) | Opus | 2 x 30k; 2-3 rounds |
| **Typical T1-T3 batch** | | **about 0.3-0.4M Sonnet, 0.05M Opus** |
| **Hero prop** | | **about 0.4M Sonnet, 0.2-0.3M Opus** |

Phase 1 art (B1-B11) comes to roughly 4M Sonnet and 1M Opus tokens. Tobin (M10) adds about 1M Sonnet and 0.2M Opus. Code milestones follow the usual implementer-reviewer loop.

## 10. Risks and how Phase 1 retires them
| Risk | How Phase 1 retires it | Where |
|---|---|---|
| Combat feel | Data-driven windows so tuning is fast; CC0 hit sounds; the user plays the arena before any content is built on it | M3, M7 |
| Co-op melee latency | Owner movement plus attacker-side and victim-side hit judging; a network-simulator walk at 150 ms and 2% loss | M3 |
| Characters (FishGame never solved them) | One hero through the whole pipeline with a fallback per part; hours and usage logged to cost the rest | M10 |
| Procedural floor repetition | Hand-authored pool with sub-themes, dressing variants, a fixed set-piece floor; an EditMode repetition metric plus the user's three-run check | M5, M7 |
| Art target drift (thin OoT press, decisions A2) | Measured bands, one gate on captures, a re-check after the 5 Nov launch | M8, M9 |
| Flat realtime-lit dungeons | The dungeon corner is lit exactly as generated floors are; the APV-per-room fallback is named | M9 |
| Scope growth | Phase 1 stops at floor 4 with one resident; the "Not in Phase 1" list in section 1 | All |
| 3-4 player scale | 2-player is the tuned case; 3-4 get a smoke run, and the scaling numbers are data | M7 |

## 11. Decisions in this plan
1. **P1 Call:** Phase 1 ends at Undercroft floor 4 with the baker's rescue; the Cisterns, Tithe Roots and the Bell-Warden come after.
2. **P2 Call:** Phase 1 delivers the greybox loop, two corners and Tobin; dressing the whole square at final quality is the vertical slice.
3. **P3 Call:** all four heirlooms are playable; the axe and the dagger and shield are tuned deep, the staff and the bow and sword stay at base depth.
4. **P4 Call:** only the four heirlooms exist in Phase 1; no other weapons.
5. **P5 Call:** three story enemies in two tiers each, plus one floor-4 elite.
6. **P6 Placeholder:** the elite's name, "Dough Warden"; the attachment names Ember, Pest-bane and Heartwood.
7. **P7 Call:** no weapon wear in Phase 1; revisit after the M7 playtest, with the DC1 curve on output as the starting point.
8. **P8 Call:** the ratio damage formula `ATK x move x 100/(100+DEF)` with named multipliers, minimum 1.
9. **P9 Placeholder:** the combat, camera, lock-on, hitstop, dodge, guard, revenge and rage starting numbers (6.5) and enemy stats (6.6).
10. **P10 Call:** the attacker's client judges player hits, the victim's client judges enemy hits, and the host owns HP, AI, revenge, haul and rewards.
11. **P11 Call:** player movement is owner-authoritative (FishGame `PlayerLocomotion` pattern).
12. **P12 Call:** in co-op, every player who hit an enemy in its last 10 s gets full experience on the weapon they used.
13. **P13 Call:** attachments sit in removable slots and are absorbed permanently at the next level-up, with a preview.
14. **P14 Call:** a build-up can be undone (re-routed) at the heroes' home workbench.
15. **P15 Placeholder:** the growth numbers: species experience values, level cost `40 x (1 + 0.5L)`, ATK gains, slot count, the 90% threshold.
16. **P16 Call:** floors use DC2's two-layer method; Phase 1's floor graph is linear 1-4; floor 4 is a fixed set-piece.
17. **P17 Placeholder:** the grid (14 x 14, 4 m cells), 4-6 rooms per floor, the pool sizes, 70% sub-theme share, and the repetition metric thresholds.
18. **P18 Call:** rooms are authored as data grids and built by an Editor builder; art swaps into the same sockets.
19. **P19 Call:** the floor loop: a gate-key carrier, a 50% locked room whose key any player can carry, rank-filtered chests, and floors stay cleared.
20. **P20 Call:** a checkpoint after floor 2 and a bank point at floor 4; a failed dive loses only the unbanked haul, and lost pieces return to their floors.
21. **P21 Placeholder:** the stir meter values (+10 per floor, +5 per piece, the player factor) and thresholds (40, 70, 100, 90 s); the effects themselves are P49.
22. **P22 Call:** the party shares one haul, held by the host.
23. **P23 Call:** co-op scales enemy life (x1.7, 2.3, 2.8) and enemy count, not attack.
24. **P24 Call:** co-op players are downed for 20 s and revived by an ally; all down ends the dive.
25. **P25 Call:** the Undercroft's entrance is the cellar stair of the heroes' cistern house.
26. **P26 Call:** placement is free on a 0.5 m snap grid in a build zone (no fixed lots), with undo and equal-weight wishes shown live.
27. **P27 Placeholder:** the baker's three wishes (12 m, facing the square, 8 m from the inn) and the loaf count (three per dive); the service itself is P48.
28. **P28 Call:** the bakery is 4 pieces plus the baker and counts only when all are placed.
29. **P29 Call:** the character pipeline is a hybrid: an MPFB2 base body, Tripo-made head, hair and outfit parts, and Mixamo animation retargeted through Unity Humanoid.
30. **P30 Call:** Tobin is the first hero through the pipeline.
31. **P31 Placeholder:** character proportions (6.5 heads, 1.15x hands and feet) and the four heights.
32. **P32 Call:** until M10, every hero is one free mannequin with a per-hero colour and its heirloom.
33. **P33 Placeholder:** the palette hexes and the value and warmth bands in 3.1.
34. **P34 Call:** the Cellars are dominated by gold lamplight; teal is only the shadow and fog tint; the red ember glow is an accent.
35. **P35 Placeholder:** texel densities and triangle and texture budgets (3.3).
36. **P36 Placeholder:** the shape-language numbers (1.4x timbers, 1-3 degrees off true, 1.5 cm chamfers, 2.2 m doors).
37. **P37 Call:** the URP stack from decisions A1; generated floors are lit in realtime, with additive room scenes plus APV as the named fallback.
38. **P38 Call:** the contents of the beauty corner (facade, fountain, stall, props, foliage) and the dungeon corner (one barrel-vault room).
39. **P39 Placeholder:** three fixed capture cameras per corner at 1920x1080, midday plus one low-sun shot.
40. **P40 Call:** the hero props are the dry fountain (town) and the iron lantern plus the breath grate (Cellars).
41. **P41 Placeholder:** the usage estimates per batch and for Phase 1 (section 9).
42. **P42 Call:** placeholder combat sounds from CC0 packs during the greybox.
43. **P43 Call:** the FishGame port schedule in section 8 (code in M1, M2 and M6; art tools in M8; fishing, world clock and journal later).
44. **P44 Call:** 2-player is the tuned co-op case in Phase 1; 3-4 players get a smoke run in M7.
45. **P45 Call:** Phase 1 defers three features the user decided in STORY 3: hero renaming, outfit customisation, and the solo town jobs of the three heroes not diving.
46. **P46 Call:** only the dagger and shield can guard; the other heirlooms rely on dodge.
47. **P47 Call:** the dive heals at the checkpoint room, the floor-4 bank point, and healing points on about half of floors 1-3 (30 s recharge is a placeholder).
48. **P48 Call:** meeting the baker's wishes opens a service: healing loaves for the next dive.
49. **P49 Call:** the stir effects: one more enemy per room, then a roaming tier-2 rat-king hunter, then "the deep closes" (reach a bank point in time or the dive fails).
50. **P50 Call:** the controller (Opus) scores the art contact sheets itself; the pinned Sonnet `reviewer` does not.
51. **P51 Call:** at most 3 art-batch agents run at once, after the user's go-ahead.

## Change log
- 2026-10-30 (user, decision A11): day palette moved toward the measured OoT 2026 frames, keeping as much of the OoT look as possible with slightly brighter light; moody, darker saturation stays. Day value bands, warmth, plaster hue, saturation and slate-teal shadows updated in 3.1.
- 2026-10-30 (user): every multiplayer session needs 2+ recorded players at start (confirmed). Claude's call: if players drop mid-session the session continues and difficulty scales down.
- 2026-10-30 (user): up to 10 worlds per player, with Solo and Multiplayer worlds in separate tabs (Claude reads this as 10 per tab; placeholder). One save file per world (M1).
- 2026-10-30 (user): added from the Kingdom Hearts sweeps: tech bonus ABS (parry, weak-part, element-match, finisher pays extra with a pop), growth verbs per heirloom (axe finishers and guard breaks, dagger hits and parries, staff multi-hits, bow weak points), breakable boss parts on the floor-4 Dough Warden that drop healing (M3). Claude added the two that synergise: hero abilities unlocked by weapon milestones (kept one tier weaker on other weapons) and spell pips refilled by landing hits (staff). All in M4 except boss parts (M3).
- 2026-10-30 (user): Claude generates Tobin's concept options for M10.
- 2026-10-30 (user): Tobin's direction is "almost like a young adult viking build and timber-yard worker": option A's rolled sleeves, leather apron and axe carried across the back, with a Norse build (broad, thick-limbed, braided or tied hair, fur or wool layers). See `docs/design/tobin-concepts.md`.
- 2026-10-30 (user, A16): the creator always hosts a multiplayer world; its save lives on the creator's PC.
- 2026-10-30 (user): the day look-test mood is right. The Cellars were too blue-green after tuning; go back to warm firelight, cleaner and less muddy (no teal tint, warm umber/plum shadows, saturated gold, crisper values). 3.1 Cellars row and the P34 paragraph updated.
- 2026-10-30 (user): go-ahead for M9 with up to 3 art agents at once (P51).
- 2026-10-30 (user reference, decision A24): user-supplied cellar mood image (private: `Reference/private/user/cellar_mood_ref_01.webp`; measured p10/p50/p90 0.04/0.20/0.49, warmth +0.31, masses deep umber `#352214`/`#140c07`/`#58371e`, candle highlights `#e9b96d`/`#f8edbb`). What it teaches: warmth comes from **local light pools** (candles, wall sconces, a hearth fire), unlit stone stays a neutral grey-brown rather than tinted orange, deep near-black shadow between pools, small bright cream-gold highlights, thin smoke and haze in the light, dense dressing (shelves of crocks, hanging herbs, barrels, chains, a table). Cellar value band moved toward it but kept brighter for combat readability: 0.06-0.10 / 0.24-0.32 / 0.55-0.68, warmth +0.25 to +0.35.
