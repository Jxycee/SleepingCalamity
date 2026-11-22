# Build plan: area 1 (Hollowmere) and dungeon 1 (the Cellars)

**Status:** the two design docs were approved by the user on 2026-11-13 with all decisions as recommended, so sessions may start. B0 goes first.
**Specs:** those two docs. **Assets:** `docs/design/area1-dungeon1-needs.md` (only what it names is downloaded).

## Rules for every session
- **Local PC only.** Every session here needs GPU captures or the editor, so each runs whole on the PC. No cloud part (CLAUDE.md, "No hybrid sessions").
- **One job per fresh session,** Sonnet 5.5 at medium effort. Subagents: Sonnet for builders, captures and Unity runs; Haiku for scans and logs. One Unity run at a time on the machine.
- **Request files:** the coordinator writes each session's request in `sessions/requests/` from its row here, naming the spec sections. A new session has no other context.
- **Captures:** iterate with `SC_CAPTURE_ONLY=<cameras>`; shoot the full set once at the end. Prove a risky piece with one reduced capture first. At most three art rounds.
- **Done-check, the same shape for every session:**
  1. Shoot the named cameras.
  2. Put each beside its reference with `Art/Blender/tools/sc_ab_sheet.py`.
  3. Run `sc_ref_palette.py --k 7` and check the value and warmth bands.
  4. Score with the `sc-asset-review` rubric. Target: 25/40, or the first score plus 5.
  5. The sheet goes to the user for corners and new spots.
- **Checks and merge:** `gate` on the branch, `check` on the tip before merging, then `sc-merge`. Anything under `Core/Cellars` also needs `soak`.
- **Review:** `reviewer` (Sonnet) for art, presentation and docs. B6 and B7 touch Core or the return flow; B7 gets `branch-reviewer` if it wires Presentation to the dive flow.
- **Find the file first.** Hollowmere layout and dressing tables live in `Core/Town/` (pure C#, so changes need EditMode tests); builders live in `Editor/Builders/`. Where this plan gives a bare file name, locate it before editing.
- **Labels:** H1-H8 and C1-C8 are concept images; DH and DC numbers are decisions; bracketed codes are assets.
- **Downloads** need the user signed in to Fab in the browser pane. Each adopted asset gets a `SOURCES.md` line with licence and URL (`sc-asset-adopt`). 2K at most.
- **Fab download method (tested in B0, 2026-11-14).** Do not download in the browser pane: it needs a manual Save click per file. Add the listing to My Library in the pane (never click a EULA accept), then in the Epic Games Launcher (computer use: grant `Epic Games Launcher` and the process `epicgameslauncher.exe`) open Fab > My Library and click the item's download icon. The Launcher's Fab download settings (Fab > Download settings: Preferred format, Quality tier) decide whether a download starts at once with no dialog. They are currently **Texture Set + Mid (2K)**. Set them to what each batch needs before you queue it: Texture Set + Mid for Megascans-style texture sets; for a batch of model listings switch the preferred format to the one the listings offer (FBX, Unity or glTF) so those also start without a dialog. Files land in `C:\ProgramData\Epic\EpicGamesLauncher\VaultCache\FabLibrary\<Name>-<id>\<format>\<tier>\`; copy them to `Art/Source/Fab/<code>_<name>/`. A listing that lacks the preferred format still opens the dialog: pick the row by hand. Check each download is the 2K tier, never 4K or 8K, and write the settings you used into your STATUS.

## Sessions

Sizes are from `QUEUE.md`: S under an hour, M a few hours, L a multi-task plan with art rounds.

| # | Session | Size | Depends on |
|---|---|---|---|
| B0 | Lighting test corner | M | approval |
| B1 | Asset intake: town | M | B0 |
| B2 | Asset intake: cellars | M | B0 |
| B3 | Square ground, sky rollout, the ruin look | L | B1 |
| B4 | Home, bakery lot, market row | M | B3 |
| B5 | Terrace, tithe gate, bell yard, Market Street | M | B3 |
| B6 | Orchard, allotments, lake edge | M | B3 |
| B7 | Town life | L | B4, B5 |
| B8 | Cellar kit and the per-floor look | L | B2 |
| B9 | Floors 1-2 and the entry | M | B8 |
| B10 | Floor 3 bakery and floor 4 wine | M | B8 |
| B11 | Floor 5, bank room, floors 6-7, the end door | M | B8 |
| B12 | Cellar life and sound | M | B9-B11 |
| B13 | Final captures and review | S | all |

Order: B0 first. After that the town line (B1, B3-B7) and the cellar line (B2, B8-B12) are independent; run them in turn, not at once, because both use Unity.

### B0. Lighting test corner (first)
- **Goal:** prove the URP light recipe for both places on the two small scenes before any dressing.
- **Scenes:** `BeautyCorner_Hollowmere.unity`, `DungeonCorner_Cellars.unity`.
- **Files:** `Editor/Builders/BeautyCornerBuilder.cs` (sun, fill, post: lines 79-92, 166, 322-330), `HollowmereLook.cs` (fog 67-74, sky 90), `DungeonCornerBuilder.cs`, `Presentation/Town/AreaLook.cs`.
- **Do, town** (Hollowmere doc section 4): HDRI sky, ambient from the sky with an APV bake, Exponential fog with colour from the horizon, the cloud-shadow cookie, the Morning and Evening presets, the sun-elevation test (54, 46, 38), the square cobbles [T01] on the corner's ground.
- **Do, cellar** (Cellars doc sections 3, 5, 6): [C01] and [C02] on the vault and floor at 1024 px/m, the lamp recipe, the warm ambient gradient, one wet patch, one sconce [C15].
- **Also:** measure concepts H1-H3 and C1-C8 with `sc_ref_palette.py` and record the numbers; they settle Decision DC5.
- **Assets:** K01 (two HDRIs), T01 (two materials), C01 (two), C02 (one), C15 (one sconce).
- **Done-check:** `Cap_Corner_Wide`, `_Mid`, `_Close` in Morning and `Cap_Corner_Wide` in Evening, beside H1, H3, H2 and the current approved row. `Cap_Cellar_Wide`, `_Mid`, `_Close` beside C1, C8, C2.
- **Ends with:** the user's approval. The approved captures become the new reference rows (`Reference/hollowmere/approved_corner/` and a first Cellars row).

### B1. Asset intake: town
- **Goal:** every Hollowmere pick in the needs list, adopted and ready as prefabs.
- **Do:** download, record source, scale, pick the LOD for the budget class, 2K cap, pack ORM, prefabs under `Content/Art/Town/Adopted/<code>/`. Look at each "title" pick before adopting; drop any that miss the style and say so.
- **Tools:** `sc-asset-adopt`, `Art/Blender/tools/sc_asset_audit.py`.
- **Done-check:** the audit passes; one contact sheet of all adopted pieces under the B0 Morning light beside H7. No scene changes.

### B2. Asset intake: cellars
- **B0 leftovers (2026-11-14, user approved the B0 sheets as is):** cellar brick (C01) still reads pink-salmon with a noisy micro-pattern, so retune the albedo toward honey-tan/ochre and soften the normal further; the Close niche brickwork reads as chunky chip-islands around the arch (fix the arch/niche mesh or UVs, not just the material); the C15 sconce orientation was not checked in Close; `UndercroftStair.unity` took the shared brick/limewash/sconce changes but was never captured. Reviewer score of the Cellars row: 23/23/21 of 40.
- As B1 for the Cellars picks, under `Content/Art/Cellar/Adopted/<code>/`. Sheet under the B0 cellar light beside C2.

### B3. Square ground, sky rollout, the ruin look
- **Spec:** Hollowmere sections 4, 6, S2; Decisions DH1, DH4, DH6.
- **Files:** `HollowmereSquare.cs`, `HollowmereFoliage.cs`, `HollowmereLook.cs`, `HollowmereBuilder.cs` (ruin footings about 484-505), `Art/Blender/town/sc_ruin_kit.py` (add the chimney stack), `HollowmereTerrain.cs` (meadow layers outside the wall).
- **Do:** roll the B0 light out to `Hollowmere.unity`; swap the square's ground; move grass to edges and footings; stage 0 on every ruined lot (chimney, saplings, debris, crow); leaf drifts, puddles, the cart rut.
- **Done-check:** `Cap_HM_HomeDoor`, `Cap_HM_Fountain`, `Cap_HM_SquareSouth`, `Cap_HM_SquareWest`, `Cap_HM_Ruins`, `Cap_HM_Overview`, Ruined preset, Morning, beside H1, H3 and the same cameras re-shot before the session.

### B4. Home, bakery lot, market row
- **Spec:** S1, S3, S4, section 6.
- **Files:** `Core/Town/HollowmereDressing.cs` and `Core/Town/HollowmereClutter.cs` (the dressing rows; extend their EditMode tests), `Editor/Builders/HollowmereHub.cs` (build point, spawn yaw), new `Art/Blender/town/sc_scaffold_kit.py`.
- **Do:** the three dressing tables; the spawn yaw (Decision DH8); the scaffold kit; stage 1 and stage 2 on the bakery; the stock pile and Tobin's bench.
- **Done-check:** `Cap_HM_HomeDoor`, `Cap_HM_HeroesHome`, `Cap_HM_Bakery`, `Cap_HM_SquareWest`, `Cap_HM_TownhouseSide`, Ruined and Rebuilt, beside H6, H4, H7.

### B5. Terrace, tithe gate, bell yard, Market Street
- **Spec:** S5, S6, S7.
- **Files:** `HollowmereTitheGate.cs`, `HollowmereHub.cs` (the trapdoor slab at line 83 becomes the hatch), `HollowmereBuilder.cs` (add `Cap_HM_BellYard`, `Cap_HM_TerraceView`).
- **Done-check:** `Cap_HM_TitheGate`, `Cap_HM_KeepWall`, `Cap_HM_J09`, `Cap_HM_BellYard`, `Cap_HM_TerraceView`, `Cap_HM_LaneMouth`, `Cap_HM_MarketStreet` beside H5 and H1.

### B6. Orchard, allotments, lake edge
- **Spec:** S8, S9, S10; Decision DH5.
- **Files:** `Core/Town/TownWallPlan.cs` (fixed variants for the orchard gap, with an EditMode test), `HollowmereFoliage.cs`, `HollowmereGarden.cs`, `HollowmereLakeArt.cs`, a new orchard builder, the apple tree via `sc-foliage`; add `Cap_HM_Orchard`.
- **Done-check:** `Cap_HM_Orchard`, `Cap_HM_Allotment`, `Cap_HM_Yard`, `Cap_HM_LakeShore`, `Cap_HM_LakeJetty` beside H8.

### B7. Town life
- **Spec:** section 5; Decisions DH2, DH3, DH7.
- **Files:** `HollowmereAmbientBuilder.cs`, `HollowmereLook.cs`, `Presentation/Town/AreaLook.cs`, the return-from-dive flow in Presentation (pick the Evening preset), an audio setup for beds and emitters.
- **Do:** the one wind and everything it drives; smoke; pigeons, crows, sparrows; anchors for cat, hens and dog (placeholders until models arrive); hero idle spots; the sound map.
- **Done-check:** stills from `Cap_HM_HomeDoor`, `Cap_HM_Fountain`, `Cap_HM_BellYard` in Morning and Evening beside H1 and H2; one walk (`sc-walk`) from spawn to the trapdoor to judge motion and sound, with its notes. Review before merge.

### B8. Cellar kit and the per-floor look
- **B0 leftovers (2026-11-14, user approved the B0 sheets as is):** cellar brick (C01) still reads pink-salmon with a noisy micro-pattern, so retune the albedo toward honey-tan/ochre and soften the normal further; the Close niche brickwork reads as chunky chip-islands around the arch (fix the arch/niche mesh or UVs, not just the material); the C15 sconce orientation was not checked in Close; `UndercroftStair.unity` took the shared brick/limewash/sconce changes but was never captured. Reviewer score of the Cellars row: 23/23/21 of 40.
- **Spec:** Cellars sections 3, 5, 6, 7; proposals P1-P5, P7.
- **Files:** `Art/Blender/cellars/sc_cellar_vault.py` and a new kit generator; `Editor/Builders/CellarFloorTestBuilder.cs`; `Presentation/Cellars/CellarFloorBuilder*.cs`; new `CellarFloorLook`; `Core/Cellars/CellarFloorWarmLights.cs` if route lamps need Core (then `soak`).
- **Do:** re-surface the kit; groin bay, round pillar, wall variants, light fixtures; the look table; route lamps; overlays.
- **Tests:** EditMode for the look table and route-lamp choice.
- **Done-check:** `Floor_Overview`, `Floor_Room`, `Floor_Corridor` in `CellarFloorTest.unity` on floors 1, 4 and 6, and `Cap_Cellar_*`, beside C1 and C8. The three floors must be told apart in the overview sheet at thumbnail size.

### B9. Floors 1-2 and the entry
- **Spec:** section 4 (floors 1, 2), dressing rows for the larder rooms, entry, exit and corridors.
- **Do:** the daylight shaft and stair (C6); pantry, cold store, hanging hall, smoke gallery; the Rat-king's court as set dressing (no creature); the sigil alcove; roots, moss, cobwebs.
- **Done-check:** `Floor_Room` on the floor 1 pantry and hanging hall, `Cap_Stair_Top`, `_Turn`, `_Bottom`, beside C2 and C6.

### B10. Floor 3 bakery and floor 4 wine
- **Spec:** section 4 (floors 3, 4).
- **Decision DC1 puts the Dough-warden on floor 3 (decided 2026-11-13):** add the guardian cell to the bakery heart (proposal P6) in `Core/Cellars` with an EditMode test, then run `soak`.
- **Do:** the beehive oven (dead and lit states), the Great Oven, tide line, kneading hall, the sign; cask rows, racks, the press, the Great Tun, salt bloom, wet floor.
- **Done-check:** `Floor_Room` in the bakery heart and the press hall, `Floor_Corridor` on floor 4, beside C7 (oven shapes) and C3.

### B11. Floor 5, bank room, floors 6-7, the end door
- **Spec:** section 4 (floors 5-7), sections 8 and 9.
- **Do:** the dome arena dressing; the bank room; coal rooms, rails, chute, furnace ring, boiler; the Damper Door and its opening.
- **Done-check:** `Floor_Room` in the bank room, a coal bunker and the end cell, beside C5 and C4.

### B12. Cellar life and sound
- **Spec:** section 10.
- **Do:** rats, moths, cinder moths, drips, ash and steam; sound beds per floor and emitters; Tuppence in the bank room if Decision DC4 is yes.
- **Done-check:** stills of a lit lamp with moths and a drip puddle beside C1; one walk through floor 1 with notes.

### B13. Final captures and review
- **Do:** the full capture set for both places, one scored review each (town review 2, first dungeon review), a leftovers list for the queue.
- **Ends with:** the user's look at both sheets.

## What the user is asked for along the way
1. Done 2026-11-13: the two design docs are approved, all decisions as recommended.
2. Sign in to Fab in the browser pane when B0, B1 and B2 download.
3. Approve the B0 corner sheets.
4. Animal models (Decision DH7), or a yes to making them with the local pipeline.
5. Look at the B13 sheets.
