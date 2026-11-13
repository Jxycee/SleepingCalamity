Title: Audit fixes finish (send-away button, check, merge) on claude/audit-fixes
Target: cloud
Status: blocked: send-away, waiting words and nits coded on claude/audit-fixes (154bebf2), core tests 1271 pass, but cloud has no Unity editor so gate/check unrun; risk review and merge not done; stranger-side waiting words lack a host-to-guest channel (needs user call)
Started: 2026-11-13 18:07 UTC (cloud launcher)
Model: sonnet
From: coordinator session 11, 2026-11-13 ~12:30 PM CST, with the user's call on the full-lobby question
Status file: Reference/sessions/audit-fixes/STATUS.md

Brief:
Finish the audit-fixes job on the existing branch `claude/audit-fixes` (tip 31beda5e or later). The full job is the `Brief:` of `sessions/requests/2026-11-13-a-audit-fixes.md`; read its STATUS for where it stands (A1, A2, A3, A5, A9 done, reviewed, gate PASS).
User decision (2026-11-13, option a, recorded in CLAUDE.md "Players and saves"): KEEP commit 89cf95c7's lobby entry for an unrecorded key on a full world, and add:
1. a host "send away" action for that player in the host lobby (front door, with a confirm step; it disconnects them with a clear end reason and words in UiText);
2. waiting words for the unrecorded player ("This world is full. Waiting for the host to add you.");
3. Start stays refused while an unrecorded player is present, with words that name the two choices (rename them as a friend, or send them away).
Tests fail first: `FrontDoorModelTests.Host_FullWorld_Stranger_SendAway_Disconnects`, `..._Stranger_SeesWaitingWords`, `HostWorldTests.FullWorld_UnrecordedInLobby_StartRefused`. Also fix the nits STATUS lists (stale comments ConnectionApprover.cs:12, HostWorld.cs:13-14, toast wording UiText.cs:39, lobby per-frame allocations).
Then: send the diff of these additions to one `risk-reviewer` pass (the rest was reviewed); merge origin/main into the branch (PL-4 may have merged first: App/AppBootstrap.cs and SceneBuilder.cs can conflict; resolve by hand; builder output: take main's side + one build); `bash Tools/cloud/unity.sh check` on the tip (SC MARKER = HEAD); merge (sc-merge, merge commit; pre-approved once check is green and the risk review has no blockers). Tick the audit queue items and set the -a-, -c- and this request to done in the merge commit.
Don't pause for the 5-hour pace. Never commit Linux float churn.
