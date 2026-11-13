Title: Audit fixes (resume after usage limit)
Target: cloud
Status: started
Started: 2026-11-13 16:55 UTC (cloud launcher)
Model: sonnet
From: coordinator session 11; the first cloud run stopped on the 5-hour limit at ~8:45 AM CST 2026-11-13
Status file: Reference/sessions/audit-fixes/STATUS.md

Brief:
Continue the audit-fixes job. The full brief is the `Brief:` section of `sessions/requests/2026-11-13-a-audit-fixes.md`: read it first and follow it.
Do NOT start over: check out the existing branch `claude/audit-fixes` (tip 71340f80 or later) and continue from it.
Already on the branch: A1 takeover fix (aa08d4a4), A2 record-on-play (d03fcb66), A2 + A9 host can let a player go and the takeover test (981f00ba), A2 start-gate words (71340f80).
Left, in order: A3 (host rename "this player is now that player", reusing the Solo rebind), A5 ("This world can't be saved. Play anyway?"), un-ignore the AUDIT tests, `bash Tools/cloud/unity.sh gate`, risk-reviewer, branch-reviewer, `check` on the tip, merge (sc-merge).
Merge order: PL-4 (`claude/pl4-way-out`) also edits `App/AppBootstrap.cs` and `Editor/Builders/SceneBuilder.cs`. If PL-4 merged first, merge origin/main into your branch, resolve by hand, re-run `check`, then merge.
Don't pause for the 5-hour pace: if the limit stops you again, push what you have and the coordinator restarts you.
When done, set this file and the `-a-` request file to `Status: done` with the merge commit.
