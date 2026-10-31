State: IN PROGRESS
Session: Wave 2: Ground tileables + foliage extension
Progress: 30%
Sheet: none
Blocker: paused by coordinator for usage

Score: -
Tris: -
Check: -
Merge: -

## Log
- 2026-10-31: started on branch claude/ecstatic-bardeen-pr8wbk; reading HollowmereTerrain, B9 foliage atlas, sc-texture-paint / sc-foliage. Unity editor still downloading in the background.
- 2026-10-31: B1 ground tileables built (2048, albedo/normal/ORM + height): meadow_grass (ambientCG Grass004, CC0), packed_soil (Poly Haven brown_mud_dry, CC0), worn_path (Poly Haven grass_path_2, CC0: the grass-soil blend). Source rows in `Art/Textures/B1_town/SOURCES.md`. Checked first: the existing pipeline `sc_tex_batches.py` (extended, not replaced) and Unity's own Terrain layers + mask maps + height blend (no custom terrain shader). Terrain layers wired (grass 3 m, soil 3 m, path 2.5 m, cobble 2 m); splat: soil on banks > 24 deg, the ditch, the lake shore; path along lane edges and on lots; grass on open ground with 30 m dry patches. Flat greybox tints removed. Foliage extension running (Unity Terrain detail prototypes for instanced grass: the standard practice, no custom grass renderer).
- 2026-10-31: PAUSED by the coordinator for usage. Done and pushed: B1 ground tileables + terrain layers (`379e189`). WIP pushed uncompiled: `HollowmereFoliage.cs` (terrain detail grass, foundation-edge grass/weeds, ruin weeds/moss, landmark and keep-wall ivy) plus a hook in `HollowmereBuilder.cs`; written by the Sonnet implementer and stopped before any compile or Unity run. On resume: compile, build, captures, sheets, check, merge.
