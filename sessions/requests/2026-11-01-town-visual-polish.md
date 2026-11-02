Title: Town visual polish: the cheap, high-read batch from the trailer review
Target: cloud
Status: done (fd8c29a5)
Model: sonnet
From: the user, via the World traversal scan session (claude/great-lovelace-r22yqw); review on main 8e434b3
Status file: Reference/sessions/town-visual-polish/STATUS.md

Brief:
The user's trailer review of Hollowmere is on main: `Reference/qa/visual/TOWN_VISUAL_REVIEW.md`, with key frames in `Reference/qa/visual/frames/` (a1-a3 = the square and the heroes' home, b1 = the cottage, b2 = the Cellars). Read part A and part B first. The town reads "polished but empty"; this session takes only the cheap fixes with the biggest visual gain. No new systems, no ambient life (that is a separate request), no open-world work.

Do, in this order, each as small builder changes (never hand-edit .unity/.prefab; scenes come from the editor builders, ProjectBuilder.RunAll):
1. A5: flat, untextured colour blocks that read as placeholders.
   - the bright red square on the heroes'-home annex upper storey (frames a3, Trim1 26.5-34 s)
   - the blue bar and red rectangle on the cottage front (b1, Trim2 4-6.5 s)
   - the oversaturated red shutter

   Find what they are (grep the builders: HollowmereBuilder.cs, HeroesHomeArt.cs, HollowmereHomeKits.cs, HollowmereArt.cs). Give them the painted-wood or sign material from the style bible (docs/PHASE1.md 3.1-3.2), or tone them to the muted palette.
2. A7: the hard-edged dark green rectangle with grass and drain grates in front of the heroes' home (a3, 31.5-34 s). Blend or remove it.
3. A3: crack weeds are an even sprinkle of identical tufts, at the same density in Ruined and Rebuilt.
   - Cluster them along walls, kerbs and cracks.
   - Thin the open centre.
   - Cut density in the Rebuilt preset (a tended town).

   Source: HollowmereFoliage.cs (and sc-foliage).
4. A4: ivy reads as flat lime cards in evenly spaced columns. Lower value and saturation to the 3.1 palette, vary size, spacing and silhouette, and add a trailing or hanging form (sc-foliage). Frames a1 (4-11.5 s), b1 (6.5 s).
5. B1: Cellars dust motes render as hard white squares (b2, Trim2 22 s): fix the particle material or texture.
6. B2: greybox leftovers visible in the undercroft (b2, 35 s): flat white unlit discs on the floor, a red glowing strip under a beam, a dark box. Find their builder and hide or replace them with art.

Rules:
- Follow `.claude/rules/session-usage.md`.
- Sonnet subagents run Unity.
- Captures only with `SC_CAPTURE_ONLY` and the fewest cameras that show the change. Cloud captures are slow, and Hollowmere captures OOM in the cloud, so prefer one reduced camera, or report "needs a PC capture" to the coordinator.
- At most 3 art rounds; score with `sc-asset-review` / `sc-scene-look`.
- M6 S4/S5 (`sessions/requests/2026-11-01-m6-s4-s5.md`) also works in town code (placement mode, the baker at his door). Merge origin/main before your check. If builder output (.unity/.mat) conflicts, take main's side, run one `build`, commit, then `check` (session-usage "Checks and merges").
- The traversal regression test (TraversalRegressionTests) must stay green. If your dressing moves blocked/narrow counts on purpose, re-run `TraversalScan.RunAll` + `WriteBaseline` and commit the baseline with a note.
- Merge on a green `bash Tools/cloud/unity.sh check` (merge commit, no PR, never force-push).

Done when: items 1-6 are merged with a green check, before/after frames are in Reference/sessions/town-visual-polish/, STATUS says DONE with the commit, and anything left (bigger items A1, A2, A6, A8-A14) is listed for the coordinator.
