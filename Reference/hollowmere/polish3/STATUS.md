Session: Polish 3 (cloud): scarecrow colour + StairFlight UVs
Job: (1) scarecrow colour story in Cap_HM_Allotment; (2) StairFlight wall UV offset (StairEntry pattern, 522018d)
Progress: 30%
Status: IN PROGRESS: colour and UV edits committed (e83e422); FBX re-export running
Blocker: none
Archive: no
Branch: claude/admiring-brahmagupta-0jjuph
Score: -
Sheet: -
SC RESULT: -
Merge commit: -

- Scarecrow cause: the canvas albedo is tan (linear ~0.37/0.22/0.11) and TintedCanvas only multiplies, so the old blue tint (0.58/0.64/0.72) came out tan-grey. New story: faded madder red coat (tint 0.80/0.36/0.50, sRGB ~0.58/0.31/0.25, complement of the garden greens), untinted light burlap head (1.0/0.96/0.86), light ochre patch and dark umber patch (value contrast both ways), dark seam thread.
- StairFlight: both sloped side walls projected the same (y, z) metre UVs; solid() takes an off= and the +x wall uses (0.413, 0.271) like StairEntry.
