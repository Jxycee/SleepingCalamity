State: IN PROGRESS
Session: Q4: Dungeon corner (B5 cellar vault kit and the room)
Progress: 90%
Sheet: Reference/hollowmere/dungeon_corner/SHEET_dungeon_corner_unity.jpg
Needs: none

- Merged: 9cf4559 on main. Check on main after the merge: SC RESULT check PASS (build PASS, test-edit 609/609, test-play 32 passed / 8 skipped / 0 failed), marker 9cf4559, clean tree. Reviewer (Sonnet): no blocking findings; its should-fix (new scene before loading assets) and capture nits applied.
- Kit: Art/Blender/cellars/sc_cellar_vault.py -> Assets/_Project/Content/Art/Cellar/B5/CellarVault/CellarVault.fbx, 12 pieces, 36,460 tris (NicheWall 8,700 after the arch fix), self 29/40 (target 32 not reached; round 3 +0.5). Sheet SHEET_cellar_vault.jpg. Refs: six licence-checked cellar photos copied from Reference/undercroft (Openverse fetch failed), REFS.md.
- Scene: DungeonCornerBuilder builds Assets/_Project/Scenes/DungeonCorner_Cellars.unity from the kit (greybox fallback per piece), B11 lanterns, candles on a niche sill, larder light, daylight shaft from the coal hatch, warm motes, DungeonCorner_Profile (no teal); DungeonCornerCapture renders Cap_Cellar_Wide/Mid/Close (`unity.sh exec --graphics --method SleepingCalamity.EditorTools.DungeonCornerCapture.CaptureAll`). Wired into ProjectBuilder.RunAll after Hollowmere.
- A24 band (p10/p50/p90 0.06-0.10 / 0.24-0.32 / 0.55-0.68, warmth +0.25..+0.35): p50 and warmth IN band on all three; p90 at/just below (0.49-0.55); p10 0.13-0.15 is ABOVE band: lowering ambient/fog doesn't move it and a volume lift change crushed or blew out the frame, so the black floor is likely in the URP grade/tonemap stack. Needs a coordinator decision or a look-dev pass (Q12-style) before showing the user.
- Coordinator review ~28/40, APPROVED for the user's gate after one polish pass (in progress): deeper cooler-neutral darks and less saturation, seated niche voussoirs + fixed niche plaster UVs, visible dust in the shafts; then re-capture, sheet, check, merge.
- Blockers: none.
