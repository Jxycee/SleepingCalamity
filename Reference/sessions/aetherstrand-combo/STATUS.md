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
| Physics (web solver port) | DONE by agent branch claude/aetherstrand-physics (files brought onto this branch by path checkout; a merge was blocked by the repo guard, no `check` recorded): C# solver reproduces web traces to <=0.001 mm mean/max over 300 frames, 7 archetypes x idle/walk/turn/gust (PHYSICS_PORT.md). Coordinator verification: live play-mode on Tobin via AetherStrandWebImport.ApplyLive works, hair follows head turn, no console errors (renders/tobin_live_web_solver_turn_*.png). NOT yet verified by me: numeric trace replay in my editor, perf in a player build |
| Open | torso collider vs Tobin shoulders untuned; strandWidthScale live re-bake not ported; Burst/perf; Blender half; Setup-window UI clicks; `check` before any merge to main |
| Blender half / any-hairstyle path | not started |

Live Unity: UnityMCP connected (deny entry removed locally, uncommitted). Run harness via execute_code: set SC_PARITY_TAG/MATSET env then ParityRender.Run().
Harness: tools/ParityRender.cs (scratch project outside repo), tools/web_export.ts (any recipe -> PresetGeometry.json).
