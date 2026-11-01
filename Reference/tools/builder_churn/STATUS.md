# Builder churn (graphics vs -nographics bytes)

- Session: builder_churn
- Job: root-cause and fix builders writing different bytes in graphics vs -nographics runs (material pass flags such as MotionVectors / _SrcBlend, scene bytes); guard with a test; merge after the lock lifts; update session-usage.md capture rule.
- Progress: fix landed on this branch (19f49b9, merged 95916f0). (A) MaterialValidation.SettleAll runs URP's ShaderGUI.ValidateMaterial over Content/Materials in BuildAll (works under -nographics), so both modes write the validated state; wrong SetShaderPassEnabled("MotionVectors", true) calls on URP Lit removed; mote _SrcBlend 5; LookTest decals _Blend 0. 36 .mat regenerated (MotionVectors pass off = what a graphics editor already wrote). (B) SceneFileNormalizer numbered scene-embedded meshes/materials by Unity's per-run old-id order; it now numbers them by first reference from the hierarchy. Guards: MaterialValidationTests (every committed .mat already validated, >50 checked), SceneFileNormalizerTests. Verified: sc_churn_check PASS (two -nographics builds clean), graphics build clean, DungeonCorner graphics capture clean, test-edit 858/858. Main merged at de7070b (build PASS, 0 materials to settle).
- Next: waiting for the merge order (procgen item 4, Polish 3b, Road 2b, then this branch). Then merge origin/main, regenerate with the builder, one check, merge, update session-usage.md.
- Status: IN PROGRESS
- Blocker: none
- Archive: no
- Branch: claude/beautiful-thompson-pvb6cv
- SC RESULT: -
- Merge commit: -
