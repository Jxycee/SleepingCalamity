# Character pipeline: rigged body + face + swappable outfits (2026-11-08)

Asked by the user 2026-11-08: find the best verified pipeline for properly rigged characters (face and body) that we can still customise, using existing paths instead of inventing our own. Method: read the repo (PHASE1 3.6/P29, STORY customisation rule, the Nov 6 research notes, `sc-ai-model-pipeline`, `sc-asset-adopt`), then web research. **Not seen:** the user's local sessions and PC memory (this was a cloud session). Where the repo says something that may be out of date locally it is flagged. Tags: VERIFIED = read on the source's own page; VENDOR = only the vendor says so; UNVERIFIED = not found.

## 0. Revision 2 (supersedes the earlier "Tripo Tobin path"): what the local session actually did
The earlier revision here assumed a clothed Tripo model and add-ons the user can buy. Both were wrong: Tobin is a **bare, hairless A-pose base**, clothes are out of scope, and the user **cannot buy add-ons right now** (the local memory note says FaceKeys was ruled out for that reason). HeadWrap, FaceKeys and CC4 are therefore dropped from the plan.

### What the "Prop concept factory + overnight 3D batch" session did (read from its transcript)
I read roughly the newest third of it (rig_v1/v2, shapes_v1/v2, face_v7/v8, clothes_v1, and its memory note excerpts). **Not read:** the early part (concept generation, neck_v1-3, hair_v1-5, face_v1-v6), and I could not open its `C:\AI` files or sheets.
- **Head:** Tripo head (closed-slit mouth, AI topology, no lip/eye/jaw loops) -> hand-made keys v1-v7 -> `shapes_v1/v2`: MPFB2 ARKit/viseme **deltas transferred onto the Tripo head** by landmark warp + masks. Result: 33 of 52 shapes pass a strict look; consonant visemes show teeth ribbons, smile shows a lip crack, jaw shapes show teeth bits. This is the "merged open-source expressions" that look creepy and distort skin.
- **Head v8:** swapped in a real MPFB2 head with its **own** keys, conformed to Tobin's face and baked his texture. The agent's verdict: mouth clearly better, likeness slightly worse, but ears crude, lids rough, eyes slightly off-axis, cheek facets and a seam. The mouth only got good when the donor topology and its native keys were used untouched.
- **Body rig:** `rig_v1` = UniRig skeleton, renamed Mixamo-style (68 bones incl. Jaw, EyeL/EyeR), Blender heat weights on the Tripo body: user verdict "only like 2 poses look natural". `rig_v2` = joints re-placed from mesh cross-sections (v1 legs/spine were up to 8.9 cm off), Y Bot weights conformed, 16 helper bones: better, but arms-overhead is still unnatural because **the Tripo body has no shoulder or armpit edge loops**; the agent's own conclusion is that the mesh, not the weights, is the limit.
- **Process:** each stage (neck, hair, face, shapes, rig, clothes) invented its own transfer/conform script on an AI mesh, then the next stage inherited its flaws. About ten versions, mostly re-running adapters.

### Root cause, in one line
**Every failure is "adapt AI-generated topology to a rig". The one thing that worked (face_v8's mouth) is the opposite: use a production template whole, with its native topology, rig, weights and keys, and move only the identity onto it.** Do that for the body too, and it also deletes the neck-ring seam work (neck_v1-3), because head and body are then one mesh.

### Verified facts that make this cheap (no purchase)
- MPFB2's rig folder ships `rig.mixamo_unity.json`: **64 bones, Mixamo names, with `mixamorig:Jaw`, `LeftEye`, `RightEye`**, plus matching weights files. That is the same skeleton rig_v1 tried to rebuild by hand. `rig.game_engine.json` is 53 Unreal-style bones with **no jaw, eyes or twist bones**; `rig.default.json` has 163 bones including eyes, jaw, 11 tongue bones. (Read from the MPFB2 repo.)
- Export Copy (MPFB docs): ARKit **54** keys, **22 Microsoft + 15 Meta visemes**, "Interpolate" onto clothes and hair, bake modelling keys, delete helpers. The face keys are generated **onto the current, customised character**, which is why the order is: identity first, keys last.
- MPFB 2.0.16 (2026-06-13): expression toolset from ARKit face units, reworked Rigify UI. The release notes say the face-unit keys "may still need polishing", so budget a pass on the five dialogue vowels plus MBP/FV, not all 52.
- The Unity-import side is **still unverified end to end** (no MPFB2-specific Unity guide found; Blender FBX has dropped relative shape keys on MakeHuman meshes before). That is the first spike gate.

### Recommended base-model pipeline (free)
Goal: a bare Tobin base, one skinned mesh plus eyes, teeth, tongue, hair on the head bone. Clothes later, built on the same skeleton.
1. **One MPFB2 figure.** `mixamo_unity` rig, native weights. Set macro shape with MPFB2 modifiers/targets against the concept views (the user judges by the concept sheet, not the Tripo mesh: "younger, softer, lean build").
2. **Body identity from the Tripo body, with a standard tool only.** Use Blender's built-in Shrinkwrap (nearest surface, low strength, masked, then smooth) to nudge the MPFB2 body toward the Tripo body's silhouette, or just sculpt. The body is the forgiving region. **Do not warp the face region this way** (that is what produced v8's crude ears and rough lids).
3. **Face identity by targets plus a limited sculpt** on the MPFB2 head (jaw, brow, nose, ears kept as MPFB2 shapes). Then **generate the keys last** (Export Copy: faceunits + visemes), so they are native to the final shape.
4. **Texture.** Bake the Tripo albedo onto MPFB2's UVs by projection (Cycles Selected-to-Active with a cage); eyes, teeth and tongue are MPFB2 assets (reuse face_v8's teeth/tongue work only if it already matches). Keep hair_v5 as a rigid mesh on the head bone.
5. **Unity spike (gate, nothing else starts before it passes):** Humanoid avatar maps with no manual fixes; ARKit keys visible and driveable on the SkinnedMeshRenderer; run the **same-frame-vs-Y-Bot test the rig_v2 agent already built** on 9 real clips (walk, run, sword, punch, sit, crouch, jump, cast, climb) and the arms-overhead pose; cutscene-distance face render at neutral, blink, jaw open, AA, OO, smile.
6. **Face playback rules (so it stops looking creepy):** separate systems for blink, gaze and lip sync; eyelids follow the eyeball; micro-saccades at irregular 200-600 ms intervals (rule of thumb from a commercial guide); limit eye rotation (about 30 deg horizontal, 20 deg vertical); data-driven blink curves beat eased ones (Disney Research study); cap shape weights below 1.0 where teeth or lip cracks appear; drive dialogue with uLipSync on the five vowels. Uncanny faces usually come from the eyes and lids, not the mouth.

### If the MPFB2 face fails the spike: the higher-ceiling option
**MetaHuman Mesh-to-MetaHuman on Tobin's head** (conforms Epic's production facial rig to a custom head, cloud autorig) plus the **free Poly Hammer Character DNA Blender add-on** (free since July 2026; imports DNA, bakes, exports; GPLv3 core; Blender 4.5+, DNA from UE 5.6+). The licence allows Unity use under $1M revenue (CG Channel, June 2025). But: no source I found shows a working DNA-to-Unity face, hair grooms are not in DNA files, Epic's cloud and Unreal are required, and the look is photoreal. Treat as a one-day head-only spike, not a plan.

### What to stop doing
- No more transfer or conform scripts between AI meshes and rigs. If a step needs one, stop and report.
- One version = one gate changed; every version scored on the section 6 scorecard plus the Y Bot same-frame test.
- Tripo is for likeness reference, texture and (later) rigid parts and clothing, which clothes_v1 already proved works.

### Added sources
- MPFB2 rig definitions: https://github.com/makehumancommunity/mpfb2/tree/master/src/mpfb/data/rigs/standard
- Character DNA now free: https://www.cgchannel.com/2026/07/the-character-dna-metahuman-add-on-for-blender-is-now-free/ ; https://www.polyhammer.com/character-dna-addon
- Eye/blink realism: https://la.disneyresearch.com/wp-content/uploads/Modeling-and-Animating-Eye-Blinks-Paper.pdf ; https://www.gamesradar.com/sony-explains-how-the-last-of-us-2-has-such-realistic-facial-animations
- Earlier sources for HeadWrap, FaceKeys, AccuRIG, CC4 remain below for reference; they need purchases or were not needed.

## 0b. How real teams do it, and the spike brief (user asked 2026-11-08)
**The common flow** (every source agrees): concept -> high-poly sculpt -> **retopology to a clean low mesh with loops at eyes, mouth, shoulders, knees** -> UVs -> **bake** the sculpt's detail and colour onto the low mesh -> rig and skin -> facial shapes -> engine. AAA adds scans and FACS capture and hand-weights the hero; indies use Blender instead of ZBrush/Maya and often start from a **shared-topology base mesh** (Polycount threads: every head on the same topology as a variant of one base; The Order: 1886 reportedly shared head topology and UVs; Disney's Cinderella mouse and horse shared topology) so **one rig, one weight set and one expression library serve every character**. Shared topology only works if eyes and mouth sit on matching loops. Sources are forum and vendor material; I found no single studio case study end to end.
**Can we copy it? Yes, the indie half.** Our Tripo mesh plays the role of the sculpt. The retopo step is "use the MPFB2 template as the low mesh", and the base-mesh/shared-rig idea is exactly what makes Wren, Pip, Ash and townsfolk cheap afterwards (same skeleton, same keys). What we cannot copy is scan/FACS capture and hand-painted weights; MPFB2's native weights stand in for those.

**Texture stays as clean as the original (user's one hard requirement)**
1. **One bake, from the original, never a chain.** Bake colour straight from the untouched Tripo head (original UVs and original texture file) onto the template's head UVs. Do not bake from a derived copy, and never re-bake a bake.
2. **Supersample.** Bake at 4096 (or 2x the final), then downscale once to the final size with a high-quality filter. No AI upscale and no repaint on the face.
3. **Cage, not guesswork.** Selected-to-Active with a cage object or tight extrusion (just enough to enclose the Tripo head); too small smears, too large ghosts. Margin 16 px or more; template UVs with no overlaps; one island for the face so there is no seam across skin.
4. **Optional zero-loss variant for the face.** Instead of baking, copy the Tripo head's UVs onto the conformed template with Blender's Data Transfer (face corner data, UVs, nearest face interpolated) and keep the original texture file as-is. This works only where the Tripo face is one contiguous UV island; across island borders it stretches, so use it on the face, bake elsewhere. Try it first, because it has no resampling loss at all.
5. **Measure, don't eyeball.** Render 1:1 crops (eye, lip edge, brow, cheek) of original vs result in the same light and compute the same sharpness number on both (the repo already has `sc_tripo_texel_analyze.py` for texel density; any Laplacian-variance script is fine). **Accept if the face crops score at least 95% of the original and a person can't pick the original in a blind A/B.** Reject on halos, seam lines, or colour shift (compare mean Lab colour per crop).

**Spike brief for the local session (one new folder, one version, report once)**
- Folder `base_v1`. Inputs: bare A-pose Tobin (neck_v3 body + face_v7 head, original textures), concept refs, MPFB2.
- Build: one MPFB2 figure, `mixamo_unity` rig, targets set against the concept sheet; body nudged toward the Tripo body with Shrinkwrap (masked, smoothed); face by targets plus a small sculpt, no whole-face warp; then Export Copy with ARKit + visemes; eyes, teeth, tongue from MPFB2.
- Texture: sections above.
- Gates, in order, stop at the first failure and report: (1) Unity Humanoid maps with no manual fix and the ARKit keys move on the SkinnedMeshRenderer; (2) the Y Bot same-frame test on the 9 real clips plus arms-overhead, no pose rated unnatural; (3) face playback test with eyes, lids and micro-saccades on, neutral, blink, jaw open, AA, OO, smile at cutscene distance; (4) texture acceptance test above.
- Rules: no new transfer/conform scripts between meshes; one change per version; every version scored on the section 6 scorecard.

## 0c. What Tripo's own docs say (read 2026-11-08, developers.tripo3d.com; API docs, the Studio UI may differ)
**Nothing in the docs describes our failure** (AI mesh won't rig or deform, textures stretch). There is no character guidance at all: the generation pages have **no T-pose/A-pose option** (a vendor blog mentions a T-pose toggle in the Studio UI only), and rig-check lists no pose, limb-gap or topology requirements. So character prep has to be done in the input images. What the docs do give us as levers:
- **Topology.** P2 (`P2-20260801`) = native quads, 48-25,000 quads, and the Models & Versions page recommends it "for game characters and props"; H3.1 (`v3.1-20260211`) also has `quad` (up to 150k, forces FBX); `smart_low_poly` is for simple shapes only (500-20,000 tris, may fail on complex ones); Retopology v3.0 can `quad` and `smart_uv` in one task. The docs give **no promise of edge loops at lips, eyes, armpits or knees**: test, don't assume.
- **UVs and texture fit.** `export_uv` (default true); **Smart UV** re-unwraps and *remaps the existing textures, rigging and animation* (`bake` true; input must be at most 80,000 tris, a quad counts as two). Texture knobs: `texture_alignment` (`original_image` favours the input colours, `geometry` favours the shape: try `geometry` if textures slide off the form), `orientation: align_image`, `delight` (default true, needs texture v3.5), `texture_quality` up to `extreme` = 8K.
- **Iterate without redoing the mesh.** `model_seed` fixes geometry; vary `texture_seed` to re-texture the same mesh. Pin model versions (the docs advise it) so results don't drift between sessions.
- **Parts (moving pieces).** `generate_parts` returns editable segmented parts but only **untextured, triangle** output (needs `texture:false, pbr:false`; ignored with `quad`; beaten by `smart_low_poly`). Segmentation v2 (beta) splits a finished model with semantic labels (`simple/balanced/detailed`, optional `ref_image`); Retopology takes `part_names`. The docs do not say whether segmentation returns separate meshes: verify on one model.
- **Rigging.** Auto Rig v3.0 (`v3.0-20260909`, explicit, "recommended for humanoids") has `spec` = tripo, mixamo, actorcore, **unity**, unreal, vrm; GLB or FBX out. **Nothing is said about face rigs or blendshapes**, and the example returns only a model URL. Tripo's rig is body-only; the face is not Tripo's job. (The project's earlier rule "a Tripo rig is never used" stands unless one test beats rig_v2.)
- **Export traps.** Leave `export_orientation` unset and convert last with `/v3/models/convert` (a wrong orientation can still report success); `auto_size` gives metres.

### What this changes
1. Tripo's docs support using it for **clothing, props and rigid parts** (quad output, parts, Smart UV, seeds) but give no way to get a production *face* or shared-topology *body*. The template-first plan stands for the base.
2. A cheap, documented experiment: generate Tobin's head/bust from the front concept with **P2, `quad: true`, `texture_alignment: geometry`**, and look at the wireframe for loops at lips and eyes. Even if good, it has no keys; it could only become a better likeness target for sculpting, not a replacement for the template.
3. Add to the intake checklist for every Tripo character: record the model version and all settings (seeds included) in the source record, export the raw GLB **and** the four PBR maps, keep orientation default, and keep the untouched texture as the bake source.
4. Prep the input, not the output: the concept image goes in as a clean A/T-pose turnaround, neutral face, hands open, arms clear of the torso, plain background, even light (all general practice from the pose guides; Tripo's own docs only ask for a clear subject, clean background, minimal occlusion).

## 0d. MetaPerson (Avatar SDK) check, 2026-11-08
User tried MetaPerson Creator; it looked great but a page reload lost the one free avatar and mobile shows no gallery. Findings from Avatar SDK's own pages: first character free to create and export; default export is LOD1 with **1K JPG textures** (2K/4K, PNG/WEBP, animation, extra LOD cost credits; no credit prices published); GLB/FBX, rigged, Mixamo-animation compatible, "has blendshapes" (ARKit/viseme naming not stated); likeness comes from one selfie or presets, so it is not Tobin. The terms (Itseez3D EULA) say nothing about games: avatars count as licensee data, but hair/clothing "Physical Features" are usable only with an Itseez3D-generated avatar, and licensing or selling rights to an avatar may require written notice and a revenue share. Whether a lost avatar is recoverable server-side is not documented; support is support@avatarsdk.com. **Use only as a placeholder/NPC option or a rig comparison; it cannot meet the clean-texture bar at 1K and does not hold Tobin's likeness.**

## 0e. MetaPerson as a candidate base (user result, 2026-11-08)
User fed the isolated Tobin face image to MetaPerson as the selfie and got "the perfect textured face"; the avatar was lost on a reload. This is the same template-first idea as MPFB2, done as a service: a fixed production head and body with a native rig, whose texture is generated from the photo. Verified from Avatar SDK's own pages: rigged full body, GLB/glTF/FBX, Mixamo-compatible animation, **15 Oculus visemes** (`visemes_15`: PP FF TH DD kk CH SS nn RR aa E ih oh ou + Laughter), extra face/body blendshapes added late 2024, face and body-shape editing, a Unity loader package and a 2026 Unity plugin. **Not stated:** ARKit-52 names (the API says the blendshape list depends on pipeline and account), mesh layout, skeleton details, 2K/4K price, credit price, game licence, and whether a lost avatar can be reopened (the pricing page says re-exporting an already generated avatar is free, which suggests it is stored server-side: log in on desktop with the same email and look). Free export is LOD1 with 1K JPG, so the clean-texture bar needs the paid 2K/4K option or a rebake of the original Tripo face texture onto its UVs (section 0b rules); never AI-upscale. Gates before adopting: (1) export a GLB and open it in Blender/Unity: Humanoid maps, which blendshapes exist (list them), jaw/eye bones; (2) face playback test; (3) texture 1:1 crops vs the original; (4) body adjustment in Blender rest pose below the neck only (moves joints with the mesh, leaves face keys alone); (5) written answer from support@avatarsdk.com on game use. Tag `licence: unconfirmed` until then.

## 0f. MetaPerson export inspected (user's Drive zip, read 2026-11-08, cloud Blender 4.5 headless; GLB read as data, nothing executed)
`avatar/model.glb`, 90 MB, one avatar, settings LOD1 / GLB / PNG-in-GLB textures / visemes + head shape + body shape on / A-pose animation `Male_Pose_A`.
- **Skeleton:** 73 joints, Mixamo-style names without the prefix: Hips, Spine, Spine1, Spine2, Neck, Neck1, Neck2, Head, LeftEye, RightEye, HeadTop_End, shoulders, arms, ForeArm + ForeArm1/2 (twist), full fingers (4 per finger), legs, feet, toes. Humanoid-mappable by name. **No jaw bone**: the jaw is the `jawOpen` blendshape (teeth meshes carry jaw keys).
- **Face (AvatarHead, 7,321 verts, 14,212 tris, 149 shapes):** **51 of the 52 ARKit shapes** (only `tongueOut` missing), **15 Oculus visemes** (CH DD E FF PP RR SS TH aa ih kk nn oh ou sil), plus ~80 identity sliders (Head_*, Jaw_*, Nose_*, Lips_*, Eye_*). Lower teeth and tongue mesh (3,464 tris) carry jaw + visemes; eyeballs have iris/pupil shapes; separate eyelashes mesh (68 shapes).
- **Body:** 14 body-shape keys (Chest, Forearms, General, Hips, Legs, Shoulders, Waist +/-) on the body skin and on the outfit mesh. **The skin mesh is only the exposed parts** (head/neck, forearms and hands, toes, 4,906 tris): torso and legs exist only inside the `outfit` mesh (39,312 tris, default t-shirt, jeans, sneakers). A bare base needs a re-export with the most minimal outfit.
- **Textures:** all PNG. Head and body base colour **2048x2048**, eyes 512, teeth 1024, outfit base 2048 and outfit normal 4096; five unreferenced extra images (likely roughness/AO maps) are not wired to materials. So the "4K" setting did not give a 4K head: effective head texture is 2K (matches the PHASE1 head budget).
- **Size:** bounding box about 1.80 m tall (Tobin is 1.90 m: scale about 1.055 or the Body_Shape keys). 72k tris total with the outfit.
- **Look (Workbench, flat shading):** clean skin with pore detail, no seams or blur in the front view, sharp irises and lashes, clean bald head and ears. Body is a slim default build, not Tobin's broad build.
- **Expression test (12 shapes at weight 1):** smile, brows, surprise, funnel, pucker and the vowels (aa, oh, ou, FF) read natural. Issues to check in engine with real lighting: (1) `jawOpen` at 1.0 shows a flat tongue filling the mouth with little visible upper teeth (looks like tongue out; fine at 0.5 and in the `aa` viseme), (2) closed eyelids render as a muddy brown patch (lid skin texture/shadow), (3) no dark mouth cavity in Workbench. These are the first things to fix or avoid by capping weights.
- **Verdict so far:** far closer to usable than the Tripo-head-plus-transferred-shapes route: native rig, native face shapes, clean texture. Not yet verified: Unity Humanoid mapping, FBX route, licence, and how it fits Tobin's physique.

## 0g. Three MetaPerson exports compared (read 2026-11-08)
- **File 1** (jeans/tee/sneakers, one combined outfit mesh, all paid shapes): full shape set, skin only on head, neck, forearms, hands, toes.
- **File 2** (tee/shorts/sandals, split outfit pieces, free export): different avatar (narrower, younger face; head 7,223 verts), 51 ARKit shapes only, real lower legs and bare feet, no visemes or sliders. Skeleton identical to file 1 (73 joints, 9 mm lower), so heads are not interchangeable.
- **File 3** (tee/shorts/sandals, split outfit pieces, all paid shapes; 152 MB): file 1's avatar (head 7,321 verts, 149 shapes, head texture byte-identical to file 1) with real lower legs and bare feet. Head: 51 ARKit + 15 visemes + 83 sliders; body and outfit pieces carry the 14 body sliders; 73 joints; 48k tris; head/body textures 2048, outfit normals 4096; `Male_Pose_A`. Torso and thighs are still absent (covered by tee and shorts). **Best base so far.**
- Free export is labelled non-commercial (panel: "LOD1 · JPG · 1K · non-commercial", 1 remaining); paid shapes cost +5 coins each; the panel has no option for hidden body skin; AI shirt texture cannot restore skin (exports trim skin under clothes).
- Next: skin-suit torso/thighs from `outfit_top` and `outfit_bottom` (flat skin tone, then texture match), cut the sandals to bare feet, scale to 1.90 m, then the Unity gates in 0e.

## 0h. Tobin base v1 built from MetaPerson file 3 (cloud Blender 4.5, 2026-11-08; not yet opened in Unity)
`Tobin_Base_v1.glb` (37 MB, scratchpad only, not committed): file 3 minus sandals, tee and shorts, plus two **underlay** meshes (`BaseUnderlay_Torso` from the tee, `BaseUnderlay_Legs` from the shorts) carrying the missing torso/upper-arm/thigh surface. Same 73-joint skeleton, all 149 head shapes, body sliders on the underlays (12 and 8 keys), eyes, teeth, lashes, `Male_Pose_A`.
- **Never seen under clothes, by construction and by test:** each underlay carries the custom property `sc_role = underlay_hide_when_clothed` (exported as glTF extras): the clothing system must disable those renderers whenever any top or bottom is worn. As a second guard each underlay is pulled **5 mm toward the nearest bone axis** (always inward; the first attempt used vertex normals, which are inconsistent on the cloth and pushed the suit through the sleeves, chest pocket and shorts hem: 13,493 leaking pixels) and **trimmed two edge rings** so it stops short of every open edge. Test: front render of the clothed avatar with and without the underlays, neutral and with the body sliders at full: **352 and 358 differing pixels of 770,000 (0.05%)**, all on existing mesh edges (a hairline crack in the shorts' centre seam and the one-pixel tee hem), none in the interior; the first attempt (normal inset, no trim) leaked 13,493 and 15,498.
- **Bare look is not a goal:** the underlay is a flat tan, a little darker and more orange than the real skin, with open gaps at the sleeve ends and shorts hem. Fix later only if a bare-torso cutscene is ever needed (sculpt + baked skin texture).
- **Scale:** exported at 1.80 m; Tobin's 1.90 m = Unity import scale 1.0556 (not baked, to keep bone scales at 1).
- Build script kept at `base_v1/build.py` in the scratchpad (`RINGS=2` env, 5 mm inset); not in the repo yet.
- **Next:** hair onto the head, then the Unity gates (Humanoid map, face playback with jaw capped, Y Bot same-frame clips).

## 0i. Tobin base v2: anatomical underlay instead of the flat skin suit (user request 2026-11-08; cloud Blender; not in Unity yet)
`Tobin_Base_v2.glb` (35 MB, scratchpad, not committed). Replaces the recoloured clothing shells with a built body for the missing chest, back, upper arms, hips and thighs, in the order a sculptor works:
1. **Measure** where the real skin ends: along each arm and leg the real skin starts at about 26% (arms) and 28% (legs) of the way from the joint; ring centre and radius measured there so the new pieces meet the real forearms and lower legs.
2. **Blockout (mannequin):** clean lofted rings along the skeleton: superelliptic torso cross-sections from an anthropometric table (hip, waist, chest, trapezius slope, neck), tapered arm and thigh tubes, started inside the body so no open ends show. First attempt reshaping the T-shirt and shorts shells failed (inside-out lining, a chest pocket, white holes, a thigh ledge), so the mesh is built from scratch.
3. **Primary forms:** pecs with lower border, sternum groove, rectus segments, obliques, lats, traps, scapulae, spine groove, glutes, quads, hamstrings, deltoids, as Gaussian displacements along the normals.
4. **Bind:** vertex-group weights copied from the clothing shells by nearest surface (Data Transfer), Armature modifier, tag `sc_role = underlay_hide_when_clothed`.
5. **Fit under the clothes:** each vertex is ray-cast from its bone axis against the tee and shorts and clamped at least 8 mm inside. Leak test (clothed, with vs without the underlay, A-pose): **258 / 1,058 / 1,056 differing pixels of 770,000** (front/side/back) versus about 13,000 / 9,600 / 9,300 before the clamp; what is left sits at the sleeve openings (skin tone filling the sleeve hollow) plus hairlines at the collar and the shorts' centre seam.
- **Honest limits:** the clamp flattens most of the chest and abs because the tee is cut close to the body, so the muscle relief is faint; the back of each shoulder still has a few ragged overlapping edges where arm tube meets torso; the underlay has **no body-slider shape keys** (the old shell version had them); skin is still a flat tone; the underlay stays hidden whenever clothes are worn.
- **References:** I could not download the Wikimedia anatomy plates (Hagström, public domain/CC0: "Muscles anterior labeled", "Muscle posterior labeled") because the proxy blocks the image host; the proportions come from anthropometric tables plus the measured pieces. Send 3-4 reference photos (front, back, side, thigh) via Drive to refine the forms, but they only matter if a bare torso is ever shown.
- Scripts (scratchpad only): `sculpt/mannequin6.py` is the final version of the build.

## 0j. Body sliders on the underlay, and the hair file (2026-11-08)
- **Sliders done:** `BaseUnderlay_Torso` and `BaseUnderlay_Legs` now carry the same 16 body shape keys as the real body (Chest, General, Hips, Neck, Shoulders, Waist, Legs, Forearms, each + and -). Each key is the weighted deltas of the nearest 6 points of the tee, shorts and real body skin. Leak test (clothed, with vs without underlay, differing pixels of 770,000, front/side/back): neutral 258 / 1,058 / 1,056; all "+" sliders at full 24 / 452 / 275; all "-" sliders at full 283 / 563 / 749. Export `Tobin_Base_v2_sliders.glb` (36 MB, scratchpad).
- **Hair file:** `hair.glb` is the raw Tripo hair: one mesh, 58,321 vertices, 51,338 triangles, a unit-cube size (about 0.96 x 1.0 x 0.98) with no head, a 4096 PNG plus two JPEGs, double-sided material, and **EXT_meshopt_compression** (gltfpack). Blender 4.5 cannot read it, the fallback buffer is empty, and there is no meshopt decoder on the cloud machine (installing one, or running `npx gltf-transform`, was blocked as external code). Needs an uncompressed re-export (Tripo `compress` off, or FBX/OBJ).
- **Digital Salon (github.com/digital-salon/Digital-Salon, MIT):** research tool for text-guided strand grooming and simulation (about 10k strands by default, built with CMake, needs a head.obj you supply). The page lists no FBX/Alembic/GLB export, no Unity/Blender integration and no GPU requirement. Not a fit for placing our existing hair; possible later as a source of a new strand hairstyle, but strands are not game-ready here (Unity's strand package is unverified on Unity 6).
- **Hair wiring plan (ready to run on the uncompressed file):** (1) scale and place the hair on the MetaPerson skull by a similarity fit (iterative closest point on the hair's inner layer), (2) skin it 100% to the `Head` bone with an Armature modifier, (3) copy the few head-shape sliders that move the skull (Head_Width, Head_Height, Head_Thick, Head_Vol, and similar) onto the hair as shape keys by nearest head vertices so hair follows the head, (4) keep the original textures and the double-sided material, (5) tag it `sc_role = hair`.

## 0k. Hair in Unity instead (user, 2026-11-08): findings
- **Project today:** Unity 6000.6.5f1, URP 17.6.0; `Packages/manifest.json` has Animation Rigging 6.6.0 but **no** glTFast, Alembic or hair package.
- **Unity Hair package** (`com.unity.demoteam.hair`, Unity Technologies, git URL install; preview 0.19.0-preview.1 in the repo's package.json): authors, imports (Alembic), simulates and renders strand hair; **procedural generation** can scatter strands on a mesh or primitive with parameters (length, curl, clumping) or a custom generator; GPU strand solver (tens of thousands of strands), automatic clustering and LOD; render modes lines, view-facing strips and tubes; Shader Graph nodes `HairVertex`/`HairVolume`; **URP is listed as supported** alongside Built-in and HDRP; compute-shader platform required. Skinning strand roots to a skinned mesh needs the separate `com.unity.demoteam.digital-human` package. **Not confirmed:** Unity 6000.6 / URP 17.6 compatibility (the repo says Unity 2020.2+, a third-party index lists Unity 6), current maintenance (no releases listed, 56 open issues), licence text (LICENSE.md not read), and one community report that it needed Vulkan on a 2022 project. Alembic is required for DCC grooms (`com.unity.formats.alembic` 2.2.2+).
- **So the Unity strand route is a testable experiment, not a known-good path.** It must be run in the editor on the PC (no Unity licence or GPU in the cloud).
- **Plan A (reliable, do first):** get our Tripo hair into Unity as a normal mesh. Either re-export from Tripo uncompressed (FBX/OBJ, or `compress` off) or import `hair.glb` with glTFast plus the meshopt decompress package (glTFast supports EXT_meshopt_compression, Blender 4.5 does not). Parent to the character's `Head` bone, fit scale/position by bounding box then by eye, keep the original textures and double-sided material, optional follow-the-head-sliders script.
- **Plan B (the "Unity tools" copy):** install Alembic + Hair (git URL), check it compiles on 6000.6.5/URP 17.6, make a Hair Asset by procedurally scattering strands over a scalp mesh cut from the MetaPerson head (the cloud can export that OBJ), tune length/curl/clumping to the concept views, render at gameplay distance in the stylised-real look, then try root-skinning to the Head bone. Report compile errors and a capture; if it fails or looks wrong at gameplay distance, stay on Plan A.

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

## 8. Face-template spike (2026-11-08, branch claude/face-template-spike): wrap Tobin's head topology onto a new face
Method: keep `AvatarHead` (7321 verts, UVs, 150 keys) and move its vertices to a new face. Scripts: `Art/Blender/tools/experimental/face_template/`. Data and sheets (not committed): `C:\AI\test_out\heroes\face_spike\` (`out/face2_proof_sheet.png`, `out/face2_closeups.png`, `out/face2.blend`, `bake/Head_BaseColor_face2.png`). Provenance: template = MetaPerson export (licence unconfirmed); target = local SDXL concept (Gemini not signed in, Nano Banana skipped) -> Hunyuan 2.1 shape -> TRELLIS.2 texture.

**What worked**
- Shared topology holds: head 7321v/14212f/150 keys, lashes 6300/68, eyeballs 512/4, lower teeth 2511/21, upper 2497/2; UV loops `array_equal` to the template, no NaN. ARKit, visemes and sliders all survive by plain delta transfer (`k_new = k_old + (basis_new - basis_old)`; Blender keys are absolute, so write Basis to both the Basis key and `mesh.vertices`). Sheet rows blink, jawOpen 0.5, aa, oh, smile all deform cleanly on the new head; eyes and teeth need only sub-millimetre rigid re-seating; lashes follow via Surface Deform.
- Standard tools were enough: Umeyama + trimmed scaled ICP, `scipy.optimize.lsq_linear`/bvls for sliders, `trimesh.registration.nricp_amberg` (on the welded mesh, `use_faces=False`, rtree missing), Blender selected-to-active Cycles diffuse bake at 2048.
- Fit (face-skin RMS/P95 mm to cropped target): template 2.05/4.47, sliders only 1.90/4.18, wrap r3 0.91/1.60. Sliders barely help (-0.15 mm); landmark+ICP alignment does most of the shape change (Head_Height_Dn, Head_Vol_Dn pegged at 1.0, then Head_Width_Dn, JawLine_Widen, Head_Cheek_In).
- Hard-stop checks after 3 wrap rounds: lips pass (no self-intersection at jawOpen 0.5/aa/oh, ring edge ratios 0.98-1.04, 0 flips); eyeBlinkLeft mean lid gap 1.205 mm vs limit 1.197 (0.008 mm over). The literal "<=0.5 mm" gate fails on the untouched template (max ~2 mm), so the gate was template + tolerance. Not a stop.

**What broke / is weak**
- Likeness is low. The wrap moves head, jaw and cheek shape but the target's brow, nose and lip identity barely transfer; slider-only and full wrap look almost the same on the sheet. Reason: the AI target has blank lid bumps, no eye cavities and slightly parted lips, so the wrap had to be held back there (zero data weight within 8 mm of lid rings and lip lines, harmonic fill), and the wrap has no per-vertex weights in `nricp_amberg` (done by pinning).
- Texture: the TRELLIS colour was unusable (orange/purple blotches, black speckles). The bake keeps only its high-frequency detail and swaps the low-frequency tone for the concept skin tone; result is mottled forehead, smeared brows, olive patch below the lower lip, neck seam needs a darkened `Body_BaseColor` (written as `Body_BaseColor_face2.png`). Normal map is still Tobin's.
- Ears follow only by displacement fill. Neck stub of the target was only ~3 cm so the neck is Tobin's. `General+/-` and `Shoulders+/-` exist on both head and body and must be applied together.
- GLB default pose is offset about 2.4 cm from the rest pose: export/evaluate in REST.
- Teeth poke-through at jawOpen 0.5 is only checked by eye, not numerically.

**Verdict.** The route is sound mechanically: topology, UVs, joints and all expression keys survive, so no key transfer is needed. It is not yet good enough to make a one-command tool. Needs first: (1) a better target (multi-view or a photo scan, eyes open, mouth closed, real brows; Hunyuan from one SDXL image gives a generic face), (2) a landmark-weighted wrap (per-vertex stiffness, lid and lip landmarks pinned to the target) instead of holding those regions back, (3) skin from the concept via projection/inpaint rather than TRELLIS colour, (4) eyebrows as hair cards. Unity gate not run (texture not good enough to justify it). Deformation transfer (Sumner-Popovic) is the upgrade if lids or lips break on a more different face.

**MPFB2 as fallback template** (checked 2026-11-08): MPFB docs state "All core assets (the base mesh, targets, skins...) are shared under CC0" and that closed-source games are allowed, add-on code is GPLv3 (https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html, https://extensions.blender.org/add-ons/mpfb/). So a CC0 MPFB2 head could replace the MetaPerson template if licence terms come back negative, and the same wrap method applies. Not verified: whether the repo ships a face-units/ARKit-like expression set with it (earlier notes in section 3 mention faceunits01); third-party assets (clothes, skins) can carry other licences. Cost: re-do the key set (52 ARKit + visemes) on the MPFB head, which this spike did not need.
