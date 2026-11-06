# Open rigged and animated assets: what fits Sleeping Calamity (2026-11-06)

Asked by the user 2026-11-06: survey the open-source, already-rigged/animated models, auto-rig AI and drag-and-drop animation tools, and map them to the story. Method: two web research passes plus a scan of the story docs and the project (`docs/STORY.md`, story bible draft, book, dungeon spec, PHASE1 3.6, `Assets/_Project`). Licences were read on the source's own page unless marked UNVERIFIED; full legal text was read only where noted. Not legal advice.

## Summary
- **Biggest win, already half in the repo:** Quaternius Universal Animation Library 1 + 2 (CC0 / free-commercial, one humanoid rig, 250+ clips incl. melee combos, farming, fishing). UAL1/UAL2 Standard FBX already sit in `Content/Animation/Quaternius/`; UAL2 adds working-life clips the townsfolk need.
- **Dungeon enemies:** Quaternius *Bestiary - Dungeon Monsters Kit* (Aug 2026; 7 enemies with weapons, humanoid rig compatible with UAL; free for commercial use under the "QAL" licence, text UNVERIFIED) is the closest ready fit, especially for the sheaf-masked **Gleaners**. Quaternius Ultimate Monsters (CC0, 50 animated) for tier-2 and later-dungeon prototypes.
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
| Saltwake fisherman Teague, lighthouse keeper | Story only | UAL2 fishing clips | Ready-made loop for the fishing-town residents |
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

## Avoid
- **Tencent HY-Motion 1.0 and other Hunyuan community licences:** License.txt read: Territory excludes the EU, UK and South Korea; above 1 million MAU needs Tencent's permission; outputs can't train other models. A worldwide PC release can't rely on it.
- **AMASS and anything trained on it** (non-commercial; covers most text-to-motion weights incl. MoMask/HumanML3D and MotionStreamer).
- **Bandai Namco motion datasets** (CC BY-NC-ND).
- **Kimodo SMPL-X checkpoint** (research-only).
- **PhilS94 wall-walking spider** (no open licence; Asset Store only). **MagicArticulate, RigAnything, Anymate, AnimaX, Active-Ragdoll repos:** licence not found, so treat as off-limits.
- **Kenney animated characters:** CC0 but blocky; tests only.
- **Blender Studio film rigs as-is:** CC-BY and lovely, but bendy-bone/driver rigs don't export to FBX; only worth it as sculpt/shape references or with a full game-rig rebuild.

## Still to verify before adopting
1. Quaternius **QAL** licence text (Bestiary kit) and the UAL2 page's CC0-vs-"free commercial" wording.
2. Ultimate Animated Animals roster (is there a rat, cat, crow?).
3. MPFB2 rig options (`game_engine`, Mixamo-compatible) and each asset pack's tag (CC0 vs CC-BY mixes).
4. Mixamo wording on Adobe's own FAQ (page 403'd; community staff answer says games are fine).
5. CMU mocap primary terms (site cert error) and Rokoko free-library terms.
6. Whether the UniRig release includes the skinning model.

## Licence quick list (as read)
CC0: Quaternius packs (UAL1, Ultimate Monsters, Animated Animals, Cute Monsters, RPG Characters, Farm Animals), KayKit Adventurers/Skeletons/Character Animations, Mesh2Motion assets, MPFB2 core assets, Kenney. MIT: Mesh2Motion code, UniRig, SkinTokens (card), Make-It-Animatable (repo), UnityRig, Kinema, unitycoder MotionMatchingUnity. Apache-2.0: Puppeteer. CC-BY 4.0: Blender Studio characters (Einar, Snow, Forest Animals). NVIDIA Open Model License: Kimodo SOMA. Custom QAL: Quaternius Bestiary. Restricted: Hunyuan community, AMASS, Bandai Namco.
