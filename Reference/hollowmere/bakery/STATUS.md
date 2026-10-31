State: IN PROGRESS
Session: Wave 1: Bakery (L, frame + parts)
Progress: 95%
Sheet: Reference/hollowmere/bakery/SHEET_bakery_unity.jpg
Needs: none

- Art APPROVED by the coordinator (round 3). Terrain merged into the branch; bakery on F02 at its pad height (lot frame f.Y + footing top), door to the square. Unity captures (Rebuilt/Ruined) in SHEET_bakery_unity.jpg; new capture camera Cap_HM_Bakery. Final check running on the tip.
- Generator made reproducible (Python hash() was salted per run; now zlib.crc32).
- Fixed on the way: cloud FBX texture paths made Unity name every slot 'Lit' (same root cause as the bell tower); export now uses path_mode STRIP.
- Score: self-review 31.5/40 after 3 rounds (first 29, target 34 not reached: round 3 gained ~0.5, stopped per brief 3).
- Tris: 44,994 (Frame 40,446 / Part_Door 1,558 / Part_Oven 656 / Part_Sign 2,334), under the 45k cap. FOOTPRINT 9.62 x 6.84 m (L max 10.5); whole asset x -5.43..5.33, z -4.42..4.99 inside the 11 m foundation.
- Export: Assets/_Project/Content/Art/Town/Ring1/Bakery/Bakery.fbx + SOURCES.md (root Bakery: Frame, Part_Door/Oven/Sign, SOCKET_* under Frame, FOOTPRINT). Door side = Unity local +Z (yaw 180 like the facade).
- Generator: Art/Blender/town/sc_bakery.py. Check: not run yet. Merge: none yet.
- Blockers: none.
