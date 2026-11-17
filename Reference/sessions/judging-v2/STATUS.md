# judging-v2 STATUS

Branch: claude/judging-v2-643655. Model: Opus 5.5. Started 2026-11-17.

State: done, merged into main (docs, skills, Tools only; no check needed). User calibration folded into the card (lines 4 and 10).

Done:
- World scorecard (10 lines, /50), method, proof run and Run 7 summary: `docs/art/scene-judging.md`.
- `Tools/art/sc_scene_diag.py` (band luminance, haze proxy, FFT tiling peak, empty cells, spectrum split; Pillow + numpy).
- `sc-scene-look` judging section and approval gate; `sc-asset-review` pointer to the world card.
- Anchors A01-A12 and sheets in gitignored `Reference/private/judging-v2/`; Abbey doc images in `Reference/private/abbey/docs/`.
- Proof: two blind runs, mean |d| 2.0/50, Spearman 0.70 (one outlier frame); anchors median 39.6 vs ours 35.0; Meadow R1 -> R4 and Epic's three toggles picked correctly; null pairs fail without the diff gate and pass once the reviewer is told nulls exist.

Archive: ready
