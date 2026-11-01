# Builder churn (graphics vs -nographics bytes)

- Session: builder_churn
- Job: root-cause and fix builders writing different bytes in graphics vs -nographics runs (material pass flags such as MotionVectors / _SrcBlend, scene bytes); guard with a test; merge after the lock lifts; update session-usage.md capture rule.
- Progress: root causes found on origin/main b4db85f. (A) Graphics only: URP's material validation (BaseShaderGUI.ValidateMaterial / UpdateMotionVectorKeywordsAndPass, Particles blend setup) runs in a graphics editor and rewrites builder-written materials (MotionVectors pass disabled on M_HM_TG_*, M_CV_*, M_DC_Flame/Wax; M_DC_Mote _SrcBlend 1->5); a capture run then saves them. -nographics never validates, so the builders' values differ. BuildAll itself is identical in both modes. (B) Not graphics at all: two -nographics builds differ from each other in Hollowmere.unity (~160 embedded RD_Cards/Terrain meshes swap fileIDs, rotation/curve last-digit float noise) and CellarFloorTest.unity (marker materials swap _BaseColor). Fix in progress: central URP validation of every builder-saved material (both modes write the validated state) + make the scene builders deterministic; EditMode guard test.
- Next (06:05 UTC): one Sonnet implementer on local branch churn-fix: central URP ValidateMaterial pass for builder-saved materials, deterministic Hollowmere road meshes and CellarFloorTest markers, EditMode guard (all .mat already validated), then two -nographics builds clean + one graphics build + DungeonCorner capture clean + test-edit. Then my diff review, merge into this branch, wait for Polish 3b, merge main, check, merge.
- Note for the coordinator: platform noise is not the main cause. Two -nographics builds on the same cloud machine already differ (fixed fileIDs, swapped mesh contents, last-digit floats), so the fix makes the builders deterministic at the source; HeroAnimator.controller no longer churns on current main.
- Status: IN PROGRESS
- Blocker: none
- Archive: no
- Branch: claude/beautiful-thompson-pvb6cv
- SC RESULT: -
- Merge commit: -
