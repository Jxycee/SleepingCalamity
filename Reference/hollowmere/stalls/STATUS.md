State: IN PROGRESS
Session: Wave 1: Market stalls x3 (fixed, ruined + rebuilt)
Progress: 90%
Sheet: Reference/hollowmere/stalls/stalls_sheet_r4.jpg
Blocker: none
Score: round 4 (self): produce rebuilt 30.5, produce ruined 31, cloth rebuilt 31.5, cloth ruined 29.5, tools rebuilt 28.5, tools ruined 29 (round 1: 30/29.5/26/26). APPROVED by the coordinator (round 4); 2K bakes: rebuilt x3 done, ruined x3 re-baking with weed UVs packed into one strip (frame texel density was halved by blade islands)
Tris (2K export, audit PASS --class hero, 1 material per state): produce 49.0k rebuilt / 52.2k ruined, cloth 41.3k / 40.1k, tools 43.2k / 51.0k; texel 123-168 px/m
SC RESULT: check running on 46e5299 (main merged: bakery + ruin integration)
Merge commit: -
Notes:
- sc_market_stall.py extended: --variant produce|cloth|tools --state rebuilt|ruined; no --variant = the approved B8 MarketStall unchanged. --combine writes Stall_<V>.fbx with children Ruined + Rebuilt (one baked 2K set each).
- Ruined: racked frame, flaked bleached paint, torn bleached awning (holes, missing scallops, hanging flap), snapped pennant rope, missing boards and brace, slipped shelf, empty slatted crates, weed clumps at the legs.
- Rebuilt goods: produce = apples/pears crates, cabbage basket, squash, loaves, jars; cloth = folded stacks, bolts, hung lengths, wool skeins, spool sign; tools = hung tool rail, pots, kettle, horseshoes, nail tray, barrel of long tools, hammer sign. Iron lantern on the hook (all rebuilt).
- Merged main (terrain pass 1e20cb2) before the builder edit. HollowmereBuilder: stall rows only (Ring1/Stalls art at the landmark row incl. its height; drops the corner B8 stall, its lantern and the B7 goods on its counter once the produce art is in).
- Checked first: the stall reuses the existing procedural generator and bake path (sc_market_stall.py, sc_pack_unity.py); no new tool.
