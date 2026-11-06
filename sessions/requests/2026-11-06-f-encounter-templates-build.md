Title: Encounter templates phase 2: build (plan approved)
Target: cloud
Status: done (merged 4dee555f)
Started: 2026-11-06T16:54Z (cloud launcher)
Model: sonnet
From: coordinator session 10; continues request 2026-11-06-a (its session wrote the plan and stopped for the go; launcher sessions can't take messages)
Status file: Reference/sessions/encounter-templates/STATUS.md

Brief:
Continue branch `encounter-templates` (tip 0e8376e0 or later). Plan: docs/superpowers/plans/2026-11-06-encounter-templates.md on that branch. Read it first, then the original request sessions/requests/2026-11-06-a-encounter-templates.md for the rules.

Coordinator GO, with answers:
- Q1 yes: the 12 pool combat rooms; bakery set pieces keep their authored guard.
- Q2 separate: no host spawner wiring here (it gets its own [net] request).
- Q3 yes: leave out Elite arena and Hunter.

Change these in the plan before coding (commit the plan edit first):
(a) User guardrail, 2026-11-06: dungeon one must not get extremely complex or overly difficult.
  - Floors 1-2 use one-wave templates only (no Reinforced) and at most 2 kinds.
  - Solo live count per room must not exceed today's CellarEncounterPlan count for that room by more than 1.
  - Per-player adds must not exceed today's CoopScaling.ExtraEnemiesPerRoom (+1 per extra player).
  - Add tests that pin both limits.
(b) Ambush: plan 5 folded Ambush into the floor modifiers. Don't roll an Ambush template once per floor on its own. Either use it only on the room the Ambush modifier already picks, or leave it out and note the hook for the wiring request. Keep the rule of no Ambush on a floor with a hazard.
(c) Seed key: cast before shifting, ((ulong)floor << 32) | (uint)roomIndex.

Then do runs A, B and C as planned:
- Sonnet implementer subagents, 2-3 tasks per run.
- `unity.sh gate` while working.
- `unity.sh soak` and full `unity.sh check` on the final tip, after merging origin/main.
- Sonnet reviewer, then merge the passing tip into fresh main (merge commit, no PR, never force-push).
If this cloud session has no Unity licence (unity.sh can't run): use `python3 Tools/cloud/sc_cloud_compile.py` and `sc_core_tests.py` instead, push the branch, set STATUS `needs coordinator: PC check` and stop; the coordinator runs check + soak on the PC and merges.
Never edit CURRENT.md or tick QUEUE.md. When done, set both request a and this request to done.

Done when: merged with green check + soak, STATUS DONE with merge commit and test counts, Archive: ready.
