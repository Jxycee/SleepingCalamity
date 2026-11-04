Status: DONE (merged ec29e0d1 on 2026-11-02: cloud check PASS on b802d7d, PC captures approved by the coordinator; the BLOCKED notes below are history)
Archive: ready
Branch: cellars-visual-b (merged ec29e0d1).
Check: gate PASS, full check PASS on tree 89823d66 (tip only adds 2x stair-stone tiling; re-run check to match the marker).
Why not merged: the cloud container renders lit surfaces near-black, so no after-frames exist; B2-B6 are fixed by analysis only. Needs a PC/GPU session to capture Undercroft + DungeonCorner, confirm the look, re-run check, merge.
B2 spawn/entry discs, special-room post+banner, door-hint spheres hidden unless CellarFloorBuilder.ShowDebugMarkers. B3 plaster hall two-tone + oak dado, lighter oak door. B4 stair stone/mortar tile 2x (FBX UVs untouched). B5 not auto-exposure (fixed exposure): lowered door spill, trailer lamp, stair limewash 14% darker; black dips are intended 0.25s cut fades. B6 ambient 0.28->0.36, fill 1.0->1.6, warm PierGlow at pillar feet. New TrailerRender.RenderStills.
Notes: cloud unity.sh needs --timeout 40+ for gate/check; Build All dirties Hollowmere.unity/BeautyCorner (reverted).
