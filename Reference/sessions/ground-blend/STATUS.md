State: IN PROGRESS
Branch: claude/ground-blend-4d3fa9 (worktree .claude/worktrees/fable-coordinator-playbook-eec554), cut from 98728afa. Not pushed, not merged.

| Commit | What |
|---|---|
| 5ec092ec | Shader: Neon's ground blend merged into the RUINSKIN block (globals outside UnityPerMaterial, grain 0.22->0.08, smoothness clamp 0.28) |
| 61bc965b | SCGroundBaker (CPU, raw .bytes), SCGroundBlendBinder (Presentation), HollowmereGroundBake hooked into HollowmereBuilder, GroundBlendTests, LFS pattern |
| 2bbb9dc1 | SCPlacementGrounding.TryGroundAndSink on the OrchardGap breach rubble and Orchard foreground slabs |
| 3545c140 | SSAO radius 0.35 -> 0.45, samples Medium -> High on BeautyCorner_Renderer (via BeautyCornerBuilder/LookTestBuilder.EnsureRendering) |
| (next) | Build output: Hollowmere scenes with the binder, baked .bytes, SSAO asset; temp collider on SquarePaving so the paving slab is baked (9078 street texels) |

Neon's code problems: NEON-NOTES.md. Open: parapet caps / lifted block / "L" are FBX geometry (see notes).
