State: IN PROGRESS (builder done and idempotent, 42 prefabs adopted, gate PASS; reviewer, check, merge left)
Branch: claude/b1-town-intake
Task: build-plan row B1, Asset intake: town (docs/superpowers/plans/2026-11-13-area1-dungeon1-build.md)
Started: 2026-11-14

## Done
- 48 Hollowmere listings saved to Fab My Library (all free) and downloaded via Epic Launcher at the Mid (2K) tier into Art/Source/Fab/<code>_<name>/ (49 folders incl. L04). SOURCES.md rows added.
- Fab settings used: Preferred format FBX (was Texture Set), tier Mid. The user may want it back at Texture Set. Scythe Wood needed the dialog (FBX Medium row picked by hand).
- Not downloaded on purpose: K01/T01/C-codes (B0), S01 audio, A01-A03 (no usable listing), A05 Labrador (rigged character, own job), T07/T09-ours/T10 (build, not intake). T01 cobble spares T01b/T01c are material sets.
- L04 Stylized Plants in Pots: FBX only; its 4096 px SPP_1.tga was deleted for the 2K cap. Licence not yet confirmed on the listing (SOURCES row says so). Texture needs a 2K re-export or a re-texture with ours.
- Materials sets (no mesh): T01b, T01c, T03, T15a, T15b. T02a/T02b are 3D (FBX), not texture sets.

## Next (for the fresh context)
1. Write Assets/_Project/Editor/Builders/B1TownIntake.cs (model on B0Fab.cs: Megascans() makes _albedo/_normal/_ms/_occ from BaseColor/Normal/Roughness/AO; Opacity/Translucency for plants and decals). Per FBX: import under Assets/_Project/Content/Art/Town/Adopted/<code>/, pick LOD (props: the single Mid FBX; plants: VarA LOD0 + LOD1 as prefab LODGroup if cheap), set scale by real size (Megascans FBX come in cm: check import scale), pivot bottom centre, URP Lit material (alpha clip for plants/decals), prefab. Wire into ProjectBuilder.RunAll only if the pattern requires it; run through Tools\unity.ps1 exec/build.
2. Look at each "title" pick (render a contact sheet: Blender or Unity) and drop any that miss the style; record drops in STATUS.
3. Run sc_asset_audit.py (Blender 5.2 is installed; find its exe) on the FBXs, record result.
4. Contact sheet of all adopted pieces under the B0 Morning light beside Reference/private/concepts H7 via sc_ab_sheet.py; SendUserFile it.
5. reviewer (Sonnet), `gate` then `check` on the tip (SC MARKER = HEAD), sc-merge into fresh origin/main, tick B1 in CURRENT.md "Now". Leave worktree clean for B2.

## Paused: user gaming (2026-11-14)
- B1TownIntake.cs written and run once via exec: 44 prefabs, 0 failures, nothing dropped, bounds sane (barrel 0.54 x 0.59 m). Prefabs under Assets/_Project/Content/Art/Town/Adopted/<code>/, materials Content/Materials/B1.
- L04: flat URP materials from the FBX slot colours (no texture); bounds 6.6 x 11.4 m, so the FBX is a layout sheet of pots, needs splitting or a pick of single pots. T11b beach rock is 7 m long (scale at placement).
- Remaining: `gate` run (not started), second exec run to confirm idempotent (no diff), style review/contact sheet, sc_asset_audit, reviewer, check, merge.

## Update 2026-11-14 (resumed)
- Builder idempotent: 2nd and 3rd exec runs write identical bytes (first run had left _MainTex empty and plant AlphaToMask/DoubleSidedGI stale; the builder now sets them). All 170 generated textures <= 2048 px.
- T11b scaled to 1.5 m tall (TargetHeight in B1TownIntake). L04 was a 64-pot layout sheet; one pot (Composition_7) built fine but renders as a flat white pot with paper-like leaves (no texture): DROPPED. T12a (sparse grey card cluster): DROPPED. Builder Skip set keeps both out. 42 prefabs remain.
- `gate` PASS on a7cd1344 (1780 EditMode tests), SC MARKER = HEAD at that commit.
- sc_asset_audit.py (Blender 5.2.2, --class prop, 44 FBX, LOD0 only plus T09/T13 LOD2 as shipped): 13 meshes over the generic 6000-tri prop budget (T02a 11.6k, T02b 9.3k, T05b 10.2k, T06a 7.3k, T09 7.5k, T11b 7.5k, T13 6.3k, T14c 6.3k, T16a 12.7k, T16b 8.1k, T16c 6.3k, T16h 7.9k, T16i 7.5k); all well inside the hero budget (60k), so they are hero/large props, not scatter. 16 WARN, all texel density (Megascans UVs are 224-6955 px/m against the 1024 target). No degenerate faces, loose verts or extra material slots.
- Contact sheet under the B0 Morning rig (editor script B1ContactSheet, scratchpad only): tiles read as stylised-real props in the Hollowmere palette. Weak but kept: T12c ivy (thin cards), T02b broken wall (floating shards, ruin piece), T12d/T12e grasses (small).
