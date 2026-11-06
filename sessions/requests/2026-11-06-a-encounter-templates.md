Title: Spec step 5a: encounter templates for the 12 v2 rooms
Target: cloud
Status: pending
Model: sonnet
From: coordinator session 10 (QUEUE.md "Ready: dive build" top item)
Status file: Reference/sessions/encounter-templates/STATUS.md

Brief:
Read first: docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md (signed off; section 12 step 5, and the decisions on encounters), docs/ROOM_METRICS.md, Core/Rooms (CellarRoomsV2, RoomDataV2, RoomValidatorV2, FightSpace), the plan 3/plan 5 dive code (DiveSession, floor modifiers) and docs/superpowers/handoff/QUEUE.md rules. Cellars = tier 1 enemies only (rough tutorial).

Task: encounter templates for the 12 v2 rooms: which enemy groups spawn where (spawn points, wave count, party-size scaling for 1-4 players), within each room's FightSpace budget, deterministic per seed, validated by RoomValidatorV2 (or a new validator rule).

Phase 1 (plan first): write docs/superpowers/plans/2026-11-06-encounter-templates.md (tasks, tests, files), commit and push it on your branch, then set the board profile needs: coordinator with note "plan ready" and WAIT for the coordinator's go (board send) before writing code.
Phase 2: implement with Sonnet implementer subagents (pinned agent types only, sc-delegate; 2-3 tasks per run). Pure logic in Core with EditMode tests. Anything under Core/Cellars needs `unity.sh soak` before merge.

Rules:
- New worktree: run `git lfs checkout` first.
- Unity only via `bash Tools/cloud/unity.sh <gate|check|soak> --out <scratchpad>`; gate while working, full check on the final tip.
- Builders only (never hand-edit .unity/.prefab).
- Follow .claude/rules/session-usage.md. Report via board profile, not messages.
- Merge: merge origin/main, run check on that tip, then merge the passing tip into fresh main (merge commit, no PR, never force-push). Never edit docs/superpowers/handoff/CURRENT.md or tick QUEUE.md (the coordinator does).
- reviewer (Sonnet) review before merge; no networking expected; if you touch NetworkBehaviours or save data, use risk-reviewer.

Done when: merged with a green check (+ soak if Core/Cellars changed), STATUS.md says DONE with merge commit and test counts, Archive: ready.
