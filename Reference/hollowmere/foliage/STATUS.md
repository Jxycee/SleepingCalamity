State: IN PROGRESS
Session: Wave 2: Ground tileables + foliage extension
Progress: 70%
Sheet: Reference/hollowmere/foliage/foliage_sheet_r1.jpg
Blocker: none

Score: round 1 = 26/40 (self; target 31). Ground reads as real PBR turf/soil/grit, banks and ditch soil, ivy faces out and sits on the keep wall,
  corner rows unchanged. Top fixes in round 2: (1) terrain tufts stand in lines on lane/square paving edges, (2) grass reads as even dots, not meadow
  (cluster it at 4-8 m), (3) old greybox green ivy boxes still on the keep wall; dry patches in the overview were blotchy (softened).
Tris: grass worst view 368k (Cap_HM_TitheGate, 2,167 clumps; 73k clumps total, instanced terrain details, no shadows);
  placed foliage ruined 1,300 objects / 596k tris town-wide, rebuilt 610 / 295k.
Check: - (round 2 runs build, captures, then unity.sh check)
Merge: -

## Log
- 2026-10-31: started on branch claude/ecstatic-bardeen-pr8wbk; reading HollowmereTerrain, B9 foliage atlas, sc-texture-paint / sc-foliage. Unity editor still downloading in the background.
- 2026-10-31: B1 ground tileables built (2048, albedo/normal/ORM + height): meadow_grass (ambientCG Grass004, CC0), packed_soil (Poly Haven brown_mud_dry, CC0), worn_path (Poly Haven grass_path_2, CC0: the grass-soil blend). Source rows in `Art/Textures/B1_town/SOURCES.md`. Checked first: the existing pipeline `sc_tex_batches.py` (extended, not replaced) and Unity's own Terrain layers + mask maps + height blend (no custom terrain shader). Terrain layers wired (grass 3 m, soil 3 m, path 2.5 m, cobble 2 m); splat: soil on banks > 24 deg, the ditch, the lake shore; path along lane edges and on lots; grass on open ground with 30 m dry patches. Flat greybox tints removed. Foliage extension running (Unity Terrain detail prototypes for instanced grass: the standard practice, no custom grass renderer).
- 2026-10-31: PAUSED by the coordinator for usage. Done and pushed: B1 ground tileables + terrain layers (`379e189`). WIP pushed uncompiled: `HollowmereFoliage.cs` (terrain detail grass, foundation-edge grass/weeds, ruin weeds/moss, landmark and keep-wall ivy) plus a hook in `HollowmereBuilder.cs`; written by the Sonnet implementer and stopped before any compile or Unity run. On resume: compile, build, captures, sheets, check, merge.
- 2026-10-31: RESUMED. Merged origin/main (bakery, bell tower, ruin kit integration, stalls, tithe gate) cleanly (`6ea2b37`). This container's Unity download had stalled at 0.6/4.2 GB since 04:45: resumed with curl -C -, then the editor gets extracted. Reviewing the foliage WIP meanwhile.
- 2026-10-31 ~09:20: Unity running in this container now (the stalled setup was finished by hand: editor extracted, 29 packages, DXC). First foliage build + captures done in both presets (23 views + the approved corner); scoring and fixing now. Next: sheets, merge main again (heroes' home landed), regenerate the scene, check.
- 2026-10-31 ~10:05: round 1 committed (`7698550`): HollowmereFoliage.cs (Unity Terrain detail prototypes, GPU instanced, density from the grass splat; edges, ruins, ivy), the B9 meshes face +Z in Unity (SOURCES.md says -Y/-Z: the yaws were flipped). The ruin kit art carries its own weeds/moss, so the ruin pass runs only on greybox stubs; the tithe gate art has its own ivy. Sheet r1 pushed. Round 2 (Sonnet) running: paving margin, clustered grass, greybox ivy removal, then check.
