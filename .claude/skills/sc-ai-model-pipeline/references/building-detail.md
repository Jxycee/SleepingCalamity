<!-- Covers: plane-space detail pipeline for buildings still short of the cottage bar (the mill). Moved from SKILL.md section 2b. -->

When steps 1–2 leave a building short of the bar (big & visible soft about 0.03, every class about 0.03–0.05, clean by eye), run the plane-space detail pipeline. Paths are relative to `Art/Blender/tools/experimental/`:
1. `python3 tripo_repair/run.py <obj> <basecolor> <rwork> --until charts`
2. `python3 tripo_detail/run.py tripo_detail/configs/<model>.json <rwork> <basecolor> <dwork> --start <step-1/2 atlas> --wood-swatch <retex swatch_timber.png>`
3. `sc_tripo_finalize.py` (above).

What it does (config-driven; `configs/mill.json` is the worked example, and its README gives the stage order):
- per-class detail from the model's own sharpest clean texels (stone, wall, tin from clean stripes only, wood);
- re-lays low-density parts into free atlas space;
- restores the original texels wherever a pass hurt them (glass panes as their own smooth class, ivy, eave bands);
- fixes sliver UVs;
- for wheels and other round parts, a polar frame plus a straight frame per spoke, value-matched to the original paint, with contrast capped (mill: cap 0.13, gain 1.3).

Results on the mill: big & visible soft 0.185 → 0.048, every visible class under 0.05 except the smooth oak spokes, which look right as they are. Reproduces bit-identically (17 min). A new building needs its own families check: the values were tuned on one building.
