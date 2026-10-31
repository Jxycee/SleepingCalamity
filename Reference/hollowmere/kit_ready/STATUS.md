State: IN PROGRESS
Session: Q2: Smithy, schoolhouse and mill (kit-ready buildings)
Progress: 20%
Needs: none

- Branch: claude/sharp-goldberg-vpyt8c (restarted from main after the bakery merge 269bb73).
- Three Sonnet subagents were writing sc_smithy.py (M), sc_schoolhouse.py (M) and sc_mill.py (landmark: Shell + Frame + Part_Wheel/Part_Door) from the bakery template; told to stop at their current step and save. Generators are uncommitted working files in this session's checkout until resume.
- RESUMED (coordinator); main merged. Integration plan: BuildingKind Smithy/Schoolhouse on ring-2 M lots R2_01 / R2_03 in Rebuilt (unused by other branches); HollowmereArt generalised; mill on its landmark row (Shell in Ruined, Frame + parts in Rebuilt, yaw 180 so the wheel is on the stream side).
- Score/tris/check/merge: none yet.

## At pause (2026-10-31)
- Smithy, schoolhouse: design plans only (`PLAN_smithy.md`, `PLAN_schoolhouse.md`); no code yet.
- Mill: `Art/Blender/town/sc_mill.py` first pass, builds with --no-render (WIP, not rendered, not exported). Tris: Shell 22,550 (cap 30k); Frame 21,956 + Part_Wheel 3,588 + Part_Door 540 = 26,084 (cap 45k). Wheel 4.3 m in a pit recessed into the +X wall; SOCKET_Wheel (5.7, 0, 1.75), SOCKET_Door (~-2.3, -3.5, 0.22). New slots ML_Rubble, ML_OakWeathered, ML_WheelOak, ML_Earth, ML_Sack, ML_Millstone, ML_Interior.
  Open issues: Frame bounds x -5.49..8.06 (limit 8.0), y min -7.11 (yard stones too far?), z min -1.10 (below the -0.55 pit floor); Shell bounds +-13.5 / +-10.5 (inner wall frames misplaced); determinism, facing and wheel clearance unchecked. To do: hoist hood, ridge tiles/eave/barge boards, shutters, wear, renders, score, export, SOURCES, sheet.
