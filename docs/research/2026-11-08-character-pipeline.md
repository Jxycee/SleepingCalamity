# Character pipeline: rigged body + face + swappable outfits (2026-11-08)

Asked by the user 2026-11-08: find the best verified pipeline for properly rigged characters (face and body) that we can still customise, using existing paths instead of inventing our own. Method: read the repo (PHASE1 3.6/P29, STORY customisation rule, the Nov 6 research notes, `sc-ai-model-pipeline`, `sc-asset-adopt`), then web research. **Not seen:** the user's local sessions and PC memory (this was a cloud session). Where the repo says something that may be out of date locally it is flagged. Tags: VERIFIED = read on the source's own page; VENDOR = only the vendor says so; UNVERIFIED = not found.

## 1. What actually needs to be true (the contract)
STORY.md (customisation, user): **outfits** are found, crafted and dyed and show on the hero in cutscenes; **faces, bodies and hair stay fixed**; heroes can be renamed. That is much smaller than a character creator. It means:
- 4 heroes + a dozen townsfolk are **authored once**, not generated at runtime. No UMA-style DNA system is needed.
- The only runtime variation is **outfit swap + dye** on a fixed skeleton.
- Faces matter in cutscenes and dialogue (a close camera), not in the 5-6.5 m gameplay boom.

Everything below is judged against these standards, which already exist, so no tool of ours has to invent a format:
1. **Unity Humanoid avatar** for body animation (UAL, Mixamo, Kevin Iglesias clips already retarget this way).
2. **ARKit 52 blendshapes (+ visemes)** for the face: the de-facto face standard, supported by every lip-sync and face-capture tool.
3. **Separate clothing meshes on one shared skeleton**, hair as its own mesh, rigid accessories parented to bones.
4. FBX/glTF out of Blender with `path_mode='STRIP'` (already a project rule).

## 2. Why recent versions drift (my read of the repo, check against the local sessions)
- The Nov 6 pipeline work (v1 -> v3, `sam3d-trellis2-combined.md`) is a **static-mesh pipeline**: it hill-climbs UV coverage, squeezed triangles, texel spread. Every version got better on those numbers. None of those numbers measure what a character needs (valid skeleton, face keys, layering), so progress there says nothing about characters.
- The note ends "Characters stay on Tripo (user, 2026-11-06)" while PHASE1 P29 says MPFB2 body + generated parts, and `sc-ai-model-pipeline` says "a Tripo rig is never used". **These contradict each other; one line needs to win** (question 1 at the end).
- PHASE1 itself says a fused Tripo mesh "bakes clothes into the body, which blocks customisable outfits" and deforms poorly. I found no independent evidence that Tripo or Meshy produce ARKit-quality face rigs; every claim is the vendor's own marketing (VENDOR). Meshy: one 2026 review says faces and hairlines need work.
- Fix for drift: score every version on the same **character scorecard** (section 6) so a gain in one area can't hide a loss in another.

## 3. Candidates, ranked

### A. MPFB2 2.0.x (Blender extension, CC0 output) - the best fit, and it is free
VERIFIED on its own docs:
- 2.0.16 (released 2026-06-13): expression toolset built from **ARKit-compatible face units**, drives Blender shape keys directly; reworked Rigify UI with "generate full rig" in one step; clothes **auto-refit** after slider changes. Release notes admit the face-unit keys "may still need polishing".
- Export Copy panel: **54 ARKit keys** (`faceunits01` asset pack), **22 Microsoft-style visemes** and **15 Meta-style visemes** (`visemes01/02`), "Interpolate visemes and faceunits" copies those keys onto **clothing and hair** child meshes, bake modelling keys, delete helpers.
- Rig menu includes `game_engine`; its material model is "specifically tailored to translate well in an FBX export" and the release notes carry a Unity import example (alpha/beta notes).
- Licence: core assets CC0, closed-source games explicitly allowed (open-rigged-assets note). Third-party asset packs are CC0 or CC-BY only.
- Anny (Naver Labs, Apache-2.0 code, MakeHuman-derived CC0 data, babies to elders) exists as a research body model, but I found no Unity/Blender game workflow, so it is a watch item, not a candidate.

Risks (all testable in a day):
- Body is plain realistic; storybook proportions and a distinct hero face have to come from sliders plus our sculpt and texture work.
- Blender FBX export has a history of **dropping some relative shape keys** on MakeHuman meshes (blender bug tracker) and a Unity forum user could not drive imported keys; no source confirms the 2.0.16 chain end to end. **Test one export first.** If FBX misbehaves, try glTF.
- Exact `game_engine` bone names/Humanoid mapping are not documented on the pages I could read (still-open item 3 in the Nov 6 note). The Unity avatar auto-map will tell us in minutes.
- Hair is not a built-in strength: cards made in Blender (standard workflow, courses exist) or CC0 hair assets from MPFB packs.

### B. Reallusion Character Creator 4/5 + Unity tools - the turnkey commercial incumbent
- 140+ facial blendshapes, hair, clothing layers, an official-ish Unity auto-setup (community `cc_unity_tools_3D`, built-in pipeline focus). Reallusion says Unity export needs an **Export licence** and store content needs export-licence versions. CC4 was about $299 at launch; I could not find the CC5 price or a clear indie-games statement (UNVERIFIED).
- It breaks the project's "free assets only" rule; the personal-use bar (user, 2026-11-06) is about licence restrictions, not paying. There is a 30-day free trial. **Keep as the fallback if the MPFB2 spike fails, not the first move.**

### C. MetaHuman - licence yes, pipeline no
- June 2025: MetaHuman characters can be used in **Unity/Godot**, free under $1M revenue, no 5% cut (CG Channel, quoting Epic). Tools need Epic cloud autorigging and texture synthesis.
- But the practical route is bad: the face is **joints + DNA-driven morphs**, facial animation exports as per-morph numbers, one forum report of face animation doing nothing in Unity, **hair grooms do not export**, forum calls it engine lock-in and "painful, may break with each update". Look is photoreal, ours is stylised. **Rejected.**

### D. VRM / VRoid
Real standard (humanoid + expressions + spring bones, UniVRM has a URP MToon in VRM 1.0, uLipSync supports VRM 1.0 expressions). Anime-toon look, wrong style for the PBR target. **Rejected for the look**, but it shows the shape of a good contract.

### E. Dead or weak
- **Ready Player Me: shut down 2026-01-31** (Netflix). Don't build on any hosted avatar service.
- **Mixamo**: still works (July 2026 guide) but unmaintained, humanoid T-pose only. Keep it for **clips**, not as the rig source. Whether its auto-rigger keeps shape keys today is UNVERIFIED.
- **UMA 2** (2.14f7, March 2026): runtime DNA/slot system we don't need; URP status unclear.
- **Whole-character AI meshes** (Tripo/Meshy/Hunyuan): fused clothes, unverified face rig. Fine for **rigid accessories and concept shape**, not as the deforming character.

## 4. Recommended pipeline: contract-first, MPFB2 spine, AI only for parts that don't deform

1. **Body + face rig (MPFB2).** One MPFB2 character per hero/NPC. Sliders to the PHASE1 3.6 heights and about 6.5 heads. `game_engine` rig. Face units + visemes enabled in Export Copy. This is the only stage that makes skeleton and face keys, so every character shares them.
2. **Hero identity.** Sculpt the face **on the MPFB2 head as one extra shape key** (keeps topology and vertex order, so ARKit keys keep working; check in the spike). Skin/eye textures hand-made or AI-assisted. Do **not** transplant an AI-generated head: moving the 52 keys onto a foreign head works (free Mesh Data Transfer by UV, Blender 4.2 tutorial) but needs manual fixes at eyelids, jaw, inner mouth and asymmetric pairs - that is exactly the invent-our-own-path trap.
3. **Outfits.** Start from MPFB2 fitted clothes (CC0 packs), restyle in Blender, or model over the body; skin by Blender's built-in weight transfer **by name**; interpolate face keys onto anything near the face. Layer meshes: boots, trousers, shirt, outer, belt. Dye = a tint-mask channel + one material colour parameter, no new meshes.
4. **Rigid parts skip skinning entirely.** Axe, apron plates, pauldrons, lantern, hat, belt buckles: generate with the Hunyuan/TRELLIS prop pipeline we already trust and **parent to a bone**. That uses the AI work where it is strong and removes the rigging risk.
5. **Hair.** Cards in Blender (standard path). Unity's strand-hair package is not confirmed for Unity 6 (UNVERIFIED), so cards first.
6. **Unity.** Humanoid avatar; one `SkinnedMeshRenderer` per layer re-bound to the body's bone array by name (the standard approach; works only for identical skeletons, which is why step 1 is the single source of the skeleton). Hide covered body regions with a region mask so skin doesn't poke through. Body clips from UAL/Mixamo through the existing `HeroAnimator.controller`.
7. **Face in game.** **uLipSync** (MIT; maps A/I/U/E/O to blendshapes; can pre-bake into Timeline) for dialogue; expression presets as ARKit-key sets; eye/head look-at with Unity's own Animation Rigging package. No custom face runtime beyond a small preset table.
8. **Shaders.** The one place we do write our own: URP skin (wrap lighting + cheap SSS fake) and an eye shader. Paid options exist (PIDI) and a free character ships URP skin/hair/eye shaders (licence unread), but the storybook look wants tuning anyway.

## 5. The spike that decides it (target: one session, Tobin only)
Pass/fail, in this order, stop at the first failure and use the fallback:
1. MPFB2 body with `game_engine` rig -> Export Copy with ARKit + visemes + interpolate -> FBX (STRIP) -> Unity. **Pass:** Humanoid avatar configures with no red bones; ARKit keys appear on the SMR and move in the inspector. *Fail -> retry as glTF; then Rigify rig.*
2. UAL clip and a Mixamo combat clip play through `HeroAnimator.controller` with no shoulder/hip collapse.
3. Two outfit layers swap at runtime (bone-by-name rebind), with a body mask, no poke-through during a run and an attack.
4. Face at cutscene distance: blink, jaw open, smile, and uLipSync on one voice line.
5. Sculpted hero face as a shape key; ARKit keys still read correctly on it.
6. One rigid prop (axe) bone-parented across the whole attack set.
If 1 or 5 fails after one retry: take the CC4 free trial and run the same six checks. Decision rule agreed in advance so the answer isn't re-argued.

## 6. Scorecard to stop the drift (score every character version on all of these)
| Axis | Measure |
|---|---|
| Rig validity | Humanoid avatar configures clean; skin deformation at shoulders/hips/elbows/knees in the 6 hero poses |
| Face | Number of working ARKit keys of 52; visemes present; 1 m render at neutral and 3 expressions |
| Outfit | Swap works at runtime; no poke-through; dye works; tris per layer |
| Style | Side-by-side with PHASE1 3.6 targets, silhouette read at 30 m |
| Licence | Every part tagged cc0 / cc-by / personal-use in the source record |
| Labour | Hours of manual fixing per character; target is falling each version |

## 7. Open items
1. Does 2.0.16 `game_engine` + Export Copy survive FBX -> Unity with shape keys intact? (the spike)
2. `game_engine` bone names and Humanoid map (read in the add-on).
3. Licence text of the free "Chinese Cyborg Warrior" URP skin/eye shaders before borrowing any.
4. CC5 price and indie-game terms if the fallback is needed.
5. Whether Mixamo's auto-rigger keeps shape keys (only matters if we ever rig an outside mesh there).

## Questions for the user
1. Which wins for **characters**: P29 (MPFB2 body + generated parts) or "characters stay on Tripo"? This note assumes P29, with Tripo/Hunyuan used only for rigid parts and concept shape.
2. OK to treat CC4 (paid, ~$300, 30-day trial) as the fallback if the MPFB2 spike fails?
3. Is face acting voiced (real audio) or text-only? That decides whether uLipSync matters or only expression presets do.

## Sources
- MetaHuman licence change: https://www.cgchannel.com/2025/06/you-can-now-sell-metahumans-or-use-them-in-unity-or-godot/
- MetaHuman to Unity workflow discussion: https://discussions.unity.com/t/metahuman-to-unity-hdrp-best-workflow/1713711
- MetaHuman face animation not moving in Unity (Epic forum): https://forums.unrealengine.com/t/export-animation-sequence-with-mesh-to-unity/2616657
- MPFB2 Export Copy options: https://static.makehumancommunity.org/mpfb/docs/exporting/export_copy.html
- MPFB 2.0.16 release notes: https://static.makehumancommunity.org/mpfb/releases/release_2016.html
- MPFB 2.0 beta 1 (game engine rig, FBX material model): https://static.makehumancommunity.org/mpfb/releases/release_20b1.html
- MakeHuman shape keys lost on FBX export: https://projects.blender.org/blender/blender-addons/issues/44710
- Transferring ARKit keys to another head: https://cgpaws.substack.com/p/transferring-52-arkit-shape-keys
- Reallusion CC4 launch/price: https://www.cgchannel.com/2022/05/reallusion-releases-character-creator-4/ ; Unity tools: https://github.com/unitycoder/cc_unity_tools_3D
- Ready Player Me shutdown: https://avatarsdk.com/blog/2026/08/31/avatar-platforms-2026-whos-alive-whos-gone/
- Mixamo status 2026: https://app.cinevva.com/guides/free-character-animations-rigging
- UMA 2 store page: https://assetstore.unity.com/packages/3d/characters/uma-2-35611
- UniVRM material/URP: https://vrm.dev/en/univrm1/material
- uLipSync: https://github.com/hecomi/uLipSync
- Anny body model: https://europe.naverlabs.com/blog/anny-a-free-to-use-3d-human-parametric-model-for-all-ages/
- Auto-Rig Pro (Unity humanoid export, paid alternative to Rigify): https://superhivemarket.com/products/auto-rig-pro
- Unity strand hair package: https://openupm.com/packages/com.unity.demoteam.hair/
- Free URP skin/hair/eye character asset: https://assetstore.unity.com/packages/slug/287098
