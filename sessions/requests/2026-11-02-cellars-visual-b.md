Title: Cellars and Undercroft visual fixes (review B2-B6)
Target: cloud
Status: blocked: B2-B6 coded on branch cellars-visual-b (check green) but cloud renders black so look unverified; needs PC capture then merge
Started: 2026-11-02T16:31Z (cloud launcher)
Model: sonnet
From: coordinator session 7 (user offered cloud sessions, 2026-11-02)
Status file: Reference/sessions/cellars-visual-b/STATUS.md

Brief:
Fix the Cellars/Undercroft items from `Reference/qa/visual/TOWN_VISUAL_REVIEW.md` part B (B1 dust motes was fixed by town polish fd8c29a5; confirm and skip):
- B2: greybox leftovers in the undercroft (flat white unlit discs on the floor, a red glowing strip under a beam, a dark box). Find which builder emits them; hide or replace them in play (debug markers stay editor-only).
- B3: the plaster room before the stair is flat and blotchy, and its door is a dark slab. Give it real PBR plaster with variation and a proper door (reuse the town kit door if one fits).
- B4: the stair shaft rock is stretched and oversized (texel density about 3.3). Fix the UVs or scale to the area's density rules (docs/PHASE1.md section 3, sc-asset-budget).
- B5: the corridor walls wash out to near-white for about 0.4 s after the dark room (exposure adaptation). Tune the auto-exposure or the local light so the transition stays readable.
- B6: the undercroft is very dark, with pillars as black silhouettes. Compare against the approved dungeon corner (Reference/hollowmere/dungeon_corner) and lift it to match: warm gold firelit, no teal.
Scenes: `UndercroftStair` and the Cellars floor builder. Builders only (never hand-edit .unity/.prefab).
Rules: `.claude/rules/session-usage.md`. Sonnet subagents run Unity. Use captures with `SC_CAPTURE_ONLY` and the fewest cameras (the DungeonCorner and Undercroft captures work in the cloud; Hollowmere OOMs, so don't capture it; use `SC_CAPTURE_NOPROBES=1` if probes crash). At most 3 rounds, stop at about 25/40. Put before/after frames side by side in `Reference/sessions/cellars-visual-b/`.
Merge timing: the user tests manually from about 3:00pm CST (21:00 UTC) today, and Assets merges freeze from 2:30pm CST (20:30 UTC). Merge only what has a green check by 20:15 UTC: merge origin/main, run one `check`, then merge the checked tip into fresh main (no PR, never force-push). Anything later waits on the branch until the coordinator lifts the freeze; ask via board report. STATUS first line: `Status:` IN PROGRESS, DONE or BLOCKED.
Done when: B2-B6 are fixed or explained, merged on a green check, frames committed, Status DONE and Archive: ready.
