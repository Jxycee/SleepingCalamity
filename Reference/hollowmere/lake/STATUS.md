State: IN PROGRESS
Session: Q10. Lake shore, stream and ravine
Progress: 10%
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

Log:
- 2026-10-31: started; terrain profile measured; two Sonnet agents running (water shader + builder; shore art generator).
