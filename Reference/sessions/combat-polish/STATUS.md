Status: DONE
Started: 2026-11-01T21:49:14Z
Finished: 2026-11-02 (local PC analysis session took over from the stalled cloud run; no Unity licence there)
Archive: ready
Plan: 1 post-hit grace, 2 packages, 3 JSON tuning loader (each own branch)

Merged (each tip check PASS, merged directly per the no-re-check rule):
- Packages: 660a026f (claude/packages c08ec012). Animation Rigging 6.6.0 (embedded in the 6.6 editor), Unity Behavior 1.0.16, PrimeTween 1.5.2 via the npm scoped registry (custom licence grant: npm route required, never ship its source to modders; smoke use: StirMeterHud fill ease), CQ URP volumetric fog 0.5.91 pinned (MIT, compiles on 6.6, not enabled yet: needs a quality toggle). libraries.md updated.
- Tuning loader: a1488aea (tuning-loader f4dea50d). Review fixes: hand-written MiniJson replaced by Newtonsoft (Core already uses it); ordered-pair validation; 10 more tests. Enemy tables untouched; RegisterSection("enemies") TODO hook for the enemy AI owner.
- Post-hit grace: ea542cbd (combat-grace 75b0ac7b). Two risk reviews. Values from combat-timings recs 1-3 (light 0.8 s, heavy 1.2 s incl. 0.9 s knockdown, multi-hit 0.15 s; guard break = heavy); Core GraceTimer; dodge/parry still resolve during grace; host heavy flag in HurtClientRpc so all peers match; red pulse through the flash limiter; owner grace pauses with the knockdown.

Follow-ups (not done here):
- No parry-during-grace PlayMode test (dagger setup heavy); the dodge case is covered.
- Move the HurtGrace values into TuningSections (loader and grace landed separately).
- Dead code: HeroAnimMap.IsHeavyHurt, HurtGrace.Knockdown.
- Volumetric fog quality toggle and look pass.
- Flaky DiveSpineNetworkTests.Contents_ArmedByTheDirector_... (board note) for the enemy AI owner.
