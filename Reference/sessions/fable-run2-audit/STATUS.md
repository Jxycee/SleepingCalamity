State: done
Archive: ready
Branch: claude/fable-run2-audit-3315c0
Merged: Fable Run 2, the save and netcode risk audit. Findings file docs/superpowers/audits/2026-11-12-save-netcode-audit.md: 2 blockers, 9 should-fix, 12 notes. Every save section and every networked system is covered (section 2 of the file).
Blockers:
- A1: online crash and rejoin. A player whose game crashed relaunches and joins again before the host has timed the old connection out; the host kicks the old connection, and the disconnect handler then frees that same player's Lobby seat, so the player who just rejoined is removed. Confirmed in code by the Opus risk review; one live Relay check is in USER_TODO item 3a.
- A2: a friend who only visits a multiplayer world's lobby is written into the world at once, in the first lobby too, and nothing can remove them, so four visitors lock the world. A decision for the user (record at Start, or add a remove action).
- Next most serious (should-fix, a decision for the user): A3, a multiplayer player whose sign-in id changes (new PC) loses their character, and a host on a full world can no longer start it.
Tests: three failing tests pin A1 and A2, marked [Ignore("AUDIT ...")]: HostWorldTests.JoinerWhoLeavesBeforeStart_IsNotRecorded, HostWorldTests.FirstLobbyVisitor_OfANewWorld_IsNotRecorded, UgsSeatTests.Takeover_OldClientDisconnect_DoesNotFreeSeatOfSameKey.
Check: check PASS on 31a525b3 (EditMode 1703 pass, 3 skipped; PlayMode 250 pass, 28 skipped).
Reviews: one Opus risk-reviewer pass on the blockers (A1 and A2 confirmed, A3 demoted, A11 added).
Also landed: new rules in sc-netcode and REVIEW.md, eight queue items plus four user-decision bullets in QUEUE.md, the Relay walk steps in USER_TODO item 3a.
Cost: weekly Fable 27% to 43%, weekly all models 56% to 65%, 5-hour window 4% to 77%. Lessons are in section 6 of docs/superpowers/plans/2026-11-11-fable-reset-plan.md.
Unverified: A1's two Relay questions (does the Lobby let the same player join twice; how fast the kick arrives); A4 path 2 and A10 need the 2-player walk.
