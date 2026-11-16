State: DONE: merged to main. Archive: ready
Branch: claude/rig-cellar-enemies
Job: QUEUE "Rig the Cellars tier-1 enemies" (slime, broom, rat-king): Blender hand rigs + clips at 60 fps (1 frame == AttackFrame), EnemyArtBuilder (importer, URP materials, controllers), EnemyAnimatorDriver (presentation only, from replicated Enemy state), tests.
Check: PASS on bf1babc3 (EditMode 1811, PlayMode 303, 36 skipped); soak PASS 47/47.
Scores (strict self, /40): rat-king ~27, broom 26, slime ~24 (3 rounds each used; slime just under 25).
Sheets: Reference/sessions/rig-cellar-enemies/sheets/ (Blender poses) and captures/ (Unity, prefab driven by clip frame).
Open: slime jam/glass reads cream under the capture sky (unverified in Cellars light); no in-game PlayMode of the driver beyond check; ember eye emission overwritten during hit flash; build leaves Hollowmere .mat churn on main (not committed; coordinator should know); FloorCardTests 2000-seed sweep moved to Soak (timed out under load).
