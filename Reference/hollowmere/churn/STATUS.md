State: DONE
Session: Q5. Builder churn fix
Progress: 100%
Sheet: none (code only)
Blocker: none
Archive: ready

Fixed on claude/practical-tesla-lqajeo (74ebff1, 0467581, 9d863fb):
- HeroAnimator.controller rewrite: its skip-signature used AssetDatabase.GetAssetDependencyHash, which differs between machines (editor build, platform, artifact cache), so every PC/cloud switch rebuilt it with new fileIDs. It now uses SHA1 of each clip's FBX + .meta bytes (.meta eol is pinned to lf, so the hash is the same on Windows).
- M_CrackWeeds.mat flip: four call sites built it from whichever identical B9 atlas loaded first; BeautyCornerArt.CrackWeedsMaterial() now always uses Weed_A.
- BeautyCorner_Hollowmere.unity +5 lines: the committed scene was stale (new CobbleSpawner fields), re-saved once; stable between builds.
- Not reproduced in the cloud: M_Pollen _SrcBlend and LookTest M_crack/M_drip/M_moss (already set deterministically in BeautyCornerBuilder.cs:133 / LookTestBuilder.cs:208), _ProbeTmp (all capture entry points already clean it in a finally). They may still show on the PC: run the new check.
- Guard: Tools/cloud/sc_churn_check.sh (two builds, fails on any git status change) printed SC CHURN PASS. New EditMode tests BuilderChurnTests.CrackWeedsMaterial_BuiltTwice_ChangesNoBytes_AndUsesTheFixedWeedASource and HeroAnimatorBuilderTests.ProjectController_MatchesItsRecordedSignature_SoABuildLeavesTheCommittedFileAlone. test-edit 602/602.
- My diff review: no findings.

SC RESULT check PASS on 941c787 (build PASS, test-edit 602/602, test-play 32 passed / 8 skipped / 0 failed).
Merge: see the 'Merge claude/practical-tesla-lqajeo: Q5' commit on main (merged tree identical to the checked tip in Assets/Packages/ProjectSettings, so the main re-check was skipped). Follow-up: M_CrackWeeds.mat committed once with the fixed builder's Weed_A textures (main's copy predated the fix; this file was on disk during that check).
PC follow-up: run bash Tools/cloud/sc_churn_check.sh (or two builds) on the PC once; if M_Pollen or the LookTest materials still flip there, it lists them.
