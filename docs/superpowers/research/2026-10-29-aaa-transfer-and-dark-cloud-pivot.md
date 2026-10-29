# What the AAA-prop work teaches FishGame's assets, and a Dark Cloud pivot (research notes, 2026-10-29)

_Research only. Nothing here is built, agreed or committed._ Source: the `fg-aaa-prop` session (branch `claude/aaa-prop-pipeline`, SKILL.md sections 6e-6l, about 60 blind A/Bs on a KCD2-style potion stand). Read against the current FishGame direction: the Riverlight painterly look (R102), the Dark Cloud cottage sheet as the colour and light target (2026-10-28), the open-world pivot, and village v1.

## Part 1. Using the work to improve FishGame assets

### 1.0 The one caveat first: the target is different
The potion stand chased KCD2 photo-realism: scanned grit, grime, soot. FishGame's world target is hand-painted HD with clean, chunky, rounded forms (the Dark Cloud cottage sheet, "Ocarina of Time 2026"), with no grime, rust or noise. Its review data shows this matters:
- Mortar v8 lost 12 vs 18 when it was judged against granite photos. Against the KCD2 crop the same pair was close (13 vs 17).
- **The reference row picks the style a gate pushes toward.**

So:
- **Transfer the process and the shader techniques.**
- **Don't transfer the surfaces** (scans, grime, soot).
- The `fg-aaa-prop` skill should stay a separate realism kit, as the memory note already says. A session porting it into Riverlight must not import its surface recipes.

### 1.1 Review-process lessons (highest value; they apply to every FishGame asset and scene)
These would make `fg-asset-review` and every art-polish rotation more reliable. Most of FishGame's review today is a single scorer: the agent's own rubric plus `fg_asset_score.py` percentiles. Its own notes say self-scores run 4-6 points high.

1. **Pairwise blind A/B beats absolute scores.**
   - Two fresh reviewers look at one JPEG sheet: references on top, two shuffled candidates below, with the key hidden (`ab_sheet.py`).
   - Keep a change only when both prefer it.
   - Absolute /40 scores drifted by about a point per reviewer on the same image. Pairwise preference was stable.
2. **Null-test calibration: reviewers confabulate.**
   - Two stand renders differed by 0.01-0.22 of 255 per pixel, so they were effectively identical.
   - Both reviewers still picked one by about 2 points, and both gave specific, confident reasons that were not in the images.
   - Rules from this:
     - (a) Run `imgdiff.py` before every A/B; skip pairs under about 0.5.
     - (b) **A result counts only when the stated reason matches what actually changed.**
     - (c) Treat a margin of 2 points or less as a tie.
     - (d) Break splits with a third reviewer who must "name the specific visual difference that decided it".
3. **A defect in a reviewer's list is not a preference.**
   - Every mortar reviewer named a "stuck-on slab". Removed, the asset *lost*: in a side-by-side, the slab read as a crack, i.e. character.
   - Every glass reviewer called the dome grime a "smeared overlay". Replacing it with correct fine specks lost 13 vs 16 ("too clean, reads as new").
   - Only the A/B decides. **Visible beats correct at review distance.** This matches `fg-asset-craft` rule 11 (story beats readable at 10 m) and strengthens it.
4. **Improve the feature reviewers already credit; don't replace it.** Five wins came this way: the jug's glaze teeth, the glass "tongues", the mortar's pale rim chips, the bottle's tall highlight, the mortar slab. Before stripping something odd, find what it does for the read.
5. **Review resolution hides small things.**
   - On a standard sheet a vessel is about 100 px. Both reviewers called a clear highlight change "identical"; a cropped sheet showed it.
   - FishGame's 48 px play-distance panel is right for silhouette. Material changes on small props need a cropped close view in the A/B.
6. **Prove a per-object win at scene level.**
   - Only the mortar's form win moved the whole stand (27 vs 24). The glass wins were real at crop scale and invisible in the stand.
   - For FishGame, scene captures from the agreed positions (stands, tug deck, camp, arrival; spec R79) should be A/B'd before and after an art batch, not just per asset.
7. **Snapshot what you A/B.**
   - A copper A/B compared v2 with itself: the "old" `.blend` re-linked the new baked textures (`snap.py`).
   - FishGame's generator plus determinism gate mostly avoids this. It still applies to anything with baked or adopted textures (Tripo cleanup, the adopted cottage).

### 1.2 Technique lessons that transfer to URP and the Riverlight look
1. **Fake refraction and fake reflection probes** (`liquid4`, `glass` g11). URP has no screen-space reflections and no depth-traced refraction.
   - **Refraction:** refract the view vector by the surface normal and look up a procedural room (dark floor, bright ceiling, a warm fire lobe). Liquids then bend and vary instead of reading as a flat fill.
   - **Reflection:** reflect the view vector and look up crisp highlight shapes (a hearth patch, a window, a soft ceiling), times Schlick Fresnel.
   - **Direct uses:**
     - **cottage window glass** (day sky and cloud streaks, night lantern glow; "lantern/window glows not done" is in CURRENT.md);
     - **bottles, lanterns and jars** in the village;
     - possibly **water glints** (Inbox: "water glints still hard"). A shaped, view-dependent glint lookup is cheaper and more controllable than chasing a specular term.
   - Cost: a few nodes in a Shader Graph, no extra pass.
2. **When a material change does nothing, switch lights off one at a time** (`AAA_LIGHTOFF`).
   - The brazier's "white disc" took two material rounds; the real cause was a point light sitting *inside* the clay wall.
   - The Inbox water-glint note ("probably a stepped specular or a foam/caustic mask term... find the term with a debug render first") is the same class of problem: isolate by elimination before tuning.
   - For lantern and window glows: check that light positions sit outside and in front of the geometry.
3. **Spalls and chips need a square arris.** On a rounded edge, a chip cutter bakes a flat pale disc that reads as a decal dot. Rounded Riverlight forms should show "run-down" by shape (a missing board, a tilt), as the world-art-target memory already says, not by cut chips on rounded edges.
4. **Planar facets on near-cylinders become long stripes.** A 1 mm trim plane on a wall turned into a "slab". Avoid chisel facets on turned forms: wells, millstones, posts, barrels.
5. **The highlight shape comes from the normal field, not the silhouette.** Smoothing a bottle profile killed its best highlight. When smoothing a lathe profile on glass or metal, re-aim the reflection instead of accepting the loss.
6. **Turned objects: copy the real making process.** The mortar's big win was making it a *turned* bowl (wide, thick rim, no lugs), not carving harder. Ask how the real object is made (turned, coopered, blown, planked) and let the generator follow it.
7. **Fire history in shadow reads as shadow.** Soot and heat tint under a pot never scored. Fire history needs the fire's light on it. For camp cauldrons and the smokehouse, light the underside with the fire.
8. **Light saturation:** desaturating the firelight lost (26 vs 28) because KCD2 is warm. The Dark Cloud target is also warm. Separate materials by their own value and colour masses, not by greying the light.

### 1.3 Concrete proposals for the session porting this (ranked)
1. Add an **A/B mode to `fg-asset-review`**: the `ab_sheet.py` + `imgdiff.py` + two-reviewer + reason-check protocol.
   - The reference row must be the Riverlight concepts or the Dark Cloud cottage sheet, never photos.
   - Keep `fg_asset_score.py` percentiles as the cheap first filter.
2. **Scene-level A/B** of the four play positions before and after each art batch (`stand_ab.py` pattern), since per-asset wins may not show.
3. A **URP fake-reflection glass shader** (window glass, lantern glass, bottles), from the g11 node recipe. One shader, many assets; it fills an open CURRENT.md item.
4. Use the **light-elimination diagnosis** for the water-glint bug before more tuning.
5. A blind A/B **null test** in each new reviewer setup, to measure its noise floor.

**Not recommended:** scans, grime, soot and the KCD palette; the per-object 25-point rubric aimed at photos.

## Part 2. A Dark Cloud spiritual successor: is a full pivot right?

### 2.1 Where the game already is
FishGame has been walking toward Dark Cloud for two weeks:
- **Open-world pivot (2026-10-17):** dropped the quota and run-loss structure.
- **Village homecoming:** Georama-style restoration with wish lists and placement wishes, which is Dark Cloud's Georama in all but name.
- **PS2-inspired proposals:** rod build-up modelled on weapon build-up, a fish derby.
- **Art target:** the Dark Cloud cottage sheet (2026-10-28).
- **Dark Cloud 2 had fishing** (fish points, contests, fish races). In Dark Cloud, fishing is a sub-system next to dungeons and building.

So "pivot to a Dark Cloud successor" is mostly **adding the missing half**: a dungeon and combat layer, plus build-up. It is not scrapping the game.

### 2.2 What would be kept vs lost
- **Kept:**
  - NGO + Relay/Lobby co-op and host authority;
  - the save system;
  - the fishing core (the Rust-faithful fight, the 25 fish, ecology, seasons, weather, clock);
  - Millpond and the village/Georama loop;
  - villagers and wish lists;
  - the tug as a moving home;
  - the whole Blender and Unity art pipeline and skills;
  - the Riverlight/Dark Cloud look.
- **Changes meaning:**
  - fishing becomes one of several verbs (resources, buffs, cooking, contests, synthesis materials) instead of *the* verb;
  - pivot pillar 2 ("a system that does not touch fishing does not ship") has to go.
- **Lost:** little code. Some design docs and the "calm, no failure state" pillar.

### 2.3 What would have to be built (where the risk is)
1. **Action combat:** melee feel, hit reactions, enemy AI, telegraphs and animation. This is the most expensive and highest-risk part of the genre, and the project has none of it. The character and animation pipeline is also the weakest area today: the villagers are capsule stand-ins, and the Deckhand is a Tripo model.
2. **Co-op combat netcode:** fishing tolerates latency; melee does not. Host-authoritative hits need client-side prediction and lag compensation for 4 players. That is a new class of networking work (the `fg-netcode` skill covers none of it).
3. **Dungeon generation:** floors, rooms, keys and loot tables (Dark Cloud style), or hand-built "floors".
4. **Weapon build-up and synthesis:** a deep but pure-logic system. It fits the project's strength in Core plus EditMode tests well.
5. **Enemy roster and bosses:** modelling, rigging and animation, many times the cost of props.

Rough order of size: the village, fishing and look are perhaps 40% of a Dark Cloud-like game. Combat, dungeons and enemies are the other 60%, and the hardest 60%.

### 2.4 Market notes (searched 2026-10-29)
- **No official remaster.** I found no official Dark Cloud HD remaster announcement in 2026. The hype is fan-driven: a Dark Cloud 2 PC recompilation in early beta (Aug 2026) and HD texture packs.
  - The nostalgia is real, but no Sony or Level-5 release is creating a new audience wave.
- **Few direct successors.** Indie "Dark Cloud successors" are rare. Project Norune (a dungeon plus one-village rebuild) is the known one. The niche looks under-served.
- **Adjacent proof (from memory; verify before relying on it):**
  - Level-5's own *Fantasy Life i* (2025) mixes life skills, fishing, town building and combat, and was reported as commercially successful;
  - *Rune Factory* (farming, fishing, town and dungeons) has kept a steady audience for 20 years;
  - *Dragon Quest Builders* proves building plus light combat.
  - The "cosy loop plus dungeon" genre sells. Pure fishing sims sell to a narrower crowd, unless they lean into horror (Dredge) or deep simulation.

### 2.5 My view
**Your instinct is right; the framing should be "evolve", not "scrap".** Fishing on its own runs thin. You noticed it, the fun-formula note (2026-10-13) said it, and the open-world pivot was a first answer. A Dark Cloud-style frame answers it properly: dungeons give the tension and loot swings you love in REPO and PEAK, the village gives the long-term goal, and fishing becomes the relaxing break between dives, which is exactly its role in Dark Cloud 2.

The honest risks are combat and co-op melee netcode. They are new, expensive and the hardest things in the genre. They should be proven before the pivot is declared. Suggested gate: a **2-3 week vertical slice**.
- One small dungeon (a flooded ruin under the river, which ties the story together: "the fishing went quiet because of what's down there");
- one weapon with a basic build-up;
- three enemies and one mini-boss;
- 2-player co-op;
- the existing village loop above it.

If the slice is fun with two players and the melee holds up over Relay, commit. If melee feels bad over the network, fall back to a co-op-friendlier combat style: lock-on with generous hit windows, or ability-driven combat like Dark Cloud 2's ranged/melee mix, which hides latency better. Keep it a spiritual successor: original systems and names (no Georama, Atla or Dark Cloud characters), as the village note already does.

**Identity question to settle first:** is it still friend-slop co-op (PEAK/REPO energy: dives that go wrong together) or a JRPG (story, single-player first)? Dark Cloud was single-player. A **co-op Dark Cloud with friend-slop chaos in the dungeons** is the more distinctive pitch, and it matches your fun formula. It also keeps all the co-op work.

## Sources
- Fan Dark Cloud 2 PC recomp in early beta: https://www.generationamiga.com/2026/08/30/dark-cloud-2-recomp-takes-another-step-towards-a-native-pc-version/
- Fan HD texture work: https://www.vierockhd.com/ps2/dark-cloud-hd
- Project Norune thread: https://lr.in.psf.lt/r/DarkCloud/comments/hwtvpg/project_norune/fzrafge/?context=3
- Internal: `docs/superpowers/design/2026-10-17-open-world-pivot.md`, `2026-10-26-village-homecoming.md`, `2026-10-26-ps2-inspired-proposals.md`, `.claude/skills/fg-asset-review/SKILL.md`, `fg-aaa-prop/SKILL.md` (branch `claude/aaa-prop-pipeline`).
