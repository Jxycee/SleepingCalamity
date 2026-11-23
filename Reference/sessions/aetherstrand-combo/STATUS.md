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
| Wind / physics parity | not started |
| Blender half / any-hairstyle path | not started |

Live Unity: UnityMCP connected (deny entry removed locally, uncommitted). Run harness via execute_code: set SC_PARITY_TAG/MATSET env then ParityRender.Run().
Harness: tools/ParityRender.cs (scratch project outside repo), tools/web_export.ts (any recipe -> PresetGeometry.json).
