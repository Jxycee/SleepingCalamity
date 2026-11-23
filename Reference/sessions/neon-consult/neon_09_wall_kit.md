# Wall kit rewrite (Neon, exchange 9, 2026-11-22)
Neon read `claude/area1-world` directly: the code opens, but the LFS images arrive as 131-byte pointers, so it can't see the frames. The replacement `sc_town_wall.py` is in Neon's Canvas (download pending). Neon reports it passes py_compile and an AST determinism check (no time, no global random, CRC32-seeded R_()).

## Root causes Neon found in the current generator
- **Lifted parapet block (OrchardGap):** on Wall_Breach, coping slabs checked `min(tp(a), tp(xm), tp(b)) >= PAR_TOP` and skipped the slab straddling |x| = 1.2, while merlons only checked `tp(xm)`. That left the merlons at x = ±1.5 hovering 12 cm over missing coping.
- **Stray "L" and the clean diagonal cut:** a hard cliff jump at |x| = 1.2 with 0.25 m core_loft steps, so slabs, planks, bearers, string courses and corbels overhang empty air.
- **Faceted cubes:** heap() put 220 rocks up to 0.92 m (blocks 0.25, 12-tri cubes) in the breach, lifted in z.

## Fixes in the replacement
- Ruined pieces (Straight_A/B_Ruined, Breach, Crumbled): no merlon teeth or caps; fused, stepped and eroded top courses (`_eroded_parapet_courses`), blocks seated 8 cm into the core, chamfered and displaced. Intact and Rebuilt pieces: an `is_coped()` check, merlons seated 4 cm into the coping, caps 3 cm into the merlons (EastGate battlements the same).
- Breach/Crumbled profiles: asymmetric 0.26 m stepped courses, core_loft sampling 0.16 m, masonry vmax clamped to the minimum across each stone, planks/bearers/string/corbels pulled back from drops.
- heap(): a low sunken talus skirt (0.08-0.22 m, no blocks, sunk -0.15 s). Hero rubble is left to Megascans. Leftover block rocks use chamfered FK.add_box; the floating walkway cubes on Crumbled are removed.
- SC_WEAR: linear FLOAT_COLOR CORNER attribute `wear`. "None" = (0,0,0,1); sums clamped to <= 2.50 so they never trip the white = no-wear rule. R grime: foot band z < 1.35, joints and footing, streaks. G moss: tops (n.z > 0.45, boosted above 2.2 m), damp low joints. B polish: edges and quoins at 0.65-1.55 m, walkway, stair treads.
- No Unity change claimed: names, pivots, 4 m lengths, TW_* slots, AddColliders footprints and export flags unchanged.
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_town_wall.py -- --out Assets/_Project/Content/Art/Town/Wall --export --no-render`
- Verify cameras: OrchardGap, Orchard (breach and ruined tops); Meadow, JettyBack, RingSouth, RingWest, RoadBack (curtain tops); LowerLane, Lake, Mill (gatehouses).

## Coordinator caveats
- "None" = (0,0,0,1) relies on the shader's hasWear rule. Check that grime/moss/polish = 0 really shows no change. Every wall vertex now counts as "has wear", so the strength constants apply everywhere.
- The talus skirt removal leaves the breach bare until Megascans hero rubble is placed (Neon's Pass A part 2). Do both in one task, or the breach reads emptier.

Job 2 (fault audit) waits for the real frames: `hollowmere_eye_frames_R19_R18.zip` (15 R19 frames, 10.9 MB) in the coordinator scratch `neon_up` folder; the user uploads it.
