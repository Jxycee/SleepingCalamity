Status: BLOCKED
Branch: plan5-nits tip 4067be5f (unmerged)
Check: not run - Unity editor missing in cloud session; sc_core_tests.py 1206/1206 PASS.
Done: key-floor card no longer prints "Find the gate key" when the exit is already open (FloorCard.ObjectiveLine exitOpen); card's pieces line dropped (StirMeterHud keeps it); valve ReportTimeout 0.6 -> 1.0 s so a short push out of the 1.8 m reach keeps the hold (reach and 0.5 m slack unchanged; longer absence still resets; test Valve_HoldSurvivesShortBlipOutOfReach).
Not done: follower bots splitting valves - FollowerBot has no goal pathing, needs nav plus a hold loop; left as a follow-up.
Next: run `unity.sh gate` then `check` on plan5-nits (touches FloorCardHud, CellarContentsSync, FloorCardHudTests, uncompiled), merge origin/main, merge. Needs soak (Core/Cellars touched).
Archive: ready
