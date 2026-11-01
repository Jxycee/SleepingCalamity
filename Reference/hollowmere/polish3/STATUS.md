Session: Polish 3 (cloud, resumed): scarecrow colour + StairFlight UVs
Job: (1) scarecrow colour story in Cap_HM_Allotment; (2) StairFlight wall UV offset (StairEntry pattern, 522018d)
Progress: 60%
Status: RESUMED: reduced captures running (re-exports already done in 095bb51)
Blocker: none
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
