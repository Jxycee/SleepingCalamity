# Q1 home kit families 2 and 4: status

State: IN PROGRESS
Session: Q1 Home kit families 2 and 4 (cloud session, branch claude/brave-keller-3d91gk; bell tower done before it)
Progress: 10%
Sheet: none yet
Blocker: none

Started 2026-10-31.
- Stone cottage (family 2, S): `Art/Blender/town/sc_cottage.py`, variants StoneCottage_A/B/C -> `Content/Art/Town/Ring1/StoneCottage/` (implementer agent, Blender only).
- Workshop (family 4, A = S, B/C = M): `Art/Blender/town/sc_workshop.py`, variants Workshop_A/B/C -> `Content/Art/Town/Ring1/Workshop/` (implementer agent, Blender only).
- Both copy the townhouse's 5.2 export layout (Frame, Part_Door, Part_Shutters, SOCKET_*, FOOTPRINT) and export with path_mode='STRIP'.
- Then, in this session: Unity placement of one of each on a free ring 2 foundation in Rebuilt (builds on HollowmereArt.cs from the bakery branch once it is on main), capture, check, merge.
