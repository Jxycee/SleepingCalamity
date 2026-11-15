State: IN PROGRESS (usage limit hit 2026-11-15; WIP committed on branch claude/b3-square-sky-ruin-e1b65b, NOT merged, no check run, no reviewer)
Task: build-plan B3 (square ground, sky rollout, ruin look)

## Done (code compiles, build passes, EditMode 1790/1790 per implementer)
- Morning/Evening town look (HollowmereLook.For/Apply; builder default Morning; HollowmereCapture default Morning+Evening, SC_CAPTURE_SUN accepts any preset name).
- Chimney stack in sc_ruin_kit.py, 9 FBXs re-exported (<10k tris each).
- HollowmereB3Ground.cs (T01c coursed cobble ground on the draped bed, T01b edge band + gutter, leaf/puddle/rut/moss decals), HollowmereRuinDressing.cs (ivy on stack, saplings M/L only, brambles, debris, one crow per lot), terrain meadow variation, old A2 tone patches/stains off, PlazaCobbles row skipped in HollowmereBuilder.
- Captures: scratchpad before/ (Midday baseline), r1, r2, r3 (Morning, Ruined). 3 art rounds used.

## Open (next session)
- Fan-setts island around the fountain still renders in r3 (HomeDoor/Fountain): skipping the PlazaCobbles row did not remove it. Find which Corner row/ring (FountainRing RingBed? CrackWeeds scatter?) still lays setts; DH4 keeps only the 6 m fan ring.
- Sparse single lime tufts in the open middle foreground (Fountain frame): not from ScatterEdges filter; find source (corner CrackWeeds scatter, ThinCornerWeeds).
- No moss in joints, ground still a bit flat/orange-tan; chimneys read as stacked slabs; saplings read like agave in overview (now M/L only, 1 each).
- Not done: A/B sheet vs H1/H3 (sc_ab_sheet.py), palette check, score, SendUserFile, reviewer, gate/check, sc-merge, full capture set, Ruins/SOURCES.md note, STORY wording is controller's.
- Untracked Content/Rendering/_ProbeTmp.meta is a capture artefact: do not commit.
Archive: not ready
