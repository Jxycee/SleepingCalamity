State: WORKING
Session: Local next: M5 QA walk (floors 1-4 via Arena_Dive)
Progress: 70% (walk test 3/3 PASS, no bugs; USER_TODO 3b script on main a821a952; QA test merges after procgen)
Status: waiting for procgen merge, then check + merge the QA test
Archive: not ready
Branch: claude/m5-next-639689
Last check: none
Next step: after "procgen merged": merge main into claude/m5-qa (51fac430), one check, merge
Sheet: none
Needs: none
Blocker: none

## Results
- Automated walk claude/m5-qa 51fac430: Tests/PlayMode/DiveWalkQaTests.cs, host + client (client drives via RPCs). test-play -Filter DiveWalkQa 3/3 PASS. Covered: dive start (client start refused), floor-1 stair shut until the gate key (carrier killed via ServerApplyHit, key picked up), chest loot into the haul on both peers, bakery piece + pieces-left on both peers, key chest + locked door, descents 1->2->3 (same LayoutHash, both at entry), floor-2 checkpoint bank + heal, stir 40 extras (room spawn count = plan) and 70 hunter + RatKing, floor-4 bank + go home (banked survives), failed second dive by party wipe (home, pieces-left restored, banked kept), third dive finds the piece again.
- Bugs found: none.
- Not automated: real melee (kills via ServerApplyHit), the E key/prompts (tests call the same Request methods), hub walking (owner teleport), stir 100 deep-close (spine tests). Note: DiveHaulSync.ServerFailDive alone does not end the dive (only DiveFailed, wipe, abandon do) - by design.
- USER_TODO item 3b rewritten as a 10-step click-by-click script (main a821a952).
