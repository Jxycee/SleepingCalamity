# Neon wall kit feedback log (Pass A round 2, 2026-11-22)

Blind A/B on the scene card: new frames lost on all six cameras. Changes to `Art/Blender/town/sc_town_wall.py` (names, pivots, 4.4 m extents, collider footprints unchanged):

1. Ruined tops (Straight_A/B_Ruined, Breach shoulders, Crumbled): Neon's `tp()` gave a smooth 0.1-0.35 m dip, so the top read as a low flat band. Added `jag_top()`: a 0.4 m grid alternating stumps (0.04-0.18 m below parapet top) and gaps/notches (0.30-0.72 m below; 0.55-0.95 on Crumbled). Why: the reviewers need a crenellated broken silhouette against the sky.
2. `_eroded_parapet_courses`: full-height stump courses 0.22-0.64 m tall (was 0.14-0.48), low-parapet courses 0.06-0.18 (was 0.08-0.24), second-course stone chance 0.45 to 0.6, chamfer 0.035 to 0.05. Still every block seated 0.08 m into the core, so nothing floats (the reason Neon removed the old merlons).
3. Wall-foot rubble: heaps now take an optional 8th tuple value (chunky block share, 0.5-0.6) and the straight ruined modules gained front/back talus heaps (they had none, hence the bare hard base line). Blocks are chamfered/wobbled `FK.add_box` pieces 0.10-0.30 m, sunk to z -0.12; breach/crumbled heaps use fewer, bigger pieces (24/20/12 instead of 32/28/18). Why: ground contact and weathering that the old rubble foot gave.

## Round 1 verification of Neon's file (coordinator session, before any change)
- Piece names, TW_* slots, pivots and +-2.2 m (4.4 m) extents are the same as the old kit; `AddColliders` uses boxes keyed by piece, so footprints are unchanged. TW_Coping slot is absent from Breach/Crumbled/Straight_B_Ruined now (no coping there); the builder maps by name, nothing broke.
- Wear: no vertex is white (R+G+B >= 2.99) in old or new. "None" = (0,0,0,1) gives grime = moss = polish = 0 in the shader, so it equals no wear. But the mean values rose: grime straights 0.17-0.20 -> 0.22-0.25, moss 0.017 -> 0.026-0.039; Breach grime 0.26 -> 0.54, Crumbled 0.28 -> 0.43. Walls are modestly grimier; not compared against a wear=0 frame (not captured).
- FBX bytes: two runs differ in about 300 bytes (header/footer timestamp and ids), same length; the old generator behaves the same. Geometry deterministic as far as seen; byte-identical is not achievable with Blender's FBX writer.
- Neon's smooth-dip ruined tops and removed foot rubble made the ruined walls read intact and bare-footed; the A/B penalised this (see changes above). Neon never saw the frames (LFS pointers); next time send frames.
- `docs/art/scene-judging.md` is not tracked in git (only in the main checkout), so Neon cannot read it from the branch either.
