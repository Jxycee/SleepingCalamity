# Shape + texture split for game creatures (RTX 5070 12 GB, Windows 11)

Date: 2026-11-06. Web research only; nothing was run. Evidence is thin: most numbers are README claims or vendor blogs, and I found no Reddit thread with a measured 12 GB run. Anything not read in a README or changelog is marked UNVERIFIED. Non-commercial use is acceptable.

## Ranked shape models (image to untextured mesh)

| # | Model | VRAM (claimed) | Windows / ComfyUI | Notes |
|---|---|---|---|---|
| 1 | TRELLIS.2, shape only (texture mode None), 512 or 1024 cascade | Official README: 24 GB minimum, tested only on Linux A100/H100. Community ComfyUI port adds FP8 models and a tiled decoder (changelog, no numbers). A low-VRAM workflow aiming at 8-10 GB is a secondhand claim, UNVERIFIED. | Yes. visualbruno/ComfyUI-Trellis2 ships Windows wheels (py3.11 torch 2.7/2.8; py3.13 torch 2.10 cu13.1). A GGUF fork exists (JanskNeh). Needs gated DINOv3 access. | Sparse-voxel, open-surface friendly, so thin horns and tails are plausible. Mesh sizes in StableGen's docs: about 4M faces at 512, 15M at 1024 cascade, so decimation is mandatory. MIT. |
| 2 | Hunyuan3D 2.1 shape (DiT) | 10 GB for shape (README); one creator reports 6 GB works (UNVERIFIED). | Yes, via visualbruno/ComfyUI-Hunyuan3d-2-1 (tested on Windows 11, py3.12) or kijai's wrapper for 2.0. | Fits 12 GB comfortably. A forum comment calls it "great for low detail chibi stuff", which suits storybook shapes. Meshes lean watertight, so very thin parts may fuse (UNVERIFIED). |
| 3 | Hunyuan3D 2mini / 2mv | About 5 GB for 2mini (UNVERIFIED, repo page not reached). | Same wrappers (UNVERIFIED for 2mv). | Fallback if 2.1 shape is tight on VRAM. 2mv takes several views, which helps back-of-creature shape. |
| 4 | TripoSG | 8 GB minimum (README), MIT. | No ComfyUI node found in the README. Script only. | Good for sharp features. Extra install work for little gain. |
| 5 | Direct3D-S2 | 10 GB at 512, about 24 GB at 1024; README says 512 is poor quality. | Not officially Windows. | Skip on 12 GB. |
| 6 | SAM 3D Objects | UNVERIFIED. | ComfyUI-SAM3DObjects nodes exist (mesh decode with vertex colours, texture bake). | Output starts as splats or vertex-colour meshes. No creature quality data found. Low priority. |

Not researched in enough depth to rank: Step1X-3D geometry, Hi3DGen, TripoSF (no VRAM or Windows data found).

## Ranked texture models (paint an existing mesh from the reference image)

| # | Tool | VRAM | PBR | Notes |
|---|---|---|---|---|
| 1 | StableGen (Blender add-on, GPL-3.0) with SDXL + ControlNet + IPAdapter, Marigold for PBR | About 8 GB for SDXL; 16 GB+ for Qwen Image. | Yes. Marigold estimates albedo, roughness, metallic and normal, baked to standard maps. | Projects views from cameras you place around the mesh, each view conditioned on earlier ones. Works on any mesh, including a TRELLIS.2 shape. Seams are not discussed in the docs, so check them. Blender 4.2-4.5 or 5.1+, not 5.0. 10-50 GB of models plus its own ComfyUI. Some optional models carry non-commercial terms (fine for us). |
| 2 | TRELLIS.2 "Mesh Texturing" nodes in ComfyUI-Trellis2 (single image, multi-view, 1536 res, experimental projection node) | Not stated. Likely the same tight budget as shape (UNVERIFIED). | Yes (base colour, roughness, metallic, alpha per official README). | Changelog confirms nodes that texture a mesh; I could not confirm they accept an arbitrary external mesh, so test that first. Best fidelity to the shape model's own latent, so a TRELLIS shape then TRELLIS texture is the most coherent pair. |
| 3 | Hunyuan3D-Paint 2.1 | Official 21 GB for texture alone. A third-party recipe for 12 GB (low_vram_mode, texture 1024, view size 512, 6 views) is UNVERIFIED. | Yes, PBR. | Strong quality, but the 12 GB fit is the risk. Needs custom_rasterizer and differentiable renderer wheels on Windows. |
| 3b | Hunyuan3D-Paint 2.0 via kijai wrapper | A third-party guide says at least 11.5 GB to texture an existing mesh (UNVERIFIED). | Wrapper has a PBR node (RunComfy listing). | Older, but proven in ComfyUI. Needs UVs (xatlas inside). |
| 4 | MV-Adapter texturing (Apache-2.0) | `--variant sd21` under 10 GB; SDXL higher (no figure given). | Not documented. Colour only as far as I could tell. | Takes a mesh plus a reference image. Via ComfyUI-3D-Pack as a three-stage workflow that can be split across runs. Older and lower fidelity than the options above. |
| 5 | Paint3D, TEXGen, Flux multiview plus projection | Not researched. | No. | Skipped. |

## Retopo and UV between stages

- Projection texturing (StableGen) and the TRELLIS.2 native bake both want a decimated mesh, not the raw 4-15M face output. StableGen offers QDC server remesh (default 100k faces), Quadriflow, and LATO.2 low-poly (200-5000 verts, NVIDIA only; skipped if you use native texture).
- Hunyuan Paint and MV-Adapter need clean UVs. Both use xatlas; kijai's notes say a patched xatlas helps on high-poly meshes.
- For the game: decimate to about 20-40k, unwrap in Blender (Smart UV or xatlas), then bake at 2048 or less. Projection textures bake into that UV set. Seam checks happen after the bake.

## Top 3 combos for 12 GB

1. **Hunyuan3D 2.1 shape + StableGen (SDXL + Marigold PBR).** Shape stage about 10 GB peak (claimed). Texture stage about 8 GB (claimed). The stages never overlap, so 12 GB holds. Install effort: medium. ComfyUI-Hunyuan3d-2-1 (shape nodes only, no paint wheels needed), StableGen add-on with its bundled ComfyUI. Safest on VRAM and the PBR path is explicit.
2. **TRELLIS.2 shape (GGUF/FP8, 512 or 1024 cascade) + StableGen texture.** Shape stage is the unknown: 8-10 GB if the low-VRAM claims hold, otherwise it fails at 12 GB (UNVERIFIED). Texture about 8 GB. Install effort: high (DINOv3 gating, wheels, version pins). StableGen's own TRELLIS.2 page documents exactly this flow. Better thin-part potential if it fits.
3. **TRELLIS.2 shape + TRELLIS.2 Mesh Texturing (same ComfyUI pack).** One install, coherent look, PBR native. VRAM per stage UNVERIFIED and the external-mesh input is unconfirmed. Treat as an experiment only.

Not recommended on 12 GB: Hunyuan3D-Paint 2.1 (21 GB official), Direct3D-S2 at 1024, Qwen Image texturing in StableGen (16 GB+).

## Open checks before committing

1. Does TRELLIS.2 shape at 1024 run in 12 GB with FP8 or GGUF on the 5070 (Blackwell needs the community "Blackwell fix")?
2. Do tails, legs and horns survive in Hunyuan 2.1 versus TRELLIS.2 on three of our creature references?
3. Is StableGen seam quality acceptable after baking to 2048?

## Sources

- https://github.com/visualbruno/ComfyUI-Trellis2 (changelog, wheels, texturing nodes)
- https://github.com/JanskNeh/ComfyUI-Trellis2-GGUF
- https://github.com/microsoft/TRELLIS.2 (24 GB, Linux only, PBR channels)
- https://github.com/sakalond/StableGen and its wiki page TRELLIS.2-mesh-generation (VRAM, PBR, retopo options)
- https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1 (10/21/29 GB figures)
- https://github.com/visualbruno/ComfyUI-Hunyuan3d-2-1 and https://github.com/kijai/ComfyUI-Hunyuan3DWrapper
- https://skills.rest/skill/hunyuan-3d-v21-devlitus (unverified 12 GB paint recipe)
- https://github.com/huanngzh/MV-Adapter
- https://github.com/VAST-AI-Research/TripoSG
- https://github.com/DreamTechAI/Direct3D-S2
- https://www.runcomfy.com/comfyui-nodes/ComfyUI-SAM3DObjects/sam3d-mesh-decode
- https://huggingface.co/Comfy-Org/TRELLIS.2/discussions/1 (single comment on chibi quality)
- https://docs.comfy.org/tutorials/3d/hunyuan3D-2 (12 GB full 2.0 pipeline claim)

Licence note (from memory, UNVERIFIED): Hunyuan3D weights carry a territory exclusion (EU, UK, South Korea). Check the LICENSE file before relying on them.
