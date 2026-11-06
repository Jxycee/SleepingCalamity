# plan5-floor-card (cloud session)
State: run B (Tasks 4-5) done, main merged 0650487; risk review of 4-5 and run C (HUD, bots) in progress
Branch: claude/upbeat-maxwell-pnadr4 (from main a85d686)
Tip: -
Check: run B 5ebbb03: gate PASS (EditMode 1577/1577), PlayMode 6 classes 73/73
Soak: -
Archive: no

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
