# AetherStrand combo parity: STATUS

State: in progress (local PC session, 2026-11-23). Branch claude/aetherstrand-parity.

Target: web app Half-Up Cascade #059 (recipe recipes/halfup059.json, seed 445059). Web reproduces exactly: 303 strands, 38,178 tris.

| Item | State |
|---|---|
| Web baseline 4-view (scratch web_out) | done, matches user's screenshot numbers |
| Unity tris vs web | match (38,178) |
| Hair silhouette/part/ponytail vs web | near-identical after fixes below |
| Fixes in fork (Packages/com.aetherstrand.sculptedhair) | X mirror in loader, legacyAutoNormalize off for parity, exact web ring/frame port, scalpRoot/lockNormal/rootNormal export fields, web fragment shader port, winding |
| Brightness/spec on lit side | still ~25% dim, broad sheen missing (open) |
| Bust shading in harness | dark (harness only) |
| Wind / physics parity | not started |
| Blender half / any-hairstyle path | not started |

Harness: tools/ParityRender.cs (scratch project outside repo), tools/web_export.ts (any recipe -> PresetGeometry.json).
