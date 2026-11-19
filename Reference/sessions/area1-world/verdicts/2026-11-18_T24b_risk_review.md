# T24b risk review (2026-11-18, risk-reviewer, read-only, `git diff 0cb0debc dbe996f5`)

No blocking findings. The reviewer checked the runtime netcode paths against the NGO 2.13 source and they hold:
- ClientSynchronizationMode stays Single.
- Late join runs the host's VerifySceneBeforeLoading (NetworkSceneManager.cs:2061), so the art scenes are excluded.
- No baked lighting or occlusion.
- The art scenes contain no NetworkObject and no collider.

Should-fix items, for T24c:
1. **Teardown while the art scenes are still loading** (`Presentation/Town/HollowmereSceneLoader.cs:310-322`): there is no OnDestroy and nothing is unloaded.
   - Race: the session goes Offline while the five additive loads are queued, and SceneFlow (`:115-124`) loads the Lobby as a Single load.
   - Any art scene that activates after that load stays in the Lobby. On the next town load, the `already.IsValid()` check at `:316` can skip it, so the town gets no art.
   - Fix: keep the load ops; unload every art scene the loader started, on destroy or when a load completes after destroy.
   - Test: tear down the frame after the town activates, then assert no art scene is in the Lobby.
2. **Filter wiring and duplicate detection are untested.** The tests only check the static `AllowsNgoSceneLoad`. "None duplicated" relies on `GetSceneByName`, which returns only the first match.
   - Fix: after the solo start, assert the live host delegate refuses `Hollowmere_Town`.
   - Assert each art name occurs exactly once across `SceneManager.GetSceneAt` (or that `sceneCount == 6`).
   - In a one-process join, `LogAssert.Expect` the five host warnings.
3. **The traversal tools miss all moved art.**
   - `Editor/Traversal/TraversalSpaces.cs:33` reads only the active scene's root. `TraversalProbe.AddVisuals` (`:142`, `:272`) and the stair `Candidate` (`:677`) depend on it.
   - `TraversalScan.RunAll` (`TraversalScan.cs:49`), `TraversalPointProbe`, `ProbeObject` and `HollowmereFaultScan.cs:136` (Census) are affected.
   - `RegressionRun` (Quick mode) is unaffected.
   - Fix: gather visuals from `HollowmereSplit.AllRoots()`.
4. **No consistency check on the scoped save** (`HollowmereSplit.cs:226-237`). If a builder change alters which subtrees move, `SC_SCOPED_SCENES=Town` writes a Town that disagrees with the main scene already on disk: art goes missing or appears twice.
   - Fix: write a manifest of moved paths per scene, and refuse a scoped save when it differs.
5. **Latent:** `Crosses` (`:162`) ignores references into ancestors (`Preset_*`, `Set`). Nothing references them today.
   - Fix: count edges into ancestors.
6. **Nits:**
   - The `HollowmereSplitTests.cs:343` header claims HollowmereHubTests check the art; they don't.
   - Each late join logs five host warnings "failed validation on the Host".
   - 52 point lights, including the AmbientFlicker brazier, moved into art scenes despite the "lighting stays in main" comments (`SceneNames.cs:47`, `HollowmereSplit.cs:3`). They pop in late. Keep lights in main, or fix the comments.
   - `IsBlocker` checks NetworkBehaviour but not a bare NetworkObject.
   - The `_HollowmereBuild*.unity` temp scenes are not gitignored.

Not verified:
- A real two-peer late join (USER_TODO, 3a step 4).
- Byte-determinism and meta GUID stability after the temp-path to final-path save.

Tests that must pass after T24c:
- HollowmereSplitTests, HollowmereSceneLoaderTests, TraversalRegressionTests.
- Validators: BuildScenes, SceneNetworkObjectHashes, MissingScripts.
- FrontDoorFlowTests, the LoopBotTests pair, TownSyncNetworkTests.
