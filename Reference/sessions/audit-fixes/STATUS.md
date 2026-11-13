Status: IN PROGRESS
Started: 2026-11-13T13:24:21Z (cloud launcher)
Branch: claude/audit-fixes

## 2026-11-13 user chose (a): keep the full-world lobby entry, add send-away + waiting words + Start words (implemented, not yet gated; no Unity editor in cloud)
(Earlier, now answered) Status: BLOCKED
Branch claude/audit-fixes tip 31beda5e (A1, A2, A3, A5, A9 done; gate PASS 1731 at 89cf95c7; full `check` on the tip was started; risk review fixes applied). NOT merged.
Branch-reviewer: no code blockers, but one should-fix changes a settled rule: commit 89cf95c7 lets a 5th (unrecorded) key into the lobby of a full (4-recorded) world before the start, so A3's rename works for a friend whose id changed. Cost: a fifth friend can sit in the lobby, every Start is refused ("world full"), and the lobby has no "send away" button. CLAUDE.md says once 4 are recorded nobody else can join.
User choice: (a) keep the lobby entry and add a host "send away" button + waiting words for the stranger; (b) revert to refusing (rule stays firm; rename then only works on a world with a free place).
Also open: stale comments (ConnectionApprover.cs:12, HostWorld.cs:13-14), toast wording UiText.cs:39, lobby per-frame allocations (nits).
