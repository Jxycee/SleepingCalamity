State: IN PROGRESS (sources downloaded and committed; adoption builder not written yet)
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
