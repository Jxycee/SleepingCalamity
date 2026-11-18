# T27 intake brief: 17 Megascans props for T25 life stations and T13c (follow exactly)

Goal: adopt 17 downloaded Megascans 3D props as prefabs through the existing intake, exactly as T19/T20 did (`briefs/T20_intake_brief.md`, commit 33cfd146: same recipe, same checks). Follow it step by step with the map below; nothing else.

Worktree: `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725` (branch `claude/area1-world`). Work only here. `Reference/sessions/area1-world/STATUS.md`, the untracked briefs and `Reference/sessions/area1-world/verdicts/` are the controller's: leave them, never commit them. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\54c4d5da-7980-453e-9883-d2236b06f4b2\scratchpad\T27`.

1. Copy each source folder's inner `*_mid/` contents into `Art/Source/Fab/<code>_<name>/` (bash; `SRC=/c/Users/jayce/AppData/Local/com.epicgames.eglv2/FabLibrary`; `cp "$SRC/<folder>"/*/* <dst>/`). Stop if any file name has 4K or 8K. Map:
   T27a_axe_a                  <- "Axe-33fba67a"
   T27b_axe_b                  <- "Axe-c2c866c9"
   T27c_chopping_block_a       <- "Wooden Chopping Block-8618feb4"
   T27d_chopping_block_b       <- "Wooden Chopping Block-942c4e75"
   T27e_old_trough_long        <- "Old Wooden Trough-7cc81f8c"
   T27f_old_trough_small       <- "Old Wooden Trough-7a7df07d"
   T27g_watering_can           <- "Metal Watering Can-1b2a1890"
   T27h_wooden_ladder_a        <- "Wooden Ladder-4156fdc9"
   T27i_wooden_ladder_b        <- "Wooden Ladder-9a062ac0"
   T27j_wicker_basket          <- "Wicker Basket-7c7dfb52"
   T27k_old_wicker_basket      <- "Old Wicker Basket-ced46ddf"
   T27l_old_crate_a            <- "Old Wooden Crate-25fcaec7"
   T27m_old_crate_b            <- "Old Wooden Crate-c94e4a8b"
   T27n_wooden_bookshelf       <- "Wooden Bookshelf-7c509eeb"
   T27o_ceramic_flower_pot     <- "Ceramic Flower Pot-4d45a563"
   T27p_wooden_fence           <- "Modular Wooden Fence-117eb23f"
   T27q_wooden_window          <- "Modular Wooden Window-48fa2137"
2. No plants in this batch: do not touch the `Plants` set. If `B1TownIntake` needs the codes listed somewhere to pick them up (check how T20/T21 were picked up, by folder prefix or a list), make only that change.
3. Append 17 rows to `Art/Source/Fab/SOURCES.md` in the `| T19a |` row format (licence "Fab Standard License (Quixel Megascans)", author "Quixel Megascans", fetched 2026-11-18, "Mid tier (2K) FBX, via Epic Launcher").
4. Run in the background and wait for the notice (no sleep/poll loops): `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725\Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.B1TownIntake.Run -OutDir <scratch>`. Need exit 0 and "failed=0". Exit 2 = LOCKED: stop, report, never kill Unity. If one code fails, report its error line and do not guess a fix.
5. Check each `Assets/_Project/Content/Art/Town/Adopted/T27x/` has fbx, prefab, maps with .meta, and `Materials/B1/M_<code>.mat`.
6. `git status --porcelain`: revert files you did not create (`git checkout -- <file>`), except the controller's files above. Known churn: M_GD_*, M_StoneCottage_B_SC_Rubble, M_HM_*, M_HH_OldStone, M_RD_RoadHedge, M_Slime_Glass (.mat), T_Water_N.png.meta, ShopSign_Inn.fbx.meta, CellarFloorSync.prefab, CellarFloorTest.unity, B9 `.fbx.meta` files. Delete untracked files under Art/Staging/area1-world/shells/.
7. Commit only `Art/Source/Fab/T27*`, `Art/Source/Fab/SOURCES.md`, `B1TownIntake.cs` (if changed), `Assets/_Project/Content/Art/Town/Adopted/T27*` (with folder .meta files), `Assets/_Project/Content/Materials/B1/M_T27*` (.mat and .meta), and this brief. No `.unity` file. Message:
   T27a-T27q: adopt 17 Megascans props (axes, chopping blocks, troughs, watering can, ladders, baskets, crates, bookshelf, flower pot, post-and-rail fence, window) through B1TownIntake

   Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
8. Gate in the background: `...\Tools\unity.ps1 gate -OutDir <scratch>\gate`; `SC MARKER` = HEAD; `git push`.

Report under 12 lines: commit, exec and gate lines, all 17 "SC B1 BOUNDS" lines condensed to `code WxHxD`, problems.
