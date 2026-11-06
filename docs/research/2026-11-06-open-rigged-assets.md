# Open rigged and animated assets: what fits Sleeping Calamity (2026-11-06)

Asked by the user 2026-11-06: survey the open-source, already-rigged/animated models, auto-rig AI and drag-and-drop animation tools, and map them to the story. Method: two web research passes plus a scan of the story docs and the project (`docs/STORY.md`, story bible draft, book, dungeon spec, PHASE1 3.6, `Assets/_Project`). Licences were read on the source's own page unless marked UNVERIFIED; full legal text was read only where noted. Not legal advice. Licence bar: personal-use (see "Restricted licences").

## Summary
- **Biggest win, already half in the repo:** Quaternius Universal Animation Library 1 + 2 (CC0 / free-commercial, one humanoid rig, 250+ clips incl. melee combos, farming; fishing is Source-only). UAL1/UAL2 Standard FBX already sit in `Content/Animation/Quaternius/`; UAL2 adds working-life clips the townsfolk need.
- **Dungeon enemies:** Quaternius *Bestiary - Dungeon Monsters Kit* (Aug 2026; 7 enemies with weapons, humanoid rig compatible with UAL; free for commercial use under the "QAL" licence, read 2026-11-06: ship in a game, no repackaging) is the closest ready fit, especially for the sheaf-masked **Gleaners**. Quaternius Ultimate Monsters (CC0, 50 animated) for tier-2 and later-dungeon prototypes.
- **Animals:** Quaternius Farm Animals and Ultimate Animated Animals (CC0) plus Mesh2Motion (MIT code, CC0 rigs: human, fox, bird, dragon, spider, kaiju) unblock **ambient life phase 2** (waiting on "the user's animal models") and Wren's cat **Tuppence**.
- **Our own meshes:** UniRig (MIT, LICENSE file confirmed) / its successor SkinTokens (MIT card) auto-rig Tripo/Blender meshes (humans, animals, objects) to FBX/GLB. Mesh2Motion for quick creature rigs.
- **Humans:** MPFB2 core assets CC0 (FAQ: closed-source use "Yes") for hero and townsfolk bodies; Mixamo stays OK for games (raw files can't be redistributed).
- **Style caveat:** every ready pack is low-poly. Use their *rigs and clips* and proportions as bases, then rebuild/retexture to the stylised-real bible through `sc-asset-adopt`; never ship them as final hero art.

## Story needs mapped to sources

| Need (where/when) | Today in the project | Best open source | Notes |
|---|---|---|---|
| Heroes Tobin, Pip, Wren, Ash (Hollowmere, all chapters; M10 Tobin first) | Greybox mannequin (YBot), `HeroAnimator.controller`, clips from Mixamo/UAL/Kevin Iglesias (`HeroClipTable`) | MPFB2 body base (CC0) + UAL1/UAL2 clips + Mixamo; Make-It-Animatable (MIT repo, Mixamo skeleton) or UniRig to rig a Tripo head/outfit | UAL2 melee combos fit sword/axe; check bow/staff coverage before downloading the ~12 Mixamo clips still owed |
| Child heroes (prologue) | none | MPFB2 age targets (CC0) + UAL clips | Same rig, scaled proportions |
| Townsfolk: baker Hollis, innkeeper Marta, smith Dun, sexton Amos, Hob, Nell (Hollowmere floors 3-9 rescues) | Story/code only, no models | MPFB2 bodies + UAL2 working clips (farming, carrying, hammering-like), Quaternius Universal Base Characters as placeholders | Idle work loops make the rebuilt town feel alive |
| Saltwake fisherman Teague, lighthouse keeper | Story only | KayKit "fishing" (CC0, verify) or UAL2 Source; Standard has no fishing clip | See `2026-11-06-townsfolk-clip-map.md` |
| Wren's cat Tuppence (follows Wren in town after the Undercroft opens) | Nothing | Quaternius "animated animals" cat (CC0, OGA listing; verify), or Mesh2Motion fox rig re-fitted to our cat mesh | Small, high-visibility; worth a custom mesh on a borrowed rig |
| Rooks/crows lifting off the bell tower (ambient) | Cards by design | Mesh2Motion bird rig (CC0); Blender Studio Sprite Fright bird (CC-BY, film rig, needs a game-rig bake) | Nothing CC0 verified for a rigged crow; rig our own |
| Farm/ambient animals for rebuilt towns, Thistledown | None | Quaternius Farm Animals (CC0: sheep, horse, dog, cow, pig, llama), OGA CC0 chicken/deer/horse | Restyle textures to our palette |
| Butterflies, bees, lantern-moths (Undercroft floor 5) | Cards | Blender Studio forest animals butterfly/ladybug/spider (CC-BY) as shape refs; keep cards/shader flutter | Cards are fine at gameplay distance |
| Eels (floor 5 dome) | Story only | Nothing verified; simple bone chain + procedural sine in Unity | Cheap to do ourselves |
| Cellars tier 1: jam-jar slime, broom (living objects) | Primitive prefabs, no rig | Own Blender mesh + 3-6 bone rig (or UniRig "object" mode), clips authored by hand or procedural squash/stretch | No pack has living jars/brooms; this is our whimsy |
| Cellars tier 1: rat-king | Primitive prefab | Quaternius Ultimate Monsters / Animated Animals rat if present (roster UNVERIFIED), else Mesh2Motion fox/quadruped rig on our mesh | |
| Gleaners: sheaf-masked demon-made servants, lieutenants unmask (every dungeon as stir rises) | Story only | Bestiary kit humanoid enemies on the UAL rig + UAL2 combat/zombie clips | Humanoid rig means one clip set for all Gleaners; mask is a prop swap |
| Later-dungeon species (3-4 per town, "born from what its town lost") | Not designed | Quaternius Ultimate Monsters (50, CC0) and Cute Animated Monsters (21, CC0) as greybox stand-ins while designing | KayKit Skeletons (CC0) for any undead idea |
| Brazen Ox (floor 5 boss; brass ox, steam) | `EnemyDoughWarden` primitives | Farm Animals cow/horse rig as a quadruped *motion* reference; own hard-surface mesh rigged with UniRig | Boss must be bespoke |
| Bell-Warden, Keepers' Wagon, Mawrrow (later bosses) | Story only | Bespoke; Mesh2Motion dragon/kaiju rigs as blocking stand-ins | |
| Spiders/crawlers if a dungeon wants them | None | Mesh2Motion spider rig (CC0); procedural legs via Unity Animation Rigging (our own code) | No open Unity spider walker with a real licence found; PhilS94's is Asset Store-only now |

## Tools worth trying
- **Mesh2Motion** (mesh2motion.org; MIT code, CC0 rigs/clips from Quaternius): web app, GLB in/out, rigs a mesh onto stock human/fox/bird/dragon/spider/kaiju skeletons with clips. Fastest path for creature greybox.
- **UniRig** (VAST-AI-Research, MIT LICENSE confirmed; SIGGRAPH 2025): local, rigs humans, animals and objects; obj/fbx/glb/vrm in, FBX/GLB out; Blender add-on. Skinning weights were "coming soon" on the HF card when checked; confirm the release has them. Training data comes from Objaverse (mixed mesh licences), which affects the weights, not our output, but note it. **SkinTokens/TokenRig** (Feb 2026, MIT card, 14 GB+ GPU) is the successor.
- **Puppeteer** (ByteDance Seed3D, Apache-2.0): auto-rig arbitrary meshes to FBX; builds on repos with their own licences. Second choice after UniRig.
- **Make-It-Animatable** (MIT repo, weights licence not stated): instant humanoid rig on a Mixamo skeleton.
- **NVIDIA Kimodo** (Apr 2026; SOMA checkpoint "NVIDIA Open Model License", commercial-ready): text-to-motion for custom clips (e.g. a baker kneading, a sexton pulling a bell rope). Outputs a 30-joint SOMA skeleton; needs retargeting to Unity Humanoid. Use only the SOMA/G1 checkpoints; the SMPL-X one is research-only. Training set (Bones-SEED) licence unconfirmed.
- **Unity Animation Rigging** (Unity package): IK, look-at, aim; already the right tool for procedural legs, heads tracking the hero, the cat following Wren.
- **UnityRig** (MIT Blender add-on): builds Unity-Humanoid metarigs, handy when re-rigging MPFB2 or custom heroes.

## Restricted licences: usable here (user, 2026-11-06)
The user plans to play the game privately with friends and probably never sell it, so non-commercial, territory and MAU clauses don't block anything. Record provenance and tag these `licence: personal-use` in the asset source record so they can be swapped if the game is ever sold.
- **Tencent HY-Motion 1.0** (1B / 0.46B Lite, 24-26 GB VRAM; text-to-motion, humanoid): now a real option for custom clips (baker kneading, sexton on the bell rope, Gleaner shuffles). Licence read: excludes EU/UK/South Korea, 1M MAU permission clause, outputs can't train other models. Retarget to Unity Humanoid.
- **AMASS-trained text-to-motion** (MoMask, MotionStreamer, HumanML3D weights) and **Bandai Namco motion datasets**: usable for clip generation and reference.
- **Kimodo SMPL-X checkpoint**: usable alongside the SOMA one.
- **Blender Studio CC-BY film characters**: always fine with credit; the blocker is their film rigs, not the licence.
Still avoid on quality or safety grounds: Kenney blocky characters (tests only); repos with no licence or no code at all (MagicArticulate, RigAnything, Anymate, AnimaX) only because there is nothing reliable to run; PhilS94 spider (paid Asset Store product; don't copy). Never: Dark Cloud names, characters, story, text, music or assets.

## Hardware fit (user PC: RTX 5070, 12 GB VRAM; checked 2026-11-06)
- **HY-Motion 1.0: no local run.** README minimum VRAM: 1.0B "26GB", Lite 0.46B "24GB" (low-VRAM tips: `--num_seeds=1`, prompt under 30 words, under 5 s). **Use Tencent's official Hugging Face Space** (`tencent/HY-Motion-1.0`, running on ZeroGPU, free with a daily quota) to generate clips, then retarget.
- **Kimodo: no local run as shipped.** ~16-17 GB, mostly its Llama-3-8B-Instruct text encoder (~16 GB); the encoder can run on a separate machine (Toolchefs notes), not worth the setup.
- **MoMask: yes.** ~150M-parameter class models peak well under 1 GB; a fork even runs it on CPU. Local text-to-motion option.
- **UniRig: yes.** README: "at least 8GB VRAM" for generation; released checkpoint does skeleton + skinning (Articulation-XL2.0); Rig-XL/VRoid checkpoints still planned.
- SkinTokens (14 GB+): no. Mesh2Motion: browser, no GPU.

## Image-to-3D on the 12 GB RTX 5070 (checked 2026-11-06)
- **Hunyuan3D-2.1** (Jun 2025, PBR): shape ~10 GB fits; PBR texture ~21 GB does not (README; `--low_vram_mode` exists, saving unpublished). Run shape only, texture with our own pipeline (`sc-ai-model-pipeline` retex / Blender bake), which we'd do anyway since AI paint blurs.
- **Hunyuan3D-2 / 2mini-Turbo** (0.6B): README "6 GB VRAM for shape generation and 16 GB for shape and texture". Shape fits easily; texture borderline, community reports 2.0 paint running on a 12 GB RTX 3060.
- **TripoSG** (VAST, MIT): >8 GB, shape only, CLI image to GLB. Fits; strong geometry, no texture.
- **TRELLIS.2** (Microsoft, Dec 2025): numbers conflict (6-8 GB claimed with offload, 16 GB at 512^3, 24 GB+ comfortable); treat as borderline at low resolution.
- **Step1X-3D**: no VRAM figure found.
- Recommendation: Hunyuan3D-2.1 or TripoSG for shape locally, our texture pipeline for paint, UniRig (8 GB) to rig. Licence: Hunyuan community licence is fine under the personal-use bar.

## Local model + texture + rig at ~8 GB (second pass, 2026-11-06)
No single local tool does all three at 8 GB. Best chain found (VRAM from READMEs and blogs; no independent RTX 5070 measurement exists):
1. **TRELLIS.2** (Microsoft, Dec 2025, MIT) via `visualbruno/ComfyUI-Trellis2`: shape + PBR (base, roughness, metal) in one run, 1-3 min per try; Low-VRAM mode ~6.8-7.8 GB at 1024^3 on 8 GB cards (blog figures, UNVERIFIED); tested on Windows 11 torch 2.7 cu128 with a Blackwell fix; needs gated DINOv3 access. Known faults: black texture speckle (issue #162), colour drift on flat illustration input, soft textures at 512-1024.
2. Blender cleanup: decimate/retopo, re-unwrap, check albedo for baked light; then our texture repaint where soft.
3. **UniRig** (~8 GB, ComfyUI-UniRig with Make-It-Animatable, FBX out) or **SkinTokens** (README 14 GB, but one pipeline ran it on a 12 GB 4070 Ti in 23 s; drops UVs, reattach in Blender). Humanoids only in practice.
Alternatives: **Pixal3D** (Tencent ARC, MIT, May 2026, on TRELLIS.2): pixel-aligned, closest to the reference, ~6.8 GB at draft, 6-8 min per try, more artifacts, 43 GB download (Pinokio one-click). **Hunyuan3D 2.1** paint needs ~12 GB even tuned (`Satyr-Astry/image-to-3d-rigged-pipeline`, 11.99 GB peak). Hunyuan 2.5/3.x are hosted only.
Verdict: good enough for props and background creatures, behind Tripo for hero characters; every result still needs a Blender pass. Blackwell risk: CUDA extensions without sm_120 wheels need MSVC builds (`TORCH_CUDA_ARCH_LIST=12.0`).

## Licence verification (primary sources, read 2026-11-06)
Tags: `cc0`, `cc-by`, `personal-use` (see "Restricted licences"). Not legal advice. Quaternius QAL and CC0 both allow shipping in a game build, so a personal-use game is covered either way.

| Item | Licence and source | Allows | Attribution | Our tag |
|---|---|---|---|---|
| Quaternius **QAL v1.0** (Bestiary Dungeon Monsters Kit; quaternius.com/license.html, pack page quaternius.com/packs/bestiarydungeonmonsterskit.html) | Custom "Quaternius Asset License". Pack page: "Free to use in personal, educational and commercial projects." Licence page: use, copy, modify, include in a Product, sell the Product with no fee or credit (s.2). | Personal and commercial use. **Not CC0:** s.3a forbids extracting, repackaging, sublicensing, selling or redistributing the Assets themselves, however modified; shipping a finished game that incorporates them is allowed. s.3b: don't claim authorship or imply endorsement. Termination clause (s.5) as summarised by a fetch tool; reread the full text before relying on it. | None required | `personal-use` (not public domain) (never put the raw files in a public asset repo or a store pack). Kit: 7 monsters with weapons, 3 colour variants each, humanoid rig, "Compatible with the Universal Animation Library". Free Standard ~60-70% of the pack, Source is paid. |
| **UAL2** (quaternius.com/packs/universalanimationlibrary2.html; itch.io page; License_UAL2.txt in the repo) | **CC0 1.0.** The page says "Free to use in personal, educational and commercial projects. (CC0 License)"; the "free commercial" wording is just the CC0 summary, not a separate licence. | Anything, redistribution included. | None (credit appreciated) | `cc0` |
| UAL2 tiers | 130+ clips overall. Free **Standard** (name-your-own-price, 17 MB) is ~60-70% (43 clips in the repo's UAL2_Standard.fbx; the devlog says 42). **Source** ($14.99+, .blend) holds the rest. Farming/fishing are named as categories; per the repo's own UAL2_Standard.fbx, Standard has `Farm_Harvest`, `Farm_PlantSeed`, `Farm_Watering`, `TreeChopping_Loop`, `Walk_Carry_Loop` and **no fishing clip**, so fishing is Source-only. | | | |
| **Ultimate Animated Animals** (quaternius.com/packs/ultimateanimatedanimals.html) | CC0 ("free to use in personal and commercial projects"). | Anything. | None | `cc0`. Page says 12 animals, 12+ animations each (attack, death, kicks, gallop, walk, jump...) but **does not name the roster**: still unknown whether a rat, cat or crow is in it. Open the download to confirm (needs a normal browser, not done here). |
| **MPFB2** (static.makehumancommunity.org/mpfb/faq) | Core assets (base mesh, targets, skins) CC0; licence page: no payment, no copyright notice, no attribution. FAQ "closed-source game": yes. Third-party asset packs carry their own licence (CC0 or CC-BY only, per the Asset Packs FAQ), which you must honour. | Selling and closed-source use of output. | None for core; credit per asset for any CC-BY pack | `cc0` for core, `cc-by` per pack asset (tag each when used) |
| MPFB2 rig options | Blender add-on rig menu: `default`, `default_no_toes`, `game_engine`, `cmu_mb`, `rigify` variants, plus a `mixamo` rig (v2.0.11 added a `mixamo_unity` variant with extra bones; animations do not transfer between `mixamo` and `mixamo_unity`). Docs: "Mixamo in MPFB" (mpfb/docs/rigging_posing/mixamo.html). The exact `game_engine` bone layout is not documented on the pages read; check in the add-on before use. | | | Plan: build bodies with `game_engine`, retarget UAL/Mixamo clips through Unity Humanoid; use the `mixamo_unity` rig only if we upload to Mixamo. |
| **Mixamo** (Adobe; helpx FAQ returned 403, so read via Adobe Community answers quoting the FAQ) | Adobe account, royalty-free. Characters and animations usable in commercial projects, games included, provided they are embedded in the product. | Use in games. Not allowed: reselling or sublicensing the raw assets or motions, redistributing the original files on a site, bulk-downloading for machine learning. | Not required | `personal-use` (ToS is Adobe's, may change; raw FBX stay out of any public repo). The 2022 FAQ called it a "technology preview": recheck the Adobe page if we ever sell. |
| **UniRig** (github.com/VAST-AI-Research/UniRig) | MIT. | Anything. | Keep the licence notice if we copy code | `cc0`-class (code, MIT). **Skinning is released:** "Skeleton & Skinning Prediction checkpoint trained on Articulation-XL2.0". Full Rig-XL/VRoid checkpoints still pending. README: "CUDA-enabled GPU with at least 8GB VRAM" for generation (training skinning needs 60 GB). |
| **Kay Lousberg KayKit Character Animations** (itch.io) | CC0; asks that unmodified copies aren't resold. | Anything. | None | `cc0`. Tool category lists digging, lockpicking, **fishing, hammering, generic working, pickaxing**; Simulation lists sitting and lying down. No cooking or sweeping. |

## Still open
1. Ultimate Animated Animals roster (rat, cat, crow?): download and read, then update the animals row above.
2. CMU mocap primary terms (site cert error in an earlier pass; the RancidMilk page quote in `animation-sources.md` is the best we have) and Rokoko free-library terms (page says commercial use is fine, no licence text).
3. The `game_engine` rig's bone names in MPFB2 (read in the add-on, not on the docs).
4. Adobe's own Mixamo FAQ page (403 from here; Community answers only).
5. Whether UAL2 Source holds a fishing clip set we want (sold at $14.99+ or Patreon; the user decides).

## Licence quick list (as read)
CC0: Quaternius packs (UAL1, Ultimate Monsters, Animated Animals, Cute Monsters, RPG Characters, Farm Animals), KayKit Adventurers/Skeletons/Character Animations, Mesh2Motion assets, MPFB2 core assets, Kenney. MIT: Mesh2Motion code, UniRig, SkinTokens (card), Make-It-Animatable (repo), UnityRig, Kinema, unitycoder MotionMatchingUnity. Apache-2.0: Puppeteer. CC-BY 4.0: Blender Studio characters (Einar, Snow, Forest Animals). NVIDIA Open Model License: Kimodo SOMA. Custom QAL v1.0: Quaternius Bestiary (use and ship, no repackaging). Restricted: Hunyuan community, AMASS, Bandai Namco.
