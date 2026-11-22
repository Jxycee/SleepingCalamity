# MetaHuman Maker -> Unity assessment (2026-11-17, research only)

## Verdict
Partly useful, not as a Blender-only tool. Wrap, sculpt and skin-map baking run in Blender (MIT add-on) and produce standard meshes and PNGs. But every step needs a licensed MetaHuman reference library. The only route to a rigged, expressive Unity-importable character goes through Unreal 5.8 + MetaHuman Creator (native capture -> "native web bundle" -> `portable_character.py` -> one GLB). That needs a UE install we do not have. It is MetaHuman realism, not our storybook stylised look. The tool is a poor match for the main goal (many stylised heads); MetaHuman-realistic NPC faces are the one thing it could add. Recommend a small pilot only if realistic faces are wanted; otherwise stay on Tobin pipeline v2 and reuse it for further heads.

## Stage -> Unity-usable? -> how
| Stage | Unity-usable | How / catch |
|---|---|---|
| Wrap / fit to MH topology (Blender + separate Python 3.12 worker) | Yes (mesh) | Result is a mesh on MH LOD0 topology + UVs; export FBX/glTF from Blender. Needs the user-supplied reference library (head/body/eyes/teeth). |
| Photo fit | Yes (mesh), weak | Only a "starting identity"; docs say likeness/hair need authoring. |
| Sculpt layers / regional repair | Yes | Plain mesh edits. |
| Skin maps (albedo, normal, cavity, roughness; to 8K) | Yes | PNGs. We would bake at 2K (our cap); MH face uses several maps + animated wrinkle maps that Unity will not have. |
| Grooms / hair | No (as grooms) | Alembic grooms exchange with Unreal only. The GLB route gives "reduced skinned groom ribbons". Our HairDeck cards + SC_HairCards stay the hair path. |
| Body (anatomical controls) | Mesh yes | Anatomical controls need installed native model/Creator data. Body arrives rigged only via the UE path. |
| Facial rig / expressions | Only via UE | Static GLB has no rig/DNA. Rig comes from Epic's "Request Full Rig" service (Epic account), then native capture, then GLB with skeleton + sampled facial morphs by name. RigLogic and animated material responses omitted. Known-issues: eye/lid calibration experimental, sampled combos approximate. |
| LODs | Unreal only | LOD review/capture is in UE; GLB conversion is LOD0 oriented (unverified). |
| MH shaders | No | Skin/eye/hair shaders become portable PBR approximations; we would rebuild in URP anyway. |

Web/GLB detail (docs/workflows.md "Web and Three.js"): (1) Static GLB = geometry + eyes/teeth + PBR approximations, no rig/DNA/grooms. (2) Native web bundle = built from a completed Unreal "Native Capture" folder (needs source-manifest.json, rigged skeletal meshes, extracted texture pixels). (3) `python create_metahuman/portable_character.py --source ./Character-WebBundle --output ./Character.glb --report conv.json [--compact --max-bytes N]` (NumPy/SciPy/Pillow) gives one embedded GLB: PBR textures, shared body+face skeleton, sampled facial morphs by name, 4 skin influences per vertex, groom ribbons. No locomotion clips (retarget to the skeleton). Default keeps an 8K face albedo; compact still has 8K face albedo, 4K face normal. We would downsize to 2K ourselves. Unity side: import GLB through a glTF importer package (glTFast), then Humanoid-map or use as generic.

## Licence (conditions)
- Tool: MIT code, but MetaHuman data, DNA, textures and Creator Core Data are not bundled and keep their own licences (repo THIRD_PARTY_NOTICES.md: https://github.com/TREE-Ind/MetahumanMaker/blob/main/THIRD_PARTY_NOTICES.md ; docs/installation.md "Prepare your reference library": user obtains their own licensed neutral head/body via MetaHuman tools).
- Epic terms: from UE 5.6 MetaHuman use falls under the Unreal Engine EULA; characters/animation count as "non-engine products", so they may ship in Unity/Godot with no 5% royalty; the $1M+ revenue seat-licence rule applies only to commercial studios. Conditions reported: no using MetaHumans as AI training data (AI-driven workflows allowed). Sources: https://www.cgchannel.com/2025/06/you-can-now-sell-metahumans-or-use-them-in-unity-or-godot/ , https://digitalproduction.com/2025/06/05/metahumans-graduate-ready-for-unity-godot-and-the-fab-cash-register/ (both secondary press). Primary pages (https://www.unrealengine.com/eula/mhc, https://gateway.unrealengine.com/en-US/faq, UE EULA, metahuman.com FAQ) returned 403/503/login-wall to my fetch: NOT verified against Epic text; one blog (strayspark.studio) disputes it. Older MH Creator EULA (UE 5.5 and earlier) tied use to Unreal-required products. A user should read the current UE EULA "Non-Engine Products" and MetaHuman sections before relying on it. For personal/friends use this is low risk either way.
- Reference library source: Epic only. Creator Core Data + MetaHuman Creator content via UE 5.8 / MetaHuman account (README/installation; no other legal source; the tool does not bundle it). Not obtainable without installing UE 5.8 (not installed here: no Epic Games folder).
- Epic DNA tools (https://github.com/EpicGames/MetaHuman-DNA-Calibration): DNA read/modify, Windows/Linux, Maya, "not updated for 5.6+ characters"; no Unity runtime. Blender "Character DNA" add-on (GPLv3 core, Pro $79; https://www.cgchannel.com/2026/07/the-character-dna-metahuman-add-on-for-blender-is-now-free/) round-trips to Unreal only.

## Facial animation in Unity
No Unity RigLogic/DNA runtime found (search came back empty; treat as unverified). Realistic options: (a) the converter's sampled morphs by name, then map to our ARKit-52 + visemes naming; (b) bake DNA blendshapes to ARKit-52 ourselves (Blender, our existing Tobin shapes_v2 approach); drive with Unity blendshape code + uLipSync (already in plan). Cost: a day or more of mapping per rig plus eyelid/corrective checks; morph counts from sampling are approximate and not guaranteed nonlinear-correct.

## Fit with our look
MetaHuman topology/UV/maps are photoreal; style bible wants storybook proportions and stylised realism. Wrap does take arbitrary source topology (Hunyuan/TRELLIS heads fit), but strong stylisation "needs regional correction"; big-eye/odd-proportion heads get forced toward human anatomy. Tri counts: not stated in docs (MH LOD0 head is tens of thousands of tris, unverified), likely heavier than Tobin's 33k head. Textures: all need rebake to 2K. Hair stays HairDeck. Verdict: complements, does not replace, Tobin pipeline v2 (which already gives ARKit shapes + visemes from a generated head with no Epic dependency).

## Setup cost on this PC
Blender 5.2.2 LTS installed (add-on needs 5.2.0+, tested 5.2.3: OK). RTX 5070 12 GB (docs: 8K/dense layers heavy; use 2K/4K). Worker: separate Python 3.12 venv (`tools/setup_surface_solver.py`); optional learned backends are multi-GB and skippable. Windows x64 only: fine. UE 5.8 NOT installed: needed for the Creator reference data, Native Capture, rig request and the web bundle (every route to a rigged Unity asset). UE 5.8 install is tens of GB plus Visual Studio build only if rebuilding the bridge (bridge prebuilt for 5.8.x). Wrap/sculpt/skin-only needs no UE once the reference library exists, but creating the library needs MetaHuman content.

## Recommended pilot (one non-Tobin head, end to end)
1. Session: one local PC session, Opus (judgement, hero-art rule), Sonnet subagent for installs. Time-box ~1 day; stop at any gate that fails.
2. Gate 0: install UE 5.8 + MetaHuman Creator Core Data; confirm current EULA text. Build a reference library from a neutral MH head.
3. Source: one Hunyuan head (stylised but near-human) from the toolchain.
4. Blender: install add-on + worker; wrap, accept front/profile/back; project skin; bake at 2K.
5. Push to Creator, request full rig, assemble in UE, Native Capture + web bundle, run portable_character.py (compact).
6. Unity: glTFast import, downsize textures to 2K, URP skin material, check morph names vs ARKit; render beside Tobin.
7. Go/no-go: does the head keep our style and animate acceptably? If not, drop it and spend the effort on a second Tobin-pipeline head instead.
