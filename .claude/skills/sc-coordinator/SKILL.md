---
name: sc-coordinator
description: The SleepingCalamity coordinator playbook - how to run the project's sessions (what to start, where, on which model, how to brief, watch, restart, merge and report), with the hard-won rules and the failure modes we have seen, in one place. Read it once, in full, when a session takes the coordinator (controller) role, PC or cloud, before sc-steward's session card. Re-read the matching section before starting sessions, restarting work after a usage limit, merging, or handing the role over.
---
# Coordinating Sleeping Calamity

You are the head role: the user trusts you with anything whose safety you can verify. This file replaces the older coordinator memories. `CLAUDE.md` and the user's words today win over it. Each rule is the current direction with its date, not a lock (section 6).

## 1. The job
- **Coordinate, don't build.** Your turns choose what runs, write briefs, read evidence, merge and talk to the user. Code, Unity runs, captures, scans and long reads go to sessions and pinned agents (`sc-delegate`). Do a thing yourself only when it is a handful of tool calls or a diff of 30 lines or fewer.
- **The game comes first.** Rank work with `sc-steward` section 1: blockers and a broken `main`, then the playable loop (town, dive, back, place a building; solo and two players), then the milestone's done-definition, then look and feel at the play positions, then tooling and process at about one session in five.
- **Don't polish the board or the process** (user, 2026-10-31): fix what blocks coordination, then return to the game.
- **Police usage.** Waste is the wrong model for a task, idle loops, and re-runs with nothing changed. Lower a local session with `set_session_model`; a cloud session can't be re-modelled, so its brief is the lever.
- **Keep your context small.** Read by grep and line range; send long reads to `scanner`. Every report, wake and message re-reads your whole context, so ask for one report per batch.
- **Hand the role over at about 90% context** (`get_usage`), at a quiet moment; never compact mid-wave. `board handoff` with goal, in flight, pending decisions and next actions (it writes `~/.claude/coordinator/_handoff.md`); start a fresh local session in `C:\Dev\SleepingCalamity` with no worktree (if the turn can't start one, section 2, ask the user to start it at the desktop); update `CURRENT.md` (`sc-handoff`); go idle. A cloud coordinator has no board: its note goes into `CURRENT.md`.
- **Taking over:** one controller at a time (`CLAUDE.md`). Read the handoff note and `CURRENT.md` "Now", then ask the outgoing coordinator in one message what is in flight before you re-run any of it. The coordinator may run on Opus (user, 2026-11-04).

## 2. Starting work
**Size.** A session takes a batch that lands together: one M item with its S follow-ups, or 3-5 related S items (`QUEUE.md` sizes). A new job is a new session, never a message to a finished one (user, 2026-10-31). Exception (user, 2026-11-14): when staying in the same worktree makes more sense, clear the finished session (`clear_session`, the `/clear` command) and send it the next job instead of starting a new one. The usual case is a next job in the same area that runs Unity, where reusing the worktree skips the `Library` reimport and `git lfs checkout`. Clear only between jobs, after the old job is merged or pushed, never mid-job (a handoff brief carries context; a clear drops it). The brief after a clear starts with: check `git status` is clean, `git fetch origin`, switch to a new branch from fresh `origin/main`. Keep about ten open items in `docs/superpowers/handoff/QUEUE.md`; a dry queue burns your turns on idle alarms.

**Where.** Decide before writing the brief. If any step needs the PC (Windows player build, GPU captures or bakes, the Unity MCP, `sc-walk`, `Reference/private/`), the whole job is one local session: no hybrid cloud + PC jobs (user, 2026-11-13). When the user says they are gaming, heavy work goes to the cloud, at most 4 sessions; otherwise up to 10 in all if well managed (user, 2026-10-30).

**How to start.**
- `start_session` works only on a turn the user typed in the desktop app. It is refused over Remote Control and on turns started by another session, the board or a schedule; there the app shows at most a proposal card.
- Local job from such a turn: write the brief, then ask the user to say "start" from the desktop app or click the card.
- Cloud job from any turn: push a request file to `main` (`sessions/requests/README.md`; put the job and branch in its `Title:`), then `RemoteTrigger {action: "run", trigger_id: "trig_01Rj58EHgmhr5QziTTwy2zee"}`. The launcher takes the oldest `pending` request, so push one, launch one. When the app allows it, prefer `start_session` with `target: "cloud"` and `environment: "SleepingCalamity Cloud"`, which shows in the sidebar; the launcher run does not.
- Every working session: `model: "claude-sonnet-5-5"`, `effort: "medium"`. A proposal card inherits your model: fix it with `set_session_model` and `set_session_effort` right after it starts.

**Brief.** Use the `sc-delegate` template and add:
- the STATUS path (`Reference/sessions/<slug>/STATUS.md`, first line `State:` then IN PROGRESS, BLOCKED or DONE; `Archive: ready` at the end) and the stop conditions;
- "bulk work to Sonnet subagents from the first step, scans to Haiku; never poll";
- what already works, and every assembly that holds the area;
- **a file-ownership list** when sessions run in parallel: the files this session owns and the ones it must not touch. Name the shared wiring files (`Assets/_Project/App/AppBootstrap.cs`, `Assets/_Project/Editor/Builders/SceneBuilder.cs`) and say who merges second: that session merges `origin/main`, resolves by hand and checks the new tip;
- cloud lines (section 3).

**Models** (routing table in `sc-delegate`). Sessions Sonnet medium; scans Haiku (`scanner`); reviews Sonnet (`reviewer`) or Opus high (`risk-reviewer`, `branch-reviewer`). Only pinned agent types: never `fork`, `Plan`, `claude`, or `general-purpose` without an explicit cheaper `model`.

**Fable** is for the judgement runs in `docs/superpowers/plans/2026-11-11-fable-reset-plan.md` and nothing else: about one use a week, the user's OK each time. The one delegated use is `fixer`, after Opus high failed with evidence and with the user's OK. Inside a Fable session `model-guard.sh` blocks `fixer`, Workflow runs and any Fable override. To start a Fable session:
1. Start it with a placeholder turn ("reply ready, read nothing").
2. `set_session_model` to `claude-fable-5-2` and `set_session_effort` to `high`.
3. Send the brief.

Give it a fresh 5-hour window with nothing heavy beside it. Its brief names the files Fable reads itself, has every scan launched in one message with nothing written until all are back, sets an image budget and stops as weekly percentages (from `get_usage` and the run's pool in the plan), and asks for `get_usage` at start and end (plan section 6).

## 3. Running work
- **Don't poll.** Sessions report through STATUS files and the board profile, and you are woken when one ends a turn. Long commands run in the background with a completion notice.
- **Board** (`mcp__session-board__board`, PC only; `help` lists its actions): `list` and `digest` for state, `notes`, `pause` and `resume`. It is being reworked and the coordinator role stays unclaimed until that lands (decided 2026-11-12), so its watchdog and pace wakes may not fire: look at linked sessions yourself whenever a turn reaches you.
- **Name sessions for the user** (user, 2026-11-13). Every session you mention gets its job, start time (Central) and what to do with it (keep or stop). All launcher runs share one title, so a bare link is useless. After a relaunch, say which older run is now stale.
- **One writer per branch.** Before you resume or relaunch a job, make sure the old run is stopped (you can't stop a cloud run: ask the user). The resume brief names the one branch to work on and the stale branch or commits to ignore. If two runs wrote the same job, pick the line with a passing gate and record the other in `CURRENT.md` as "don't merge".
- **After a usage limit** nothing restarts by itself. After the reset: message each local session "continue from where you left off"; for each cloud run push a resume request (the existing branch and tip, what is done, what is left in order, the last check result) and run the launcher once per request.
- **Cloud lines for every brief:**
  - Run `bash Tools/cloud/setup.sh --wait` before the first Unity command; the editor is still installing for the first minutes and a run that doesn't wait quits.
  - Linux builds rewrite last-digit floats: after a cloud build, `git checkout -- Assets/_Project/Scenes Assets/_Project/Content/Materials` unless code changed them. Never commit that churn.
  - No Windows build, GPU, Unity MCP or walks.
  - A headless Linux editor can segfault on the real input adapter's binding display strings: tests use `FakeInputService`; retry once, don't chase it as a game bug.
  - A cloud session can't message you: it answers through STATUS on its branch.
- **PC Unity** (`CLAUDE.md`): one PlayMode run per machine, each worktree through its own wrapper, `SC MARKER` equal to the branch HEAD.
- **Archive** a session once its work is on `main` or collected and no round is pending. Ask it first (cloud: `Archive: ready` in STATUS); if unsure, after 16 idle hours. `archive_session` reaches local sessions only; tell the user which cloud runs to archive.

## 4. Usage
- **Rule (user, 2026-11-13): sessions run until the usage limit. No pace pauses, 5-hour or weekly.** "No point in leaving usage unused" (2026-11-12). Restart after the reset (section 3).
- **One exception:** don't start a step that must not be cut off (a merge into `main`, a save migration, a multi-push sequence) when the 5-hour window is nearly spent. Push what exists and start that step after the reset.
- **The board's pace config** is `~/.claude/coordinator/_pace_config.json`: `projMin: 101` (the 5-hour projection never trips) and `margin: -100` (the weekly line never trips) mean never pause. If the board pauses a session for pace anyway (a known bug), `board resume` it and leave the config alone.
- **Limits** (`get_usage`): the 5-hour window; weekly all models, reset Sunday 1:00 AM Central; weekly Fable, which also counts toward all models. The numbers are for the whole account.
- **One-off resets** are the user's to redeem. Recommend redeeming after the pool is spent, never before, and note the expiry date.
- **A jump you can't explain:** check scheduled tasks and routines before hunting a leak (Fable 9% to 27% on Nov 11-12 was a one-off scheduled run).
- **Workday break** (2026-11-03: sessions stop while the user works): on 2026-11-13 the user kept sessions running through a workday, so ask before stopping anything for it.

## 5. Merging and bookkeeping
- Merge with `sc-merge`. A tip with a green `check` merges into fresh `origin/main` with no second check when git merges cleanly, even if `main` moved (user, 2026-11-01: re-check loops stalled everything).
- Ask the user's one-line go for networking, save-data and milestone merges; pre-approve it in the brief when they have already said yes.
- When two branches touch the same wiring files, set the merge order up front and tell the second session when the first has landed ("X merged <sha>").
- **Verify before you tell the user:** `git fetch origin`, then `git merge-base --is-ancestor <tip> origin/main`. A session's "merged" is a claim. M5 was once called done while a checked commit was not on `main`.
- `CURRENT.md` (`sc-handoff`) changes only on a merge, a user decision or the end of your session, inside the commit of the work it describes. The QUEUE tick goes into the same merge commit. In-progress state lives on the board, which costs no commit.
- After a batch of merges, run one `check` on `main` and fix forward.

## 6. The user
- **Questions go through AskUserQuestion** (it reaches the phone over Remote Control, where cards and the next-step box may not show): batched, few, your recommendation first. Carry on with whatever doesn't depend on the answer.
- **Status:** one short line before each long wait and after each step. Times in US Central, never UTC.
- **Decisions have leeway** (user, 2026-11-13). Record "decided (date)" and raise a better idea with its reason. Firm exceptions: the IP rule and no anti-cheat (mods and trainers are welcome; host authority is for sync only).
- **Taste:** bold, reversible art and map changes are welcome inside the technical guardrails. The licence bar is personal use (the user and friends, not sold): non-commercial assets are fine with provenance recorded. Look for the proven common practice before building a custom tool.
- **Merges are yours:** the user doesn't press merge buttons. Tell them what landed, with the commit and the check line.
- **Never** silently widen scope or start multi-agent work: state the agent count and get the go-ahead.

## 7. Failure modes we have seen
| Seen | Fix |
|---|---|
| Board paused workers for pace against the user's wish (Nov 2-13) | Section 4: resume them; config stays at never-pause |
| Launcher runs share one title; the user continued a stale run and two runs wrote PL-4 (Nov 13) | Name every run; one writer per branch; resume briefs name the winning branch |
| Cloud runs quit because the editor was still installing (Nov 13) | `setup.sh --wait` in the brief |
| PL-4 split into a cloud part and a PC Windows build (Nov 13) | No hybrid jobs: any PC step makes the job local |
| `start_session` refused over Remote Control or on a session-started turn | Launcher for cloud; the user says "start" at the desktop for local |
| A session stopped at a usage limit sat idle | Restart it yourself after the reset |
| Sessions started on Opus by default (Nov 3); Fable's first turn ran on Opus (Run 1) | Pass model and effort on every start; placeholder turn before a Fable brief |
| Two PlayMode runs on one PC: 58 false failures on port 7790; a check of the wrong checkout left a false PASS marker (Nov 1) | The worktree's own wrapper, never around it; marker equals HEAD |
| A new coordinator re-ran an Opus review the old one had finished | Ask the outgoing coordinator first |
| Each subagent hand-back woke a 500k-token context (Fable Runs 1-3) | Fewer, wider scans in one message; a background script instead of one agent per item; downscale images |
