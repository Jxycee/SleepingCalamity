State: IN PROGRESS
Session: Q10. Lake shore, stream and ravine
Progress: 25%
Sheet: none yet
Blocker: none

Score: -
Tris: -
SC RESULT: -
Merge commit: -

Plan (2026-10-31):
- Terrain measured from the eroded RAW (no terrain or layout change): the ravine floor is flat at about -14 m, rims about -10.5 m, so it sits BELOW the
  lake (-12). Falls therefore come from stepped pools held by rock weirs: reach 1 at -10.85 (spring bowl at the head), W1 0.3 m fall, reach 2 -11.15,
  W2 0.3 m fall, reach 3 -11.45 to the mill pad (culvert under the mill), mill tail fall W3 below the mill down to the lake level -12.
- Water: hand-written URP shader SC_Water (depth colour, opaque-texture refraction, scrolling normals, Fresnel + reflection probe, broken lapping shore
  foam, vertex-colour flow foam at falls) + HollowmereWater.cs builder (lake grid clipped by the terrain, reach corridors, fall ribbons, probes).
- Dressing: sc_lakeshore.py Blender generator (shore stones, stream boulders, reed clumps, a small oak jetty) in one FBX, placed by the builder.
- Captures: Cap_HM_LakeShore, new Cap_HM_LakeJetty, Cap_HM_Ravine, Cap_HM_MillLane, plus Cap_HM_Mill, midday and low sun.

Common practice checked: Unity has no URP water system (the HDRP Water System is HDRP-only); paid stylised-water assets are out (rule). The standard URP
approach (depth-fade colour from the camera depth texture, refraction from the opaque texture, scrolling normal maps, Fresnel + probe reflection,
depth-difference shoreline foam, flow/vertex-colour foam at falls, as in Unity's Boat Attack demo and the common Shader Graph water recipes) is
adopted as a hand-written HLSL shader like the project's SC_WearLit.

## Usage waste (asked by the coordinator, 2026-10-31)
1. ~45%: the Sonnet water agent started before the Unity editor was installed. It wrote the shader and builder blind with no compile signal, then
   handed back three times; I resumed it once with nothing it could do. Fix: start Unity-dependent agents only after `setup.sh --wait` finishes
   (a background wait now), and write the shader once there is a compile loop.
2. ~25%: my own Opus reads at the start (the whole HollowmereTerrain.cs, skills, brief, layout excerpts, the mill sheet). Fix: let a Haiku scanner
   pull only the lines I need. The terrain profiling script, by contrast, paid off: it found that the ravine sits below the lake.
3. ~10%: duplicate hand-back reports (the same report delivered twice) and the extra resume turn. Fix: one resume per real state change, not per
   notification.
No retries, renders or captures yet, so none of the spend went there.

Log:
- 2026-10-31: started; terrain profile measured; two Sonnet agents running (water shader + builder; shore art generator).
- WIP 9c3fcd4: SC_Water.shader, HollowmereWater.cs, lake cameras, uncompiled. Waiting for the Unity editor install (cloud setup still extracting).
- Paused by the coordinator (usage). Shore-art agent stopped before it wrote any files (it was choosing Commons references). Unity editor is now
  installed. On resume: compile + fix SC_Water/HollowmereWater, capture the five lake cameras, then relaunch the shore-art generator.
- Resumed after the usage pause: compile + capture loop (Sonnet), then the shore-art generator (Sonnet).
