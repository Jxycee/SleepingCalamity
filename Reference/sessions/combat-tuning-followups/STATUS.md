Status: DONE (partial, see Skipped; Unity compile / gate / check and the new PlayMode test NOT run: no Unity editor or licence in this cloud session)
Branch: claude/combat-tuning-followups
Tip: see `git log -1` on the branch (this file is committed with the work)
Archive: ready
Merged: no (the request said push only)

Done:
- Parry-during-grace PlayMode test: CombatArenaTests.Solo_DaggerParry_InsideTheGraceWindow_StillStunsTheAttacker (written, not run).
- HurtGrace values moved into Core/Tuning/TuningSections.cs `HurtGraceTuning`, section "hurtGrace" (heavyKnockback, lightSeconds, heavySeconds, multiHitSeconds, knockdownSeconds, flickerSeconds; heavySeconds < knockdownSeconds rejects the section). HurtGrace.* are properties reading it. Users (HeroCombat, CombatHost, HeroHurtPulse) unchanged: they call the HurtGrace methods. Tests: 2 new in TuningStoreTests.
- Removed dead HeroAnimMap.IsHeavyHurt + HeavyHurtFraction and HurtGrace.Knockdown (grep over Assets: only their own tests used them; sc_lsp refs unavailable without a csproj) and their tests.
- TuningSections stale TODO pointer fixed (comment).

Tests: sc_core_tests.py full run PASS 1205/1205 (105 of 157 EditMode files; 1 .NET-only skip). Unity compile, gate, check, PlayMode: not run.

Skipped: enemy attack ids tunable. Bigger than small: AttackCatalog enemy attacks are static readonly AttackData held by reference in EnemyDefinition.Attacks, AllEnemyAttacks (tests index it by position), EnemyAttackById, the AI and tell rules; tuning needs replace-in-place or registry lookups across Core/Combat and Gameplay plus EnemyAttackRules validation. Note left in the TuningSections.cs comment.
