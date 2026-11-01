# M2 hero camera evidence (2026-11-01)
Commit: 5170431 (claude/m4-ui, merge of origin/main e525a51/498924b). Software-rendered (xvfb, no GPU) in a cloud container, 1280x720, JPG q88 from PNG.
Command: SC_CAPTURE_DIR=<scratch>/png bash Tools/cloud/unity.sh test-play --graphics --filter HeroCaptureTests --out <scratch>/cap --timeout 60  (PASS 1/1)

- free_1_spawn: hero at spawn, free camera behind.
- free_2_orbit_right: free camera orbited right; hero seen from the side/back with axe.
- free_2b_orbit_block_corner: orbit with a block at the corner of the view.
- free_3_wall_deoccluded: camera near a wall, de-occluded so the hero stays visible.
- lock_1_near_dummy: lock camera on the nearest dummy.
- lock_2_strafe_right: lock camera while strafing right (hero circles the target).
- lock_3_switched_target: lock switched to another dummy (yellow marker above the locked dummy).
