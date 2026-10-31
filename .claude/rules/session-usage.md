# Session usage rules

From the working sessions' own usage reviews (2026-10-31: bell tower, bakery and foliage sessions; side notes `usage-waste-answers.md`). Each one names where the spend went. The user paces every 5-hour window at about 0.3% a minute, so waste here stops other work.

## Sessions and agents (about 40% of the spend)
- One job per fresh session. A finished session sets `Archive: ready` and stops; it never takes the next job (the coordinator starts a new session).
- The session's own turns plan, score and merge. Generators, render loops, Unity runs and fixes go to Sonnet subagents (`model: sonnet`) from the first step; scans, logs and mechanical edits go to Haiku (`model: haiku`). Don't do art loops in the controller.
- One fresh subagent per step (build, capture, check) with a short brief. Don't resume a big agent: every resume re-reads its whole transcript. Stop an agent as soon as it reports.
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
- Merge origin/main, regenerate `Hollowmere.unity` with the builder (a `-nographics` build), commit, then run one `check` right before merging.
- If main then gains only commits outside `Assets/`, `Packages/` and `ProjectSettings/`, merge without re-checking. If it gains Asset changes, merge main once more and re-check.
- Never commit materials or scenes written by a graphics capture run: they differ from `-nographics` builds (the MotionVectors pass flip) and break the check marker.
