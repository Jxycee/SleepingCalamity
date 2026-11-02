<!-- Covers: ranked Tripo source settings and the Hunyuan3D licence note. Moved from SKILL.md section 0. -->

## 0. Generate (source settings, ranked)
Researched from Tripo's docs; not yet A/B-tested by us.
1. Multi-view input (front/back/left/right, same light and scale). This attacks the root cause: surfaces the generator can't see come out blurred (P2).
2. `texture_quality=detailed` (the HD texture option); 4K.
3. A/B test `texture_alignment=geometry` against `original_image`.
4. A sharp, evenly lit input with no baked shadows.
5. A smear in a bad spot: re-roll `texture_seed`, or Magic Brush the patch in Tripo Studio.

Hunyuan3D (local, needs an NVIDIA GPU) textures the same way (multi-view paint), so expect the same blur. The pipeline below applies unchanged. Check its licence first: as remembered (unverified), the Tencent Hunyuan Community License excludes some territories (EU, UK, South Korea) and large services; read the licence file in the download.
