Session: M1-M4 closeout: weapon-tree UI, abilities, captures
Job: (1) M4 greybox weapon-tree UI + host path; (2) M4 ability effects in CombatHost; (3) M2 HeroCaptureTests evidence images
Progress: 10%
Status: IN PROGRESS: (1) and (2) implementers running in parallel worktrees; (3) waiting for the cloud Unity editor download
Blocker: none
Archive: no
Branch: claude/m4-ui, claude/m4-ab, claude/m4-cap (local worktrees, merged to main one by one)
Last check: -
Merge commits: -

- Previous job (polish 3) parked: Reference/hollowmere/polish3/STATUS.md on branch claude/admiring-brahmagupta-0jjuph (1d28819).
- (1) finding: no host path existed for build-up / re-route / attach / detach; the implementer adds owner->host ServerRpcs on HeroCombat that call Core on the host and re-push growth. Re-route's "home workbench" rule sits behind a named host predicate (true in the arena greybox) until a workbench exists.
- (2) finding: Core had unlock rules only (AbilityCatalog, 8 placeholder ids, MaxTier 2), no effects. Placeholder effects in one Core tuning class, user to tune: tier = MaxTier on the ability's own weapon family, MaxTier-1 on others. axe_quake finisher dmg +15%/tier; axe_cinder_sweep finisher fire +5/tier; staff_spark_ward incoming dmg -10%/tier; staff_warden_pulse spell dmg +20%/tier; dagger_riposte +25%/tier for 2 s after a parry; dagger_bane_flurry +10%/tier vs pest; bow_eagle_eye crit +5%/tier; bow_ember_volley charged fire +5/tier.
