State: merged to main (check PASS, marker 8ac9396e)
Archive: ready
Done: per-player enemy drops (CellarLoot.RollEnemyDrop, CellarContentsSync TickDrops) and hash bar (ReplicatedStateHash.AssertBagsAndStirMatchHost). Full check PASS on d72d18e0 (SC MARKER = d72d18e0). Risk review (Opus): no blockers.
93c904e4 = review fixes (despawn test via ServerClearEnemies, real CombatHost hit-path test, rank bonus passed, key stride, comments). Gate + PerPlayerLoot|StateHashSmoke 32/32 passed before a mutation proof; not re-run after revert; no check on 93c904e4.
Next:
1. Run gate, then check on the tip (SC MARKER = HEAD).
2. Docs in the merge commit: PARKING_LOT :64 (drops done), tick QUEUE lines 62 and 67, CURRENT.md "Now" line.
3. sc-merge into fresh origin/main (pre-approved 2026-11-14); merge before the network-conditions/smoke session if both ready.
Note: HitLog is deliberately not copied at queue time (killing blow is appended after ServerDied); copy at pay time if wanted.
