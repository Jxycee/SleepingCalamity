Status: BLOCKED
Step 0 done on branch m6-s3-townsource (b4ddcf5f): Core tests 518/518 via sc_core_tests.py; DiveDirector/DiveHaulSync changes uncompiled. Not merged (no green unity.sh check).
Blocker: Unity editor not installed in this cloud session (download still running, no licence), so compile/check/PlayMode are impossible. S4 and S5 not started.
Open decision: banked loot double-count fixed by restoring from Stock.Loot only (min(banked, stock) per item); a persisted MovedLoot in DiveSave is needed if the town ever spends loot.
Next: rerun with the editor ready (setup.sh --wait), compile+check the branch, risk-review, merge, then S4, S5.
