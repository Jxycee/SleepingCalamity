# T24c brief: T24b risk-review fixes (Hollowmere additive split)

Goal: close the five should-fix items and the nits from the T24b risk review, so the additive art scenes can never be lost, duplicated or left behind in the Lobby, and the editor traversal tools see all art again. No art change.

Branch: `claude/area1-world` in this worktree (`C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`). Work and commit here (no extra worktree: the controller is idle while you run). Run Unity only through `Tools\unity.ps1` of this worktree, `-OutDir` in your scratchpad.

Read first: `Reference/sessions/area1-world/verdicts/2026-11-18_T24b_risk_review.md` (the findings with line numbers), then `sc-netcode` skill section on scene loads.

## Files and symbols
- `Assets/_Project/Presentation/Town/HollowmereSceneLoader.cs` (:310-322 load path, no OnDestroy), `SceneFlow` (:115-124, Lobby Single load; `AllowsNgoSceneLoad`), `SceneNames.cs:47`.
- `Assets/_Project/Editor/Builders/HollowmereSplit.cs` (:3 header comment, :162 `Crosses`, :226-237 scoped save, `IsBlocker`, `AllRoots()`).
- `Assets/_Project/Editor/Traversal/TraversalSpaces.cs:33`, `TraversalProbe.AddVisuals` (:142, :272), stair `Candidate` (:677), `TraversalScan.RunAll` (`TraversalScan.cs:49`), `TraversalPointProbe`, `ProbeObject`, `HollowmereFaultScan.cs:136` (Census).
- Tests: `Tests/EditMode/HollowmereSplitTests.cs` (:343 header), `Tests/PlayMode/HollowmereSceneLoaderTests.cs`.
- `.gitignore`.

## Do
1. **Loader teardown.** Keep the `AsyncOperation`s the loader starts. On `OnDestroy` (and when a load completes after destroy), unload every art scene this loader started (`SceneManager.UnloadSceneAsync` on the loaded handle; for one still loading, unload in its `completed` callback). The `already.IsValid()` skip must not skip a stale copy left in another scene context: if the scene is loaded but not by a live loader, treat it as stale (unload, then load) or make the skip check only scenes this loader instance owns. State the authority line (none: local per peer) in the class comment as now.
   Test (PlayMode, `HollowmereSceneLoaderTests`): tear the town down the frame after it activates (go to Lobby), then assert no `Hollowmere_*` art scene is loaded once the Lobby is up; then load the town again and assert each art scene is loaded exactly once.
2. **Live filter + duplicates.** After the solo start, assert the live host `VerifySceneBeforeLoading` delegate refuses `Hollowmere_Town` (call the delegate NGO holds, not only the static). Assert each art name occurs exactly once across `SceneManager.GetSceneAt(0..sceneCount-1)` (GetSceneByName returns only the first match). In any one-process join test that triggers it, `LogAssert.Expect` the five host "failed validation on the Host" warnings, or better: silence them at source if NGO allows a quiet refusal; say which.
3. **Traversal tools.** Make `TraversalSpaces` and every caller in the list above gather visuals from `HollowmereSplit.AllRoots()` (main + art scenes, opening them via `HollowmereSplit.OpenAll` when needed) instead of only the active scene. `RegressionRun` Quick mode must stay unchanged (TraversalRegressionTests baseline must not move).
4. **Scoped-save manifest.** When the full split runs, write a manifest of moved subtree paths per art scene (a small text/JSON asset next to the scenes, committed, deterministic order). A scoped save (`SC_SCOPED_SCENES=...`) recomputes the moved paths and refuses (clear error, non-zero exit) when they differ from the manifest for any scene; message says to run the full build.
5. **Ancestor edges.** `Crosses` must count references into ancestors (`Preset_*`, `Set`, etc.) as crossing edges.
6. **Nits.** Fix the `HollowmereSplitTests.cs:343` header claim. Lights: **decision (controller): keep the 52 point lights where they are** (in the art scenes, with their art) and fix the "lighting stays in main" comments (`SceneNames.cs:47`, `HollowmereSplit.cs:3`) to say what is true (directional light, volumes, probes stay in main; local point lights travel with their art). `IsBlocker` also treats a bare `NetworkObject` as a blocker. Gitignore `_HollowmereBuild*.unity` (and its `.meta`).

## Out of scope
Any art, placement, capture or material change; the two-peer late join walk (stays a user item); Core.

## Check
- One BuildAll (the manifest is new output); then a second BuildAll only if you changed ordering in the split: outputs identical. Revert known churn before committing (STATUS "Known churn" list).
- Commit by path. Then `gate` with `SC MARKER` = HEAD.
- `test-play` with `-Filter "HollowmereSceneLoader|FrontDoorFlow|LoopBot|TownSyncNetwork|HollowmereHub|LoopGuideHud|SquareLoopWalkQa"` and EditMode `HollowmereSplitTests`, `TraversalRegressionTests`, validators `BuildScenes`, `SceneNetworkObjectHashes`, `MissingScripts` (the gate covers EditMode; name them in the report).
- No captures (no art moves).
- Push. Delete probe scripts.

## Report (under 15 lines)
Commits; gate line + marker; PlayMode line (passed/failed/ignored); per item 1-6 what changed in one line; how item 2's warnings were handled; anything unverified.
