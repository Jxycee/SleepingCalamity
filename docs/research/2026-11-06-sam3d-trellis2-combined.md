# SAM 3D Objects + TRELLIS.2 combined, part-based generation, 12 GB limits

Date 2026-11-06. RTX 5070 12 GB. Own words. "Unverified" means I found no source or test; "inference" means my reasoning from sources.

## 1. How people combine SAM 3D Objects and TRELLIS / TRELLIS.2

- **No published recipe found.** Searches of GitHub, ComfyUI sites, Reddit snapshots and blogs turned up no workflow that chains SAM 3D Objects into TRELLIS or TRELLIS.2. Treat any such chain as something we build ourselves.
- **Latent formats.** The SAM 3D paper says it keeps TRELLIS v1's two-stage design: a 1.2B geometry model predicts a 64^3 coarse voxel shape plus pose, then a 600M sparse latent flow transformer adds detail and texture on the active voxels. Its two decoders (mesh, Gaussians) share one structured-latent encoder "as in TRELLIS" ([paper](https://arxiv.org/html/2511.16624v2)). So SAM 3D's `slat.pt` is a TRELLIS v1-style SLAT, though the paper mentions its own VAE improvements, so even v1 decoders may not accept it (unverified).
- **TRELLIS.2 is a different latent.** From its design (O-Voxel sparse structure, a new compression VAE, shape and texture latents decoded to PBR), not a v1 SLAT. Inference: SAM 3D slat/sparse_structure cannot be fed into TRELLIS.2 decoders. Mixing must happen at mesh level.
- **Mesh-level bridge that does exist.** visualbruno's pack has `Trellis2MeshTexturing` (our `nodes.py` line ~2818), multi-view texturing, and "Voxel to Mesh" nodes ([README](https://github.com/visualbruno/ComfyUI-Trellis2)). Pipeline: SAM 3D `mesh.glb` as external mesh, voxelise it, run the TRELLIS.2 texture flow conditioned on the image. Works in principle; no user quality report found. ComfyUI core also gained native TRELLIS.2 nodes (shape, texture, upsample stages) on 2026-08-22 ([ComfyUI blog](https://blog.comfy.org/p/trellis2-and-pixal3d-are-now-native)).
- **SAM 3 masks to parts then per-part TRELLIS:** no documented workflow. SAM 3D itself takes a mask per object and places several objects in one scene (it predicts layout), which is the scene-level route ([repo](https://github.com/facebookresearch/sam-3d-objects)). For props in a scene that is useful; for one creature it is not.

## 2. Part-based generation

| Tool | Input | Notes | VRAM |
|---|---|---|---|
| PartCrafter | one image | several part meshes at once, ~1024 tokens per part, shape only | README min ~8 GB; fewer parts or tokens lowers it ([repo mirror](https://awesome.ecosyste.ms/projects/github.com%2Fwgsxm%2FPartCrafter)) |
| PartPacker | one image | dual-volume packing, cheapest, weakest on parts in one benchmark | ~10 GB fp16 ([README mirror](https://gitee.com/hysmile_admin/PartPacker)) |
| OmniPart | image plus 2D part masks | voxel layout with per-part boxes | not stated |
| HoloPart | existing mesh segments | completes partial parts; slow; weak on part relations | not stated |
| Hunyuan3D-Part (P3-SAM + X-Part) | existing mesh | 3D segmentation then part completion; light X-Part release | not stated ([HF](https://huggingface.co/tencent/Hunyuan3D-Part/blob/main/README.md)) |

- A benchmark table (one paper) puts PartPacker, HoloPart, OmniPart in a similar band and PartCrafter lowest on part metrics ([PartMat](https://arxiv.org/pdf/2608.01825)). Single source.
- **Joining.** I found no real user write-ups. Reasonable practice (unsourced): for game characters keep parts as separate objects (weapon swap, per-part rig weights, per-part texel density); if one mesh is needed, voxel-remesh or boolean-union, then rebake albedo/normal/ORM from the parts onto a fresh UV set (we already have Blender bake tooling).

## 3. Does combining fix the memory limit?

- **Mostly no.** TRELLIS.2 normalises each input to its own unit cube. A head cropped to its own image fills the whole 1024 grid, so its token count is roughly the whole object's count times (part area / whole area) times (scale-up)^2. For four equal parts this is about the same as the whole (inference, not measured). Splitting buys detail per part, not lower peak memory. It lowers memory only if each part is run at a lower resolution.
- **Where the bottleneck sits.** Our cat: 20,919 tokens at 1024 fails in `decode_shape_slat` (sparse-conv neighbour cache). At cascade 768, 541 s/11.76 GB, close to the ceiling, with texture decode and export still to come in the same process. Part generation gets more, shorter runs and adds a join/rebake step.
- **Proven decoder fixes.**
  - Espalier-2 tiles the last two shape/texture decoder stages, chunks the mesh-extraction neighbour tensor, and streams weights stage by stage. At 1536 on a 16 GB 5070 Ti: shape decoder 8.8 GB, texture decoder 9.5 GB, export 7.5 GB (PyTorch peaks), whole-process 11.9-13.0 GiB ([card](https://huggingface.co/overyonder/espalier-2), [write-up](https://over-yonder.tech/games/espalier/)). It is standalone, not ComfyUI. Tiling is off by default and changes about 13% of shared voxels with an 8-voxel margin (2.5% with 16). Its 49,152-token cap is raised with `--max-tokens`.
  - visualbruno's pack already has `use_tiled_decoder` (default True in our nodes.py), FP8 models and `low_vram` since early 2026 ([README](https://github.com/visualbruno/ComfyUI-Trellis2)). Our cat OOM happened with these defaults, so open question: does its tiling cover the failing sparse-conv stage? Check tile size and margin.
  - IgorAherne's stableprojectorz fork claims 1024^3 on 8 GB by moving large tensors to RAM; details undocumented ([repo](https://github.com/IgorAherne/TRELLIS.2-stableprojectorz)).
  - No TRELLIS.2 GGUF found. Generic notes say GGUF is not faster than FP8 anyway.
- **SAM 3D is the memory winner**: ~72 s, 6.5 GB peak, no 1024 sparse-conv decode.

## 4. Quality (user reports)

- No head-to-head SAM 3D vs TRELLIS.2 creature tests found; sources are vendor blogs. Secondhand consensus: TRELLIS family highest visual fidelity, Tripo/Rodin best shape accuracy, thin parts and eyes are weak for every tool ([Scenario](https://help.scenario.com/en/articles/comparing-generative-3d-models/), [3daistudio](https://www.3daistudio.com/blog/pixal3d-vs-trellis-2-vs-hunyuan-3d-comparison)). One Reddit comment says TRELLIS.2 gives the best single-image mesh but often botches eyes.
- Our own runs are the best evidence: look at `sam3d_run_3` vs `dog_b768` side by side before trusting any ranking.

## Proposed project pipeline

All times and VRAM are measured where marked M, otherwise estimates E.

**Prop (barrel, dagger, chest)**
1. Reference image, cutout. 2. TRELLIS.2 default cascade 1024 via ComfyUI (M: 150-610 s, 4.5-10 GB). 3. Blender: decimate to budget, UV check, ORM repack, export FBX (`STRIP` paths). SAM 3D is a fallback (~72 s, 6.5 GB) when TRELLIS.2 OOMs. Script: batch driver already exists (`cat_try.sh`); add auto-retry at 768/640.

**Creature (cat, dog, enemy)**
1. Cutout. 2. Shape+texture: TRELLIS.2 at cascade 768 (M: 312-541 s, 6.9-11.8 GB). 3. Candidate faster path to test: SAM 3D shape (72 s, 6.5 GB) then TRELLIS.2 Trellis2MeshTexturing on it (E: 60-150 s, 5-9 GB). 4. Blender retopo to enemy budget, rig. 5. Reserve tiling-tuned 1024 as an upgrade for hero creatures. Scripts: `sam3d_to_trellis_tex.py` (ComfyUI API driver), `sc_creature_import.py` (retopo, bake, export).

**Humanoid NPC**
Do not generate whole humans. 1. MPFB2 base body (already approved in sc-asset-adopt) for proportions and rig. 2. Generate head/hair, clothes, props, weapons as separate parts: TRELLIS.2 at 1024 each (E: 150-400 s, 5-9 GB each), these are small and low-token. 3. Fit in Blender, bake into the shared character atlas. Script: `sc_part_fit.py` (scale/snap parts to body anchors, rebake). PartCrafter/Hunyuan3D-Part only as a way to split an already generated humanoid, not as the main path.

## Experiments to run next (sequentially; GPU is shared)

1. **SAM 3D mesh to TRELLIS.2 texturing.** Use `sam3d_run_3/mesh.glb` (cat) with `Trellis2MeshTexturing`, texture-stage resolution 512 then 1024. Record time, peak VRAM, and compare against the 541 s cat and `mesh_textured.glb`. Pass if peak under 10 GB and texture beats SAM 3D's own bake.
2. **Tile tuning on the cat at cascade 1024.** Look at the tiled-decoder code path for tile size/margin, shrink tiles (espalier uses 128 voxels, 8-16 margin) and rerun with `SC_CHUNK` ignored. Pass if `decode_shape_slat` completes under 11.5 GB; else port the neighbour-chunking idea from espalier's repo.
3. **Split test on the dog.** Crop head and body images, run each through TRELLIS.2 at 768 and 1024, record tokens and VRAM per part, join in Blender with remesh and rebake. This confirms or refutes the "splitting does not cut tokens" estimate and shows whether the seam is acceptable.
