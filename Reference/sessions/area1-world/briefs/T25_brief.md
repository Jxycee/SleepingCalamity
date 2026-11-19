# T25 brief: story and life pass (Area 1 world, Run 8)

Goal (user, 2026-11-18): Hollowmere reads as the home four kids kept alive for ten years, above a demon asleep under their feet, not as a beautiful template with nothing connecting it. Rule: the ruins are the four's house; empty lots are repurposed as rooms of their life, as if the four just stepped out of frame.

Full design text (read it, it is the spec): `C:\Users\jayce\AppData\Roaming\Claude\side-session-notes\local_88bfb1c1-21c7-4c37-8634-472d98acdc40\T25-story-pass.md` (sections "T25 story pass" items 1-6 and "T25 part 2: life stations" 1-14 plus connective tissue). Prop sourcing per station (OWNED / MEGASCANS / BUILD with paths and sizes): `T25-prop-list.md` in the same folder. Megascans candidates with IDs: `megascans_yes_names.csv` there. Story canon: `docs/STORY.md`, placements `docs/design/area1-hollowmere.md` (hero houses, Wren's list wall, Tuppence spots).

Two runs, one implementer each:
- **T25a (town core + story layer):** story items 1-6 (lived-in traces and worn paths between the hero houses, well, allotments, lookout, workshop; drag furrows and scrape marks toward the church and tithe gate, readable from the high cameras, cart ruts down the tithe ramp; the Bargain's over-rich soil; bleached taken lots against vivid growth; warmth from below: drain steam, a dew-free ring at the tithe gate, a greener line over the Undercroft; the empty bell frame) plus stations 1 fire circle, 2 well yard, 3 woodyard, 10 Wren, 11 Tobin, 12 Pip, 13 Ash, 14 devotion.
- **T25b (outskirts + connective tissue):** stations 4 salvage yard, 5 allotments, 6 orchard, 7 cat's pond, 8 jetty, 9 dye vats; connective tissue (paths between stations, rope and washing lines, lantern string, planks over rubble, stepping stones, Pip's signs, surveyor stakes, items left mid-route) and goat signs (chewed posts, salt lick).

Branch `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`. Include the controller's uncommitted Reference/ files unchanged in your first commit; never edit STATUS.md. Do not merge main. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\afea9a59-8f25-4f8e-af79-2a9f20c645f7\scratchpad\T25`. Hollowmere is split into additive scenes (`HollowmereSplit.cs` header; `SC_SCOPED_SCENES` scoped builds; manifest mismatch = one full `build`).

## Rules
- Each station = one anchor prop + 3-5 supports + a trace (wear, path, spill) + one story detail. Every eye camera sees at least one station and one in-transit item within ~20 m.
- Props grouped by use and grounded (contact decals, debris, grass at the base; no lone props on open cobbles). Scanned props keep their detail; budget per camera (visible tris with Mesh LOD, T12's ~6 M worst view as reference); combine small static props (`HollowmereCombine`), Mesh LOD on new FBX.
- Ready props: `Adopted/T27a-q` (axes, chopping blocks, troughs, watering can, ladders, baskets, crates, bookshelf, flower pot, fence, window; bounds in STATUS T27 row; T27a/b lie flat), plus everything in STATUS intake rows T16-T21. BUILD items (initials, chalk drawings, tally marks, signs, height marks = decals; windlass, tripod, yoke, washboard, sawhorse, frames = simple builder meshes from adopted plank/log pieces via the `Timber` helper, as T13a did). New Megascans needed: list them in the report (Megascans wanted) rather than downloading; the controller fetches them.
- Rebuild the cube hero props (Wren's board, washing line, bell post, saw frame) if any are still cubes.
- Colliders: props over ~0.5 m in walking range get simple colliders; never block a traversal path or a door; play loop after.
- Tuppence and the goats exist after T24: place stations around them, don't move them.
- Keep the art target's saturated day colour in the nature; only the taken lots bleach.

## Story notes (coordinator, 2026-11-19): build meaning, not just props
1. **Ten years visible:** repairs of different ages side by side (silver-grey weathered patch wood next to pale new planks); height marks on a doorframe climbing from child to adult height; paths worn deepest between Wren's house and the fire circle.
2. **One dye per hero** (from the T25b dye vats): Wren woad blue, Tobin ochre, Pip madder red, Ash undyed/leather. Each hero's corner carries their colour on a cloth, a shutter or a ribbon on their gear, so players read whose place is whose. T25a puts the colours on the hero corners; T25b's vats show the same three dyes.
3. **Wren's hand:** her surveyor stakes use the same hand and paint as her list wall (rows of name-like marks, no readable names), so stakes and wall read as one person's work.

## Capture and judging
Round 0 = the newest committed frame per camera (`R19/` from T24, else `R18/`, else `R17/`). Check cameras: Postcard, Balcony, Spawn, BellYard, Orchard, West, InnFront, Bakery, MarketStreet (T25a); Orchard, Allotment, Lake, JettyBack, Mill, Meadow, OrchardGap, RoadOut (T25b); plus any `HollowmereScoped.Pick` lists. Prove the first station with one reduced capture before dressing the rest. Rounds end after two in a row with no visible gain. Finals: T25a `R20/`, T25b `R21/` with sheets. T25 ends the milestone: after T25b the controller runs the blind A/B plus the absolute /50 two-run on all eye cameras.

## Check
Build twice only if builder code adds randomness/time/ordering (seeded placement is fine). Churn reverted (STATUS "Known churn" + T28 row). No embedded meshes. Commit by path. Gate (`SC MARKER` = HEAD). Play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression|HollowmereSceneLoader"` (colliders change). Push. Delete probe scripts.

## Report (under 15 lines)
Commits; gate/play lines; stations placed (with one line each: anchor + story detail + camera that sees it); BUILD items made; colliders added; per-camera visible tris for the worst 3 cameras; views better/worse; Megascans wanted.
