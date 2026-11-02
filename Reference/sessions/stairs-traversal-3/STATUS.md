Status: BLOCKED (partial; branch claude/stairs-traversal-3 at 38c5eb5 NOT merged; before/after frames are on that branch)
Archive: ready
Done: job 2 (Terrace black sliver = tilted Gutter bars on lane segments beside flights; LaneKitPlan.KerbsAndGutters now skips gutters/grates next to a flight; golden rows 1201->1098).
Partial: job 1 (A9): lane-flight tread shade under each riser deepened 0.09->0.14 m only; no new treads/risers, not clearly visible. Keep stairs already read as steps.
Not done: job 3. R2_23/R2_16 sit inside the town wall: needs a lot-placement change in HollowmereLayout. Scan (town only, after jobs 1/2): Ruined 364 total (Unreach 2, Stuck 3, Blocked 25, Island 14); Rebuilt 340 (1, 0, 23, 17). No delta, no baseline written.
Check: gate PASS (compile + EditMode 1193) on the working tree before commit, marker c4a9ba6, not the tip. No full check. Tip push of the STATUS commit was blocked by the gate guard (tip not gated).
Needs: PC session: build (Linux build rewrites Hollowmere.unity ~300k float-noise lines; scenes were reverted, not committed), then check and merge; then finish job 1 treads and job 3.
Capture: cloud Hollowmere capture needs `unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll` (~20-25 min).
