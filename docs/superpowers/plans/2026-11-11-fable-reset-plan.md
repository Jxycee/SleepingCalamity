# Fable reset plan (2026-11-11)

How we spend the Fable allowance between now and Nov 22, including the one-off Fable reset Anthropic gave for the Fable 5.2 launch (it expires Nov 17, 1:59 AM CST). Goal: work that pays off this week and keeps paying off after the Fable budget is gone.

## Budget and timing
Measured 2026-11-11 ~5:40pm CST (`get_usage`): Fable weekly 0% used, all-models weekly 2%, both reset Sun Nov 15 1:00 AM CST.

That gives three Fable pools if the reset is redeemed at the right moment:
- **Pool A:** now until Nov 15 1:00 AM. Unused allowance is lost at the weekly reset, so spend it.
- **Pool B:** from Nov 15 1:00 AM.
- **Pool C:** redeem the one-off reset late on Nov 16, after pool B is used up and before Nov 17 1:59 AM. The refill then lasts until the following weekly reset (Nov 22) instead of a few hours. Redeeming earlier, while a pool still has room or just before a weekly reset, wastes it.

Fable also counts against the all-models weekly limit. Run 1 measures how many all-models points one Fable job costs; later runs are sized from that number.

## What makes a Fable run worth it
Anthropic's Fable 5.2 guidance: best as a planner and orchestrator for cheaper models, strongest on hard reasoning, long autonomous work and dense visual judging; start at `high` effort; delegate execution to subagents; give it a memory surface. So each run:
- is one fresh session at `high` effort with a short brief, a done-definition and a round cap, and stops when done;
- decides and writes; Sonnet subagents and the local GPU models do the building, rendering and sweeping;
- leaves durable output in the repo (plans, scripts, tests, skill updates), not only chat;
- ends by appending "lessons for the next Fable run" to this file, so each run starts smarter.

## Runs, in order
Long-run value first. Each run has a stop point so one job can't eat a pool.

| # | Pool | Run | Why Fable | Durable output | Done when |
|---|---|---|---|---|---|
| 1 | A | **Playable-loop milestone plan.** Read the specs, Core systems and the thin Gameplay/UI/App glue; design the milestone that makes town → dive → back to town → place a building playable, solo and with 2 players | The weakest part of the project (lots of Core logic, little playable glue). Planning for other models is Fable's documented strength | `docs/superpowers/plans/` milestone plan: tasks batched for Sonnet sessions, tests per task, briefs ready to launch, QUEUE items | Plan reviewed by you; first batch launchable. Also records Run 1's measured cost |
| 2 | A | **Save and netcode risk audit**, using the rewritten `sc-netcode` | Hard cross-assembly reasoning; a save-format bug found now saves every future world | Findings with fixes queued; `sc-netcode` and `REVIEW.md` updated with anything new | Every save section and networked system reviewed once; blockers filed |
| 3 | B | **Asset factory design.** Cleanup stage for the local prop pipeline (decimate, UV, rebake, colour fix) plus automatic quality gates and settings per asset type | Design once, then Sonnet and the GPU produce assets without Fable forever | Gate scripts + recipes in `Art/Blender/tools/` and `sc-ai-model-pipeline` rewritten around the local pipeline | One prop and one creature pass the gates end to end |
| 4 | B | **Tobin hair (and face if time).** Fable judges, local HairDeck sweeps generate, numeric checks pre-filter, one comparison sheet per round | Hard visual judging after Sonnet and Opus stalled | Finished hair; the sweep-and-score tool stays for every future character | 25/40 on visible faces, or 3 rounds |
| 5 | C | **Combat and weapon-growth balance.** A Sonnet-built Python fight simulator runs thousands of fights on the GPU; Fable reads the curves and sets the numbers | Fable reads charts, not raw fights; the simulator is reusable | Simulator in `Tools/`, tuned numbers in Core tuning data | Time-to-kill and growth curves inside targets for floors 1-7, solo and co-op |
| 6 | C | **Reserve.** `fixer` escalations, a second hair round, or a plan revision after Run 1's first batch lands | Keeps the one-a-week escalation rule covered | As needed | Pool C empty or Nov 22 |

## Prep before Run 1 (cheap, not Fable)
- Hair sweep-and-score tool (Sonnet): HairDeck variants → numeric checks → one sheet, so Run 4 doesn't spend Fable on tooling.
- `sc-netcode` rewritten on current code (Opus agent, 2026-11-11), so Run 2 starts from a correct checklist.
- Worktree cleanup and the 2026-11-11 rule changes are done, so Fable doesn't read stale rules.

## After Nov 22
Back to about one Fable use a week: the weekly planning session (plan, rank, write briefs) or a `fixer` escalation, whichever is worth more that week.

## Lessons for the next Fable run
(Each run appends here: measured cost in Fable % and all-models %, what worked, what to brief differently.)
