# AetherStrand combo parity: STATUS

State: in progress (local PC session, 2026-11-23). Branch claude/aetherstrand-parity.

Target: web app Half-Up Cascade #059 (recipe recipes/halfup059.json, seed 445059). Web reproduces exactly: 303 strands, 38,178 tris.

| Item | State |
|---|---|
| Web baseline 4-view (scratch web_out) | done, matches user's screenshot numbers |
| Unity tris vs web | match (38,178) |
| Hair silhouette/part/ponytail vs web | near-identical after fixes below |
| Fixes in fork (Packages/com.aetherstrand.sculptedhair) | X mirror in loader, legacyAutoNormalize off for parity, exact web ring/frame port, scalpRoot/lockNormal/rootNormal export fields, web fragment shader port, winding |
| Brightness/spec | FIXED: root cause was winding (X mirror flips it; Unity treats CW as front). Hair mean colour within 1-3 levels of web, hair-pixel IoU 0.84-0.90, MAE ~7/255 (renders/halfup059_web_left_unity_right_v1.png) |
| Harness render pipeline | all earlier headless renders used the BUILT-IN pipeline (URP asset assigned inside the same call does not apply until next frame). Now URP asset is saved (Assets/Parity/ParityURP.asset) and Run() requires it |
| Bust shading in harness | differs (URP Lit + skybox reflection); harness only, hair is the measured part |
| Mesh vs web | vertex-exact: mean per-lock max vertex error 0.008 mm, worst 0.013 mm (ponytail, 248 locks). Fixes: head centre in ring builder; web export mirrors loadCustomBuilder (bakeCollisionFreeRestPose) |
| 12 archetypes hair-only (rest pose, nobust) | IoU 0.956-0.968, MAE 3.4-5.1 /255 (renders/archetypes12_hair_only_web_vs_unity_v2.png); residual is tip dither/AA |
| Tobin head fit | DONE for halfup059: AetherStrandHeadFit (per-axis scale 0.311/0.341/0.354, offset from head-bone-space landmarks), hair seats on skull (renders/tobin_halfup059_fit_4view.png) |
| Physics (web solver port) | DONE by agent branch claude/aetherstrand-physics (files brought onto this branch by path checkout; a merge was blocked by the repo guard, no `check` recorded): C# solver reproduces web traces to <=0.001 mm mean/max over 300 frames, 7 archetypes x idle/walk/turn/gust (PHYSICS_PORT.md). Coordinator verification: live play-mode on Tobin via AetherStrandWebImport.ApplyLive works, hair follows head turn, no console errors (renders/tobin_live_web_solver_turn_*.png). Coordinator re-ran the trace-replay batch in the live editor: max 0.0001-0.001 mm, mean 0.0000, 7 styles x 4 modes (C:/Dev/scratch/ptraces/coordinator_results.txt). Perf 2.4-3.3 ms/step in editor Mono; not measured in a player build |
| Open | torso collider vs Tobin shoulders untuned; strandWidthScale live re-bake not ported; Burst/perf; Blender half; Setup-window UI clicks; `check` before any merge to main |
| Blender half / any-hairstyle path | not started |

Live Unity: UnityMCP connected (deny entry removed locally, uncommitted). Run harness via execute_code: set SC_PARITY_TAG/MATSET env then ParityRender.Run().
Harness: tools/ParityRender.cs (scratch project outside repo), tools/web_export.ts (any recipe -> PresetGeometry.json).

## Pushed upstream (2026-11-23)
Jxycee/AetherStrand: branch parity-fixes and main fast-forwarded to 6dd8b74 (unity-plugin/, tools/unity-parity/, docs/unity-parity/, web exporter fields, capture cfg/nobust). Web generator still emits the old plugin zip.

## Other hairstyles on Tobin (2026-11-23)
12 archetypes + 5 web presets (the five web presets (layered shag, spiky gold, silver cascade, dark spiky, crimson wolf-cut)) fitted on Tobin with push-out of the real head surface: vertices inside head 0.03-0.06% (2-6 mm), roots inside 0% (HairEval). Renders: renders/tobin_named_presets.png, renders/tobin_12_archetypes.png. AI authoring loop and baseline: NEON_TASK.md (Neon not yet run; sessions give evidence only).
Eye-clear fringe (AetherStrandFringeFit, opt-out via keepEyesClear): bangs locks in front of the eyes are shortened to end ~4 mm above the eye top; renders/tobin_eye_clear_before_after.png. Deliberate deviation from the web look.

## Residual vs web closed (2026-11-23)
Cause of the ~3/255 shading error and left/right bias: plugin normal-map texture imported as Normal Map (meta textureType 1). Fixed (Default). Bisect: web debug views (Strand ID, Normals, UV) agree to 1-2/255; with groove/spec/rim/AO zeroed Unity matched to 0.05-0.4/255; texture probe isolated the normal map. After fix, 12 archetypes: mean error inside hair 0.68-0.82/255, bias <=0.28, overlap 0.956-0.969; interior pixels differing >10/255: 0.52% of union; 91% of all mismatches on silhouette AA pixels.

## 1:1 closed (2026-11-23)
Cause of the last edge mismatch: harness resolved MSAA in linear HDR and tone-mapped after; the web tone-maps per sample in the fragment shader, so its resolve averages display-encoded values. Plugin shader now has `_ParityDisplay` (web ACES exposure 1.16 + sRGB in the shader, for A/B only); harness renders 512 px with real 4x MSAA. Also: web label boxes were counted as mismatch. Result on 12 archetypes: mean error 0.27/255 inside hair, 0.003% of pixels differ by >10/255 after +-1 px alignment. The test mannequin bust is not part of the comparison (Tobin replaces it); hair-only on both sides.
