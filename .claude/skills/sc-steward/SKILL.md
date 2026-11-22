---
name: sc-steward
description: Runs a SleepingCalamity working session end to end and keeps it on course - opens every main session with a short session card (where we are, what the user owes, the ranked next three, one proposed goal with a done-definition and stop point), guards that goal against drift by parking side requests in the Inbox instead of silently switching, checkpoints progress, and closes with a wrap-up and handoff. Use at the start of every controller (main) session, local or cloud, whenever the user asks "what next" / "where are we" / changes direction mid-session, and before ending a session.
---

# Steering a SleepingCalamity session

The user works fast and in many directions at once (their words, 2026-10-25: "I am all over the place sometimes"). Your job is to **take charge of direction**: propose, hold the line, park the rest, and close cleanly. The user can always override; they should never have to steer alone.

Sources of truth, in this order: the user's words today → `docs/superpowers/handoff/CURRENT.md` (Now / Next / Inbox) → `docs/PHASE1.md` and the active plan under `docs/superpowers/plans/` → `docs/superpowers/handoff/QUEUE.md`.

## 1. Open the session (5 minutes, before any other work)
Read `sc-coordinator` first (once per coordinator session): it is the playbook for running the other sessions; this skill steers your own.
1. Read `CURRENT.md`'s Now, Next and Inbox sections (not the whole file) and `git branch -r` for open branches.
2. Rank candidate work with this order, top first:
   1. anything **blocking the user** or a broken `main` / red check;
   2. the **playable loop**: a person can play town → dive → back to town → place a building, solo and with two players. Until that works end to end, prefer the glue, UI and wiring it needs over new systems or polish;
   3. the current milestone's **definition of done** (`docs/PHASE1.md` and the active plan);
   4. what makes the game **look or play** better at the play positions (the town square, the dungeon entrance, the first floors);
   5. tooling, skills and process. **Cap this at about one session in five** unless it unblocks 1-4 (lesson of 2026-10-25: a whole night of skill work, measured afterwards, mostly bought discipline and speed, not new game; 2026-11-11 audit: process commits and session overhead were crowding out the game).
3. Post the **session card** and ask one question:
```
Milestone: <current milestone and its state, one line>
Since last time: <what landed, with commits>
You owe: <checks, decisions, PC-only steps; "nothing" is fine>
Next three: 1. <...>  2. <...>  3. <...>
Proposal: <one goal> - done when <evidence: a check line, a capture, a merged commit>; stop at <scope edge>; cost <agents / rough $ if any>
Inbox: <n items parked> (top: <one>)
Go with the proposal, pick another, or something else?
```
4. When the user answers, the goal is fixed. Write it at the top of your working notes (or the task list) and work to its done-definition, not past it.

## 2. Hold the goal (during the session)
Classify every new request:
- **On goal** → do it.
- **Quick aside** (under about 5 minutes, no branch change) → do it, then say "back to <goal>".
- **New direction** → one line: *"That's outside today's goal (<goal>). Park it in the Inbox, or switch to it now?"* Default to park when the user doesn't say. Switching is fine: then re-state the new goal and its done-definition, and note what was left half-done.
- **Never** silently expand scope, merge, or start multi-agent work beyond the goal. Over-scoping is Claude's most common failure here, not the user's.
- Anything costly (multi-agent runs, more than about $20, `Assets/` changes, merges to `main`) → state the agent count and rough cost first (CLAUDE.md rule).

**The Inbox** holds parked ideas so nothing is lost and nothing derails the session: the controller (PC or cloud) adds a line to `CURRENT.md`'s Inbox section. One line each: the idea, who asked, the date.

## 3. Checkpoints
- When something lands on `main`: one line in the handoff (sc-handoff) with its evidence, inside that merge commit. In-progress steps go to the board profile, not the handoff.
- About hourly, or when the user returns after a gap: a 3-line status (done, doing, next) before continuing.
- If two attempts at the same thing fail, stop and re-plan with the user instead of trying a third variation.

## 4. Close the session
When the goal's done-definition is met, the user says stop, or the context is getting long:
```
Landed: <items with commits / check lines>
Open: <what's half-done and where it lives>
Next session starts with: <one concrete first action>
Inbox: <count; anything new>
```
Then update `CURRENT.md` (sc-handoff) if anything changed that no merge commit already recorded, and push it in one commit.

## 5. Housekeeping (when the card shows it, about weekly)
- `CURRENT.md` over sc-handoff's cap (about 2,800 words) → prune it to the five sections plus Inbox before anything else, moving history to a dated file. (2026-10-25: it was about 10k words and 446 lines.)
- Open remote branches: list the ones merged or stale (no commit in 7 days) and propose deleting or merging them, one user decision for the batch.
- Old handoff files: fold anything still true into `CURRENT.md`, leave the rest as history.

## Sources
Patterns borrowed and adapted: scope boundary plus a refusal template, and a status block at session start (an article on team Claude workflows, Medium, 2026); "Current State / Next Steps" maintained per session (SitePoint's CLAUDE.md workflow); Claude over-scoping as the most common workflow failure (a 91-session workflow review, Medium); Anthropic's skill guidance (one workflow with a checklist, a default with an escape hatch).
