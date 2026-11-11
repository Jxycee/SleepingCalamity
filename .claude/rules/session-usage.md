# Session usage rules

From the working sessions' own usage reviews (2026-10-31: bell tower, bakery and foliage sessions; side notes `usage-waste-answers.md`). Each one names where the spend went. The user paces every 5-hour window at about 0.1-0.15% a minute (weekly allowance must last to the Sunday reset), so waste here stops other work.

## The game comes first
Process was crowding out the game (2026-11-11 audit): about 2,075 commits in 13 days, the most common topic "handoff" (124), plus queue (36), session request (22) and STATUS (20), and about 40% of spend on session overhead. Bookkeeping exists to keep game work moving; keep it to the minimum that does that.
- **Bookkeeping rides along with real work.** A `CURRENT.md`, `QUEUE.md`, STATUS or session-request edit goes into the commit of the work it describes (the merge commit, the task commit, or a session's final push), never into a commit of its own. One exception: the controller may push one bookkeeping commit at the end of a session if nothing else carried it.
- **Update `CURRENT.md` when something lands on `main` or the user decides something**, not after every step. In-progress state lives in the session board profile, which costs no commit.
- **Fewer, bigger sessions.** A session takes a batch of related work that lands together (one M item with its S follow-ups, or 3-5 related S items), not a single small item. Each new session pays for a brief, a STATUS file, a request, a context build-up and a handoff.
- Tooling, skills and process work stays at about one session in five unless it unblocks game work (`sc-steward`).

## Sessions and agents (about 40% of the spend)
- One job (one batch, above) per fresh session. A finished session sets `Archive: ready` and stops; it never takes an unrelated next job (the coordinator starts a new session).
- The session's own turns plan, score and merge. Generators, render loops, Unity runs and fixes go to Sonnet subagents (`model: sonnet`) from the first step; scans, logs and mechanical edits go to Haiku (`model: haiku`). Don't do art loops in the controller (exception: hero-character art loops, which run in an Opus session).
- One fresh subagent per distinct step (build, capture, check) with a short brief. Send review findings for that step back to the same implementer (`sc-delegate`), but don't resume a large finished agent for new work: every resume re-reads its whole transcript. Stop an agent as soon as it reports.
- In every new worktree run `git lfs checkout` first: a fresh worktree holds LFS pointer stubs and the first Unity run fails on texture import (cost the Q12 baseline run).
- Run subagents in their own git worktree, so the controller checkout stays clean and the stop hook doesn't loop on uncommitted changes.
- Read files by grep and line range, never whole. Read long Unity logs through a subagent summary.
- STATUS files change only when the state changes (started, blocked, waiting on the user, done), and the change goes into the next real commit. Progress in between goes to the board profile.
- Report status through the session board, not by message: `board` action `profile` (state, tip, check, next, waitingOn, holdsPort, needs). A message to the coordinator starts a turn on its large context. Message only when a decision is needed.
- Every subagent report starts a turn on the session's whole context (plan 3, 2026-11-04: ~2,650 messages, a big Sonnet turn per "task N done"). So give each implementer run 2-3 related plan tasks with one report, and don't take a turn just to say "still waiting".
- Context handoff (user). At about 250k tokens of context (local: `get_usage` self; cloud: the compaction warning), hand off at the next milestone boundary, not mid-step:
  1. Finish the current step and push your branch.
  2. Write the successor's request in `sessions/requests/` from the template in its README, with the full handoff in the brief, and set your STATUS to `Archive: ready` and `Handoff: <request file>`, in one commit.
  3. Start the successor with the launcher routine (`RemoteTrigger run trig_01Rj58EHgmhr5QziTTwy2zee`), or report "handoff request <file> pushed" to the coordinator if you can't run it.
  4. Stop. The user archives the old session.

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
- Once the tip has a passing `check` (`SC MARKER` equals the tip), merge that tip into a fresh `origin/main` directly if git merges it cleanly, even if main moved since the check (user, 2026-11-01: re-check loops stalled everything). No merging main into the branch again and no second check. If git can't merge it cleanly, merge main into the branch, resolve, and check the new tip.
- Graphics capture runs now write the same material and scene bytes as `-nographics` builds (BuildAll settles every material with URP's ShaderGUI validation; `MaterialValidationTests` fails on any material a graphics editor would rewrite). Verified for the graphics build and the DungeonCorner capture; Hollowmere and BeautyCorner captures were not re-run (Hollowmere OOMs in the cloud). If a capture still leaves `.mat`/`.unity` changes, don't commit them: run one `build` and report the file to the coordinator as a churn bug.
