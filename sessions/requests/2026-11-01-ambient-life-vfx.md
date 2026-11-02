Title: Ambient life for Hollowmere, effects first (no animal models yet)
Target: cloud
Status: done (merged b8b2c4cc)
Model: sonnet
From: the user, via the World traversal scan session (claude/great-lovelace-r22yqw); decisions on main 8e434b3
Status file: Reference/sessions/ambient-life/STATUS.md

Brief:
Hollowmere and its valley read "empty but polished" (Shadow of the Colossus). Read the review and the user's decisions of 2026-11-01 first: `Reference/qa/visual/TOWN_VISUAL_REVIEW.md`, part C, "Decisions" and "Ambient life: sourcing plan".

Settled by the user:
- **No people in Hollowmere** until a rebuild unlocks a resident. Ruined Hollowmere has wildlife only, plus smoke from the heroes' home chimney. Rebuilt adds smoke from every rebuilt house.
- **Ambient life goes in only if it matches the style** (docs/PHASE1.md section 3: stylised realism, muted daytime colour, heavy atmosphere).
- **Animals:** the user is looking for pre-rigged models. Do NOT source, generate or adopt animal models in this session. That is phase 2, after the user hands models over.

This session (phase 1, effects only, no models):
1. Distant birds: flocks over the valley, crows circling the bell tower and ruins, swallows over the square, gulls toward the lake. GPU particles or vertex-animated cards with a wing-flap shader, small enough that no model quality question arises (20 m and more). A flock lifts when the hero comes close.
2. Butterflies and bees over the meadows and the garden; extend the existing town pollen if useful.
3. The lake: fish rings, and reed sway if the reeds don't move yet (HollowmereLakeArt.cs, HollowmereWater.cs).
4. Wind: wind waves through long grass and fields; drifting cloud shadows across the valley (a light cookie or shader).
5. Chimney smoke: the heroes' home in both presets; every rebuilt house in Rebuilt only.

Rules:
- Builders only (never hand-edit .unity/.prefab); hook into ProjectBuilder.RunAll.
- Keep it cheap. The quality-first target allows real cost, but no per-frame allocations, and pool the particles.
- Follow `.claude/rules/session-usage.md`: Sonnet subagents run Unity; captures with `SC_CAPTURE_ONLY` and the fewest cameras (Hollowmere captures OOM in the cloud: prefer one reduced camera, or ask the coordinator for a PC capture).
- Each effect gets a one-line on/off in one tuning spot.
- The traversal regression test must stay green (effects must not add colliders).
- Merge on a green `check` after merging origin/main (merge commit, no PR, never force-push). Town polish (`2026-11-01-town-visual-polish.md`) and M6 S4/S5 also touch Hollowmere builders: on a builder-output conflict take main's side, run one `build`, then `check`.

Phase 2 (not this session; it waits for the user's pre-rigged animal models):
- perched crows (5-15 m)
- rabbits, a fox and deer at field edges (10-40 m)
- a heron at the lake
- feral sheep in a fold

Each model needs: a licence check and a SOURCES.md row (`sc-asset-adopt`), conversion to the style if needed, and a score with `sc-asset-review`.

Done when: the five effects are merged with a green check, a short before/after clip or frame set is in Reference/sessions/ambient-life/, and STATUS says DONE and "phase 2 waits for the user's animal models".
