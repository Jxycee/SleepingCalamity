Status: WORKING (polish round 4, resumed)
Archive: no
Branch: claude/hollowmere-polish-4 (worktree m5-next-639689)
Parked branches: claude/stall-lods 2486f8a6 and claude/backdrop-shade aa70f707 merged in full (via this branch); nothing retired.
Rounds: 3 art rounds (GPU, SC_CAPTURE_ONLY), then one full set (126 Hollowmere PNGs, both presets + LowSun) and stall LOD frames. No capture churn.

1 Backdrop (A12): sheet backdrop_before_after.jpg (Friedrich Riesengebirge beside main and after). ~16/40 -> ~24/40.
  Root cause of the flat dark Mawrrow: the mountain grid's triangle winding gave downward normals, so the key light never reached it (it showed only through back faces; the backdrop-shade vertex bake could not help). Fixed winding, spur/gully erosion + fine crag octave, neutral cool rock, haze 0.32-0.42 with less on the upper slopes, shader key/fill controls (_KeyWrap/_KeyBias/_KeyBoost/_SkyFillDir/_FillBoost/_HazeTop*, defaults keep the ridge layers as they were). Windmill: pale tower / dark timber sails, wider wrap and fill (camera looks into the sun, so its faces are backlit).
  Short of 25: the mountain's lit planes are a smooth mid-grey; a painted warm/cool split across planes would be the next step.
2 Terrace stairs: sheet stairs_before_after.jpg (worn slabs ref beside main and after; play-rig cameras Cap_HM_TerraceDown/Up: pivot 1.5 m, boom 5 m, 15 deg down, FOV 50). ~14/40 -> ~24/40.
  Cause: each LaneStep tread was three 0.19 m sett rows, and the long row joints read as planks; the pale nosing strip added another band. Now builder-made full-depth slabs (staggered joints, chamfered nosing, collider unchanged), a jointless Poly Haven stone (worn_rock_natural_01, CC0, graded into B1 as step_stone), darker risers, a contact band, and a polished-front/grimed-back tone ramp so each step shows a light nosing line from above.
  Short of 25: slabs read clean and new (little moss or edge damage).
3 Stall LODs: sheets stall_lods_sheet.jpg (switch-distance pairs; terrain hides 4 of 6 at switch 2) and stall_lods_close_sheet.jpg (each LOD forced at close range, front and back). Found: after the LOD FBX merge the builder built every stall empty (kept only an exact state-name child) - fixed 5bc66798. PC lodBias 2, heights 0.25/0.125/0.01: LOD1 at 25-37 m, LOD2 at 50-75 m. No holes; pair diffs tiny (the one outlier was falling leaves).
  Close forced-LOD frames: no holes (the LOD2 cloth-hole fix holds), no smears, no missing parts; LOD1/2 a touch darker on front faces (only seen from 25 m+). Not done: no change to the import heights.
Follow-ups (low): StallLodCapture restore in finally + hide terrain in transition frames; stale M_LK_StepNosing.mat files are no longer produced (Hollowmere + BeautyCorner folders), delete after checking BeautyCorner; stairs could take moss/edge damage; mountain lit planes could take a warm/cool painted split.

## Polish round 4 (2026-11-03, branch claude/hollowmere-polish-4, Sonnet medium)
Fix list: found (scan, both presets) float 8, sunk 2, interpenetration 7 (likely contact), foliage-vs-building 462 (unfiltered), uv_density 4, uv_stretch 33, z-fight 2 / fixed 0 / deferred 0. Ranked: scan_ranked_round4.md. PAUSED (usage pace) before captures and fixes.
