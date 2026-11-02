Title: Combat polish: post-hit grace, libraries, JSON tuning loader
Target: cloud
Status: started
Started: 2026-11-01T21:48Z (cloud launcher)
Model: sonnet
From: coordinator session 6, user decisions 2026-11-01 (docs/PHASE1.md change log)
Status file: Reference/sessions/combat-polish/STATUS.md

Brief:
Three pieces, each on its own branch, each merged to main on a green `bash Tools/cloud/unity.sh check` with origin/main merged in right before. Merge commits, no PRs. Keep your board profile current, and use the board merge queue (state merge-ready) instead of messaging.
1. Post-hit invulnerability grace (docs/research/combat-timings.md calls it the top "too hard" fix). Add it in GuardMath.cs and HeroCombat.cs (hit resolve around lines 547-584; HurtStagger is 0.25f at line 23). The section also covers a red hurt pulse and a knockdown at knockback >= 3. Host-authoritative: follow the sc-netcode skill and get a risk-reviewer pass. Test the window edges in EditMode, and add a PlayMode host+client hit test.
2. Packages: add Unity Animation Rigging, PrimeTween (check its LICENSE first and record it in docs/research/libraries.md) and Unity Behavior (official). Install only, plus one tiny smoke use each where it fits naturally (a tween on an existing HUD element; Behavior and Rigging installed with no gameplay use yet). Also evaluate CristianQiu volumetric light: check its licence and URP 17.6 compatibility; install only if both are fine, otherwise note why not. Keep ArchitectureTests green (Gameplay, Presentation and UI rules).
3. JSON tuning loader in Core (pure C#, Newtonsoft is already in the project). Load optional tuning files (for example `StreamingAssets/Tuning/*.json`, and a user override folder for mods) over the code defaults for AttackCatalog, Poise, WeaponGrowth, TargetingConfig and the HeroCombat constants. Do NOT touch the enemy tables (EnemyModel, EnemyRules, Core/Combat/Ai): another session is changing enemy AI now. Leave a TODO hook for them. Unknown or invalid values: keep the default and log once. Tests: defaults unchanged with no files; an override applies; bad JSON is ignored. Update docs/design/m3-combat-greybox.md (line 141) to describe the loader.
Usage rules: .claude/rules/session-usage.md (Sonnet subagents for builds and Unity runs).
