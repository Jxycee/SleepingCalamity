State: DONE
Session: Wave 1: Market stalls x3 (fixed, ruined + rebuilt)
Progress: 100%
Sheet: Reference/hollowmere/stalls/stalls_sheet_r4.jpg
Blocker: none
Merge commit: d1ecf3c (main; tree identical to the checked branch tip 3822958)
SC RESULT check PASS (3822958: build PASS, test-edit 592/592, test-play 32 passed 0 failed 8 skipped; first run on c38e0e3 identical result)
Score: round 4 approved by the coordinator (coordinator: produce 30/30, cloth 30/28, tools 27/28 rebuilt/ruined; self: 30.5/31, 31.5/29.5, 28.5/29). Rounds: 1, 3, 4 (round 2 superseded mid-run).
Tris (audit PASS --class hero, 1 material per state, 2048 maps): produce 49.0k rebuilt / 52.2k ruined, cloth 41.3k / 40.1k, tools 43.2k / 51.0k; texel 123-168 px/m (weed blades packed into one atlas strip)
Export: Assets/_Project/Content/Art/Town/Ring1/Stalls/<Produce|Cloth|Tools>/Stall_<V>.fbx (children Ruined + Rebuilt) + Stall_<V>_<State>_BaseColor/_Normal/_ORM, SOURCES.md one level up.
Integration: HollowmereBuilder stall rows only: each landmark (layout row incl. its height) instances its variant FBX, keeps the preset's state child, URP Lit material from that state's set, box collider. Once the produce art is in, the corner's B8 MarketStall, its counter lantern and the B7 goods on its counter are left out of Hollowmere (the stall carries its own); BeautyCorner scene untouched.
Generator: Art/Blender/town/sc_market_stall.py --variant produce|cloth|tools --state ruined|rebuilt [--stage bake --res 2048 --export], then --combine; no --variant = the approved B8 MarketStall unchanged.
Checked first: reused the existing procedural stall generator, bake path and sc_pack_unity.py; references via sc_ref_fetch.py (Reference/hollowmere/stalls/REFS.md). No new tool.
Note: each cloud check run rewrites HeroAnimator.controller(.meta) (pre-existing) and, from the second run, sets _MainTex on the six M_Stall_* materials (URP mirrors _BaseMap; same settle as the ruin foliage); left uncommitted.
Needs: none
