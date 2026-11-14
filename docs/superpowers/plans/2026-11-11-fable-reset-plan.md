# Fable reset plan (2026-11-11)

How we spend the Fable allowance between now and Nov 22, including the one-off Fable reset Anthropic gave for the Fable 5.2 launch (it expires Tue Nov 17, 1:59 AM CST). Goal: work that pays off this week and keeps paying off after the Fable budget is gone.

## 1. Budget and timing
Measured 2026-11-11 ~5:40pm CST (`get_usage`): Fable weekly 0% used, all-models weekly 2%, both reset Sun Nov 15 1:00 AM CST. Pro plan.

Three Fable pools, if the reset is redeemed at the right moment:

| Pool | Window | Runs | Rule |
|---|---|---|---|
| A | now to Sun Nov 15 1:00 AM | 1, 2 | Unused allowance is lost at the weekly reset, so spend it |
| B | Sun Nov 15 1:00 AM onward | 3, 4 | Normal weekly pool |
| C | redeem the reset late Mon Nov 16 (before Tue 1:59 AM), lasts to Sun Nov 22 | 5, 6 | Redeem only once pool B is used up. Redeeming earlier, or just before a weekly reset, wastes most of it |

Fable also counts against the all-models weekly limit, which every other session needs. Run 1 measures the cost of one Fable job in both Fable % and all-models %; later runs are sized from that. If one run costs more than about 40% of a Fable pool, the plan drops to one big run per pool and Run 6 goes first.

## 2. Keeping Fable work on Fable only
A Fable session must spend Fable only on its own judgement; everything it delegates runs on cheaper models. Enforced in three layers (all in the repo):
1. **Pinned agent types** (`.claude/agents/*.md`): `implementer` and `reviewer` run Sonnet, `scanner` and `Explore` Haiku, `risk-reviewer` and `branch-reviewer` Opus. A pinned model never inherits the caller's.
2. **Default for unpinned agents:** `.claude/settings.json` sets `CLAUDE_CODE_SUBAGENT_MODEL=sonnet`, so `general-purpose`, `Plan`, `claude` and any other unpinned agent run Sonnet, not the caller's model.
3. **`.claude/hooks/model-guard.sh`** (PreToolUse on Agent, Task, Workflow, start_session) reads the calling session's model from its transcript:
   - every session: `fork` subagents are blocked (a fork always runs on the caller's model);
   - a Fable session: a `model: fable` override, the `fixer` agent (pinned to Fable), Workflow runs, and `start_session` without an explicit non-Fable model are all blocked.
   Tested 2026-11-11 with 13 cases (Opus and Fable callers, prompts that merely mention "fable" pass).

Each Fable run's brief still says it: delegate only to pinned agent types; pass `model: "claude-sonnet-5-5"` to any session it starts.

## 3. How every run works
- **Launch:** the Opus coordinator starts one fresh local session with `model: "claude-fable-5-2"`, `effort: "high"`, after the user's go for that run. One Fable session at a time.
- **Brief:** goal and why, done-definition, round cap, stop point, files to read first, delegation map (below), and "measure with `get_usage` at start and end".
- **Fable decides; others build.** Sonnet implementers write code, run Unity and builders; Haiku scanners read logs and inventories; your GPU models generate and sweep; Opus reviewers check networking and save data. Fable reads summaries and one comparison sheet per round, not raw logs or every render.
- **Durable output:** every run leaves files in the repo (plan, scripts, tests, skill updates) and commits them with the work, per `session-usage.md`.
- **Memory surface:** each run ends by appending its measured cost and lessons to section 6, so the next run starts smarter.
- **Stops** at the done-definition, the round cap, or when its slice of the pool is spent, whichever comes first, and reports in plain full sentences.

## 4. Runs
### Run 1 (pool A): Playable-loop milestone plan
- **Why first:** the weakest part of the project is that there's no playable loop people have tested: Core holds most of the logic (175 files), while Gameplay (21), UI (4) and App (8) hold little of the glue. Planning work for cheaper models is what Anthropic says Fable does best, and this plan steers the next one to two weeks of Sonnet work.
- **Goal:** design the milestone that makes town → dive → back to town → place a building playable, solo and with 2 players, in the current dive and town systems.
- **Reads (through Haiku scanners where wide):** `docs/PHASE1.md`, the Hearthdeep dungeon spec and dive roadmap, `QUEUE.md`, Core/Cellars, Core/Town, App (`SceneFlow`, `WorldSaveFlow`), UI, the playtest notes, `USER_TODO.md`.
- **Produces:** `docs/superpowers/plans/<date>-playable-loop.md` with: the loop broken into screens and transitions; what exists vs missing (glue, UI, wiring); tasks batched for 3-5 Sonnet sessions, each with acceptance tests and a ready brief; which tasks need `risk-reviewer`; queue items replacing stale ones.
- **Done when:** you have read and approved the plan, and the first session brief is ready to launch.
- **Cap:** one planning pass plus one revision after your comments.
- **Also:** records the measured cost of a Fable run (both pools) in section 6.

### Run 2 (pool A): Save and netcode risk audit
- **Why:** a save-format bug caught now protects every future world; a desync caught now saves weeks of co-op debugging. The rewritten `sc-netcode` (2026-11-11) gives it a correct checklist.
- **Goal:** review every world-save section and every networked system once against `sc-netcode` and `REVIEW.md`.
- **Delegation:** Haiku scanners list every NetworkBehaviour, RPC, NetworkVariable and save section with file:line; Fable reviews by area; Opus `risk-reviewer` double-checks anything Fable rates blocking; Sonnet implementers write the failing tests for confirmed bugs (fixes go in later normal sessions).
- **Produces:** a findings file with severity and a concrete failure path for each; failing tests for blockers; new rules added to `sc-netcode` / `REVIEW.md`; queue items.
- **Done when:** every section and system is covered, blockers have a failing test or a written repro.

### Run 2b (pool C, moved 2026-11-13: all-models weekly runs out Saturday): Coordinator playbook
- **Why (user, 2026-11-12):** every coordinator should start with Fable's knowledge and process, not rebuild it from scattered memories.
- **Goal:** a new `.claude/skills/sc-coordinator/SKILL.md` (about 1,500 words) that every coordinator reads once on claiming the role; `sc-steward` and the board's `claim` point to it.
- **Inputs:** the coordinator memories (handoff, focus and usage police, Nov 1 lessons, board watchdog, usage pace), `session-usage.md`, section 6 here, and how recent coordinator sessions actually ran (Haiku scans of their transcripts).
- **Done when:** the skill is merged, the memories it replaces are trimmed to pointers, and the cost is appended to section 6.

### Run 3 (pool B, Sun Nov 15): Area 1 and dungeon 1 design (replaces the asset factory here; user, 2026-11-13)
- **Why (user):** Fable designs Hollowmere (area 1) and the Cellars (dungeon 1) in full, and Sonnet sessions build them to spec. Scans (Megascans) plus Fable's judgement make worlds that are beautiful, engaging and lively.
- **Goal:** one design doc per place: layout map with play positions and sight lines, light and atmosphere per spot (target URP, not Lumen), prop-by-prop dressing plan naming exact Megascans, items from the user's Fab library (user, 2026-11-13: the spec names them, so only those get downloaded) and our own pieces, ambient life (animals, smoke, cloth, sound), how each dungeon floor reads (landmarks, set pieces), and a build plan split into Sonnet sessions, each with a capture-camera done-check beside the reference row. First build step: a URP lighting test corner.
- **Builds on, not redoes:** `docs/PHASE1.md` section 3 (style bible), the signed-off dungeon design and `docs/ROOM_METRICS.md`, the floor generator (rooms v2, 7 floors), the Hollowmere already standing, the NieR Replicant cohesion target. A generator change only if the design needs one, with the reason.
- **Asset search is delegated (user, 2026-11-13):** Fable never browses catalogs itself. Cheap agents search: `general-purpose` with `model: "haiku"` (it has web tools; the pinned `scanner` has none) searches Megascans and Fab per need ("mossy cellar stone wall", "worn cobble", "bakery props"), and reads the user's library list `Reference/private/fab-library-2026-11-13.md`. Each returns a capped shortlist: name, link, format (FBX/glTF/Unity or Unreal-only), licence, texture sizes, preview image URL. One Sonnet implementer downloads the previews into `Reference/private/` and builds contact sheets with `Art/Blender/tools/sc_ab_sheet.py` beside the reference row. Fable judges the sheets, not pages. Launch all searches in one message and write nothing until they are back (Run 1 lesson).
- **Where:** one local PC session (needs `Reference/private/`, the browser pane for Fab pages, GPU captures; no hybrid).
- **Done when:** both docs and the build plan are merged and the user has approved them.

### Run 3b (pool C): Asset factory design (moved from Run 3; Megascans and Fab cover much of the need now)
- **Why:** design once, then Sonnet and your GPU produce assets without Fable from then on.
- **Goal:** finish the local prop pipeline (`C:\AI\scripts\ai_prop.py`: Hunyuan3D shape + TRELLIS.2 texture) with the missing cleanup stage (decimate, UV, rebake at 2K max, colour fix toward the style bible) and automatic quality checks per asset type (prop, creature, foliage, kit piece).
- **Delegation:** Sonnet implementers write the Blender cleanup and check scripts; local GPU runs the generation sweeps; Haiku reads the audit output; Fable sets the thresholds and judges the contact sheets.
- **Produces:** cleanup + gate scripts in `Art/Blender/tools/`, a recipe per asset type, and `sc-ai-model-pipeline` rewritten around the local pipeline (it currently names scripts that aren't in the repo).
- **Done when:** one prop and one creature go from concept image to Unity-ready FBX through the gates with no hand fixes, and score at least 25/40.

### Run 4 (pool B): Tobin's hair (face if time)
- **Why:** hard visual judging after Sonnet and Opus stalled; the hero character is seen in every cutscene.
- **Prep (Sonnet, before the run, not Fable):** a hair sweep-and-score tool: HairDeck variants (seeds, reference crops, `hd_fit` settings, shader tints) → numeric checks (silhouette match via SAM 3 masks, `sc_ref_palette` colour distance, builder CLEARANCE/EARS/CROWN checks, front cards jutting) → top 3-4 beside the reference in one sheet (`sc_ab_sheet.py`).
- **Delegation:** local GPU sweeps; Sonnet runs captures in Unity; Fable reads one sheet per round, picks, and sets the next sweep.
- **Produces:** final hair in Unity on spring chains; the sweep tool stays for every future character.
- **Done when:** 25/40 on visible faces, or 3 rounds.
- **Needs:** the PC free (no gaming) while sweeps run.

### Run 5 (pool C): Combat and weapon-growth balance
- **Why:** the numbers decide whether the game is fun; a simulator is reusable for every later area.
- **Delegation:** a Sonnet implementer builds a Python fight simulator from Core's damage, weapon growth and enemy data (mechanics researched from the Dark Cloud decomp, in our own words) and runs thousands of fights on the GPU with torch; Fable reads the curves (time to kill per floor, damage taken, weapon growth per dive, 1-4 player scaling) and sets the numbers.
- **Produces:** simulator in `Tools/`, tuned values in Core tuning data with EditMode tests, a short balance note.
- **Done when:** curves sit inside the targets for floors 1-7, solo and co-op, with the tier-1-only rule for the first dungeon kept.

### Run 6 (pool C): Reserve
A second hair or face round, a plan revision once Run 1's first sessions land, or a `fixer`-style escalation on whatever stalled this week. Decided on Nov 16 from section 6.

## 5. Timeline
| When | What |
|---|---|
| Wed Nov 11 evening | Your go on this plan. Start the hair tool prep (Sonnet). Run 1 |
| Thu Nov 12 | Review Run 1's plan; Run 2. First Sonnet session from the Run 1 plan |
| Fri-Sat Nov 13-14 | Run 2 finishes; pool A spent by Saturday night |
| Sun-Mon Nov 15-16 | Run 3 (area 1 + dungeon 1 design, local) and Run 4 (pool B); PC free for GPU sweeps |
| Mon Nov 16 late | Redeem the one-off reset once pool B is spent |
| Tue-Sun Nov 17-22 | Pool C: Run 2b playbook, Run 5 balance, Run 3b asset factory if room, Run 6 reserve |
| After Nov 22 | About one Fable use a week: the weekly planning session or an escalation |

## 6. Lessons for the next Fable run
(Each run appends: date, run, Fable % and all-models % at start and end, what worked, what to brief differently.)

### 2026-11-11, Run 1: playable-loop plan (`2026-11-11-playable-loop.md`)
- **Cost.** Weekly Fable 0% to 8%. Weekly all models 4% to 8%. 5-hour window 28% to 58%. These are upper bounds: the Fable coordinator session was working at the same time and the numbers are for the account. The run stayed far below its 40% stop, but one run took about 30 points of a 5-hour window, so a Fable run needs a fresh window and nothing else heavy beside it. Session context ended near 220k tokens.
- **Where it went.** Five Haiku scanners did the reading (about 490k subagent tokens in total); Fable itself read about 450 lines (the queue, the delegate skill, the stale walk script, the dev panel, one catalogue file) and wrote one 200-line plan with about 20 small edits. Each scanner hand-back woke Fable on its whole context, so the five reports cost five turns before any writing.
- **What worked.**
  - Scanners with a word cap, "file:line for every claim" and a closing "what is missing" list. Their reports were enough to design from without opening the code.
  - A second, narrow scan that asked what the player presses and sees found what the wide scans missed.
  - A final fact-check scan of the thirteen claims the plan leaned on, run while the plan was being written. It found two real breaks (no release-build button for the return-dive floor pick or for eating a loaf) and corrected three design points (the dive's end reason is not replicated, the roster is not replicated, fake input can be wired per hero).
- **What to brief differently.**
  - The brief's premise was off: it counted Gameplay, UI and App and called the glue thin. The glue is in Presentation (`DiveDirector`, `DiveHaulSync`, `PlacementMode`, `TownSync`), and the loop was already wired and had a passing automated walk. Say what is known to work and name every assembly that holds the area, so the run does not spend a scan round finding out.
  - Ask player-level questions in the first scan round ("what does a player press and see at each step, and which steps need a dev key"), not only "which systems exist". That would have saved the second round.
  - Give the right paths: `USER_TODO.md` is under `docs/superpowers/handoff/`, and playtest notes are in `Reference/qa/playtest/BUGS.md` and `docs/playtest/NOTES.md`, not in `CURRENT.md`.
  - Start the session on Fable. The first turn ran on Opus and was stopped; its scans were kept, so little was lost, but the baseline had to be measured again.
  - Launch every scan in one message and tell the run to write nothing until all are back, so it takes one long turn instead of one per report.
  - Say which earlier decisions the run may propose to reopen. This plan asks to change plan 3 decision 10 (decision D1) and marks it as waiting for a yes.

### 2026-11-12, Run 2: save and netcode risk audit (`docs/superpowers/audits/2026-11-12-save-netcode-audit.md`)
- **Cost.** Weekly Fable 27% to 43% (16 points). Weekly all models 56% to 65% (9 points; the stop was 68%). 5-hour window 4% to 77%. Account numbers, so upper bounds, though nothing else heavy ran. About 105 minutes of wall clock, 24 of them the implementer's `check`. Session context ended near 395k tokens.
- **Where it went.** Eight Haiku scanners (about 945k subagent tokens), one Opus risk review (118k), one Sonnet implementer (130k, three tests and one `check`). Fable itself read about 3,500 lines of the save spine and the session seams, and wrote one 190-line findings file plus the rule, queue and walk edits.
- **Result.** 2 blockers, 9 should-fix, 12 notes. Both blockers were found by Fable reading two files side by side, not by a scanner: each scan had listed its half correctly and none connected them.
- **What worked.**
  - Scanners as maps, Fable as the reader of the code that matters. The save spine is small enough to read whole, and reading it while the scans ran filled the wait.
  - The Opus risk review earned its cost: it widened one blocker (the first lobby is affected too), demoted another with a reason, and settled an NGO ordering question from the package source.
  - Docs, rules and queue committed before the implementer ran, so the one `check` covered the final tip and the merge needed no second run.
  - `System.Reflection.DispatchProxy` fakes an SDK interface in an EditMode test in about a dozen lines; the "needs a seam" finding got a real failing test.
- **What to brief differently.**
  - One run took a whole 5-hour window and ended above the 250k-token handoff line. Split a future audit in two runs (save, then network), or cap Fable's own reading to named files.
  - Name the files Fable reads itself in the brief, and give the scanners the rest. The scans of the save spine repeated what the direct read found.
  - Ask every network scan one cross-file question: "which other handlers run on the same event, and what do they free or write". That is where both blockers were.
  - Subagents run in the background here, so eight scans cost eight wake-ups whatever the brief says; fewer, wider scans (four) would have cost less and mapped the same ground.
  - A stop three points away from the measured end (65% against 68%) was luck. Give an audit run a budget with room for its risk review and one `check`.

### 2026-11-13, Run 3: area 1 and dungeon 1 design (`docs/design/area1-hollowmere.md`, `dungeon1-cellars.md`, `area1-dungeon1-needs.md`, `2026-11-13-area1-dungeon1-build.md`)
- **Cost at the end:** weekly Fable 2% to 37%, weekly all models 3% to 23%, session context 578k tokens. The 5-hour window ran out as the docs were pushed; the review and merge ran after the reset. Interim figures when the docs were written: 5-hour window 19% to 92% in about 2 h 15 min. Weekly all models 3% to 13%. Weekly Fable 2% to 19%. Session context 510k tokens.
- **Where it went.** Images. About 60 were read at full size (18 concepts at 2400 px, references, 30 contact sheets), which put the context past 500k halfway through, and every later turn re-read it. Eighteen subagent hand-backs (7 scans, 11 searches) each woke Fable on that context.
- **What worked.**
  - Seven scans with a word cap, file:line and a "what is missing" list, all launched in one message. The generator and built-town scans were enough to design from.
  - Looking at the latest captures of the real scenes before designing. They showed gaps no document mentions (the sky, the empty ruin, stone scale).
  - Searching Megascans by set name ("Historic Medieval Farm", "Historic Bazaar", "Old Mine"): one query lists a whole matching prop family.
  - One background script for the second search pass (36 needs, no agents, no wake-ups).
- **What to brief differently.**
  - Start in a fresh 5-hour window. This run began at 19% and reached 92% before the writing started.
  - Test the search pipeline on one need before fanning out. `fab_search.py --type` was broken (HTTP 400), so the first pass (11 Haiku agents, about 1M subagent tokens) ran unfiltered and its sheets were mostly noise.
  - Use style rank for materials only. For objects it ranks scene-like thumbnails first; Fab's relevance order with the type filter, Megascans first, gave usable sheets.
  - Run searches as one background script, not one agent per need. Agents add wake-ups and no judgement.
  - Downscale images before Fable reads them (1100 px sheets were enough; concepts at 1280 px would be) and set an image budget in the brief.
  - Say in the brief that the dungeon is floors 1 to 7 of the Undercroft in code, and that the spec, STORY and the concept sheet disagree on floor themes and on the Dough-warden. The run found this from scans.
  - Say that downloads need the user's Fab sign-in, so picks can be confirmed but not fetched by the run.

### 2026-11-13, Run 2b: coordinator playbook (`.claude/skills/sc-coordinator/SKILL.md`)
- **Cost.** Weekly Fable 42% to 47% (5 points). Weekly all models 26% to 29%. 5-hour window 74% to 94% in about 70 minutes, measured before the merge and the memory trim. Account numbers; the coordinator session was also active. Session context ended near 195k tokens.
- **Where it went.** Three subagents (Haiku repo digest 76k tokens, Haiku transcript search 125k, Sonnet review 54k). Fable read the four short skills, the coordinator memories (about 4,600 words) and about 150 lines of handoff and plan text itself, and wrote one skill of about 2,200 words.
- **What worked.**
  - Two scans launched in the first message, with the short files read directly while they ran.
  - Trims and review fixes applied by one small replace script per round instead of many edits.
  - The Sonnet review found two real gaps: a handoff step that would stall on a turn that can't start sessions, and unclear wording on the `fixer` exception.
- **What to brief differently.**
  - Start in a fresh 5-hour window (the third run to say so). This one began at 74% and reached 94% before the merge.
  - The transcript search returned snippets, not incidents. The lessons listed in the brief by the outgoing coordinator were worth more. A coordinator should note incidents in the shared notes folder as they happen.
  - Seven required topics don't fit in 1,500 words. Give the word target or the coverage list priority.
  - A session can't authorise edits to `CLAUDE.md` or `.claude/rules/`. If a run needs one, the user asks for it directly, or the run leaves the exact line for them.
  - A playbook for cloud coordinators can't lean on PC-only things (memories, the board, `~/.claude/coordinator/`). Say so in the brief.

