<!-- Covers: Tripo-style part construction (deform, overlap, loft) for blockouts. Moved from SKILL.md section 5. -->

## 5. Tripo-style construction (style-independent)
A teardown of an AI-generated cottage showed why its shapes beat clean primitives: every part is deformed (bent, tapered, twisted, sagging, chamfered), parts are pushed into each other so the silhouette is a compound, faces buried inside another part are deleted, and the texture carries the fine detail. Use it for blockouts and kit silhouettes (chimneys, dormers, roof slabs, barrels, window frames):
1. **Deform every part**, one random draw per part, so no two are alike.
2. **Overlap into compounds** (sink a cap 3 cm into its shaft), then cull buried and flush faces. Occluders must be closed solids.
3. **Lofted rings** (a stack of offset, turned rings) are the cheapest way to match a measured AI profile; slice only where a deform needs vertices (slicing everywhere cost 2-4x tris).
FishGame's `fg_handmade.py` has helpers for this (`tbox`, `bend`, `twist`, `warp`, `loft`, `kink`, `compound`, `cull_buried`, `roof_slab`, `arch_ring`); they are not ported (0b), so write them fresh in the family generator.
