# Blender craft library (reference, not skills)

Source: https://github.com/arjun988/blender-skills at commit 8f778d2405a214b508d4c7d80742be8e43acdd52 (2026-07-10), MIT licence (c) arjun988. Copied 2026-10-27 at the user's request, without the horror skills. Each `SKILL.md` was renamed to `<name>.md`, so these files are not loaded as skills. Read one only when a SleepingCalamity skill points you to it.

**House rules win.** These are generic production notes. When they conflict with `sc-asset-craft`, `sc-asset-budget`, `sc-asset-review`, `sc-texture-paint` or the spec, the SleepingCalamity rules win:
- Riverlight painterly look (R102).
- Generators are the source, never a hand-made .blend.
- Grey-centred detail maps.
- Unity, not Unreal.

Adaptations:
- `character-artist` is written for AAA realism. Use its topology and proportion notes. For shape, use the toon direction (memory: Toriyama-style heroes, rounded forms).
- `collision-proxy` uses Unreal UCX/UHX naming. In Unity, use primitive colliders or a convex `MeshCollider` made by the builders.
- Its "via MCP" steps assume its own blender-mcp setup. Use our Blender MCP or the generator scripts under `Art/Blender/`.

| Need | File |
|---|---|
| Export to Unity (scale, axes, rigs, LODs) | `unity-export/unity-export.md`, `unity-export/references/` |
| Character model and topology | `character-artist/`, `retopology/` |
| Rig, IK/FK, weights, face | `rigging/` |
| Prop modelling, kitbash, tiers | `prop-artist/` |
| Shape language (rounded, readable) | `cartoon-style/`, `stylized-style/` |
| Painted albedo, bakes, ORM packing | `hand-painted-style/`, `texture-workflow/` |
| LODs and colliders | `lod-pipeline/`, `collision-proxy/` |
| Splash, spray, particles | `vfx-fx/` |
| Look-dev loop, QA gate | `lookdev/`, `qa-review/` |
