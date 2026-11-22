---
name: sc-handoff
description: Update docs/superpowers/handoff/CURRENT.md, the running SleepingCalamity handoff on main, when something lands on main or the user decides something (merge, user decision, session end). Keeps it a short, evidence-cited state file.
---
# Updating CURRENT.md

`docs/superpowers/handoff/CURRENT.md` is the project's working memory between sessions. Only the controller on `main` edits it (a PC or a cloud session, one at a time); feature branches never do.

Keep it a **state file under 300 lines and about 4,000 tokens** (`wc -w` under ~2,800 words). Claude Code re-reads files after compaction only up to 5,000 tokens, so a bigger file drops out exactly when it is needed.

## Sections (in this order)
1. **Now:** date, the one-paragraph status, and what the user is doing or owes.
2. **Next:** the next three actions, in order.
3. **Branches and worktrees:** each open branch, its state (in progress / in review / green, waiting for merge), and its last check result with the commit.
4. **Blockers and open questions:** what is waiting on whom.
5. **Read next:** pointers (paths) to the design docs, plans and checklists that matter right now.
6. **Inbox:** ideas and requests parked during sessions (sc-steward), one line each with who asked and the date. Promote one into Next, or drop it, when a session card is made.

## Rules
- Edit in place; don't rewrite sections that didn't change.
- Every status claim cites its evidence: a commit, a check result line (`SC RESULT check PASS`, test counts), or the user's words with a date. No claim without one.
- History, tooling inventories, finished work and lessons don't belong here. Move them to a dated file in `docs/superpowers/handoff/` (history) or to `CLAUDE.md`, a skill or `.claude/rules/` (lasting rules), and leave a one-line pointer.
- If the file is over the cap, prune it before adding anything.
- **When:** a merge into `main`, a user decision, or the end of a controller session. Not after every step: in-progress state goes to the session board profile.
- **Commit it with the work it describes:** inside the merge commit (`sc-merge` step 4) or the task commit. Only when nothing else carries it, push one `handoff: <what changed>` commit at the end of the session.
- "Now" is one paragraph about today, not a stack of dated entries: when you write a new one, move the old paragraph to the dated history file.
