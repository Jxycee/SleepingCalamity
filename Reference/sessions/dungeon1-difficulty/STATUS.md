Status: DONE

Results: see RESULTS.md in this folder.
- Tough bot, 3 seeds (7, 31, 12345) clear floors 1-7 in 827-1697 s, modifiers ON and OFF alike; DiveBot_Tough_Seed12345 bar closed (3 of 3).
- Modifiers ON vs OFF within noise; no tuning softened. Plain bot (no healing) dies on F3 either way (bot limit).
- Party-size gap fixed in Core (e9e7d6db): template room enemy count = plan count + CoopScaling extras per extra player; solo unchanged. check PASS (EditMode 1626, PlayMode 199/0 fail), soak PASS 46/46.
- Open: live 2-4 player bot run on the fixed tip; coop host SoftLock finding on F3 ("solo hero downed 30 s and nothing happened"); build churn in M_HM_Surf_WornRun.mat, BeautyCorner_Hollowmere/CellarFloorTest/Hollowmere .unity after check (churn bug, not committed).
- ON/OFF numbers recovered from a run killed by a container restart; OFF toggle (likely FloorModifierTuning percents = 0) not recorded.

Archive: ready
