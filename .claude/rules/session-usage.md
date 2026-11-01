# Session usage rules

From the working sessions' own usage reviews (2026-10-31: bell tower, bakery and foliage sessions; side notes `usage-waste-answers.md`). Each one names where the spend went. The user paces every 5-hour window at about 0.3% a minute, so waste here stops other work.

## Sessions and agents (about 40% of the spend)
- One job per fresh session. A finished session sets `Archive: ready` and stops; it never takes the next job (the coordinator starts a new session).
- The session's own turns plan, score and merge. Generators, render loops, Unity runs and fixes go to Sonnet subagents (`model: sonnet`) from the first step; scans, logs and mechanical edits go to Haiku (`model: haiku`). Don't do art loops in the controller.
- One fresh subagent per step (build, capture, check) with a short brief. Don't resume a big agent: every resume re-reads its whole transcript. Stop an agent as soon as it reports.
- In every new worktree run `git lfs checkout` first: a fresh worktree holds LFS pointer stubs and the first Unity run fails on texture import (cost the Q12 baseline run).
- Run subagents in their own git worktree, so the controller checkout stays clean and the stop hook doesn't loop on uncommitted changes.
- Read files by grep and line range, never whole. Read long Unity logs through a subagent summary.
- Batch STATUS lines into real commits; no STATUS-only commits between milestones.

## Captures and renders (about 25-30%)
- Iterate with `SC_CAPTURE_ONLY=<cameras>` (only the cameras that show the change). Shoot the full capture set once, for the final sheet, when the code is final for that round.
- Before any 40-minute run, prove the risky piece with one reduced capture of the camera that shows it.
- Cloud captures are software-rendered (about 40 minutes a set); heavy capture rounds go to a local PC session (GPU) when one is free.
- Blender previews: 960-1000 px, about 20 samples, until the final round. At most 3 art rounds.
- Reuse the shared helpers instead of writing new ones: `Art/Blender/tools/sc_ab_sheet.py` (reference-beside-render sheet), `Art/Blender/tools/sc_asset_audit.py` (FBX audit), `sc_imgdiff.py`.
- Export FBX with `path_mode='STRIP'`: absolute texture paths resolve only on the export machine and cost four capture runs once.
- Wait on Unity with one blocking command (`--timeout 90` or `120` for capture runs), never by polling in turns.

## Checks and merges (about 20%)
- Merge origin/main, then run one `check` right before merging. Builds are deterministic since c4d19fa (two builds in a row, and graphics vs `-nographics`, write the same bytes; `Tools/cloud/sc_churn_check.sh` proves it), so a separate regenerate run is needed only when the merge conflicts in builder output (`.unity`/`.mat`): take main's side (`git checkout --theirs`), run one `build`, commit, then `check`. Never hand-merge those files.
- If main then gains only commits outside `Assets/`, `Packages/` and `ProjectSettings/`, merge without re-checking. If it gains Asset changes, merge main once more and re-check.
- Graphics capture runs now write the same material and scene bytes as `-nographics` builds (BuildAll settles every material with URP's ShaderGUI validation; `MaterialValidationTests` fails on any material a graphics editor would rewrite). Verified for the graphics build and the DungeonCorner capture; Hollowmere and BeautyCorner captures were not re-run (Hollowmere OOMs in the cloud). If a capture still leaves `.mat`/`.unity` changes, don't commit them: run one `build` and report the file to the coordinator as a churn bug.
