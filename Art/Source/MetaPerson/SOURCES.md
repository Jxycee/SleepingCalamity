# MetaPerson sources

Personal-use game; research notes: `docs/research/2026-11-08-character-pipeline.md` sections 0e-0k.

| File | Origin | Settings | Licence | Where |
|---|---|---|---|---|
| File 3, `avatar/model.glb` (152 MB) | MetaPerson Creator (Avatar SDK) export from the user's Tobin face image, 2026-11-08 | LOD1, GLB, PNG textures (head/body 2048), tee/shorts/sandals, visemes + head shape + body shape on (paid coins), `Male_Pose_A` | licence: personal-use (unconfirmed); written game-use answer pending from Avatar SDK | not committed; PC `C:\AI\test_out\heroes\tobin\metaperson_v1\file3\`, Drive https://drive.google.com/file/d/1yknfAKwu5kC2uBEcOQoNrAQRucLqrClA/view?usp=sharing |
| `Tobin_Base_v2_sliders.glb` (36 MB) | Built from file 3 by `Art/Blender/tools/experimental/sc_tobin_underlay_build.py` (sandals, tee, shorts removed; built underlay tagged `sc_role = underlay_hide_when_clothed`) | 73 joints, 149 head shapes, 16 body sliders on the underlay | same as file 3 | this folder (Git LFS) |
