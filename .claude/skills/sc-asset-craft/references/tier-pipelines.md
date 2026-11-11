<!-- Covers: the per-tier bake pipeline (T4 hero / T3 high), T2 kit pieces, T3 instanced pieces, buildings, batching. Moved from SKILL.md section 1. -->

## 1. The pipeline per tier
**T4 hero, and the high of any T3 family** (sc-aaa-prop holds the round-by-round lessons):
```
part primitives (lathe / box / tube, real metres)
 -> high: voxel remesh (lists of parts are UNIONED), fillets only at the concave seams, authored damage (spalls, chips, dents, checks)
 -> smart material in part space (CC0 scan + procedural layers, `sc_aaa_mats2.py` style), edge and cavity masks
 -> low: clean source mesh for turned / hard-surface parts, collapse-decimated high for organic / chipped parts
 -> UV + pack -> one Cycles selected-to-active bake: BaseColor, Normal, Roughness, Metallic, AO, Emission
 -> `sc_pack_unity.py` for URP Lit -> review renders (sc-asset-review)
```
- **Dense damaged high, baked to a low.** A bevelled mid-poly baked in place reads clean CG; the jump came from a damaged voxel high.
- **Voxel size** for building members and large parts: about `min(w, h) / 12`, with 4-6 mm working on the cottage. Finer cost 11M tris and a 6.3 GB peak.
- **Low strategy per part.** Collapse-decimate organic and chipped parts; keep the clean source low (Catmull-resampled lathe profiles) on turned and hard-surface parts, or they get facet bands.
- **Union, then fillet only the seams.** Separate intersecting parts read as glued-on; smoothing the whole mesh melts every rim.
- **Join all highs into one object before a selected-to-active bake.** Cycles re-syncs the scene once per selected high: about 1 h down to 4.5 min on the cottage.
- **Albedo carries no baked light.** AO ships as its own map.
- **Glass and liquids:** fake refraction and reflection (refract or reflect the view ray into a procedural room, Schlick Fresnel) instead of real transmission. Liquids dark and saturated; bright ones read neon.
- **Metals need something to reflect:** an empty dark room renders metallic black. Add an environment and a probe in the review scene, and a reflection probe in Unity.

**T2 kit pieces:** low-poly pieces with UVs in metres at the PHASE1 3.3 texel density, sharing the area's tileables and trim sheet (one fix to a tileable reaches every piece). Wear and grime come from vertex-colour or mask blends between tileables and from decals, not a unique bake.

**T3 instanced pieces:** a few sculpted variants per family (the cottage used 16 slates), baked into one 2K atlas, instanced with jitter (bond, tilt, scale). Cull faces buried in the wall or ground at assembly. The cottage's slate roof is the model.

**Buildings** split like the cottage: plaster walls unique-UV and baked from a smart plaster with storytelling masks, repeated members (tiles, stones) as T3 kits, timber frame baked unique, hero bakes only for the door and lantern. Budget: PHASE1 3.3 (a whole building at most 75k tris, 6x2K + 2x1K; nothing bakes above 2048 px).

**Batch, don't build one by one** (PHASE1 9): one generator per family, all its items run headless in one Blender session, one joined Cycles bake per batch, one contact sheet per batch.
