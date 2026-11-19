# Goat and cat (T24, 2026-11-19)

Generated locally with the project's AI pipeline (no outside asset): concept image (user's Tripo Studio session, 2026-11-06, `C:\AI\test_out\props\concepts\animal_goat.png`, `animal_cat.png`) -> Hunyuan3D 2.1 shape (40k tris) -> TRELLIS.2 texture -> cleanup/bake (2048 base colour, normal, ORM) via `C:\AI\scripts\ai_prop.py`; UniRig skeleton + skinning; procedural clips (`C:\AI\gr\scripts\gr_clips.py`: Idle, Walk, Run, Eat, Attack_or_Peck, Hit); exported with `C:\AI\gr\scripts\gr_export.py` (FBX, path_mode STRIP). Made 2026-11-07.

Licence: Hunyuan3D community licence (non-commercial, territory limits) for the shape; TRELLIS.2 MIT; UniRig MIT. Tag: restricted licence (Hunyuan); swap if the game is ever sold.

| File | Source | Tris | Textures |
|---|---|---|---|
| Goat/animal_goat.fbx | local AI pipeline (above) | 40,000 | 3 x 2048 (BaseColor, Normal, ORM) |
| Cat/animal_cat.fbx | local AI pipeline (above) | 39,998 | 3 x 2048 |

## Crow (bird_crow.fbx, added by T24)
Photo of a standing carrion crow, Wikimedia Commons "Standing Carrion Crow, Hyde Park, London.jpg" (CC BY-SA 2.0, per the file's licence field read through the Commons API; credit the photographer named on the file page), background removed (GrabCut), run through the same pipeline (`ai_prop.py`, 14,000 tris, 3 x 2048 maps), base colour regraded by hand (saturation x0.3, value x0.5; the raw bake was electric blue). Static mesh (no rig); the model is exported nose-down, Z up: `HollowmereAnimals.CrowFix` turns it. Licence tag: CC BY-SA 2.0 photo plus Hunyuan community licence: restricted, swap if the game is ever sold.
The cat's base colour was desaturated by hand (x0.28) for a grey tabby (the concept was brown).
