Session: Q12. Whole-town look pass (local PC)
Job: B art polish batch (incl. Q10 weir foam)
Progress: 100%
Status: DONE, merged to main (85d2c7e fence/scarecrow/stair, 4ada664 weir foam)
Blocker: none
Branch: claude/polish-merge, claude/weir-final (both merged)
Last check: PASS on 82bed3e (build, edit 711/711, play 33/0/8) and PASS on 1a170fc (play 33/0/8)
Next: none for this job; follow-ups listed below (scarecrow colour, weir broken foam via shader, StairFlight duplicate UVs, Q14 sacks after Q14 merges)
Archive: ready

- Fix 1 (Q6 garden front fence): cause: no duplicated geometry inside the lot (checked the placed pieces: gate + two 1.1 m stubs on the front, closed side and back runs, no overlap) and the Q11 lane garden walls cannot overlap (WallPointBlocked keeps them 1 m off every lot footprint); the "2-3 deep" read came from the two front stubs sitting between the gate and the side runs, so the front showed gate, stub, open gap, side run, corner post as stacked rows. Change: HollowmereGarden.Allotment front is the gate alone (stubs and FrontRun/GatePost removed). Sheet: Reference/hollowmere/polish/SHEET_polish_hm.jpg (top row).
- Fix 2 (scarecrow flat tint): cause: every cloth part sat on the one `canvas` slot and was coloured only by the Blender `tint` attribute, which Unity does not read. Change: sc_garden.py scarecrow() now uses slots coat / burlap / patch_ochre / patch_rust / patch_seam / leather (canvas textures in Blender, tinted per slot), Scarecrow.fbx re-exported (path_mode STRIP, tris unchanged 2264); HollowmereGarden.Slots() adds one tinted copy of the canvas material per slot (TintedCanvas). Straw stays Straw, post and ties oak_timber, buttons iron. Sheet: same file (top row, patch and head now read apart from the coat; the coat is a muted tan-grey, not a clear blue; scarecrow reads apart only slightly, a stronger colour pass is a follow-up).
- Q10 weir foam: first attempt (2b67a33) reverted; parameter pass 8e04683 merged: _FlowFoam 0.8->0.35, foam colour dimmer, Runout peak 0.9->0.5, falls foam 0.3+0.7e^2 -> 0.2+0.4e^2. Blades gone in Cap_HM_Ravine (weir_ab.jpg); weir now calm, could take a little more broken foam (shader work, follow-up). The params are global: all town water foam is calmer.
- Sheet note: sc_ab_sheet.py is a blind reference-plus-two-candidates sheet that shuffles A and B, so the before/after sheet was composed with a small PIL script (left before on origin/main, right after).
- Captures: reduced (SC_CAPTURE_ONLY, Midday, Rebuilt): before Allotment, Yard, Ravine; after Allotment, Ravine (an intermediate Ravine round with only the vertex change was worse and was replaced by the final one). Cap_HM_Yard frames only the back wall of a home (known, not part of these fixes), so it is not on the sheet.
- Graphics-run output discarded; Hollowmere.unity, BeautyCorner_Hollowmere.unity, water meshes and the new M_GD_* materials come from a -nographics `build` (PASS). test-edit PASS 711/711. `check` not run (the controller schedules it).
- Q14 sacks without burlap: skipped, Q14 not merged
- Q13 Stair_Bottom (522018d): StairEntry +x wall UV offset; also shifts the approved DungeonCorner StairEntry right-wall stones (texture only). StairFlight walls have the same duplicate-UV pattern, not in view (follow-up). Sheet: Reference/hollowmere/polish/stair_bottom_ab.jpg
- Sheet note 2: SHEET_polish_hm.jpg weir row shows the reverted attempt.
