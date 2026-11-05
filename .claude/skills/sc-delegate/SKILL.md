---
name: sc-delegate
description: Brief template and routing for delegating SleepingCalamity work to the project agents (implementer, reviewers, scanner, fixer). Use whenever the controller hands a task to a subagent.
---
# Delegating SleepingCalamity work

## Route by risk
| Work | Agent | Override per call |
|---|---|---|
| Any implementation | `implementer` (Sonnet, medium) | effort `high` for networking or PlayMode tasks; model `haiku`, effort `high` when the brief contains the complete code |
| Review: networking/host sync, save data | `risk-reviewer` (Opus, high) | |
| Review: everything else (gameplay, Core, data, art, presentation, docs) | `reviewer` (Sonnet, medium) | |
| Fix of 30 lines or fewer in one area | no agent: read the diff yourself | |
| Milestone branch before merging into `main` | `branch-reviewer` (Opus, high) | never raise it to xhigh or max (user, 2026-11-03: usage) |
| Locate symbols, inventories, counts | `scanner` (Haiku) | |
| Fails twice, or nobody can find the root cause | `fixer` (Fable) | |
| Docs, design notes, triage, plan drafts | `implementer` (Sonnet, medium) | model `opus`, effort `medium` for a plan section on networking, save schema or a state machine (`high` only if the first draft failed review) |
| Story and book prose | `implementer` | model `opus`, effort `high` (prose only; the bible assembly and fixes stay Sonnet) |

**When the branch review can be skipped (user, 2026-11-03: usage).** Skip `branch-reviewer` when the branch only touches pure Core logic with its EditMode tests, docs, tuning data, test-only code or art, every task passed its own review, and `check` is green on the tip. Always run it when the branch touches networking or host sync, save data, or wires systems together across assemblies (Presentation, Gameplay, App, Networking, builders and scenes): those break at the seams between tasks, which per-task reviews don't see. For the dive roadmap that means plans 1 and 4 skip it, plans 2 and 3 get it. Say in the merge commit which applied.

**Session base model (user, 2026-11-04).** Every working session runs Sonnet 5.5 at medium effort: pass `model: "claude-sonnet-5-5"` and `effort: "medium"` to `start_session`, and fix a proposal card (it inherits the coordinator's model) with `set_session_model` / `set_session_effort` right after it starts. Opus belongs in the pinned reviewers and the Opus overrides above, never in a session's base. The board flags any other session whose base is Opus.

**Models never come from the coordinator (user, 2026-11-03).** Every delegated task names one of the agent types above, whose model and effort are pinned in `.claude/agents/`. Never delegate through `fork` (it always runs on the coordinator's model and effort) or through the built-in `general-purpose`, `Explore`, `Plan` or `claude` agents (no pin, so they would inherit the coordinator). Pass `model` or `effort` only as the "Override per call" column says. Safety net: `.claude/settings.json` sets `CLAUDE_CODE_SUBAGENT_MODEL=sonnet`, the default for any agent without a pinned model.

**Risk reviews per milestone, not per task (user, 2026-11-04: usage).** On a plan with a final `branch-reviewer`, run `risk-reviewer` once per networking or save-data milestone (a batch of related tasks, typically the midpoint where the save schema or net spine lands), not after every task; other tasks get `reviewer` or the controller's own diff read. Without a final branch review, keep one risk review per networking or save-data task.

Do it yourself when it takes a handful of tool calls. Batch 2-3 related same-area tasks into one implementer run and one review (each report costs a session turn on its whole context). One implementer touches Unity at a time (the project lock); reviewers are read-only and can run alongside. Continue an agent with SendMessage instead of spawning a fresh one, but never SendMessage a running workflow agent.

## Brief template
```
Goal: <one sentence, and why it matters for the game>
Branch: <branch to work on / cut from>
Files and symbols: <path:line for each thing to change or read first>
Behaviour: <what must be true afterwards; network authority (host vs owner) if networked>
Tests: <test names and cases; which must fail first>
Out of scope: <what not to touch>
Check: <the Tools/unity.ps1 (PC) or Tools/cloud/unity.sh (cloud) command that proves it; cloud without a Unity licence: sc_cloud_compile.py / sc_core_tests.py plus the tests a licensed run must pass>
Report: what changed, the check result line, anything unverified.
```
Name exact files and symbols so the agent doesn't search. Give the reason behind non-obvious constraints. Put code in the brief only for the tricky parts (math, state machines, RPC sequences), or all of it when routing to Haiku.

## After the agent reports
- Check its evidence (the PASS/FAIL line, the diff), not its summary.
- Send review findings back to the same implementer. Fix blocking findings; decide should-fix ones; note nits for the next push. At most three fix-review rounds (`REVIEW.md`).
- Update `CURRENT.md` with `sc-handoff` when a task lands.
