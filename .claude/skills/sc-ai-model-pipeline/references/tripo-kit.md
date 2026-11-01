<!-- Covers: building shapes ourselves the Tripo way (lofted rings, deforms, compounds). Moved from SKILL.md section 5. -->

## 5. Building it ourselves, the Tripo way (kit, K1–K2)
When we generate instead of adopt, copy Tripo's construction, not its primitives:
1. **Measure the AI part, then rebuild it as lofted rings:** `loft(rings, sides)`, with `kink` for stepped silhouettes. Rebuilt this way, chimney, barrel and dormer came out within 1.3× Tripo's tris.
2. **Deform every part:** `bend`, `twist`, `warp`, `taper`, `sag`. Nothing perfectly straight.
3. **Overlap parts into compounds, then cull buried faces:** `compound`, `cull_buried` (closed solids only).
4. **Use `cut` slices only where a deform needs vertices.** Round 1 cost 2–4× tris without this rule.
5. Let the bake and the material carry the fine detail (stone courses, shingles, planks).

FishGame's `fg_handmade.py` helpers are not ported; see sc-asset-craft section 5.
