# New project hand-off: Sleeping Calamity, a Dark Cloud-style action RPG (from FishGame, 2026-10-29)

**Read this first in the first session of the new repo.** It replaces FishGame as the active project. FishGame is frozen as a parts bin (section 4): copy from it, don't delete it.

**User decisions, 2026-10-29:**
- Scrap FishGame and start a **new repo** with a clean context. Carry over only the proven parts.
- Redo **all assets and maps in one consistent style**: Dark Cloud-styled, closest to the user's picture of **The Legend of Zelda: Ocarina of Time on Switch 2 (2026)**.
- Study the user's **own copies of Dark Cloud 1 and 2** (and other owned PS2 games, section 3.3) with REA.
- Work in the order real studios use, and watch scope: projects like this keep growing.
- **Phase 0** (user's definition) = a locked story, a large reference library for the first area and the first dungeons, the REA research, and a detailed Phase 1 plan (assets, how to make them, the style, and how to make many AAA-quality models at once without burning usage; the user has a free usage reset).

Everything below that is not a user decision is a recommendation. Change it when the work shows something better.

---

## 1. The game (draft)
- **Title: Sleeping Calamity** (user, 2026-10-29). Checked that day: no existing game uses it.
- **Premise draft** (to finish in Phase 0): the disaster that ruined the towns isn't over; it is **asleep** beneath them. Each dive goes deeper toward it, and the deeper floors stir it; the ending is waking it and facing it. The depths can be called **the Hearthdeep** (the user liked the name).
- **Genre:** action RPG in the Dark Cloud tradition: dive into dungeons, bring back pieces to rebuild a ruined town, grow weapons by using them, calm side activities (fishing and others) between dives.
- **Original IP.** A spiritual successor, never a remake: none of Dark Cloud's names, characters, story, text, music or assets; our own world and words. Systems are adapted for feel, not copied one to one.
- **Open question: co-op.** FishGame's NGO + Relay stack carries over, and co-op 1-4 fits the user's fun formula (REPO, PEAK). Dark Cloud was single-player, and co-op melee needs latency work. Decide while locking the story and the Phase 1 plan; it changes dungeon and combat design.

## 2. Art direction
**User's words:** "Dark Cloud styled, ... how the Legend of Zelda Ocarina of Time 2026 for the Switch 2 looks." The user asked Claude to research that look online.

### 2.1 What the Ocarina of Time Switch 2 remake looks like (researched 2026-10-29)
Announced at the 40th-anniversary Zelda Direct, due 5 Nov 2026. Sources: Nintendo Life's gallery of 25 official screens and XDA. Seven screens viewed: Castle Town market, the Great Deku Tree, the Water Temple, Link's render, Sheik, a Goron, Ganondorf.
- **Stylised realism, not hand-painted.** Press calls it "modern-looking, realistic", close to the fan Unreal remakes.
  - **Materials:** real PBR at high detail: cobblestone, half-timber and plaster, dressed stone with gilded and enamel inlay, woven cloth, stitched leather, fur, bark, moss.
  - **Shapes:** stylised and readable: Link's chunky boots and gauntlets, the Goron's cartoon face on realistic skin, big silhouettes.
- **Colour:** saturated and clean by day (red roofs, blue sky, white plaster, green trees). Dark scenes in rich teal and gold (Water Temple) or cold blue storm light.
- **Light and atmosphere carry the look:** volumetric god rays, haze and aerial perspective, drifting leaves and fireflies, soft cinematic key light with deep readable shadows.
- **Architecture:** Castle Town is a European half-timbered market (fountain, banners, awnings, barrels, sacks), very close to Dark Cloud's Norune and Brownboo, modernised.
- **Dense dressing:** grass, ferns, roots and props in every corner.

### 2.2 What it means here
- **Target:** Dark Cloud's whimsical shapes and town and dungeon themes, rendered with OoT-2026 stylised realism: real materials, storybook proportions, saturated daytime colour, heavy atmosphere.
- **Compared with the potion stand (KCD2 target):** cleaner, brighter, more saturated surfaces with tidy readable wear, and chunkier, slightly exaggerated forms.
- **The fg-aaa-prop pipeline becomes core** (section 4.3): high-poly damage, smart materials and CC0 scans, baked PBR sets, fake glass and liquid shaders, the blind A/B gate.
- **Rendering:** URP with volumetric fog or light shafts, good shadows, SSAO, bloom and a filmic grade. Research common stylised-realism URP set-ups before building custom.

## 3. Phase 0: story, references, research, Phase 1 plan
Four outputs. They can run in parallel: research doesn't wait on the story, and the references feed both the story and the plan.

### 3.1 Locked story
`docs/STORY.md`. "Locked" means the user signs off on it and later changes are deliberate. Suggested contents:
- The world and the calamity: what it is, why it sleeps, what wakes it.
- The full arc in chapters, one paragraph each (number of towns and dungeon sets).
- Main characters: the player character(s), the town residents of the first area, the antagonist(s).
- **The first area and the first dungeon set in detail:** the town before and after the calamity, what the player rebuilds, each dungeon's theme, floors, enemies and boss, and the story beats that happen there.
- How the story drives the loop: why you dive, why you rebuild, how the deeper floors "stir" the calamity.
- Tone (whimsical with a dark undertow, as Dark Cloud).

### 3.2 Reference library for the first area and the first dungeons
A large, organised set, gathered after the first-area and dungeon themes are drafted:
- **Style frames:** the official OoT Switch 2 screens (press galleries) and Dark Cloud captures from the user's own play in PCSX2. These are copyrighted: study only, never shipped, traced or projected. Keep them in a gitignored folder (`Reference/private/`).
- **Real-world photos** (CC0, public domain, CC BY via `fg_ref_fetch.py`): the town's architecture (half-timbered villages such as Rothenburg, Colmar, Cotswold stone), its materials (plaster, timber, cobble, slate, thatch), the landscape and plants, and each dungeon theme (caves, mines, crypts, root systems, flooded ruins: whatever the story picks).
- **Paintings** (public domain) for colour and value: storybook and Romantic landscape.
- **Measured targets** per set with `fg_ref_palette.py`: palette, value range, light warmth. These become the style bible's numbers.
- Layout: `Reference/<area or dungeon>/<subject>/` with a `REFS.md` (source, licence, what it's for) per folder; free-licence images can be committed (Git LFS).

### 3.3 Research: Dark Cloud and related games
Output: `docs/research/` notes in our own words and tables. Mechanics and numbers only; never copy code, data files, text, models, textures or audio into the repo; analyse copies in the scratchpad.

**a. Read existing decompilations and reimplementations first.** Where a full decomp exists, reading its source answers the questions faster and more completely than REA.

| Game | Source | Status (checked 2026-10-29) |
|---|---|---|
| Dark Cloud 2 | **ChronicleTwo**, github.com/TheMoonPeople/ChronicleTwo | Matching decomp, 100% matched (PAL). Main source for DC2 systems. |
| Dark Cloud 1 and 2 | **OpenCloud**, github.com/Gundorada-Workshop/OpenCloud | Reimplementation of both games. |
| Dark Cloud 2 | DC2-PS2RECOMP (Red-tv141) | Static recompilation (runs natively; less readable than the decomp). |
| Jak and Daxter | OpenGOAL | Full. Movement, camera, combat feel, streaming worlds. |
| Ratchet & Clank 1 | OpenRAC rac1-decomp | About 99-100%. Third-person camera, weapon upgrade-by-use (very close to weapon growth). Going Commando, Up Your Arsenal and Deadlocked also have decomp projects. |
| Sly Cooper | sly1 decomp | Stealth platforming, camera. |

Topics for DC2 (and DC1 where its decomp or REA reaches): damage formula; weapon growth, build-up and synthesis maths; durability; floor generation, keys and back floors; enemy stats, AI and drop tables; Georama requirement scoring; camera and lock-on; side activities (fishing, invention, photography).

**b. REA and Ghidra on owned copies** for games without a full decomp (list in section 3.3c).
- The user dumps their own discs to ISO (and their own BIOS for PCSX2). Copies stay in the scratchpad.
- REA 6.4 + Ghidra 12.1.4 are installed (FishGame memory `reverse-engineering-tools`). PS2 code needs the **ghidra-emotionengine-reloaded** extension (chaoticgd), optionally **ghidra-ps2sdk**; check which Ghidra version the release targets (the listing mentioned 12.1.2). Ask the user once before downloading them.
- Some PS2 ELFs ship STABS or .mdebug debug symbols: check each game first, it changes how much is readable.
- PCSX2 save states can give runtime values (stats, tables in memory).

**c. REA list** (no full decomp as of 2026-10-29; decomps.net was checked on page 1 of several, so re-check before starting each):

| Game | Why | Decomp status |
|---|---|---|
| Dark Cloud 1 | The original Georama, weapon growth and dungeon rules; DC2's decomp doesn't cover it | WIP decomp (DCDecomp/"Chronicle"): use its matched functions, REA the rest |
| Rogue Galaxy | Level-5's successor to Dark Cloud: real-time party combat, weapon synthesis, the Factory | Recomp only |
| Dragon Quest VIII | Level-5: town and field scale, alchemy pot, cel-shaded art direction | Recomp; one listing mentioned a decomp, check |
| Kingdom Hearts 2 | Lock-on, combo and reaction-command combat feel | None listed |
| Kingdom Hearts 1 | Lock-on, camera, party AI | WIP decomp: use it, REA the gaps |
| Champions of Norrath / Baldur's Gate: Dark Alliance | 4-player co-op action RPG on PS2: loot tables, co-op scaling, enemy waves | None listed |
| Monster Hunter (PS2) | Hit-stop, hitboxes, weapon weight and commitment | WIP decomp (mh1j, JP): use it, REA the gaps |
| Okami | Stylised world art, combat grading, brush mechanic | None listed (HD PC port exists) |
| Torneko 3 / Shiren | Mystery-dungeon floor generation and item economy | None listed |

Play-study only (no REA needed, the value is in how they look and play): Okage, Radiata Stories, Atelier Iris, Suikoden III, Ape Escape.

**d. Genre landscape:** what modern games in the space do well (Rune Factory, Fantasy Life i, Dragon Quest Builders, the Dark Cloud HD re-releases) and where the gap is.

### 3.4 Phase 1 plan
`docs/PHASE1.md`. Phase 1 builds the first area and the first dungeon set. The plan names:
- **Scope:** exactly what Phase 1 delivers (playable or a walkable beauty build; combat greybox included or not; co-op or not), from the story and the research.
- **The style bible:** measurable rules from 3.2: palette and value range, material list, texel density, shape language, light and atmosphere settings, character proportions. One beauty corner (town) and one dungeon corner built first and approved by the user on captures, so every later asset is made against an agreed target.
- **The asset list**, by category, with a method and a quality tier for each item (section 3.5).
- **The batch production method** (section 3.5) and a rough usage estimate per batch.
- **Characters:** player, residents, enemies, boss. Choose the pipeline (hand-built in Blender, AI model plus cleanup with `fg-ai-model-pipeline`, or bought base meshes) and the animation source (Mixamo-style retargeting, bought packs, hand-keyed). FishGame never solved characters, so test one first.
- **Maps:** town layout and dungeon floor layouts (from the story), blockout first, then dressing.

### 3.5 Making many AAA-quality models efficiently
What the potion stand and the cottage taught, and how studios do it:
- **Tier the assets.** Most of a scene is kits and tileables, a few things are heroes.
  1. **Tileable materials and trim sheets** (plaster, timber, cobble, slate, stone, bark, cloth): made once per area as smart materials (CC0 scans + `aaa_mats2.py`-style procedural layers), shared by every kit piece. This is where most of the quality comes from and it costs nothing per asset.
  2. **Modular kits** (walls, beams, roofs, stairs, dungeon walls and floors, arches): low-poly pieces using the tileables and trims, plus vertex-colour or mask blends for wear. No unique bake.
  3. **Instanced sculpted pieces** (slates, cobbles, rocks, roots): a few unique highs baked once, scattered many times (the cottage split).
  4. **Hero props** (the fountain, a shrine, a boss door, an altar, a signature weapon): full fg-aaa-prop treatment with unique bakes and blind A/B.
- **Build in batches, not one by one.** One generator script per family (all barrels and crates, all market stalls), run headless in one Blender session; one Cycles bake run per batch with joined highs (the cottage's 1 h to 4.5 min speed-up); one contact sheet per batch.
- **Review by batch.** One reviewer agent scores the whole batch's contact sheet against the reference row and names the worst three; only heroes and new families get the two-reviewer blind A/B. Run `imgdiff.py` before any A/B.
- **Spend the expensive model on judgement, not typing.** Sonnet or Haiku agents run generators and renders from a brief; Opus reviews and decides. Small JPEG contact sheets in context, never full-resolution renders.
- **Parallel where nothing is shared:** separate families in separate agents or sessions, each in its own scratch folder.
- **Reuse beats rework.** Fix a material once in the tileable and every kit piece picks it up. Improve what reviewers already credit instead of replacing it (lesson 6g).
- **Usage note:** the user has a free usage reset; batching still matters because a review loop per single prop is what burned the most usage on the potion stand.

## 4. What to extract from FishGame (the parts bin)
FishGame: `C:\Dev\FishGame`, branch `main` (b559060d+), plus `claude/aaa-prop-pipeline` for the A/B tools. **Copy, never move.** For code: rename the namespace (`FishGame.*` to the new one), delete fishing-specific references, and get each assembly compiling with its tests green before taking the next.

### 4.1 Infrastructure and process

| Item | Path in FishGame | Note |
|---|---|---|
| Unity wrapper | `Tools/unity.ps1` | Exit codes, log parsing, PASS marker, `-dev-profile` isolation. The most useful script. |
| csproj sync | `Tools/sync-csproj.ps1` | For worktrees. |
| Cloud setup | `Tools/cloud/` | Linux sessions without Unity. |
| Hooks | `.claude/hooks/guard.sh`, `cloud-session-start.sh` | The guard blocks hand edits of `.unity`/`.prefab` YAML. |
| Agents | `.claude/agents/*.md` | implementer, reviewer, risk-reviewer, branch-reviewer, scanner, fixer, with pinned models and efforts. |
| Review rules | `REVIEW.md` | Strip FishGame specifics. |
| CLAUDE.md | `CLAUDE.md` | Rewrite for the new game; keep the Unity, git, agents and review sections. |
| .gitattributes / .gitignore | root | LFS for `.blend`/FBX/PNG. |
| Session skills | `.claude/skills/fg-steward`, `fg-handoff`, `fg-delegate`, `fg-merge`, `fg-walk` | Rename the prefix; rewrite project paths. |
| Netcode checklist | `.claude/skills/fg-netcode` | NGO 2.x host authority, RPC and NetworkVariable pitfalls. |
| Reference skill and tools | `.claude/skills/fg-reference`, `Art/Blender/tools/fg_ref_fetch.py`, `fg_ref_palette.py` | Licence-filtered fetching and palette measuring. Needed in Phase 0 (3.2). |
| Asset skills and tools | `.claude/skills/fg-asset-craft`, `-budget`, `-review`, `-adopt`, `fg-texture-paint`, `fg-foliage`, `fg-scene-look`, `fg-water-look`; `Art/Blender/tools/fg_asset_*.py`, `fg_tint.py`, `fg_paint_pass.py`, `fg_concept_match.py` | Rules tied to Riverlight (painterly) get rewritten from the new style bible; the audit, preview and review protocol stay. |
| AI-model cleanup | `.claude/skills/fg-ai-model-pipeline`, user skills `texture-bleed-cleanup`, `pose-tuning` | If Tripo or Hunyuan is used for characters. |

### 4.2 Code
Unity 6000.6.5f1, URP 17.6, NGO 2.13.3, Multiplayer Services 2.3.3, Input System 1.20, Cinemachine 6.6, ProBuilder 6.1.

| Keep | Path | Why |
|---|---|---|
| Assembly layout and ArchitectureTests | `Assets/_Project/*/*.asmdef`, `Tests/EditMode` | Core (pure C#) / Runtime / Gameplay / Presentation / UI / App / Networking / Editor, forbidden references enforced by a test. |
| Networking | `Assets/_Project/Networking/` | UGS bootstrap, Relay + Lobby, local session, connection approval, transport. Proven in co-op walks. |
| App flow | `Assets/_Project/App/` | Bootstrap, scene flow, save flow, dev startup. |
| Runtime services | `Runtime/Save`, `Settings`, `Input`, `Services`, `Audio`, `Dev` | Generic. |
| Core utilities | `Core/Save`, `Random`, `Hashing`, `Versioning`, `Net`, `Input`, `Diagnostics` | Seeded RNG, save schema and versioning, hashing. |
| Editor builders | `Editor/Builders`, `Editor/Validation`, `CompileLogHook.cs` | Scenes and prefabs built by code; no hand-edited YAML. |
| Test patterns | `Tests/EditMode`, `Tests/PlayMode` | PlayMode co-op recipes, the `-dev-profile` setup. |
| When needed | `Core/Fishing`, `Fishing/`, `Core/World` (clock, weather, seasons), `Core/Village` (wish lists, lots, placement scorer), `Core/Journal` | The fishing fight and the Georama-like village logic are well tested; bring them in when the plan reaches fishing or town rebuilding. |

**Not reused:** every FishGame model, texture, map and scene, the Riverlight painterly shaders, the fish content, the fishing-game design docs.

### 4.3 Art pipeline and lessons
- **fg-aaa-prop** (skill on branch `claude/aaa-prop-pipeline`): `aaa_mats2.py`, `props.py`, `compose.py`, `ab_sheet.py`, `stand_ab.py`, `imgdiff.py`, `snap.py`; SKILL.md sections 6e-6l hold the round-by-round lessons. The potion stand itself is not reused.
- **AAA building lessons** (fg-aaa-prop section 6m; `Art/Blender/aaa_cottage/LOG.md` and `HANDOFF.md` on branch `claude/affectionate-heisenberg-yci2gw`). Lessons and tooling only; the cottage model is not reused (user, 2026-10-29: every asset is built fresh in the locked style). What carries over:
  - the building split: unique-baked walls, instanced sculpted slate and stone kits, hero bakes only for the door and lantern;
  - its budget: at most 75k tris, 2x4K + 4x2K + 2x1K;
  - the voxel rule (voxel size about min(w,h)/12, 4-6 mm);
  - joining highs before a bake (1 h to 4.5 min);
  - four library bug fixes (stale `matrix_world`, numpy-socket segfault, NaN at t>1, relative image paths) and the generic scripts (`ck_lib.py`, `fetch_textures.py`);
  - its A/B was self-judged (no agents in that cloud run), so treat its results as unconfirmed.
- **Lessons from the stand:**
  - Blind A/B: two fresh reviewers, shuffled candidates, real references in a reference row. Run `imgdiff.py` first (under about 0.5/255 is identical at review size; reviewers invent 2-point preferences on identical images). A result counts when the stated reason matches the change. 2-point margins are ties; a third reviewer breaks splits.
  - A defect named in a review is not a preference: removing it can still lose. Improve credited features instead of replacing them. Visible beats correct.
  - Copy how the real object is made (turned, planked, coopered). Spalls need a square arris. Planar facets on cylinders read as stripes. The highlight comes from the normal field.
  - When a material change has no visible effect, switch lights off one at a time before touching the material again (the brazier's "white disc" was a light inside its wall).
  - Fake refraction and reflection probes (refract or reflect the view ray into a procedural room, Schlick Fresnel) give URP-friendly glass and liquids.
- **Working habits:** research common practice before building custom; long Unity walks go to subagents that return about 5-line summaries; a short `CURRENT.md` with history in dated files; no PRs (review, green check, merge commit); never commit `Library/`, `Temp/`, `*.csproj`.
- Port the user and feedback memories (ask-via-tool, progress-updates, bold-art-changes, research-common-practice-first, no-anticheat, merge-policy) from `~/.claude/projects/C--Dev-FishGame/memory/` into the new project's memory; drop the FishGame project entries.

## 5. After Phase 1 (outline, to plan when there)
Studio order, roughly: the playable loop proven in greybox (combat feel, dungeon floors, weapon growth, town rebuild; co-op if chosen), then a vertical slice at final quality used to measure the cost per floor, enemy, building and weapon, then production planned from those numbers, then alpha, beta, polish. Ideas that belong later go in `docs/PARKING_LOT.md` with a date so nothing is lost.

## 6. First session in the new repo
1. Read this file.
2. Repo setup: the user creates the GitHub repo (suggested `SleepingCalamity`, cloned at `C:\Dev\SleepingCalamity`); new Unity 6000.6.5f1 URP project; copy the infrastructure (4.1) and the assembly skeleton with ArchitectureTests; `unity.ps1 check` green.
3. Ask the user (one AskUserQuestion batch): co-op or solo; OK to download the Ghidra PS2 extension; which disc dumps are ready.
4. Start Phase 0: story draft with the user, research from the decomps (3.3a), reference gathering as themes settle.

## 7. Risks to watch
- **Combat feel** (and co-op melee latency if co-op): nothing like it exists in FishGame.
- **Characters and animation:** FishGame never solved them (villagers stayed capsules; the Deckhand needed heavy cleanup).
- **Scope growth:** the user's own concern; the parking lot keeps ideas without building them.
- **IP:** spiritual successor only; research stays as notes in our own words.

## Sources
- ChronicleTwo: https://github.com/TheMoonPeople/ChronicleTwo · OpenCloud: https://github.com/Gundorada-Workshop/OpenCloud · decomp lists: decomps.net, unbaked.games
- ghidra-emotionengine-reloaded: https://github.com/chaoticgd/ghidra-emotionengine-reloaded
- FishGame internal: `docs/superpowers/research/2026-10-29-aaa-transfer-and-dark-cloud-pivot.md`; `docs/superpowers/design/2026-10-26-village-homecoming.md` (Georama adaptation); memory `reverse-engineering-tools`.
