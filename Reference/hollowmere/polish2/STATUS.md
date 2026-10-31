State: PAUSED
Session: Town polish 2: wall stone, merlons, ruined clutter
Progress: 60%
Sheet: round 1 in-engine (Ruined only, scratch): WallRun / LowerGate / EastGate; final sheet committed with round 2
Needs: Q14 clutter on main (fix 3 builds on HollowmereClutter); Q15 merged first (both touch HollowmereTownWall.cs)
Blocker: paused by coordinator for usage
Archive: no

Job: coordinator notes (1) wall stone tone/coursing, (2) merlon rhythm, (3) Ruined clutter near lanes; max 2 rounds
Branch: claude/hopeful-planck-afz6d9
Status:
- (1) done in sc_town_wall.py: plain rubble over 4 tints (new slots TW_RubbleDark/Warm/Pale, also in HollowmereTownWall TwSlots), wandering course lines, split and pinning stones; patches and dressed ends stay regular.
- (2) done: every merlon varies in width/height/offset/yaw; Rebuilt chips ~1 in 10; new Wall_Straight_A/B_Ruined (chip ~1/3, missing ~1/3) used by the builder in Ruined; gate battlements vary, ruined Shell chips ~1/3.
- All 10 wall FBX + both gates re-exported (budgets met), SOURCES.md updated. New camera Cap_HM_WallRun.
- (3) waiting for Q14: plan is a LaneWrecks pass after AddRuined (Handcart_Broken tipped, RainBarrel_Broken, CrateStack_Broken at lane edges, every row through Q14's Fits = path-graph safe).
Next (on RESUME): review in-engine capture; Q14 lands -> fix 3 + Ruined lane capture; after Q15 lands merge origin/main, regenerate Hollowmere (-nographics), one check, merge.

Score: round 1 wall self 29.5/40 (was ~28.5): tint spread and broken gate battlements read in engine; WallRun camera was in the ditch (moved onto the bank top); Rebuilt not seen (capture run OOM-killed at ~10 GB on the second preset: round 2 runs one preset per run via SC_CAPTURE_PRESET after Q15 lands)
SC RESULT: - (no check yet)
Merge commit: -

## Usage waste
- round 1 capture (23 min, first Library import included) lost its Rebuilt half to an OOM kill: both presets in one graphics run exceed the container's memory; one preset per run from now on
