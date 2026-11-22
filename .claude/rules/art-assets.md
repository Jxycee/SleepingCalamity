---
paths:
  - "Art/**"
  - "Reference/**"
  - "Assets/_Project/Content/**"
  - "Assets/_Project/Shaders/**"
  - "Assets/_Project/Presentation/**"
  - "Assets/_Project/Editor/**"
---
# Art and assets

- No paid assets. Free assets are allowed once the source is checked as safe and trustworthy (user, 2026-10-14): official Blender extensions, Poly Haven CC0, Unity's own free packages, free Unity Asset Store items the user adds to their account, and trusted free asset APIs and sites for models, textures, HDRIs and sound. The game is for the user and friends, not for sale (user, 2026-11-06), so non-commercial, research-only and community licences (e.g. Tencent Hunyuan) are fine. Record source + licence for each asset, and tag restricted-licence ones so they can be swapped if that ever changes.
- Art direction (user, 2026-10-29): Dark Cloud's whimsical shapes and town and dungeon themes, rendered in the stylised realism of the 2026 Ocarina of Time remake: real PBR materials at high detail, chunky readable storybook silhouettes, clean moody daytime colour (OoT-like, slightly muted), warm gold firelit dark scenes with no teal in the Cellars, heavy atmosphere (volumetric light, haze, particles), dense dressing. Every asset and map is made fresh in this one style; nothing from FishGame's art is reused. Measurable rules: the style bible in `docs/PHASE1.md` section 3.
- The asset skills (`sc-asset-*`, `sc-texture-paint`, `sc-foliage`, `sc-scene-look`, `sc-water-look`, `sc-ai-model-pipeline`, `sc-reference`) follow this style. Some FishGame tools they mention are legacy; `sc-asset-craft` section 0b has the tool-status table.
- Copyrighted style frames (press screens, Dark Cloud captures) go only in gitignored `Reference/private/`: study only, never shipped, traced or projected. Free-licence references go in `Reference/<area>/<subject>/` with a `REFS.md` (source, licence, purpose).
- Blender generators live in `Art/Blender/`; exports go through them so models are reproducible from text. `.blend`, FBX and image files are Git LFS.
- Export FBX with `path_mode='STRIP'` (absolute texture paths break the Unity import elsewhere). Session usage rules: `session-usage.md`.
