# T24c risk review (2026-11-18, risk-reviewer, read-only, `git diff e2579733 041ec032`)

No blocking findings; T24b items 1-6 fixed.
- Loader teardown sound: the stale-unload path runs only when no slot exists, so it cannot unload a live loader's scene; NGO never tracks the art scenes (DefaultSceneManagerHandler.cs:238, NetworkManager.cs:1607); in-flight loads unload themselves in OnLoaded (:511), covered by the race test.
- `DisableValidationWarnings` (SceneFlow.cs:54) silences all five NGO call sites on host and client; NGO recreates the scene manager per start, and the filter is reinstalled on OnServerStarted/OnClientStarted. Only art-scene refusals remain.
- Manifest deterministic (sorted ordinal, LF, no BOM, `.gitattributes` pins LF).
- `Crosses` ancestor rule correct (Hollowmere.unity has a single root).

Should-fix (low confidence), folded into T23 item 0:
1. `HollowmereSplit.cs:124/139-145`: manifest paths are names only (20 duplicate paths today, e.g. `Hollowmere/Preset_Rebuilt/Road/Hedge_A`); swapping which same-named sibling moves would pass a scoped save. Add the sibling index per segment.

Nits:
2. `HollowmereSplitTests.cs:578` checks manifest paths exist in the art scene but not that they are absent from Hollowmere.unity (the "art twice" case).
3. `HollowmereSplit.cs:261` writes the manifest before the main scene save; a failed main save leaves them out of step.
4. `HollowmereSceneLoader.cs:466/482`: if `UnloadSceneAsync` returns null, the stale path still reloads (second copy). Only if Unity refuses the unload.
5. `HollowmereSceneLoaderTests.cs:700-725`: the late-join test would pass with no filter; real coverage is the live-delegate check at :650-655.
6. `SceneFlow.cs:40`: replacement warning drops NGO's "on the Host/Client" wording.
