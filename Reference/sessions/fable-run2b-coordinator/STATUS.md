State: DONE
Archive: ready
Session: Fable Run 2b - coordinator playbook (sc-coordinator), local PC, Fable 5.2 high
Branch: claude/fable-coordinator-playbook-eec554, merged into main as "Merge Fable Run 2b: sc-coordinator playbook" (docs only, no Unity check needed)
Started: 2026-11-13 ~6:25 PM CST (5-hour 74%, all-models weekly 26%, Fable weekly 42%). At the merge: 94%, 29%, 47%.

## Result
`.claude/skills/sc-coordinator/SKILL.md`, about 2,200 words (the target was 1,500-2,000; every part of the coverage list is in). One Sonnet `reviewer` pass: no blocking findings, six should-fix items applied.

Outline:
1. The job: coordinate, don't build; game first (sc-steward ranking); usage police; small context; hand over at 90%; taking over.
2. Starting work: batch size; local vs cloud (no hybrid); when `start_session` is refused and what to do; launcher + request file; Sonnet medium; brief additions (STATUS, file-ownership list, shared wiring files); model routing; how to start a Fable run.
3. Running work: no polling; the board (role unclaimed during the rework); naming sessions for the user; one writer per branch; restart after a usage limit; cloud lines for every brief; PC Unity; archiving.
4. Usage: run into the limit, no pace pauses; the one exception; pace config; limits and resets; unexplained jumps; workday break.
5. Merging and bookkeeping: sc-merge, the user's go, merge order, verify on main, CURRENT.md only on merges, decisions and session end.
6. The user: AskUserQuestion, status lines, Central time, leeway, taste, licence bar, merges are yours.
7. Failure modes seen, with the fix for each (12 rows).

Wired in: `sc-steward` section 1 opens with "Read `sc-coordinator` first". Nothing was removed from `sc-steward`: the ranking stays there and the playbook points to it.

## Memories trimmed (outside the repo, edited in place)
To a pointer plus what is still unique: coordinator-handoff, coordinator-focus-usage-police, coordinator-lessons-2026-11-01, coordinator-on-opus-ok, usage-pace-rate, session-launcher-routine, spawned-session-model, name-sessions-for-user, archive-finished-sessions. board-watchdog: stale pace line fixed, the rest kept (plugin details). New pointer memory: sc-coordinator-playbook. Left as they are (they apply to every session): merge-policy, no-hybrid-sessions, decisions-have-leeway, ask-via-tool, progress-updates, timezone-central.

## Instruction-file edits (the user's OK, 2026-11-13 evening: "Yes, apply both")
- `CLAUDE.md` "Running handoff": a session taking the coordinator role reads the `sc-coordinator` skill first.
- `.claude/rules/session-usage.md` intro: the old 0.1-0.15% a minute pacing sentence replaced by the current rule (run into the limit, restart after the reset, no pace pauses).
Merged into main as "Merge Fable Run 2b follow-up: CLAUDE.md pointer, usage rule".

## For the coordinator
- `CURRENT.md` "Now" (2026-11-12 entry) says "weekly-line pauses stay" and Next item 5 says "Pace 0.1-0.2%/min". Both are older than the 2026-11-13 rule; align them with the next merge.
- `sc-delegate` forbids `general-purpose` outright; the playbook allows it with an explicit cheaper model (agents that need web or MCP tools, as Run 3 and this run used). Align `sc-delegate` or the playbook.
- Ask the user: does the workday break (2026-11-03) still stand? On 2026-11-13 sessions were kept running through a workday. The playbook says "ask before stopping anything for it".
- The 2026-11-01 note "launcher runs get no working Unity" looks outdated (PL-4 launcher runs passed `gate` on 2026-11-13); confirm on the next launcher run that needs a full `check`.

## For the board-rework session
The board's `claim` action should tell the new coordinator to read the `sc-coordinator` skill first (plan Run 2b: "`sc-steward` and the board's `claim` point to it"). The plugin was not edited here.
