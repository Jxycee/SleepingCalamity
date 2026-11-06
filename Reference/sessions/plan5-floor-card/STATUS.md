# plan5-floor-card (cloud session)
State: MERGED into main (see merge commit "Merge claude/upbeat-maxwell-pnadr4: plan 5"); branch review: merge, no blockers; risk review: no blockers (fixes in 2cea726)
Branch: claude/upbeat-maxwell-pnadr4 (from main a85d686)
Tip: f774e1f (branch, before this status commit)
Check: unity.sh check PASS on f774e1f (build PASS, EditMode 1594/1594, PlayMode 198 passed 0 failed 21 skipped), SC MARKER == f774e1f
Soak: unity.sh soak PASS 45/45 on 1c030ee (Core/Cellars unchanged since)
Archive: ready

## Open questions (placeholder taken, work continues; plan "Decisions" 1-11)
1. Modifiers: one or two per floor? Taken: two slots (hazard+reward, boon), floors 2/4 at most one, floor 7 both possible, calm floors 1/3/5/6 none.
2. Existing hidden twists (Ambush/Thin/Crowded) are folded into modifiers and printed on the card (Ambush named, room not). OK?
3. Substitutions: Warm Hands = healing points recharge 15 s (they already heal fully); Hurried reward = +30 gold each on leaving (push-on stir reward is in DiveSession, another session's file); Dim = forced dark floor + better chest rolls. Brimming/Fat Larder/Still Air deferred to step 5.
4. Hazards are never declinable at the stair.
5. Hold the pump allowed on any generated floor (2, 4, 6, 7).
6. Modifier rewards are ordinary loot/gold (no stir or bank input).
7. Names: hand-written table, 5 per sub-theme, no repeat within a dive.
8. Card re-open: no map screen exists, so a compact always-on corner panel replaces "re-open from the map".

9. Valve fallback: layouts have 4-6 rooms, so valves may sit in adjacent rooms when no farther pair exists; 4.5% of valve rolls fall back to the gate key (fewer than 2 eligible combat rooms).
10. Stirred's +10 can latch the extra enemy / hunter for the rest of the half (kept: it is the hazard; card says so).

## Follow-ups (outside this plan)
- Tools/cloud/sc_core_tests.py: its using-filter rejects `SleepingCalamity.Core.Combat`, so ~21 EditMode files (incl. CellarRosterTests) never run under it and it reports green; widening shows only RingPlannerTests.Plan_DoesNotAllocate_PerTick failing under .NET 8 (24 B). Needs its own small task.
- Unexplained host-hero drift (~2 m/s, ~0.4 s after a Move) in DiveSpineNetworkTests floor-7 helper; test pins the hero per frame. Under branch review.
- Floor-7 drift: branch review judges it live enemies pushing the hero's capsule (Enemy.MoveHorizontal ignores heroes) or knockback, both on main; players can be pushed out of valve reach (1.8 m) and lose the hold after 0.6 s. Tuning note; test keeps its pin.
- Floor5_RealButtonPushOn (bank-input test) flaked once in a full run, passed alone twice and in both full checks; not from this branch per review.
- Nits left: card prints "Find the gate key" on a key floor that opened at arm time (no carrier); card's pieces line repeats StirMeterHud's; co-op follower bots don't split valves.
