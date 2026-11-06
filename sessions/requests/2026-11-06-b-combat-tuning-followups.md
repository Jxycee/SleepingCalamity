Title: Combat-polish follow-ups + TuningSections TODO
Target: cloud
Status: started
Started: 2026-11-06T16:41Z (cloud launcher)
Model: sonnet
From: coordinator session 10 (QUEUE.md "Ready: bugs and test health", two S items)
Status file: Reference/sessions/combat-tuning-followups/STATUS.md

Brief:
Two related small items (QUEUE.md lines for combat-polish follow-ups and TuningSections):
1. combat-polish follow-ups (read Reference/sessions/combat-polish/STATUS.md lines 13-16): add a parry-during-grace test; move HurtGrace values into TuningSections; remove dead HeroAnimMap.IsHeavyHurt / HurtGrace.Knockdown if truly unused (check with LSP/grep first).
2. TuningSections.cs:101 points at a TODO that no longer exists, and enemy attack ids aren't tunable: fix the stale pointer and make enemy attack-id tuning tunable through TuningSections (or file a clear note if it's bigger than S).

Rules:
- New worktree: run `git lfs checkout` first.
- Unity only via `bash Tools/cloud/unity.sh <gate|check> --out <scratchpad>`; gate while working, full check on the final tip.
- Implementation by a Sonnet implementer subagent (pinned agent types, sc-delegate); reviewer (Sonnet) before merge.
- Follow .claude/rules/session-usage.md. Report via board profile.
- Merge: merge origin/main, run check on that tip, merge the passing tip into fresh main (merge commit, no PR, never force-push). Never edit CURRENT.md or tick QUEUE.md.

Done when: merged with a green check, STATUS.md says DONE with merge commit and test counts, Archive: ready.
