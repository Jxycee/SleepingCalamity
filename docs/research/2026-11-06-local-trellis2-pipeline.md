# Local TRELLIS.2 + UniRig pipeline (RTX 5070 12 GB), 2026-11-06

Status: web search was thin. Items marked (UNVERIFIED) are inference or came from one source. Treat the measured numbers from our own runs (shape 334 s first run / 9.2 GB peak; textured dagger 199 s / 4.5 GB; textured cat OOM in `decode_latent` at 11.9 GB) as the truth and tune against them.

## 1. Speed and VRAM on 12 GB

What the node actually exposes (visualbruno changelog): texture resolution up to 1536 in Mesh Texturing, a Tiled decoder, sampler choice (Heun, RK4, RK5; RK4/RK5 are much slower, so cut steps if used), attention backends sdpa and flash_attn_3, FP8 weights, a cascade sparse-structure resolution up to 128 (experimental), a `bake_on_vertices` option, and mesh-cleanup nodes (simplify, remesh, quad remesh, weld, fill holes). No parameter table or 12 GB advice is published; the README is silent.

- Decode OOM: switch the decoder to the Tiled variant (exists, no published numbers; UNVERIFIED that it fixes `decode_latent` for the cat). Also: shape and texture as two separate runs, free models between stages (issue #168 reports VRAM not released between stages on Blackwell), restart ComfyUI after any OOM (fragmentation), and drop the texture stage to the lowest decode resolution that still reads. Issue #169 blames a too-large `chunked_apply` chunk (65536) at 2048; if the tiled decoder does not cure it, a one-line chunk-size edit in the node source is the fallback (UNVERIFIED).
- Resolution: 512 for seed sweeps, 1024 for the keeper, 1536 only for hero shape if it fits. Reference timings on an H100: 512 about 3 s, 1024 about 17 s, 1536 about 60 s (shape plus material), so our 5070 at 3-10x slower is consistent with 200-330 s at 1024.
- Torch: README tests torch 2.7 + cu128; one user reports torch 2.9.1+cu130 breaks TRELLIS.2 and 2.8.0+cu128 works; Blackwell (sm_120) wheels may lack kernels (issue #197). Do not upgrade torch casually.
- torch.compile, fp16/bf16 tricks: nothing found (UNVERIFIED). FP8 models exist in the node and are the one documented VRAM lever to try first.
- Realistic budget: shape-only 5 min cold, 2-3 min warm; textured 3-4 min for small props; large organic shapes may not fit textured.
- Batch/queue: ComfyUI keeps models resident when nodes are not set to offload, so queue many seeds in one session (Queue Batch Count with a seed increment). Cost trade-off: low_vram offloading saves memory but reloads each run. Do the sweep as shape-only at 512, pick the best 1-2, then run textured at 1024 on those only.

## 2. Input prep

- RGBA is mandatory in practice. The bundled rembg step hung for us. Use BiRefNet (ComfyUI core TRELLIS.2 nodes and the "Remove Background" node both use it; it handles fine edges such as fur) in a separate step, or run BiRefNet/BEN2 through any ComfyUI background-removal node and feed the node with remove_background off. Check the alpha by eye; blended edges become geometry.
- Framing: subject centred, whole object visible, a little padding (the node has a `padding` option), neutral background, soft even light, no cast shadow, object about 80 percent of frame. Image 1024 px on the long side is plenty (UNVERIFIED; the model conditions on a DINOv3 crop).
- View: a 3/4 front view at slightly raised camera shows more form than a pure front (common practice, UNVERIFIED for TRELLIS.2 specifically).
- Multi-view: the node pack lists multi-view nodes (Sparse, Shape, Tex Slat MultiView generators, ReconViaGen sparse generator, DINO-Lock). Upstream TRELLIS.2 has no multi-view. Worth a test with front plus back images for assets where the back matters (UNVERIFIED quality, more VRAM).
- Flat concept art tends to drift in colour in the texture stage (noted in our earlier research). Mitigation: first restyle the art into a clean, lit, 3D-looking render on a plain background with an image model, then feed that. Keeps shading cues out of the albedo only partly; expect de-lighting in Blender.

## 3. Output to game-ready

- TRELLIS.2 outputs base colour, roughness, metallic and opacity (alpha in the base-colour texture). No normal map, no AO. Normal comes from a bake high-to-low in Blender; ORM must be packed by us (R = AO baked, G = roughness, B = metallic). Upstream exports 4096 textures and a 1M-face target; the node pack lets us pick lower. GLB exports opaque by default, wire alpha by hand if needed.
- Black speckle: not found in issue #162 (that issue is a different error); issue #195 reports noisy texture on an RTX 3070 that was independent of weights and torch version. Plan on a clean-up pass: median filter or inpaint pixels below a luminance threshold, then the bleed/halo repair in `sc-ai-model-pipeline` section 2.
- Geometry budgets (`sc-asset-budget`): T3 instanced piece 50-400 tris, T2 kit piece 0.5-5k, T4 hero prop 30-60k tris with up to 2 x 2K sets. TRELLIS output is dense (hundreds of thousands); bring it down in two steps: Blender Decimate (collapse) to roughly 3x the target, then QuadriFlow or Instant Meshes for an even low-poly for props that need clean silhouettes. Decimate per part, not one ratio (budget skill, "Fixing an over-budget model").
- Re-bake: keep the dense mesh as the high, UV the low (xatlas or Blender Smart UV), bake albedo, normal, AO, roughness, metallic at 2048 into the low. Cap 2048 px.
- Scale and pivot: output is unitless around the unit cube, Y-up vs Z-up depends on the exporter. Scale to the real size in Blender, origin at the base centre, apply transforms before FBX (STRIP texture paths, per project rules).

## 4. Rigging

- UniRig (MIT, 8 GB VRAM claimed): skeleton plus skinning, humans, animals, objects; our install works with an sdpa shim.
- SkinTokens (successor, MIT): README says 14 GB minimum; the paper claims large skinning-accuracy gains over UniRig, Puppeteer and MagicArticulate. One pipeline reportedly ran it on a 12 GB card (UNVERIFIED). Output GLB, delete the `glTF_not_exported` node in Blender.
- Puppeteer (Apache-2.0): a February 2026 paper reports incomplete skeletons (missing hand rigs) and blocky skinning on some assets (single qualitative source). Second choice.
- Mixamo auto-rig / AccuRIG: reliable for T-pose bipeds with a clean mesh; best path for stylised humanoids that will use Kevin Iglesias humanoid clips (both give a Humanoid-mappable skeleton). Quadrupeds: UniRig or SkinTokens; no free auto-rig gives a Humanoid avatar for them, so animate with a Generic rig and a few hand-made clips.
- Per category, no head-to-head evidence found. Recommendation: humanoids via Mixamo or AccuRIG, or UniRig output re-mapped; animals via UniRig/SkinTokens; always check skin weights at shoulders and hips.
- Unity: import FBX, set Rig to Humanoid, Configure Avatar and fix any mismatched bones (arms and fingers most often). Kevin Iglesias clips then retarget by Humanoid avatar. A UniRig skeleton needs bone renaming to Mixamo names first to auto-map (UNVERIFIED; mapping can be done by hand).

## 5. Where it pays

| Tier | Verdict |
|---|---|
| Hero (named NPCs, heroes) | No. Silhouette and face are under art direction; keep MPFB2/Tripo plus hand texture. |
| Enemies / creatures | Yes, for organic concept-to-mesh (cat, raccoon, bats): best saving, plus UniRig. Needs full retopo and re-bake. |
| NPC crowd, townsfolk variants | Maybe, only if Mixamo rig works; quality of faces uncertain. |
| Props (barrels, lanterns, gear) | Mixed: our procedural generators win for tidy hard-surface kit; TRELLIS for odd organic props (statues, fruit, sculpted junk). |
| Clutter, T3 instances | Yes as shape source: 5 min unattended, bake to a 50-400 tris proxy with a shared atlas. |
| Textures | Weak; we repaint anyway. |

## Recommended settings

- Candidate sweep: 512, shape only, 6-10 seeds, default steps, sdpa, low_vram on, FP8 if available. About 1-2 min each (estimate).
- Keeper: 1024 shape, then separate texture run, tiled decoder on, texture size 1024-1536 (not 2048), one asset at a time, restart ComfyUI after any OOM.
- Torch 2.7/2.8 + cu128 only; BiRefNet RGBA input prepared outside the node.
- Do not run other GPU work during runs; the cat needed 11.9 GB.

## Proposed pipeline

1. Concept: restyle to a clean 3D-lit render on a plain background (image model). 3 min.
2. BiRefNet cutout, centre, pad, 1024 px; script `Tools/ai3d/prep_rgba.py` (to write). 2 min.
3. ComfyUI API workflow JSON, shape sweep of N seeds (script `Tools/ai3d/sweep.py`, to write: posts the workflow, increments seed, saves previews). 10-15 min unattended.
4. Pick one; run shape plus texture at 1024 (4-6 min).
5. Blender script `Art/Blender/tools/sc_trellis_intake.py` (to write): scale, pivot, decimate to budget, xatlas UV, bake albedo/normal/AO, pack ORM at 2048, speckle clean. 5 min.
6. Rig if needed: Mixamo/AccuRIG for humanoids, UniRig for others. 10-15 min including weight check.
7. Unity import via existing URP Lit step; review with `sc-asset-review`.

Expected: 30-45 min per finished static asset (mostly unattended), 50-70 min with a rig; interactive attention about 15 minutes.

## Sources

- https://github.com/visualbruno/ComfyUI-Trellis2 (changelog, nodes, install)
- https://github.com/visualbruno/ComfyUI-Trellis2/issues (#162, #168, #169, #193, #195, #197)
- https://github.com/microsoft/TRELLIS.2 (timings, PBR outputs, export defaults)
- https://github.com/VAST-AI-Research/SkinTokens (14 GB, GLB output)
- https://github.com/VAST-AI-Research/UniRig, https://arxiv.org/html/2504.12451
- https://arxiv.org/html/2602.04805 (SkinTokens), https://arxiv.org/html/2602.12740 (Puppeteer critique)
- https://docs.comfy.org/tutorials/utility/remove-background-birefnet
- https://www.instasd.com/post/trellis-2-pixal3d-native-comfyui-guide (via search snippet only; page returned 403)
- Internal: docs/research/2026-11-06-open-rigged-assets.md, .claude/skills/sc-asset-budget, sc-ai-model-pipeline
