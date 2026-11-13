Title: Audit fixes: gate, risk review, check, merge on claude/audit-fixes (tip 154bebf2)
Target: local
Status: started (local session, coordinator 2026-11-13)
Model: sonnet
From: coordinator session 11, 2026-11-13 ~12:20 PM CST; request f stopped because the cloud Unity editor was still installing
Status file: Reference/sessions/audit-fixes/STATUS.md

Brief:
All code is written on `claude/audit-fixes` (tip 154bebf2 or later): A1, A2, A3, A5, A9 plus the full-world lobby send-away, waiting words and Start words (user's call, option a). Only checks and the merge are left. Read the branch's STATUS first.
1. `git lfs checkout`, then `powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>ToolsNity.ps1 gate -OutDir <scratchpad>` (run_in_background, wait for the completion notice).
2. One `risk-reviewer` pass on `git diff 20ccd4de..154bebf2`; fixes back to one implementer.
3. Merge origin/main into the branch (PL-4 may merge first: App/AppBootstrap.cs, SceneBuilder.cs can conflict; resolve by hand; builder output .unity/.mat: take main's side + one `unity.ps1 build`). Then `<worktree>ToolsNity.ps1 check -OutDir <scratchpad>` on the tip (SC MARKER = HEAD).
4. Merge into main (sc-merge, merge commit; pre-approved once check is green and the risk review has no blockers). Tick the audit queue items; set requests -a-, -c-, -f- and this one to done in the merge commit.
Known gap, accepted for now: the stranger's own front door cannot show the waiting words in real play (no host-to-guest channel; FrontDoorHost.WaitsForPlace is false). Add one QUEUE item for it ([net], S) instead of building a channel.
Local PC session: PlayMode takes the machine lock (`SC WAIT` normal). Don't pause for the 5-hour pace. Never commit Linux float churn.
