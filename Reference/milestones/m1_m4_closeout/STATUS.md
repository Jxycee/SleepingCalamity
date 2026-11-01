Session: M1-M4 closeout: weapon-tree UI, abilities, captures
Job: (1) M4 greybox weapon-tree UI + host path; (2) M4 ability effects in CombatHost; (3) M2 HeroCaptureTests evidence images
Progress: 100%
Status: DONE
  (1) weapon-tree UI: DONE, merged ded7cd2 (check PASS 5170431: edit 836/836, play 46/0/8). Review fixes: Menu context while open (cursor free, clicks not attacks), edge-triggered keys respecting CanFit/CanBuildUp/CanReroute, tree push deduped by SyncKey (forced on spawn, swap, click). Needs a manual walk (B, 1/2/3, Shift+1/2/3, F7, F8). Gaps by design: no attachment inventory (fit always in stock, returned attachments dropped), AtWorkbench stub true.
  (2) ability effects: DONE, merged ef7aced (check PASS d8c5b0d).
  (3) M2 capture: DONE, 7 images in Reference/milestones/m2/ (HeroCaptureTests PASS 1/1, --graphics, on 5170431).
Blocker: none
Archive: ready
Branch: claude/m4-ui, claude/m4-ab, claude/m4-cap (local worktrees, merged to main one by one)
Last check: PASS 5170431 (claude/m4-ui)
Merge commits: ef7aced (2), ded7cd2 (1)

- Previous job (polish 3) parked: Reference/hollowmere/polish3/STATUS.md on branch claude/admiring-brahmagupta-0jjuph (1d28819).
- (1) finding: no host path existed for build-up / re-route / attach / detach; the implementer adds owner->host ServerRpcs on HeroCombat that call Core on the host and re-push growth. Re-route's "home workbench" rule sits behind a named host predicate (true in the arena greybox) until a workbench exists.
- (2) finding: Core had unlock rules only (AbilityCatalog, 8 placeholder ids, MaxTier 2), no effects. Placeholder effects in one Core tuning class, user to tune: tier = MaxTier on the ability's own weapon family, MaxTier-1 on others. axe_quake finisher dmg +15%/tier; axe_cinder_sweep finisher fire +5/tier; staff_spark_ward incoming dmg -10%/tier; staff_warden_pulse spell dmg +20%/tier; dagger_riposte +25%/tier for 2 s after a parry; dagger_bane_flurry +10%/tier vs pest; bow_eagle_eye crit +5%/tier; bow_ember_volley charged fire +5/tier.
- Review notes for the user (placeholders, tune later): dagger_riposte stacks with the parry stagger's open-hit bonus (x2.25 for the first 1.1 s); ability fire sets ElementMatch on fire-weak enemies (pays the tech ABS bonus); bow_ember_volley applies to the Charge attack, not the aimed shot.
- Known: every check build rewrites Hollowmere.unity and CellarFloorTest.unity even right after committing the build output (non-deterministic builder output); restored, not committed.
