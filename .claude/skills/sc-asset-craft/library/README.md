# Blender craft library (reference, not skills)

Source: https://github.com/arjun988/blender-skills at commit 8f778d2405a214b508d4c7d80742be8e43acdd52 (2026-07-10), MIT licence (c) arjun988. Copied 2026-10-27 at the user's request, without the horror skills. Each `SKILL.md` was renamed to `<name>.md`, so these files are not loaded as skills. Read one only when a SleepingCalamity skill points you to it.

**House rules win.** These are generic production notes. When they conflict with `sc-asset-craft`, `sc-asset-budget`, `sc-asset-review`, `sc-texture-paint` or the style bible (`docs/PHASE1.md` section 3), the Sleeping Calamity rules win:
- The look is stylised realism: real PBR bakes (albedo, normal, ORM) with chunky storybook shapes, not hand-painted albedo, NPR or cel shading.
- Generators are the source, never a hand-made .blend.
- Unity URP Lit, not Unreal.

Adaptations:
- `character-artist` is written for AAA realism. Use its topology, proportion and face notes, pushed to the proportions in PHASE1 3.6 (about 6.5 heads, hands and feet about 1.15x, eyes about 1.1x).
- `cartoon-style` and `stylized-style`: use only their shape-language and silhouette notes. Their NPR, cel-shading and limited-palette advice is not our look.
- `hand-painted-style`: not our look (brushwork albedo, subtle or no normals). Skip it; materials are smart PBR materials (`sc-texture-paint`).
- `collision-proxy` uses Unreal UCX/UHX naming. In Unity, use primitive colliders or a convex `MeshCollider` made by the builders.
- Its "via MCP" steps assume its own blender-mcp setup. Use our Blender MCP or the generator scripts under `Art/Blender/`.

| Need | File |
|---|---|
| Export to Unity (scale, axes, rigs, LODs) | `unity-export/unity-export.md`, `unity-export/references/` |
| Character model and topology | `character-artist/`, `retopology/` |
| Rig, IK/FK, weights, face | `rigging/` |
| Prop modelling, kitbash, tiers | `prop-artist/` |
| Shape language (silhouette, readable masses) | `cartoon-style/`, `stylized-style/` (shape notes only) |
| Bakes, ORM packing, bake troubleshooting | `texture-workflow/` |
| LODs and colliders | `lod-pipeline/`, `collision-proxy/` |
| Splash, spray, particles | `vfx-fx/` |
| Look-dev loop, QA gate | `lookdev/`, `qa-review/` |
