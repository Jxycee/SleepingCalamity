# PL-1 Loop proof STATUS
State: done. Merged to main from `afa1720a` (check PASS, SC MARKER == tip).
Archive: ready

Results: `RESULTS.md`. Tests: PlacementModeInputTests, DiveEntryInputTests (12/12), LoopBotTests (SC_BOT=1: solo Tough x3, Measure x3, PairLoop), EditMode additions (D1, D4, saved-frame load path).
Breaks for later sessions:
- PL-4 4.2: release trapdoor press with a held checkpoint starts floor 1 and spends the checkpoint; prompt still says "G changes the floor" (`DiveDirector.cs:765,825-830`; pinned by `ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint`). Not networking-only; floor pick needs a button.
- Pair client cannot be driven through PlacementMode in-process (declines scene loads); the two-process run is the user's walk. Client go-home press in the pair test is an RPC (FollowerBot).
- Flaky seen once: CombatArenaTests.Solo_SixRatsOnOneHero_DoNotStack (passed on rerun).
