# Self-serve work queue (PC coordinator, 2026-10-31)

**CLAIMING: OPEN (usage window reset 12:10 UTC).** The coordinator may set this to PAUSED for usage pacing; while it says PAUSED, a session that finishes sets DONE and stops without claiming.

A cloud session that has finished and merged its task takes the top item whose `Claimed:` line is still `-`:
1. Pull main.
2. Write your session title and the date on that item's `Claimed:` line, then commit and push to main (subject `queue: claim <item>`). If the push is rejected, pull and pick again.
3. Point your STATUS.md `Session:` at the item title, report in the item's folder, and do the work.

The shared rules are unchanged: `CLAUDE.md`, the mass-production design (`docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md`), `docs/art/beauty-corner-brief.md` scoring (25 floor, first score + 5, reference-beside-render sheet every round, at most ~4 rounds), and pad heights from `HollowmereLayout` (never y = 0). `HollowmereBuilder.cs` and the generated `Hollowmere.unity` are shared: merge origin/main right before your merge, and regenerate the scene with the builder instead of hand-merging it. No merge order between sessions: the first green check merges, and the others merge main again, regenerate and re-check. Never wait idle on another session. Run `unity.sh check`, then merge. Models (coordinator, usage pacing): do the bulk of the work through subagents: generator writing, render loops, Unity runs and fixes on Sonnet (`model: sonnet`); scans, file hunts, log reading and mechanical edits on Haiku (`model: haiku`). Keep your own turns for planning, scoring and merge decisions. Never idle-loop. Study references (user): Fable 2 and Fable 3 towns (Bowerstone, Oakvale, Brightwood) alongside Dark Cloud and the OoT remake, for storybook-real stone and timber, crooked rooflines, lived-in clutter and warm haze. Use them as a described target only: their images are copyrighted, so never commit them. Only the coordinator edits this file's item list; sessions edit only the `Claimed:` lines.

## Q1. Home kit families 2 and 4
Folder: `Reference/hollowmere/kits/`
Claimed: cloud session claude/brave-keller-3d91gk (bell tower done), 2026-10-31
Design 5.4 family 2 (stone cottage, S: rubble walls like the tower, thatch or tile, low and wide) and family 4 (workshop with lean-to, S/M: timber and board, open front). One seeded generator each (`Art/Blender/town/sc_cottage.py`, `sc_workshop.py`). Each exports Frame, Part_Door, Part_Shutters, SOCKET_* and FOOTPRINT, with 3 variants per family, to `Content/Art/Town/Ring1/<Family>/<Variant>/`. Place one of each on a free ring 2 foundation in the Rebuilt preset.

## Q2. Smithy, schoolhouse and mill
Folder: `Reference/hollowmere/kit_ready/`
Claimed: cloud session claude/sharp-goldberg-vpyt8c (bakery done), 2026-10-31
Kit-ready list from design section 6. Smithy (M, forge and chimney, uses the workshop outbuilding look), schoolhouse (M/L, bell-cote, long windows), and mill (the shell and the restored state, on the stream at its landmark row height, about -10 m; wheel at the stream). One generator each, following the 5.2 contract. Place the mill on its landmark row in both presets; the smithy and schoolhouse go on free ring 2 foundations in Rebuilt.

## Q3. Ring 2 fill and the play-position captures
Folder: `Reference/hollowmere/ring2/`
Claimed: cloud session claude/brave-keller-3d91gk (bell tower, Q1 done), 2026-10-31
Once Q1 and the townhouse (`Reference/hollowmere/townhouse/`) are on main: give the Rebuilt preset a believable spread of homes across ring 2. Draw from the merged kit families with seeded variants and turns, at pad heights, leaving some lots empty with ruins. Then the design 7 capture set: square entry from the heroes' home, fountain, tithe gate and a lane mouth, each in Ruined and Rebuilt, at midday and low sun, with a contact sheet beside the approved corner (`Reference/hollowmere/approved_corner/`).

## Q4. Dungeon corner: B5 cellar vault kit and the room
Folder: `Reference/hollowmere/dungeon_corner/`
Claimed: cloud session claude/sharp-goldberg-vpyt8c (bakery, Q2 done), 2026-10-31
`docs/PHASE1.md` 3.7, the B5 row and line 124: one barrel-vault cellar room with a wine-niche wall, larder shelves of jars, the coal chute, the B11 cellar lantern (on main), the breath grate, roots trickling in, and dust in the lamp shafts. It's lit exactly as generated floors will be. Generator `Art/Blender/cellar/sc_cellar_vault.py` (the kit pieces: vault segment, end wall, niche wall, arch, pillar, stair, floor tiles, chute) plus an editor builder for a `DungeonCorner_Cellars` scene with fixed capture cameras, like `BeautyCornerBuilder`. Decision A24: aim the room at the user's reference image (see `docs/superpowers/handoff/decisions-for-review.md`).

## Q5. Builder churn fix
Folder: `Reference/hollowmere/churn/`
Claimed: -
Every build rewrites `BeautyCorner_Hollowmere.unity`, flips `M_Pollen` `_SrcBlend`, and rewrites `HeroAnimator.controller` (and its .meta) with no real change, so every session leaves them uncommitted. Make the builders deterministic, so that two `unity.sh build` runs in a row leave `git status` clean, and add an EditMode test or a check script for it. Code only; a `reviewer`-grade self-review before merging.

## Q6. Garden and allotment decorations
Folder: `Reference/hollowmere/garden/`
Claimed: -
The decorations still kit-ready in design section 6: vegetable beds, a trellis, a scarecrow, a water butt, fences and a cold frame, each a small free-grid decoration with a footprint (design 5.3). Contact sheet per batch (B7 style); place a few beside ring 2 homes in Rebuilt.

## Q7. Cellars room pool, first rooms (M5 authoring)
Folder: `Reference/hollowmere/room_pool/`
Claimed: -
`docs/PHASE1.md` M5 and 7.2: the room pool as data for floors 1-4, in greybox. Start from whatever M5 code is on main (look first; don't duplicate it). Author 8-12 rooms with sub-themes and dressing variants as data, with EditMode tests that every room is valid (exits connect, the key is reachable). Code and data only, no art. If M5's Core floor generator isn't on main yet, write only the room data format plus its tests, and say so in STATUS.md.

## Q8. Town wall, Lower Gate and East Gate
Folder: `Reference/hollowmere/town_wall/`
Claimed: -
Replace the greybox town wall on its raised bank with real art: a weathered sandstone curtain wall with a walkway, crenels, buttresses and breaches in Ruined (patched in Rebuilt); the Lower Gate (onto the Saltwake road) and the East Gate (down Mill Lane) as gatehouses with timber doors. A kit generator (`Art/Blender/town/sc_town_wall.py`) that tiles along the layout's wall path at its heights. Captures: low east, lake shore, both gates, in both presets.

## Q9. Keep terrace: the keep hall and the terrace dressing
Folder: `Reference/hollowmere/keep/`
Claimed: -
The bell tower is on main (`ee4fe79`), but its hall stays greybox. Build the keep hall (an old stone hall with tall windows and a slate roof, beside the tower on the +4 terrace), plus the terrace dressing: the buttressed retaining wall with ivy planters (the layout already has them as greybox), both stairs with balustrades, and benches. Captures: keep stairs and keep wall views, in both presets.

## Q10. Lake shore, stream and ravine
Folder: `Reference/hollowmere/lake/`
Claimed: -
The lake basin (water at -12 m) and the stream ravine, using `sc-water-look`: a stylised-real water surface with a shoreline foam and depth tint, reeds, shore stones, a small jetty, and stream rocks with small falls down the ravine toward the mill. Keep within the terrain from `HollowmereTerrain`. Captures: lake shore, the ravine and Mill Lane, at midday and low sun.

## Q11. Lane kit: lane walls, steps and lane dressing
Folder: `Reference/hollowmere/lanes/`
Claimed: -
The design keeps the lane kits at final quality but unplaced. Build the ring 2 lane kit (garden walls, terrace retaining walls, lane steps and ramps matching the 20 degree rule, wall lanterns, gates into yards, drain gutters) and place it along the lanes at their heights in both presets. Captures: Market Street, a lane mouth, the terrace steps.

## Q12. Whole-town look pass
Folder: `Reference/hollowmere/look/`
Claimed: -
Take this one only after Q3 or Q8 has merged, so there is a real town to light. Use `sc-scene-look` on the whole of Hollowmere: light, atmosphere (haze, volumetrics, pollen), the colour grade at midday and low sun, matched to the approved corner's bands (`sc_ref_palette.py`) and the style bible 3.1/3.5. Fixed capture set (overview, square, lanes, keep, lake) as a contact sheet beside the approved corner. Change only lighting, volume and grade settings and the builder's look hooks.
