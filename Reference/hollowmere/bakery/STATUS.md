State: IN PROGRESS
Session: Wave 1: Bakery (L, frame + parts)
Progress: 80%
Sheet: Reference/hollowmere/bakery/SHEET_bakery_r3.jpg
Needs: none

- Art APPROVED by the coordinator (round 3). Waiting on the terrain merge to main; integration code is drafted on the branch (HollowmereArt.TryPlaceBakery on F02, lot frame at the pad height, footprint checked against the class). Unity editor still downloading in this container.
- Score: self-review 31.5/40 after 3 rounds (first 29, target 34 not reached: round 3 gained ~0.5, stopped per brief 3).
- Tris: 44,774 (Frame 40,226 / Part_Door 1,558 / Part_Oven 656 / Part_Sign 2,334), under the 45k cap. FOOTPRINT 9.62 x 6.84 m (L max 10.5); whole asset x -5.43..5.33, z -4.42..4.99 inside the 11 m foundation.
- Export: Assets/_Project/Content/Art/Town/Ring1/Bakery/Bakery.fbx + SOURCES.md (root Bakery: Frame, Part_Door/Oven/Sign, SOCKET_* under Frame, FOOTPRINT). Door side = Unity local +Z (yaw 180 like the facade).
- Generator: Art/Blender/town/sc_bakery.py. Check: not run yet. Merge: none yet.
- Blockers: none.
