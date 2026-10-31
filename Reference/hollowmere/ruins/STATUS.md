State: DONE
Session: Wave 0B: foundation ruin kit generator
Progress: 100%
Merge commit: 6ae22a6 (main; check ran on a8ae44b, identical under Assets/Packages/ProjectSettings; branch claude/wonderful-mendel-zckrzc @ 81e4b5f)
SC RESULT check PASS (main a8ae44b: build PASS, test-edit 586/586, test-play 32 passed 0 failed 8 skipped; same on branch tip 81e4b5f)
Sheet: Reference/hollowmere/ruins/ruin_kit_sheet_final.jpg
Score: 31/40 (rounds 26, 28, 29.5, 30, 31; round 5 approved by the controller)
Tris per variant: S_A 8312, S_B 7480, S_C 7570, M_A 9078, M_B 9466, M_C 9324, L_A 9276, L_B 9904, L_C 9020 (all < 10k)
Export: Assets/_Project/Content/Art/Town/Ruins/<Name>/<Name>.fbx (+ B9 foliage atlas copy, SOURCES.md). Not wired into HollowmereBuilder (controller: later integration step after the terrain pass).
Contract: inside the S x S square, pivot at centre/ground, front -Y (Unity +Z), children Footing / Foliage / FOOTPRINT / SOCKET_Doorstep (threshold top, z 0.22); Footing slots FK_Sandstone, FK_ChimneyStone, FK_PlinthJoint, FK_Oak; linear 'wear' colour attribute (WearLit)
Note: unity.sh check rewrites HeroAnimator.controller(.meta) and BeautyCorner_Hollowmere.unity on every cloud run (pre-existing builder churn, left uncommitted)
Needs: none
Archive: ready
