# Self-serve work queue (PC coordinator, 2026-10-31)

A cloud session that has finished and merged its task takes the top item whose `Claimed:` line is still `-`:
1. Pull main.
2. Write your session title and the date on that item's `Claimed:` line, then commit and push to main (subject `queue: claim <item>`). If the push is rejected, pull and pick again.
3. Point your STATUS.md `Session:` at the item title, report in the item's folder, and do the work.

The shared rules are unchanged: `CLAUDE.md`, the mass-production design (`docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md`), `docs/art/beauty-corner-brief.md` scoring (25 floor, first score + 5, reference-beside-render sheet every round, at most ~4 rounds), and pad heights from `HollowmereLayout` (never y = 0). `HollowmereBuilder.cs` and the generated `Hollowmere.unity` are shared: merge origin/main right before your merge, and regenerate the scene with the builder instead of hand-merging it. No merge order between sessions: the first green check merges, and the others merge main again, regenerate and re-check. Never wait idle on another session. Run `unity.sh check`, then merge. Use the right-sized model for each step, and never idle-loop. Only the coordinator edits this file's item list; sessions edit only the `Claimed:` lines.

## Q1. Home kit families 2 and 4
Folder: `Reference/hollowmere/kits/`
Claimed: -
Design 5.4 family 2 (stone cottage, S: rubble walls like the tower, thatch or tile, low and wide) and family 4 (workshop with lean-to, S/M: timber and board, open front). One seeded generator each (`Art/Blender/town/sc_cottage.py`, `sc_workshop.py`). Each exports Frame, Part_Door, Part_Shutters, SOCKET_* and FOOTPRINT, with 3 variants per family, to `Content/Art/Town/Ring1/<Family>/<Variant>/`. Place one of each on a free ring 2 foundation in the Rebuilt preset.

## Q2. Smithy, schoolhouse and mill
Folder: `Reference/hollowmere/kit_ready/`
Claimed: -
Kit-ready list from design section 6. Smithy (M, forge and chimney, uses the workshop outbuilding look), schoolhouse (M/L, bell-cote, long windows), and mill (the shell and the restored state, on the stream at its landmark row height, about -10 m; wheel at the stream). One generator each, following the 5.2 contract. Place the mill on its landmark row in both presets; the smithy and schoolhouse go on free ring 2 foundations in Rebuilt.

## Q3. Ring 2 fill and the play-position captures
Folder: `Reference/hollowmere/ring2/`
Claimed: -
Once Q1 and the townhouse (`Reference/hollowmere/townhouse/`) are on main: give the Rebuilt preset a believable spread of homes across ring 2. Draw from the merged kit families with seeded variants and turns, at pad heights, leaving some lots empty with ruins. Then the design 7 capture set: square entry from the heroes' home, fountain, tithe gate and a lane mouth, each in Ruined and Rebuilt, at midday and low sun, with a contact sheet beside the approved corner (`Reference/hollowmere/approved_corner/`).

## Q4. Dungeon corner: B5 cellar vault kit and the room
Folder: `Reference/hollowmere/dungeon_corner/`
Claimed: -
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
