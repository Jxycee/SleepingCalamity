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

### Run 3 (pool B): Asset factory design
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
| Sun-Mon Nov 15-16 | Runs 3 and 4 (pool B); PC free for GPU sweeps |
| Mon Nov 16 late | Redeem the one-off reset once pool B is spent |
| Tue-Sun Nov 17-22 | Runs 5 and 6 (pool C) |
| After Nov 22 | About one Fable use a week: the weekly planning session or an escalation |

## 6. Lessons for the next Fable run
(Each run appends: date, run, Fable % and all-models % at start and end, what worked, what to brief differently.)
