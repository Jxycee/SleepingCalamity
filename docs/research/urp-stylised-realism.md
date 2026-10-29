# URP stylised realism: research notes (2026-10-29)

Own-words notes from a first web pass. Unverified claims are marked (unverified). Nothing here is a settled decision; the Phase 1 style bible decides.

## 1. What is publicly known about the 2026 Ocarina of Time remake

Only early hands-on and showcase impressions exist (release is reported as 5 Nov 2026), so treat all of it as provisional.

- Nintendo frames it as a "faithful interpretation using modern technologies"; one outlet stresses it is still recognisably Ocarina, not a Breath of the Wild-style rethink.
- Press contrast it with BotW/TotK: those leaned cartoonish, this goes more grounded. Stone and rock get more realistic texture, foliage looks closer to real vegetation (KitGuru).
- One reviewer likened the tone to stop-motion and dark-fantasy film rather than the original's anime look; "stylised yet realistic", more modern than nostalgic.
- The one close look at Kokiri Forest is noticeably darker than the original; enemies read as menacing.
- Engine and AA guesses (in-house engine vs UE5-like atmosphere) are speculation. Not confirmed.
- Takeaway for us: the brief in CLAUDE.md ("saturated day colour, heavy atmosphere") partly conflicts with press describing a darker, grounded mood. Our target is a blend: real PBR materials and storybook proportions, with saturation we choose. Re-check once more footage is out.

Sources:
- [Creative Bloq, what the remake looks like on Switch 2](https://creativebloq.com/entertainment/gaming/this-is-what-nintendos-zelda-ocarina-of-time-remake-actually-looks-like-on-switch-2)
- [NPR hands-on (KUOW mirror)](https://www.kuow.org/npr-news/2026-10-14/hands-on-with-the-ocarina-of-time-remake-the-most-ornate-zelda-game-ever-made)
- [Retro Handhelds showcase discussion](https://retrohandhelds.gg/lets-talk-about-that-ocarina-of-time-switch-2-showcase/)
- KitGuru piece (via search summary, page not fetched): https://www.kitguru.net/?p=746214

## 2. Volumetric fog and light shafts in URP (Unity 6 / URP 17, Render Graph)

URP has no built-in froxel volumetric fog (HDRP does). Options:

| Option | Notes |
|---|---|
| CristianQiu Unity-URP-Volumetric-Light (MIT, free, UPM git URL) | Fetched README: Unity 2022.3 through 6000.6, explicit Render Graph and compatibility mode support, main/spot/point lights with shadows and cookies, realtime and mixed lights, APV support, URP Volume override, Forward/Deferred/Forward+/Deferred+. Limits: transparents do not blend with the fog, fully baked lights work only via APV, fog range is capped, some noise at angles, many additional lights are slow (README recommends Forward+/Deferred+). Roadmap: quarter-res, reprojection, density volumes. Console: reported working on Steam Deck, Xbox, PS5; Switch untested. |
| Volumetric Fog FX (paid, Notion docs) | Claims Unity 6.3+, URP 17.3+, single fullscreen pass, Render Graph native. Price/quality unverified. |
| Screen-space god rays assets (Asset Store, Corgi God Rays on Fab) | Radial-blur style shafts; listings do not confirm Render Graph (unverified). Cheap, but only a sun-in-frame effect. |
| Custom Renderer Feature (Kodeco-style light scattering) | Tutorials predate Render Graph; would need porting to RecordRenderGraph. Only if the free package fails us. |

Common practice: use a ready volumetric package for the main directional light plus a few spot shafts, and fake the rest with authored cards/cones (additive soft cones, dust motes particles) for dungeon windows. Cheap haze and aerial perspective come from the built-in exponential fog plus a colour-gradient sky, not volumetrics.

Sources:
- [CristianQiu/Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light)
- [OpenUPM listing](https://openupm.com/packages/com.cqf.urpvolumetricfog/)
- [Volumetric Fog FX docs](https://marmalade-denim-5a2.notion.site/Volumetric-Fog-FX-357c67e77ad18055b0bac47f04eb09ad)
- [Kodeco: light scattering as a URP Renderer Feature](https://www.kodeco.com/22027819-volumetric-light-scattering-as-a-custom-renderer-feature-in-urp)

## 3. SSAO

- URP SSAO is a Renderer Feature. Key knobs per the Unity 6.3 reference: Intensity (cheap), Radius (high cost; smaller is faster), Source (Depth Normals is more accurate; Depth rebuilds normals with 1/5/9 samples by Low/Medium/High), Direct Lighting Strength (cheap).
- Third-party starting values (unverified): Intensity ~2, Radius ~0.5, Direct Lighting Strength ~0.5, Medium samples.
- Practice for a PC quality target: Depth Normals source, Radius kept small (about 0.3 to 0.5 m for storybook-scale props), keep Direct Lighting Strength low so lit areas stay clean. Pair SSAO with baked AO/probe occlusion in materials so contact shadows stay stable.
- Check whether a Downsample option still exists in the 6000.6 inspector (older docs had it; Unity 6 docs snippets did not list it).

Sources:
- [Unity 6.3 SSAO reference](https://docs.unity3d.com/Manual/urp/ssao-renderer-feature-reference.html)
- [Unity 6.3 SSAO overview](https://docs.unity3d.com/Manual/urp/post-processing-ssao.html)

## 4. Bloom, tonemapping, grading

- Tonemapping per Unity docs: Neutral remaps range with little hue/saturation change and is the gentler start for heavy grading; ACES is more contrasty, shifts hue and saturation, and Unity does grading in ACES colour spaces when it is on. Docs give no explicit "pick this with a LUT" advice.
- Proven practice (inference): choose one tonemapper, then build the look on top. For a saturated, controlled storybook palette, start with Neutral plus Color Adjustments / Lift-Gamma-Gain / Split Toning, and bake the final grade to a 3D LUT only if we need a per-region look (town vs dungeon). Test ACES as an alternative for a punchier filmic roll-off; it will desaturate and shift brights, which fights a saturated palette.
- Bloom: low threshold-based, modest intensity, high scatter for the soft cinematic glow; keep lens dirt off. Use HDR rendering (HDR on in the URP Asset, Rec.2020-safe grading only if we plan HDR output).
- Unity 6 adds HDR output options for tonemapping (Neutral range reduction BT2390) if we want an HDR display mode later.

Sources:
- [Tonemapping override, 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/effect-list/tonemapping.md)
- [Tonemapping override, Unity 6.2](https://docs.unity3d.com/Manual/urp/post-processing-tonemapping.html)

## 5. Shadows (third-person, directional sun)

- Unity's guidance is generic: cascades raise near-camera fidelity without raising map resolution; Max Distance is a main cost lever; Soft Shadows help quality at lower resolution (cost matters mostly on tile-based GPUs, not our PC target).
- Common third-person setup (practice, tune by profiling): 4 cascades, Max Distance 60 to 120 m for towns (shorter indoors in dungeons), 2048 to 4096 main light resolution, soft shadows Medium/High, small depth bias with normal bias tuned on foliage. Add a separate small high-quality cascade focus on the player cluster for co-op if the camera spreads out.
- Foliage alpha-clip casts shadows through cards, which is expensive; use a simple proxy shadow caster for trees and grass shadow off.

Sources:
- [Unity shadows optimization](https://docs.unity3d.com/Manual/shadows-optimization.html)
- [URP performance settings, 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)

## 6. Global illumination: Adaptive Probe Volumes vs lightmaps

- APV (Unity 6): probes auto-placed from scene geometry density, baked indirect lighting, streaming for memory, bake-time much shorter than lightmaps, blending between bakes (day/night scenarios), good for dynamic objects and characters. Needs no lightmap UVs.
- Lightmaps: highest fidelity for fully static detail and sharp indirect shadows, but need UVs, long bakes, and large textures. The Unity 6 GI blog presents APV as the answer to those pain points.
- No source frames it as an official either/or. Practice (inference): APV as the default for both towns and dungeons (procedural dungeons make lightmaps impractical; they also need baked data per generated floor, which does not work). For hand-built towns with showpiece spots, an optional lightmap layer can be added later.
- Important for us: Hearthdeep floors are generated at runtime, so baked GI cannot cover them. Plan for realtime lighting there (Light probes placed per room prefab, APV baked per authored room set, or Enlighten-free fallback: ambient gradient plus a few realtime lights and SSAO).
- The CristianQiu fog package supports APV, which helps pairing.

Sources:
- [Unity 6.3 APV concept](https://docs.unity3d.com/Manual/urp/probevolumes-concept.html)
- [Unity blog: new ways of applying GI in Unity 6](https://unity.com/de/blog/engine-platform/new-ways-of-applying-global-illumination-in-unity-6)
- [Unity forum: GI changes with Unity 6 beta](https://discussions.unity.com/t/feedback-request-global-illumination-changes-with-the-unity-6-beta-2023-3-release/941601)

## 7. Foliage and grass at density

- GPU Resident Drawer (Unity 6) instances MeshRenderer GameObjects automatically through BatchRendererGroup. Requires Forward+, SRP Batcher, BatchRendererGroup Variants set to Keep All, strongest when many objects share a mesh. Good for trees, rocks, props, instanced buildings.
- Dense terrain grass is normally done with a dedicated system: Terrain details (built in, fine for moderate density), or a GPU-instanced grass renderer (Kiwi Grass, Flora, GPU Instancer Pro are the known Asset Store options; an 80.lv write-up shows an "infinite GPU-instanced grass field" in URP). None verified by us.
- Recommended pattern: indirect-drawn grass blades or clumps with distance LOD (blades near, cards far), wind in the vertex shader, player-push via a global vector array (up to 4 players). Keep alpha-to-coverage cutout, no sorting.
- Forward+ is also what the volumetric package recommends for many lights, so Forward+ fits the whole stack.

Sources:
- [GPU Resident Drawer, 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html)
- [80.lv infinite GPU-instanced grass in URP](https://80.lv/articles/infinite-gpu-instanced-grass-field-in-unity-urp)
- [Kiwi Grass](https://assetstore-fallback.unity.com/packages/tools/terrain/flora-161293) (listing is Flora; Kiwi page not fetched)

## 8. How shipped URP games get a similar look

Not researched in depth this pass (searches returned nothing usable). General known practice, to verify with a targeted pass later: strong sky/ambient colour, hand-painted or PBR-lite textures with baked AO, low-poly chunky silhouettes, one dominant key light with soft shadows, fog colour matched to the sky horizon, bloom plus colour grading doing most of the "mood", and particles (pollen, dust, fireflies) for life. Follow-up task: collect 3 to 5 named shipped URP titles with dev talks.

## 9. Recommended starting stack

1. URP 17.6, Forward+ rendering path, HDR on, Render Graph (default), GPU Resident Drawer on.
2. Sun: one directional light, 4 cascades, Max Distance about 100 m (shorter in dungeons), soft shadows High, 4096 map.
3. GI: Adaptive Probe Volumes for towns; per-room probe sets plus ambient gradient for generated dungeon floors. No lightmaps at first.
4. SSAO: Renderer Feature, Depth Normals source, small radius, low Direct Lighting Strength.
5. Fog: URP exponential fog for haze plus CristianQiu volumetric package for god rays on sun and key spots; authored cone cards where cheap.
6. Post: Neutral tonemapper, Color Adjustments / Lift-Gamma-Gain, low-threshold bloom, gentle vignette; compare ACES before locking.
7. Foliage: GPU Resident Drawer for trees/props, a dedicated GPU-instanced grass renderer with LOD and player-push.
8. Materials: real PBR (albedo, normal, metallic-smooth), baked AO, slight rim from fog and sky.
9. Build a single "look test" scene (town square plus a dungeon room) to compare each step in captures before adopting anything.

## 10. Risks

- Volumetric package is a single-maintainer community project: transparent blending, range cap, and Switch/PS4 untested. Have a fallback (cone cards, built-in fog).
- Generated dungeons cannot use baked GI; the dungeon may look flatter than towns unless we invest in per-room lighting.
- SSAO and volumetrics both cost real GPU time at 4 players with spread cameras; budget early.
- Many alpha-tested foliage shadows are a common hidden cost.
- The OoT remake reference is based on thin early press; the "heavy atmosphere, darker" reports may shift the target.
- Forward+ and GPU Resident Drawer constraints (shader support, Keep All variants increase build time and size).
