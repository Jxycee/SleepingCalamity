Title: Dungeon-1 difficulty check: modifiers on/off + DiveBot Tough 12345
Target: cloud
Status: done (merged; party-size fix e9e7d6db, modifiers within noise, Tough 3/3 seeds clear; see STATUS)
Started: 2026-11-06T20:15Z (cloud launcher)
Model: sonnet
From: coordinator session 10 (QUEUE.md: modifier difficulty check S + DiveBot Tough Seed12345 S)
Status file: Reference/sessions/dungeon1-difficulty/STATUS.md

Brief:
1. Plan 5 floor modifiers are approved with a guardrail: dungeon one must not be extremely complex or overly difficult. Measure: DiveBot solo runs (and one 2-player follower run if cheap) with plan 5 modifiers on vs off over a few seeds; report clear rate, deepest floor, time per floor, deaths by cause. If modifiers add a lot of difficulty, soften in the single tuning spot (e.g. floor 7 at most one hazard; Stirred's latch ends at the next stair) and re-measure. Keep it to placeholders in FloorObjectiveTuning / the modifier tables; no new systems.
2. DiveBot Tough Seed12345: the bar needs 2 of 3 seeds finished (docs/superpowers/specs/2026-11-05-queue-triage-notes.md ~:67). Run it, report, close or file.
Encounter templates (4dee555f) are not wired into the spawner yet (a parallel session does that); measure the live game as it is on main.
Write results in STATUS (table). Bot runs are long: use the soak/category the DiveBot tests already use, one run at a time.

Rules:
- First: `git lfs checkout`, then `bash Tools/cloud/setup.sh --wait` (the Unity editor finishes downloading a few minutes into a new session; earlier sessions that skipped this had no Unity). If Unity still cannot run: push the branch, set STATUS `needs coordinator: PC check` and stop; the coordinator checks on the PC.
- Unity only via `bash Tools/cloud/unity.sh <gate|check|soak> --out <scratchpad>`; gate while working, full check on the final tip (soak if Core/Cellars changes).
- Implementation by Sonnet implementer subagents (pinned agent types, sc-delegate). Follow .claude/rules/session-usage.md. Report via STATUS.md.
- Merge: merge origin/main, check that tip, merge the passing tip into fresh main (merge commit, no PR, never force-push). Never edit CURRENT.md or tick QUEUE.md.
- User guardrail (2026-11-06): dungeon one must not get extremely complex or overly difficult.

Done when: results table in STATUS, any softening merged with green check (+ soak), STATUS DONE, Archive: ready.

Added by coordinator after spawner wiring merged (edf1d82d): risk review follow-up 1 says party size does not change enemy count (CellarTemplateSpawns trims to the plan count, which ignores party size) and wave 1 survives only when plan count >= 4. Settled rule: difficulty scales to the players present. Measure it in the bot runs and propose the fix (or make it, if small and Core-only).
