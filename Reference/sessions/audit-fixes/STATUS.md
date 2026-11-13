Status: DONE (merged to main 2026-11-13, check PASS at aa48247a: EditMode 1735, PlayMode 255 pass)
Update: PlayMode save-test failures fixed at 70165efd (tests started hosts without the start gate; 24/24). Remaining: HostClientTests.FifthUniquePlayer_IsRefusedByApproval pins the old refusal, follows the user rule call; then one full check on the tip.
Started: 2026-11-13T13:24:21Z (cloud launcher)
Branch: claude/audit-fixes

## 2026-11-13 user chose (a): keep the full-world lobby entry, add send-away + waiting words + Start words (implemented, not yet gated; no Unity editor in cloud)
(Earlier, now answered) Status: BLOCKED
Branch claude/audit-fixes tip 31beda5e (A1, A2, A3, A5, A9 done; gate PASS 1731 at 89cf95c7; full `check` on the tip was started; risk review fixes applied). NOT merged.
Branch-reviewer: no code blockers, but one should-fix changes a settled rule: commit 89cf95c7 lets a 5th (unrecorded) key into the lobby of a full (4-recorded) world before the start, so A3's rename works for a friend whose id changed. Cost: a fifth friend can sit in the lobby, every Start is refused ("world full"), and the lobby has no "send away" button. CLAUDE.md says once 4 are recorded nobody else can join.
User choice: (a) keep the lobby entry and add a host "send away" button + waiting words for the stranger; (b) revert to refusing (rule stays firm; rename then only works on a world with a free place).
Also open: stale comments (ConnectionApprover.cs:12, HostWorld.cs:13-14), toast wording UiText.cs:39, lobby per-frame allocations (nits).

## 2026-11-13 BLOCKED (no Unity editor in cloud)
Tip 154bebf2 has send-away, waiting words, Start-refused words, nits. Needs: licensed gate/check (new tests FrontDoorModelTests x2, HostWorldTests x1, PlayMode NoHost fake), one risk-reviewer pass on `git diff 20ccd4de..154bebf2`, merge origin/main, merge. Open: FrontDoorHost.WaitsForPlace is false in real play (no host-to-guest channel), so the stranger's own door cannot show the waiting words yet.

Archive: ready
