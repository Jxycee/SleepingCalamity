# Q1 home kit families 2 and 4: status

State: DONE (merged to main 3e4c2d8)
Session: Bell tower: bake, integrate, check, merge
Job: Q1 Home kit families 2 and 4 (cloud session, branch claude/brave-keller-3d91gk)
Progress: 100%
Sheet: Reference/hollowmere/kits/kits_town_sheet.jpg (also cottage_sheet.jpg, workshop_sheet.jpg)
SC RESULT: SC RESULT check PASS on 7376e14 (test-edit 593/593, test-play 32 passed / 8 skipped / 0 failed); the merge's Assets equal the checked tip
Merge commit: 3e4c2d8
Blocker: none

Result:
- Stone cottage (family 2, S): Art/Blender/town/sc_cottage.py, StoneCottage_A/B/C (25,488 / 28,296 / 26,014 tris), coordinator review 26/40 + fixes (outline variety,
  glass bands and curtains, roof value patches). Workshop (family 4): Art/Blender/town/sc_workshop.py, Workshop_A (S) / B (M) / C (M) (20,092 / 25,564 / 24,916 tris),
  self-score 26.5-28, controller read about 26.
- Town: StoneCottage_B on R2_09 (6 m lot), Workshop_B on R2_08 (8 m lot), both on the east Ring Lane at pad y -3.47, Rebuilt preset (HollowmereHomeKits.cs,
  BuildingKind.StoneCottage / Workshop, layout test, Cap_HM_Kits camera). No missing slots or 'Lit' names on import.
- Unverified: variants A and C of both families are not placed, so their Unity slot maps are untested; thatch tint has no licensed thatch reference (CC BY-SA only);
  thatch roughness is approximated in Unity (smoothness -0.2); workshop boards A/C read dark in Blender (no lift applied, they are not placed).

History:
Started 2026-10-31.
- Stone cottage (family 2, S): `Art/Blender/town/sc_cottage.py`, variants StoneCottage_A/B/C -> `Content/Art/Town/Ring1/StoneCottage/` (implementer agent, Blender only).
- Workshop (family 4, A = S, B/C = M): `Art/Blender/town/sc_workshop.py`, variants Workshop_A/B/C -> `Content/Art/Town/Ring1/Workshop/` (implementer agent, Blender only).
- Both copy the townhouse's 5.2 export layout (Frame, Part_Door, Part_Shutters, SOCKET_*, FOOTPRINT) and export with path_mode='STRIP'.
- Then, in this session: Unity placement of one of each on a free ring 2 foundation in Rebuilt (builds on HollowmereArt.cs from the bakery branch once it is on main), capture, check, merge.

Paused 2026-10-31 (coordinator, usage). Both WIP commits are saved on branch claude/brave-keller-3d91gk (merges 19ba3d8 cottage, then the workshop draft):
- Cottage `sc_cottage.py` (fd7e997): builds all three variants (A tile + porch 25,378 tris, FOOTPRINT 4.85 x 4.98; B thatch + dormer 27,136, 4.61 x 4.38;
  C thatch + tiled lean-to 25,322, 4.91 x 4.52). Not yet rendered, exported or audited; no score. New slots SC_Rubble (B1 sandstone, grey tints) and SC_Thatch
  (B1 oak_timber, straw tint; no thatch tileable exists). Next: --export --audit, round 1 renders, sheet, SOURCES.md.
- Workshop `sc_workshop.py` (cbfcf2d): first draft, never run; known bugs listed by the agent (lean-to logs/rafters, extents, hatch, window spacing, stove z, stack size).
  Design question for resume: the agent made the open front always open and put Part_Door in a side wall (SOCKET_Door on that side). Check against the builder's
  lot orientation (door side faces the lane) before keeping it; the open front should face the lane.
- Fresh worktrees need `git lfs pull` for Art/Textures/B1_town and the Reference images before Blender can load them.

Resumed 2026-10-31: main (inn, stalls, tithe gate, heroes' home, ruins) merged into claude/brave-keller-3d91gk without conflicts. Both generator agents continue in their
worktrees. Workshop door question settled: the builder turns the building FRONT (Blender -Y) to the lot's lane side regardless of SOCKET_Door, so the open front is -Y
and the man door sits in a side wall. Unity next: route one free S ring-2 Home lot to a cottage and one S/M lot to a workshop (greybox fallback when the art is missing).

Second pause state (saved on claude/brave-keller-3d91gk):
- Workshop (3a1b948, merged 6d0a353): round 2 of 4, exported to Content/Art/Town/Ring1/Workshop/, re-import audit clean, SOURCES.md, workshop_sheet.jpg.
  A (S) 20,092 tris FOOTPRINT 4.83 x 4.34; B (M) 25,984, 6.73 x 6.30; C (M) 24,916, 6.95 x 5.28. Rough self-score 26-27/40. Next fixes: board cladding too dark on A/C,
  thin lean-to roof, see-through board gaps on B (inside view).
- Cottage (88bb6eb): round 2 geometry, scratch-exported and audited (names, slots survive STRIP, wear everywhere, normals clean). A 25,408 tris 4.85 x 4.79;
  B 27,546, 4.61 x 4.38; C 25,944, 4.91 x 4.52. Not yet exported to the repo, no sheet/score/SOURCES yet. Known: rubble reads tan ashlar, lean-to opening small.
- No new refs: Commons thatch photos were CC BY-SA only (excluded) and the API rate-limited; sheets reuse repo refs (Bibury h_arch_04/07).

Third pause state (all on claude/brave-keller-3d91gk):
- Both families accepted (controller read of the sheets: about 26/40 each, above the floor). Workshop b876fb0 (A 20,092 / B 25,564 / C 24,916 tris; self 27 / 28 / 26.5),
  cottage f50b975 (A 25,408 / B 27,546 / C 25,990 tris; self 27 / 26.5 / 26). Sheets: Reference/hollowmere/kits/{cottage,workshop}_sheet.jpg.
- Unity integration WIP 7f4b756 (compiles, not yet run): BuildingKind.StoneCottage / Workshop, StoneCottage_B on R2_09 and Workshop_B on R2_08 (east Ring Lane),
  HollowmereHomeKits.cs (materials like HollowmereTownhouse, WearLit, no raw HDR), Cap_HM_Kits camera, layout test. The first capture run timed out right after the
  terrain step: rerun with a longer --timeout. Left: capture + slot greps, town sheet, check, merge.

