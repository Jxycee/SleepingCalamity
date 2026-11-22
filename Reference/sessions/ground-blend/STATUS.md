State: DONE
Branch: claude/ground-blend-4d3fa9 (worktree .claude/worktrees/fable-coordinator-playbook-eec554), cut from 98728afa. Not pushed, not merged.

| Commit | What |
|---|---|
| 5ec092ec | Shader: Neon's ground blend merged into the RUINSKIN block (globals outside UnityPerMaterial, grain 0.22->0.08, smoothness clamp 0.28) |
| 61bc965b | SCGroundBaker (CPU, raw .bytes), SCGroundBlendBinder (Presentation), HollowmereGroundBake hooked into HollowmereBuilder, GroundBlendTests, LFS pattern |
| 2bbb9dc1 | SCPlacementGrounding.TryGroundAndSink on the OrchardGap breach rubble and Orchard foreground slabs |
| 3545c140 | SSAO radius 0.35 -> 0.45, samples Medium -> High on BeautyCorner_Renderer (via BeautyCornerBuilder/LookTestBuilder.EnsureRendering) |
| ad721abe | Build output: Hollowmere scenes with the binder, baked .bytes, SSAO asset; temp collider on SquarePaving so the paving slab is baked (9078 street texels) |

Neon's code problems: NEON-NOTES.md. Open: parapet caps / lifted block / "L" are FBX geometry (see notes).
| 457bf846 | R20 frames (Spawn, Orchard, OrchardGap, Meadow), 1:1 crops, diag.md |

## Per-camera before (R19) / after (R20)
| Camera | HF share whole frame | Blind A/B (after vs before, /50) | Winner |
|---|---|---|---|
| Spawn | .1488 / .1464 | 31.5 vs 31.0 | after (null pair) |
| Orchard | .1465 / .1445 | 35.5 vs 33.5 | after |
| OrchardGap | .0496 / .0485 | 33 vs 30 | after |
| Meadow | .1539 / .1482 | 32 vs 31 | after (marginal) |

Notes: effect on HF is small (OrchardGap near .2028 -> .1783 the largest). Reviewers did not find docs/art/scene-judging.md in the worktree for some runs and scored from general criteria; Meadow reviewer saw labelled crops. Shore stones not covered by these cameras: dry-look of the 0.28 clamp unchecked (needs LakeShore/Jetty). Parapet caps, lifted block, "L" need Blender wall-kit regen (not done).

Check on tip 1ee4a6e9 (SC MARKER = 1ee4a6e9): build PASS, EditMode 1869/1869, PlayMode 310 passed / 0 failed / 36 skipped (346). First run failed one test, CombatArenaTests.Solo_SixRatsOnOneHero_InsideARoom_StayInRoomAndApart (hover overlap 0.953 vs 0.98), passed on rerun with no code change: flaky, unrelated to this branch.

Verdict: renders near-identical (mean luma diff 2-6, HF share -1..4% relative); A/B margins small. Not a fix for the cohesion gap on its own. Grounding of rubble/slabs is the visible gain.

Archive: ready
