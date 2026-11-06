Title: Floor 3 deaths + coop F3 SoftLock
Target: cloud
Status: done (merged; F3 = intended rat_king content, out heroes now stand up at the stair; see STATUS)
Started: 2026-11-06T22:51Z (cloud launcher)
Model: sonnet
From: coordinator session 10 (QUEUE.md: Floor 3 deaths S + Coop SoftLock finding on F3 S)
Status file: Reference/sessions/floor3-deaths/STATUS.md

Brief:
Context: Reference/sessions/dungeon1-difficulty/RESULTS.md (merged 5d2dcf8c). Guardrail (user 2026-11-06): dungeon one must not be extremely complex or overly difficult.
1. Plain DiveBot (no healing, no top-ups) dies on floor 3 on every seed with modifiers on or off (fails 49/30/33 on seeds 12345/31/7; 328 hits on F3 for seed 12345 against ~145 on F1). Seed 7's bot report logs "took damage with no enemy within 4 m" several times on F3, and seed 12345 logs a Combat finding on F5 ("fought 60 s without the room going quiet"). Find what deals the F3 damage (floor hazard, enemy hit reach, projectile, or a bug), with evidence from the bot's per-hit log (add the damage source to the bot's hit log if it is missing). Fix it if it is a bug. If it is intended content, report how hard F3 is against F1/F2 and propose a light softening that fits the guardrail; make the change only if it is a small tuning number in Core, with a test.
2. Coop finding (CoopBot_Pair_Seed12345, F3): "solo hero downed 30 s and nothing happened". Find the rule: a downed player alone should be revived or the dive should fail. Fix it with a PlayMode test (networked: follow sc-netcode; this needs a risk-reviewer pass before merge).
Rules: wait for `bash Tools/cloud/setup.sh --wait` before Unity runs. One PlayMode run at a time. Keep STATUS.md updated at each milestone (the board flags 2 h of silence as a stall). Merge on your own green check (marker == tip) per CLAUDE.md; soak if Core/Cellars changes. If Unity won't run, push the branch and set STATUS to "needs coordinator: PC check". Don't commit builder churn (.unity/.mat rewritten by cloud runs).
