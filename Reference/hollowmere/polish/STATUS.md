Session: Q12. Whole-town look pass (local PC)
Job: B art polish batch
Progress: 100%
Status: DONE
Archive: ready

- Fix 1 (Q6 garden front fence): cause: no duplicated geometry inside the lot (checked the placed pieces: gate + two 1.1 m stubs on the front, closed side and back runs, no overlap) and the Q11 lane garden walls cannot overlap (WallPointBlocked keeps them 1 m off every lot footprint); the "2-3 deep" read came from the two front stubs sitting between the gate and the side runs, so the front showed gate, stub, open gap, side run, corner post as stacked rows. Change: HollowmereGarden.Allotment front is the gate alone (stubs and FrontRun/GatePost removed). Sheet: Reference/hollowmere/polish/SHEET_polish_hm.jpg (top row).
- Fix 2 (scarecrow flat tint): cause: every cloth part sat on the one `canvas` slot and was coloured only by the Blender `tint` attribute, which Unity does not read. Change: sc_garden.py scarecrow() now uses slots coat / burlap / patch_ochre / patch_rust / patch_seam / leather (canvas textures in Blender, tinted per slot), Scarecrow.fbx re-exported (path_mode STRIP, tris unchanged 2264); HollowmereGarden.Slots() adds one tinted copy of the canvas material per slot (TintedCanvas). Straw stays Straw, post and ties oak_timber, buttons iron. Sheet: same file (top row, patch and head now read apart from the coat; the coat is a muted tan-grey, not a clear blue; scarecrow reads apart only slightly, a stronger colour pass is a follow-up).
- Fix 3 (Q10 weir foam): attempted in 2b67a33, reverted: still white blades in Cap_HM_Ravine; needs a proper foam pass (follow-up). HollowmereWater.cs, SC_Water.shader and the HM_Water_* assets are back to origin/main.
- Sheet note: sc_ab_sheet.py is a blind reference-plus-two-candidates sheet that shuffles A and B, so the before/after sheet was composed with a small PIL script (left before on origin/main, right after).
- Captures: reduced (SC_CAPTURE_ONLY, Midday, Rebuilt): before Allotment, Yard, Ravine; after Allotment, Ravine (an intermediate Ravine round with only the vertex change was worse and was replaced by the final one). Cap_HM_Yard frames only the back wall of a home (known, not part of these fixes), so it is not on the sheet.
- Graphics-run output discarded; Hollowmere.unity, BeautyCorner_Hollowmere.unity, water meshes and the new M_GD_* materials come from a -nographics `build` (PASS). test-edit PASS 711/711. `check` not run (the controller schedules it).
- Q14 sacks without burlap: skipped, Q14 not merged
- Q13 Stair_Bottom (522018d): StairEntry +x wall UV offset; also shifts the approved DungeonCorner StairEntry right-wall stones (texture only). StairFlight walls have the same duplicate-UV pattern, not in view (follow-up). Sheet: Reference/hollowmere/polish/stair_bottom_ab.jpg
- Sheet note 2: SHEET_polish_hm.jpg weir row shows the reverted attempt.
