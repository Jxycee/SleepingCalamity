# T20/T21 intake brief (Haiku): 42 Megascans items

Goal: adopt 42 downloaded Megascans items as prefabs through the existing intake, exactly as T19 did (`briefs/T19_intake_brief.md`, commit d490809c: same recipe, same checks). Follow exactly.

Worktree: C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725 (branch claude/area1-world). Work only here. If `Reference/sessions/area1-world/STATUS.md` or other files under `Reference/sessions/area1-world/briefs/` show modified or untracked, they are the controller's: leave them, never commit them. Scratch: C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\1635b7dd-88db-474e-8e1b-4f6bc1a3f6e2\scratchpad\intake5

1. Copy each source folder's inner `*_mid/` contents into `Art/Source/Fab/<code>_<name>/` (bash; SRC=/c/Users/jayce/AppData/Local/com.epicgames.eglv2/FabLibrary; `cp "$SRC/<folder>"/*/* <dst>/`). Stop if any file name has 4K or 8K. Map:
   T20a_icel_rock_mossy_large_03     <- "Tundra Icelandic Rock Mossy Volcanic Large 03-425306a5"
   T20b_icel_rock_mossy_large_09     <- "Tundra Icelandic Rock Mossy Volcanic Large 09-d8d74265"
   T20c_icel_rock_mossy_medium_16    <- "Tundra Icelandic Rock Mossy Volcanic Medium 16-e5e423fd"
   T20d_jp_rock_cluster_mixed_m02    <- "Forest Japanese Rock Cluster Mixed Medium 02-af76dcf6"
   T20e_jp_rock_cluster_stone_s02    <- "Forest Japanese Rock Cluster Stone Small 02-4b14036e"
   T20f_jp_rock_ledge_large_01       <- "Forest Japanese Rock Ledge Stone Large 01-c494e84c"
   T20g_jp_rock_plain_medium_04      <- "Forest Japanese Rock Plain Stone Medium 04-97a32aa8"
   T20h_jp_rock_plain_small_11       <- "Forest Japanese Rock Plain Stone Small 11-624e220f"
   T20i_rounded_cobble               <- "Rounded Cobble-7bc2386f"
   T20j_wild_mushroom_set_01         <- "Forest American Wild Mushroom Set 01-4a0ad23c"
   T20k_wild_mushroom_set_04         <- "Forest American Wild Mushroom Set 04-6a27ee3a"
   T20l_wild_mushroom_single_04      <- "Forest American Wild Mushroom Single 04-27668e99"
   T20m_wild_mushroom_single_16      <- "Forest American Wild Mushroom Single 16-6f3de9e8"
   T20n_mossy_tree                   <- "Mossy Tree-24ae38f4"
   T20o_mossy_tree_branch_a          <- "Mossy Tree Branch-beb295de"
   T20p_mossy_tree_branch_b          <- "Mossy Tree Branch-cb05c66b"
   T20q_dead_tree_branch             <- "Dead Tree Branch-e4950a10"
   T20r_wooden_log                   <- "Wooden Log-8ec9b7c5"
   T20s_mossy_wooden_log             <- "Mossy Wooden Log-eb4364df"
   T20t_farm_barrel_wood_05          <- "Historic Medieval Farm Barrel Wood 05-279be9f6"
   T20u_farm_chest_wood_s            <- "Historic Medieval Farm Chest Wood S-140b25c2"
   T20v_farm_log_pile_s_01           <- "Historic Medieval Farm Log Pile S 01-3a772086"
   T20w_farm_sack_burlap_02          <- "Historic Medieval Farm Sack Burlap 02-bdd94d09"
   T20x_farm_cauldron_iron_05        <- "Historic Medieval Farm Cauldron Iron 05-a65ea51a"
   T20y_library_chest_wood_old_03    <- "Historic Medieval Library Chest Wood Old 03-841bb2cd"
   T20z_rusty_sword                  <- "Historic Medieval Library Sword Metal Rusty-ab03b5f7"
   T21a_mossy_gravestone_a           <- "Mossy Gravestone-b7060731"
   T21b_mossy_gravestone_b           <- "Mossy Gravestone-e3487d59"
   T21c_bundled_rope_coils           <- "Bundled Rope Coils-546b4c29"
   T21d_field_poppy                  <- "Field Poppy-66cb2706"           (plant)
   T21e_herb_robert                  <- "Herb Robert-b0a21363"           (plant)
   T21f_narrowleaf_plantain          <- "Narrowleaf Plantain-2d5f02f9"   (plant)
   T21g_clovers                      <- "Clovers-e7671b8f"               (plant)
   T21h_bushy_starwort               <- "Bushy Starwort-f7623801"        (plant)
   T21i_bermuda_buttercup            <- "Bermuda Buttercup-2023c45a"     (plant)
   T21j_rose_hip                     <- "Rose Hip-07e36f85"              (plant)
   T21k_silvery_glade_fern           <- "Silvery Glade Fern-981de672"    (plant)
   T21l_tundra_grass                 <- "Tundra Grass-3d87458a"          (plant)
   T21m_wild_grass_a                 <- "Wild Grass-5848d16a"            (plant)
   T21n_wild_grass_b                 <- "Wild Grass-50d9a417"            (plant)
   T21o_grass                        <- "Grass-5de49921"                 (plant)
   T21p_native_violet                <- "Native Violet-5b73fe69"         (plant)
2. Plants: in `Assets/_Project/Editor/Builders/B1TownIntake.cs`, add "T21d" through "T21p" (13 codes) to the `Plants` HashSet beside the T12 and T19 codes. Change nothing else. If a plant folder has no VarA LOD FBX files (only one FBX), report it and leave it out of `Plants`.
3. Append 42 rows to `Art/Source/Fab/SOURCES.md` in the `| T19a |` row format (licence "Fab Standard License (Quixel Megascans)", author "Quixel Megascans", fetched 2026-11-17; plants: "Mid tier (2K) FBX VarA-E LOD0-3, via Epic Launcher").
4. Run in the background, wait for the notice (no sleep/poll loops): `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725\Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.B1TownIntake.Run -OutDir <scratch>`. Need exit 0 and "failed=0". Exit 2 = LOCKED: stop, report, never kill Unity. If one code fails, report its error line and do not guess a fix.
5. Check each `Assets/_Project/Content/Art/Town/Adopted/T20x|T21x/` has fbx (plants: LOD fbx files), prefab, maps with .meta, and `Materials/B1/M_<code>.mat`.
6. `git status --porcelain`: revert files you did not create (`git checkout -- <file>`), except the controller's files above; known churn: M_GD_*, M_StoneCottage_B_SC_Rubble, M_HM_*, M_HH_OldStone, M_RD_RoadHedge, M_Slime_Glass (.mat), T_Water_N.png.meta, ShopSign_Inn.fbx.meta, CellarFloorSync.prefab, CellarFloorTest.unity; delete untracked files under Art/Staging/area1-world/shells/.
7. Commit only `Art/Source/Fab/T20*`, `Art/Source/Fab/T21*`, `Art/Source/Fab/SOURCES.md`, `B1TownIntake.cs`, `Assets/_Project/Content/Art/Town/Adopted/T20*` and `T21*` (with folder .meta files), `Assets/_Project/Content/Materials/B1/M_T20*` and `M_T21*` (.mat and .meta). No `.unity` file. Message:
   T20a-T21p: adopt 42 Megascans items (mossy and forest rocks, wild mushrooms, mossy logs and branches, farm barrel/chest/log pile/sack/cauldron, rusty sword, mossy gravestones, rope coils, 13 wild plants) through B1TownIntake

   Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
8. Gate in the background: `...\Tools\unity.ps1 gate -OutDir <scratch>\gate`; `SC MARKER` = HEAD; `git push`.

Report under 14 lines: commit, exec and gate lines, all 42 "SC B1 BOUNDS" lines condensed to `code WxHxD`, problems.
