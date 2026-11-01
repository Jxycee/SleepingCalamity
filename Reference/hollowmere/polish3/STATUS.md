Session: Polish 3 (cloud, resumed): scarecrow colour + StairFlight UVs
Job: (1) scarecrow colour story in Cap_HM_Allotment; (2) StairFlight wall UV offset (StairEntry pattern, 522018d)
Progress: 85%
Status: APPROVED: waiting to merge after the procgen and Cellars floor art sessions land (both regenerate scenes)
Blocker: merge order (after procgen + Cellars floor art)
Archive: no
Branch: claude/vibrant-heisenberg-csgj6z (continues claude/admiring-brahmagupta-0jjuph)
Score: coordinator APPROVED the final sheet at ~25-26/40 (own read ~27); StairFlight UV fix proven
Sheet: Reference/hollowmere/polish3/polish3_final.jpg (Richter PD painting beside round 1 / round 2 Allotment crops; Kassel cellar stair photo beside Stair_Turn)
SC RESULT: -
Merge commit: -

- Scarecrow cause: the canvas albedo is tan (linear ~0.37/0.22/0.11) and TintedCanvas only multiplies, so the old blue tint (0.58/0.64/0.72) came out tan-grey. New story: faded madder red coat (tint 0.80/0.36/0.50, sRGB ~0.58/0.31/0.25, complement of the garden greens), untinted light burlap head (1.0/0.96/0.86), light ochre patch and dark umber patch (value contrast both ways), dark seam thread.
- StairFlight: both sloped side walls projected the same (y, z) metre UVs; solid() takes an off= and the +x wall uses (0.413, 0.271) like StairEntry.
- Done before parking: e83e422 (tints + UV offset), 095bb51 (Scarecrow.fbx tris 2264 unchanged; StairFlight is in CellarStair.fbx, tris 13500 unchanged, +x wall UV range shifted by (0.413, 0.271)); all with path_mode STRIP.
- Left to do on resume: reduced captures (Cap_HM_Allotment Midday Rebuilt; one Undercroft stair camera that frames a StairFlight, e.g. Cap_Stair_Top), sheet with reference beside render, score, then merge origin/main, -nographics build, check, merge.
- Note: sc_garden.py `--only X --export` rewrites Garden/SOURCES.md with only that row (reverted, not committed).
- 2026-11-01 resume (claude/vibrant-heisenberg-csgj6z): re-export already done (095bb51, Scarecrow.fbx 2264 tris unchanged, path_mode STRIP), not redone.
- Round 1 Cap_HM_Allotment (Rebuilt, SC_CAPTURE_ONLY=Allotment): coat reads red, sRGB 0.67/0.43/0.26 against yellow-green grass 0.55/0.52/0.24 and the stone wall; close in hue to the soil path (0.62/0.47/0.32), but the dark fence and wall behind hold the silhouette. Head read dark (0.16/0.18/0.14): it sits in the hat brim's midday shadow and a multiply cannot lift the tan canvas albedo.
- Round 2 (3dc26d6): burlap slot = PaleCanvas (canvas weave normal kept, albedo map dropped, base #d8c69c). Capture running, then one Undercroft stair capture for the StairFlight wall UV proof. Branch merges main after the captures (not during a Unity run).
- Round 2 Cap_HM_Allotment (first attempt OOM-killed at ~10.6 GB, retry ok): coat 0.56/0.29/0.22, hat 0.82/0.79/0.42, face 0.24/0.26/0.22 (was 0.16). Clear story: red coat, yellow straw hat, light ochre + dark umber patches against green beds and grey wall. Max 2 rounds reached.
- Known follow-up (no round left): the face still sits in the hat brim's midday shadow; fix in sc_garden.py scarecrow() by raising/narrowing the brim or tipping the head forward (re-export, tris about equal).
- Undercroft stair capture (UndercroftStairCapture, 3 cameras, small scene): Stair_Turn frames both StairFlight walls; stone layouts differ left/right, no mirrored repeat. Stone still stretches along the slope (separate from this job).
- Coordinator notes, not now: the round 2 head still renders dark grey (sheet label corrected), and the forms read a bit smooth and plastic.
