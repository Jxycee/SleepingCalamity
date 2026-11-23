State: DONE. A/B delivered (AetherStrand v3.0.0 shipped preset vs HairDeck). Verdict: HairDeck wins. Waiting on the user's pick, and on whether to run a round-2 physics retune.

Branch claude/hair-aetherstrand-b297e1, cut from claude/hair-sweep-tool-abafe3, with origin/main merged in (dab358df). origin/main is still on Unity 6000.6.5f1 (6.8 has not landed there), so the branch stays on 6.5.

- dab358df Merged origin/main (clean).
- 2e9e436b Plugin v2.1.0 embedded as shipped, plus our asmdefs, texture importer, HairAB builder (two separate Tobin prefabs), A/B capture tool, PlayMode audit tests, Blender generator wrapper, interim FBX, neon-feedback draft. Gate PASS on dab358df+working tree (compile, EditMode 1867/1867, no shader errors).

- af0b7a61 Built both prefabs and the AetherStrand material. Build-twice is byte-identical (both prefabs, the .mat and their .meta files). The capture dry run found the plugin shader does NOT compile (`redefinition of 'ShiftTangent'`), so AetherStrand renders magenta. Sent to Neon via the coordinator.
- 6a4447f3 H override 0.23 m and margin 3 mm in our preset. The audit still fails: kink 150.5 deg (the rig's rest pose already bends 155 deg; 35/44 chains are above 28 deg at rest), stretch 52 %, tip sway 3.4 cm, penetration 0. HairDeck bones (wind 0): tip sway 0.30 cm idle, 0.93 cm after the head turn. Numbers are in neon-feedback.md.
- Forward+ torch check: HairDeck hair does receive the point light (hair value 23 off, 99.8 on). AetherStrand cannot be measured until its shader compiles.
- The audit PlayMode tests (2 of 4) fail on plugin metrics. So check will be red until v2.2 fixes them or the user decides what to do with those tests.

- 071d0a23 v3.0.0 replaces the package. Its own asmdefs and texture metas are used, and our texture importer and fake-null workaround are dropped. The shipped PresetGeometry is fitted per axis to Tobin's scalp (x 0.317, y 0.433, z 0.375; roots snapped a median 17 mm).
- fec908ba Prefabs and material rebuilt; build-twice identical. Shader compiles, and the torch reaches the hair (diff 117, against HairDeck's 76).
- v3 audit: colliders 6, penetrating 0, kink 68.8 deg, stretch 58.8 %, tip sway 1.53 cm (the 90-frame and head-turn-snap tests FAIL on kink/stretch). Bone measure (wind 0): AetherStrand tip sway 1.42 cm idle, 12.6 cm after the head turn (max 19.5), 8.7 cm after +0.9 s. HairDeck: 0.30 / 0.93 / 0.30 cm.
- Verdict: the current HairDeck hair wins. AetherStrand on Tobin reads as a dense helmet of flat ribbons, with the fringe over the eyes and a hoop of crown-flick locks. In motion it flies apart: tips 18-28 cm out on the head turn and run-stop, still 6 cm out at 1.2 s. Round 1 of 2 allowed. A round 2 would be a physics retune in our preset (stiffness/inertia) and a shorter fit: user call.

- Check on 88856370 FAILED. build PASS, EditMode 1867/1867, PlayMode 308 passed / 3 failed / 36 skipped. Failures: the 2 AetherStrand audit tests (plugin kink/stretch, as expected) and StairHandoverTests.Stairs_F1ToF3_ThenHome(Rtt300Loss5) (network timing, not this branch's code; possibly flaky). No PASS marker.
- Churn for the coordinator: the build rewrote 6 Hollowmere materials (M_GD_*, M_StoneCottage_B_SC_Rubble), CellarFloorSync.prefab and CellarFloorTest.unity. Not this branch's files; reverted. A first check attempt failed on Win32 IO 1224 (scene file in use).

## How the AetherStrand hair is made
- Blender: `Art/Blender/tools/experimental/tobin_hair/aetherstrand/tobin_aetherstrand.py`. It imports the installed HairDeck FBX and turns each card island into a lock spine (uv.v bins, so 554 cards give 554 locks). Lock settings follow Neon's neon_07 recipe: width ×0.85, thickness clamp(w×0.38, 6-18 mm), core/outer layerAO 0.72/0.94, seed from uv1.x. It writes the PresetGeometry.json, runs the vendored, unmodified generator (44 chains × 5 bones), moves the rig onto the Head bone, re-weights the root rings to Head, keeps the same scalp shell, and exports with STRIP, linear colours and rig_export axes. Zones: Rear 358, Bangs 85, Crown 76, Sides 35. 29.9k hair tris, against 24.2k for HairDeck.
- Neon's own extractor is copied for reference to `vendor/extract_hairdeck_to_preset_json.py`. It was not run: our bin sampling covers the same recipe without the round(v, 2) row grouping.
- Unity: `TobinHairABBuilder.Run` builds HairAB/Tobin_HairDeck.prefab and HairAB/Tobin_AetherStrand.prefab. Each is a separate instance, so no bones are shared. The builder asserts Hair_ roots = chain count, the head bone, and 6 colliders. `TobinHairABCapture.Run` shoots the A/B. `ab_sheets.py` builds the sheets.

## Next
- When v2.2 lands: re-vendor it, regenerate, build, capture the full set (stills and motion), make the sheets, send them to the user, then run check and build-twice.


Archive: ready
