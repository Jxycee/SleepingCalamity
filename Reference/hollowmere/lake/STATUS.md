State: IN PROGRESS
Session: Q10. Lake shore, stream and ravine
Progress: 85%
Sheet: Reference/hollowmere/lake/SHEET_water_r2.jpg
Blocker: none

Score: coordinator APPROVED water r2 ~26/40 (my 27/40); shore set (Blender) 29/40 self, controller agrees ~29; water r2 in Unity 27/40 (composition 3.5, proportion 3.5, form 3.5, surface 3, colour 3, story 3.5, set fit 3.5, liquid 3.5; target first+5 = 26.5 met); r1 was 21.5/40 (silhouette 3, proportion 3, form 2.5, surface 2.5, colour 2.5, story 2, set fit 3, liquid 3): the W1 fall is a white slab, normal speckle, pools too teal, no dressing yet. Coordinator: shore set ~28/40 APPROVED
Tris: LakeShore.fbx 11,118 (6 rocks 330-880, 3 reed clumps 560-732, jetty 5,650)
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
- 63af216: sc_lakeshore.py + LakeShore.fbx (rocks, reeds, jetty; baked PBR, CC0/own) + SHEET_lakeshore_r1.jpg. Next: water compile/capture, then place the set.
- 18:00 UTC: pushes are current (db85588: water compile fixes + generated meshes). Water agent capturing the lake cameras; next a fresh Sonnet agent places the shore set. Note for merge: M_HM_WaterSurface.mat came from a graphics run; regenerate it and the scene with a -nographics build before the check (session-usage rule).
- Compile: SC RESULT compile PASS (9c3fcd4). Round-1 capture (pre-Q12 look, 5 lake cams): water works (refraction, depth colour, reflection, glint, clean bank edges, pools and weirs read); capture hit the 60 min timeout before LowSun Rebuilt.
- c3f1149: merged origin/main (Q12 look) as the coordinator asked; clean.
- Round 2 running (fresh Sonnet agent): place LakeShore.fbx (jetty, reeds, shore stones, weir boulders), fall foam as streaks, no foam sheet on flat ground, calmer pool colour; reduced capture (LakeJetty, Ravine, Mill).
- Worktree note: subagents run in this checkout, not their own worktree: a fresh worktree would re-import the whole Unity Library on the cloud box (hours).
- b7375df round 2: shore set placed (jetty 1, reeds 85, shore stones 55, stream boulders 31) by HollowmereLakeArt.cs; larger softer ripples (normal 0.08, tiles 16/9 m), muted colour, falls foam at lip/base only, shore foam gated by water depth. SC RESULT test-edit PASS 681/681; two reduced captures PASS.
- Round 3 (last, per coordinator): less teal, weir sill reads as a pale plinth (dark wet stone + denser boulder rows), Mill cam pale slab, MillLane cam sees only the mill's shaded back, jetty framing. Then the -nographics rebuild, one check, merge.
- Coordinator approved round 2; round 3 cancelled (agent stopped before it changed any file). Known, not blocking: the weir foam reads as white spikes/wedges, the near-shore lake is still a bit teal, the bare far shore is Q15's backdrop job; also the MillLane camera frames the mill's shaded back and the far jetty piles stop short of the bed.
- 690fedf: merged origin/main (Q13). Graphics-run M_LS_*.mat deleted; a fresh Sonnet agent regenerates Hollowmere.unity + materials with a -nographics build, then one check. Code review (Sonnet reviewer) runs in parallel.
