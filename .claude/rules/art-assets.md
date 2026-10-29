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

- No paid assets. Free assets are allowed once the source is checked as safe and trustworthy (user, 2026-10-14): official Blender extensions, Poly Haven CC0, Unity's own free packages, free Unity Asset Store items the user adds to their account, and trusted free asset APIs and sites for models, textures, HDRIs and sound. Check the licence allows use in a commercial game and record source + licence for each asset.
- Art direction (user, 2026-10-29): Dark Cloud's whimsical shapes and town and dungeon themes, rendered in the stylised realism of the 2026 Ocarina of Time remake: real PBR materials at high detail, chunky readable storybook silhouettes, saturated clean daytime colour, rich dark scenes, heavy atmosphere (volumetric light, haze, particles), dense dressing. Every asset and map is made fresh in this one style; nothing from FishGame's art is reused. Measurable rules come from the style bible in `docs/PHASE1.md` once written; until then, handoff section 2.
- The asset skills (`sc-asset-*`, `sc-texture-paint`, `sc-foliage`, `sc-scene-look`, `sc-water-look`, `sc-ai-model-pipeline`, `sc-reference`) were rewritten for this style on 2026-10-30. Some FishGame tools they mention are legacy; `sc-asset-craft` section 0b has the tool-status table.
- Copyrighted style frames (press screens, Dark Cloud captures) go only in gitignored `Reference/private/`: study only, never shipped, traced or projected. Free-licence references go in `Reference/<area>/<subject>/` with a `REFS.md` (source, licence, purpose).
- Blender generators live in `Art/Blender/`; exports go through them so models are reproducible from text. `.blend`, FBX and image files are Git LFS.
