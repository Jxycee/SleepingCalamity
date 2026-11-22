State: IN PROGRESS. Integration is done. Final hair generation and the A/B captures wait for the corrected AetherStrand export (v2.2), as the coordinator asked on 2026-11-22.

Branch claude/hair-aetherstrand-b297e1, cut from claude/hair-sweep-tool-abafe3, with origin/main merged in (dab358df). origin/main is still on Unity 6000.6.5f1 (6.8 has not landed there), so the branch stays on 6.5.

- dab358df Merged origin/main (clean).
- 2e9e436b Plugin v2.1.0 embedded as shipped, plus our asmdefs, texture importer, HairAB builder (two separate Tobin prefabs), A/B capture tool, PlayMode audit tests, Blender generator wrapper, interim FBX, neon-feedback draft. Gate PASS on dab358df+working tree (compile, EditMode 1867/1867, no shader errors).

- af0b7a61 Built both prefabs and the AetherStrand material. Build-twice is byte-identical (both prefabs, the .mat and their .meta files). The capture dry run found the plugin shader does NOT compile (`redefinition of 'ShiftTangent'`), so AetherStrand renders magenta. Sent to Neon via the coordinator.
- 6a4447f3 H override 0.23 m and margin 3 mm in our preset. The audit still fails: kink 150.5 deg (the rig's rest pose already bends 155 deg; 35/44 chains are above 28 deg at rest), stretch 52 %, tip sway 3.4 cm, penetration 0. HairDeck bones (wind 0): tip sway 0.30 cm idle, 0.93 cm after the head turn. Numbers are in neon-feedback.md.
- Forward+ torch check: HairDeck hair does receive the point light (hair value 23 off, 99.8 on). AetherStrand cannot be measured until its shader compiles.
- The audit PlayMode tests (2 of 4) fail on plugin metrics. So check will be red until v2.2 fixes them or the user decides what to do with those tests.

## How the AetherStrand hair is made
- Blender: `Art/Blender/tools/experimental/tobin_hair/aetherstrand/tobin_aetherstrand.py`. It imports the installed HairDeck FBX and turns each card island into a lock spine (uv.v bins, so 554 cards give 554 locks). Lock settings follow Neon's neon_07 recipe: width ×0.85, thickness clamp(w×0.38, 6-18 mm), core/outer layerAO 0.72/0.94, seed from uv1.x. It writes the PresetGeometry.json, runs the vendored, unmodified generator (44 chains × 5 bones), moves the rig onto the Head bone, re-weights the root rings to Head, keeps the same scalp shell, and exports with STRIP, linear colours and rig_export axes. Zones: Rear 358, Bangs 85, Crown 76, Sides 35. 29.9k hair tris, against 24.2k for HairDeck.
- Neon's own extractor is copied for reference to `vendor/extract_hairdeck_to_preset_json.py`. It was not run: our bin sampling covers the same recipe without the round(v, 2) row grouping.
- Unity: `TobinHairABBuilder.Run` builds HairAB/Tobin_HairDeck.prefab and HairAB/Tobin_AetherStrand.prefab. Each is a separate instance, so no bones are shared. The builder asserts Hair_ roots = chain count, the head bone, and 6 colliders. `TobinHairABCapture.Run` shoots the A/B. `ab_sheets.py` builds the sheets.

## Next
- When v2.2 lands: re-vendor it, regenerate, build, capture the full set (stills and motion), make the sheets, send them to the user, then run check and build-twice.

Archive: not ready
