Title: Wire the host spawner to encounter templates
Target: cloud
Status: pending
Model: sonnet
From: coordinator session 10 (QUEUE.md dive build; follows spec 5a merge 4dee555f)
Status file: Reference/sessions/spawner-wiring/STATUS.md

Brief:
Spec 5a merged (4dee555f): Core/Rooms EncounterTemplate(s), RoomEncounterTable, EncounterBuilder, EncounterValidator, plan docs/superpowers/plans/2026-11-06-encounter-templates.md. Nothing in the live game uses them yet.
Task: make the host-side Cellars spawner (find it from CellarEncounterPlan / CellarEnemyMix / CellarContentsSync usage) place enemies from EncounterBuilder for the 12 pool combat rooms: kinds, spawn cells, wave 0/1 (wave 1 spawns when wave 0 is cleared), party size = players present. Ambush hook: isAmbushRoom = CellarEncounterPlan.AmbushRoom == roomIndex. Bakery set pieces unchanged.
First (reviewer nits from 5a): add a golden test pinning the template pick for a fixed (seed, floor, roomIndex); add a comment that the solo cap uses Base.Spawns.Count while live plan counts vary by twist (Thin/Crowded/co-op extras), and make the wiring respect the validator's caps.
This is host-authoritative networking: read the sc-netcode skill first, state authority in class comments, and get a risk-reviewer (Opus) pass before merge. DiveBot and the dive PlayMode tests must stay green; if seed goldens change, re-pin deliberately and say so in STATUS.

Rules:
- First: `git lfs checkout`, then `bash Tools/cloud/setup.sh --wait` (the Unity editor finishes downloading a few minutes into a new session; earlier sessions that skipped this had no Unity). If Unity still cannot run: push the branch, set STATUS `needs coordinator: PC check` and stop; the coordinator checks on the PC.
- Unity only via `bash Tools/cloud/unity.sh <gate|check|soak> --out <scratchpad>`; gate while working, full check on the final tip (soak if Core/Cellars changes).
- Implementation by Sonnet implementer subagents (pinned agent types, sc-delegate). Follow .claude/rules/session-usage.md. Report via STATUS.md.
- Merge: merge origin/main, check that tip, merge the passing tip into fresh main (merge commit, no PR, never force-push). Never edit CURRENT.md or tick QUEUE.md.
- User guardrail (2026-11-06): dungeon one must not get extremely complex or overly difficult.

Done when: merged with green check + soak, risk review no blockers, STATUS DONE with merge commit and test counts, Archive: ready.
