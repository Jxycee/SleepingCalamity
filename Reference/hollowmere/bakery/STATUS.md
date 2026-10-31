State: DONE
Session: Wave 1: Bakery (L, frame + parts)
Progress: 100%
Sheet: Reference/hollowmere/bakery/SHEET_bakery_unity.jpg
Needs: none

- Merged: 269bb73 (Merge claude/sharp-goldberg-vpyt8c) on main.
- Check: SC RESULT check PASS on the branch tip 9a4f1a4 (build PASS, test-edit 591/591, test-play 32 passed / 8 skipped / 0 failed); main's Assets/Packages/ProjectSettings are identical to that tip (main only added queue docs).
- Score: self-review 31.5/40 after 3 rounds (first 29); coordinator approved round 3 (~30/40) and the Unity captures.
- Tris: 44,994 (Frame 40,446 / Part_Door 1,558 / Part_Oven 656 / Part_Sign 2,334), cap 45k. FOOTPRINT 9.62 x 6.84 m (L max 10.5), checked by the builder against the lot's class.
- Export: Assets/_Project/Content/Art/Town/Ring1/Bakery/Bakery.fbx + SOURCES.md. Builder: HollowmereArt.TryPlaceBakery on F02 at its pad height, door to the square; new capture camera Cap_HM_Bakery.
- Sheets: SHEET_bakery_r3.jpg (Blender, reference beside render), SHEET_bakery_unity.jpg (Unity Rebuilt/Ruined).
- Notes for others: cloud FBX exports need path_mode='STRIP' (else Unity names every textured slot 'Lit'); the facade kit export has the same latent issue. A -nographics build rewrites HeroAnimator.controller with float/order noise each run (not committed here).
- Blockers: none.
