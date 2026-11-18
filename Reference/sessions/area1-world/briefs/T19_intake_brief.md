# T19 intake brief (Haiku): 25 Megascans items

Goal: adopt 25 downloaded Megascans items as prefabs through the existing intake. Follow exactly.

Worktree: C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725 (branch claude/area1-world). Work only here. If `Reference/sessions/area1-world/STATUS.md` shows modified, it is the controller's: leave it, never commit it. Scratch: C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\6cea6def-8e61-49b6-8ea3-1619f7abe648\scratchpad\run7\intake4

1. Copy each source folder's inner `*_mid/` (or `*_2k/`) contents into `Art/Source/Fab/<code>_<name>/` (bash; SRC=/c/Users/jayce/AppData/Local/com.epicgames.eglv2/FabLibrary; `cp "$SRC/<folder>"/*/* <dst>/`). Stop if any file name has 4K or 8K. Map:
   T19a_wooden_grave_cross  <- "Wooden Grave Cross-e479fe93"
   T19b_farm_bench_m01      <- "Historic Medieval Farm Bench Wood M 01-b23a5b46"
   T19c_farm_bench_m03      <- "Historic Medieval Farm Bench Wood M 03-e88e0d5e"
   T19d_church_wall_straight01 <- "Historic Medieval Church Ruin Wall Stone Straight 01-7546d222"
   T19e_church_wall_modular01  <- "Historic Medieval Church Ruin Wall Stone Modular 01-d4b3c149"
   T19f_church_wall_modular02  <- "Historic Medieval Church Ruin Wall Stone Modular 02-cccfcee0"
   T19g_church_wall_top01      <- "Historic Medieval Church Ruin Wall Stone Top 01-dfc2b341"
   T19h_church_wall_top02      <- "Historic Medieval Church Ruin Wall Stone Top 02-63a859a1"
   T19i_church_wall_niche01    <- "Historic Medieval Church Ruin Wall Stone Niche 01-d78f1074"
   T19j_church_wall_niche02    <- "Historic Medieval Church Ruin Wall Stone Niche 02-a975ed2e"
   T19k_church_wall_skirt01    <- "Historic Medieval Church Ruin Wall Stone Skirt 01-294d9fb3"
   T19l_church_wall_skirt02    <- "Historic Medieval Church Ruin Wall Stone Skirt 02-f9ce3271"
   T19m_farm_whetstone      <- "Historic Medieval Farm Whetstone-6e1d6237"
   T19n_farm_scoop          <- "Historic Medieval Farm Scoop Wood-6a76e313"
   T19o_farm_chair          <- "Historic Medieval Farm Chair Wood-392cd474"
   T19p_farm_hayfork        <- "Historic Medieval Farm Hayfork Metal-992b19aa"
   T19q_farm_plow           <- "Historic Medieval Farm Plow Wood-3f569cb2"
   T19r_farm_tool_hack      <- "Historic Medieval Farm Tool Hack-da626b9c"
   T19s_farm_pot_s          <- "Historic Medieval Farm Pot S-48a29ad2"
   T19t_farm_pitchfork      <- "Historic Medieval Farm Pitchfork Wood-fa40713b"
   T19u_sowthistle          <- "Sowthistle-48a50407"           (plant)
   T19v_hemp_nettle         <- "Common Hemp Nettle-e52ee219"   (plant)
   T19w_yellow_flower       <- "Yellow Flower-297473a0"        (plant)
   T19x_white_windflower    <- "White Windflower-c424145b"     (plant)
   T19y_sunflower           <- "Sunflower-c6ba9469"            (plant)
2. Plants: in `Assets/_Project/Editor/Builders/B1TownIntake.cs`, the `Plants` HashSet (around line 27) lists "T12a".."T12e". Add "T19u", "T19v", "T19w", "T19x", "T19y" (plants use VarA LOD0..LOD2 under a LODGroup, alpha clip, two-sided). Change nothing else.
3. Append 25 rows to `Art/Source/Fab/SOURCES.md` in the `| T18a |` row format: code, name, URL column = `Fab listing "<folder title without the -id suffix>" (Launcher library id <id8>)`, licence "Fab Standard License (Quixel Megascans)", author "Quixel Megascans", fetched 2026-11-17, files "Mid tier (2K) FBX, via Epic Launcher; mesh + 2K maps copied as published" (plants: "Mid tier (2K) FBX VarA-E LOD0-3, via Epic Launcher").
4. Run in the background, wait for the notice (no sleep/poll loops): `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725\Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.B1TownIntake.Run -OutDir <scratch>`. Need exit 0 and "failed=0". Exit 2 = LOCKED: stop, report, never kill Unity. If one code fails, report its error line and do not guess a fix.
5. Check each `Assets/_Project/Content/Art/Town/Adopted/T19x/` has fbx (plants: LOD fbx files), prefab, albedo/normal/ms/occ with .meta, and `Materials/B1/M_T19x.mat`.
6. `git status --porcelain`: revert files you did not create (`git checkout -- <file>`), except STATUS.md; known churn: M_GD_*, M_StoneCottage_B_SC_Rubble, M_HM_*, M_HH_OldStone, M_RD_RoadHedge, M_Slime_Glass (.mat), T_Water_N.png.meta, CellarFloorSync.prefab, CellarFloorTest.unity; delete untracked files under Art/Staging/area1-world/shells/.
7. Commit only `Art/Source/Fab/T19*`, `Art/Source/Fab/SOURCES.md`, `B1TownIntake.cs`, `Assets/_Project/Content/Art/Town/Adopted/T19*` (with folder .meta files), `Assets/_Project/Content/Materials/B1/M_T19*.mat(.meta)`. Message:
   T19a-y: adopt 25 Megascans items (church ruin wall kit x9, grave cross, farm benches, medieval farm tools, 5 wild plants) through B1TownIntake

   Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
8. Gate in the background: `...\Tools\unity.ps1 gate -OutDir <scratch>\gate`; `SC MARKER` = HEAD; `git push`.

Report under 12 lines: commit, exec and gate lines, all 25 "SC B1 BOUNDS T19" lines condensed to `code WxHxD`, problems.
