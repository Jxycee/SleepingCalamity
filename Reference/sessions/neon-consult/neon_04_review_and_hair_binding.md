# Ground Bake Review Notes & AetherStrand Rig Binding
(Neon, exchange 4, 2026-11-22. Summarised by the coordinator; the original is in the Gemini thread.)

## 1. Review notes
- **Asset format:** next revision writes raw `.bytes` with `File.WriteAllBytes` (height 512 KB, albedo 1 MB). The binder loads them with `LoadRawTextureData` + `Apply`. LFS-friendly, -nographics-safe, deterministic. R16 is about 0.3 mm precision over 20 m, so no EXR.
- **BoxCollider streets:** the tint fallback is fine for a first pass, since height and normal blending do about 80 % of the work. v2: when the collider is not a MeshCollider, use a planar world UV (`hit.point.xz` x `_BaseMap` tiling).
- **Wet band v2:** global `_SC_WaterHeight`. Inside `[waterY, waterY + 0.4 m]` smoothness ramps to 0.65-0.78 and albedo darkens to about 0.65x, noise-modulated (`SCNoise(pW.xz * 3)`). Above the band the 0.28 clamp stays. No new cbuffer fields.

## 2. Tobin hair: drive the existing hair_<k>_<i> bones directly
- No re-rig or re-skin of the HairDeck mesh.
- Init: snapshot each bone's rest local pose relative to Head; the rest aim comes from `child.position - parent.position`, so any local axes work.
- LateUpdate (after Animator/IK): particle 0 pinned to the Head; integrate gravity, head inertia, wind and drag; solve inextensible distance constraints, per-zone rest-pose stiffness tethers and head/neck/shoulder capsule collisions.
- Write-back is rotations only (FromToRotation from the rest direction to the solved direction, applied to the rest rotation). localPosition is never written, so the mesh can't stretch or drift.
- Neon needs to know: (a) does each chain end in an unskinned tip bone, or must a virtual tip particle be added; (b) does `<k>` encode a zone (bangs/sides/crown/rear) or is it a flat 0..41 index? If flat, send each chain's root local position on Head.
