Session: M1-M4 closeout: weapon-tree UI, abilities, captures
Job: (1) M4 greybox weapon-tree UI + host path; (2) M4 ability effects in CombatHost; (3) M2 HeroCaptureTests evidence images
Progress: 45%
Status: IN PROGRESS
  (1) weapon-tree UI: implemented (claude/m4-ui ee201b2), compiles; risk review: no blockers, 3 should-fix (HUD buttons unclickable with locked cursor -> push Menu context; key-repeat spams Fit -> edge-trigger + CanFit; tree RPC on every growth sync -> send on change). Fixes + Unity tests + check queued after (2).
  (2) ability effects: implemented (claude/m4-ab), test-edit 823/823 + test-play 38/0/8 PASS on 0782da4; risk review found the host never calls HeroAbilityBook.Update (abilities never unlock in real play) and asked for a 2-peer test; fix + merge origin/main + check running now.
  (3) M2 capture: first attempt died on a full disk (three worktrees importing Unity at once); runs after (1) in the single shared worktree.
Blocker: none now. Session disk allowance fits only one Unity Library, so (1)-(3) run one after another in one worktree.
Archive: no
Branch: claude/m4-ui, claude/m4-ab, claude/m4-cap (local worktrees, merged to main one by one)
Last check: -
Merge commits: -

- Previous job (polish 3) parked: Reference/hollowmere/polish3/STATUS.md on branch claude/admiring-brahmagupta-0jjuph (1d28819).
- (1) finding: no host path existed for build-up / re-route / attach / detach; the implementer adds owner->host ServerRpcs on HeroCombat that call Core on the host and re-push growth. Re-route's "home workbench" rule sits behind a named host predicate (true in the arena greybox) until a workbench exists.
- (2) finding: Core had unlock rules only (AbilityCatalog, 8 placeholder ids, MaxTier 2), no effects. Placeholder effects in one Core tuning class, user to tune: tier = MaxTier on the ability's own weapon family, MaxTier-1 on others. axe_quake finisher dmg +15%/tier; axe_cinder_sweep finisher fire +5/tier; staff_spark_ward incoming dmg -10%/tier; staff_warden_pulse spell dmg +20%/tier; dagger_riposte +25%/tier for 2 s after a parry; dagger_bane_flurry +10%/tier vs pest; bow_eagle_eye crit +5%/tier; bow_ember_volley charged fire +5/tier.
- Review notes for the user (placeholders, tune later): dagger_riposte stacks with the parry stagger's open-hit bonus (x2.25 for the first 1.1 s); ability fire sets ElementMatch on fire-weak enemies (pays the tech ABS bonus); bow_ember_volley applies to the Charge attack, not the aimed shot.
