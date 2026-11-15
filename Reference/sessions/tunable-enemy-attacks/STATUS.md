State: done, gate PASS (compile + EditMode 1804/1804), branch claude/tunable-enemy-attacks not merged, not pushed

QUEUE 53 "Enemy attack ids tunable", no balance change.

- AttackData: tunable fields are get/private-set properties; `Retune(v)` writes them in place, `Copy()` clones.
- AttackCatalog: `s_enemyDefaults` snapshot, `ResetEnemyAttacks()`, `TuneEnemyAttack(id, values)`.
- Tuning sections `enemyAttacks` (EnemyAttackTuning) and `enemyPacing` (EnemyPacingTuning, AttackPacing bases/tiers now settable statics). Shared logic with `attacks` (AttackTuning.Tuned), plus aliases mult / hitOnFrame / hitOffFrame so simulator override files apply verbatim.
- `EnemyAttackRules.Problem(a)` combines the existing rules; an override that breaks one is rolled back whole with a warning.
- Tests: Tests/EditMode/Combat/EnemyAttackTuningTests.cs (pin table, identity, reset, rejects, pacing) and an alias test in BalanceExportTests. game_numbers.json untouched (exporter unchanged).
- README of Tools/balance_sim: mapping table and the apply recipe.

Limits: EnemyTell frames (pose end, glint commit) are not tunable, so hitOn changes must keep the 12-24 frame glint lead; `enemies.<Kind>.hp/atk/def/speed` stays a separate item.
