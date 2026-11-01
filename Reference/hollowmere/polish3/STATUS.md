Session: Polish 3 (cloud, resumed): scarecrow colour + StairFlight UVs
Job: (1) scarecrow colour story in Cap_HM_Allotment; (2) StairFlight wall UV offset (StairEntry pattern, 522018d)
Progress: 70%
Status: IN PROGRESS: round 2 Allotment capture + Undercroft stair capture running (one Unity process at a time)
Blocker: merge lock (merge after M5 contents and procgen)
Archive: no
Branch: claude/vibrant-heisenberg-csgj6z (continues claude/admiring-brahmagupta-0jjuph)
Score: -
Sheet: -
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
