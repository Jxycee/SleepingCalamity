State: IN PROGRESS
Session: Wave 1: Market stalls x3 (fixed, ruined + rebuilt)
Progress: 35%
Sheet: none yet (round 1 renders in progress)
Blocker: none
Score: -
Tris: produce 49.0k/49.1k, cloth 39.5k/48.4k, tools 37.3k/48.5k (rebuilt/ruined, look stage, before trims)
SC RESULT: - (Unity editor still downloading in this container)
Merge commit: -
Notes:
- sc_market_stall.py extended: --variant produce|cloth|tools --state rebuilt|ruined; no --variant = the approved B8 MarketStall unchanged. --combine writes Stall_<V>.fbx with children Ruined + Rebuilt (one baked 2K set each).
- Ruined: racked frame, flaked bleached paint, torn bleached awning (holes, missing scallops, hanging flap), snapped pennant rope, missing boards and brace, slipped shelf, empty slatted crates, weed clumps at the legs.
- Rebuilt goods: produce = apples/pears crates, cabbage basket, squash, loaves, jars; cloth = folded stacks, bolts, hung lengths, wool skeins, spool sign; tools = hung tool rail, pots, kettle, horseshoes, nail tray, barrel of long tools, hammer sign. Iron lantern on the hook (all rebuilt).
- Merged main (terrain pass 1e20cb2) before the builder edit. HollowmereBuilder: stall rows only (Ring1/Stalls art at the landmark row incl. its height; drops the corner B8 stall, its lantern and the B7 goods on its counter once the produce art is in).
- Checked first: the stall reuses the existing procedural generator and bake path (sc_market_stall.py, sc_pack_unity.py); no new tool.
