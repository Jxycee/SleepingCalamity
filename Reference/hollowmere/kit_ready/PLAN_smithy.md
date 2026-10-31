# Smithy plan (Q2, paused 2026-10-31 before any code)

Generator to write: `Art/Blender/town/sc_smithy.py` (root `Smithy`, class M), export `Assets/_Project/Content/Art/Town/Ring2/Smithy/Smithy.fbx`.
Brief: the shared Q2 brief (bakery template `sc_bakery.py`, contract 5.2, path_mode STRIP, deterministic seeds), budget < 35k tris.

- Reuse from `sc_bakery` (import as BK): timber_wall, window, doorway, merge, face_xf, mem, tube, tile_courses, ridge_tiles, plinth_run; own EXTRA, write_wear, to_object, build.
- Workshop body x -3.5..0.5, y -2.2..3.2; plate z 3.4, ridge ~5.7; HIPPED roof, ridge along Y (distinct from bakery and facade).
- Wide plank double door x ~ -2.7..-0.9, small window beside it, loft hatch with hoist under the front hip.
- Forge bay on +X (x 0.5..3.5), open to the -Y front; the main +X slope breaks into a shallower catslide over the bay (flashed joint, eave ~ x 3.9); rear and +X side board-over-stone half walls.
- Hearth against the workshop's +X wall (y ~ -0.3..1.5), soot-lined chimney breast in the Frame, tall square stone/brick stack through the roof to ~ z 8.
- Anvil stump ~ (2.3, -0.6). Yard: quench trough front right, tool rack front left, scrap and charcoal heap under the back eave. Footprint ~ 7.3 x 5.5 m.
- Parts: Part_Forge (fire bed with BK_Ember, bellows, hood), Part_Anvil (anvil on stump, hammer), Part_Sign (sign on bracket); each pivoted at its SOCKET_*.
- Slots: reuse FK_* / BK_* (BK_Ember for the fire); extra: a water slot for the trough, soot/brick tints.
- Refs: Openverse fetch failed (timeouts, HTTP 500) at pause; `Reference/hollowmere/kit_ready/smithy/` not created yet.
