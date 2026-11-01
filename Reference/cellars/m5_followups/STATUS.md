State: WORKING
Session: Local next: M5 follow-ups (healing, stir effects hook, stir HUD, flaky parry test)
Progress: 5% (plan; items 1 and 4 starting in parallel)
Status: items 1 + 4 in progress, 2 + 3 queued
Archive: not ready
Branch: claude/m5-next-639689 (each item on its own subagent branch, merged to main on its own green check)
Last check: none yet
Next step: implement healing (branch claude/m5-healing) and parry-test root cause (claude/fix-parry-flake)
Sheet: none
Needs: none
Blocker: none

## Plan (each item merges on its own green check)
1. P47 healing: Core picks healing points for about half of floors 1-3, deterministic per (seed, floor) on its own Rng.Derive stream (floor goldens untouched); 30 s recharge placeholder in one tuning spot; full heal at the floor-2 checkpoint and floor-4 bank point on a successful bank. Host-authoritative via HeroVitals.ServerHeal. EditMode + PlayMode. risk-reviewer.
2. Stir effects hook: no cellar enemy spawner exists yet (only the arena's), so: a Core encounter plan per (layout, stir) giving enemies per room = spawn points + ExtraEnemiesPerRoom and a rat-king placeholder cell once the hunter latches; host exposes it; greybox marker for the rat-king. risk-reviewer if networked.
3. Minimal stir HUD meter in the UI assembly reading DiveHaulSync's summary (value bar + latch flags + deep-closes seconds). reviewer.
4. Flaky CombatArenaTests.Solo_DaggerGuard_Reduces_AndAParryStopsTheHit: root cause (real-time parry window), deterministic fix, no retry hacks. reviewer.

## Results
(none yet)

## Usage waste
(none yet)
