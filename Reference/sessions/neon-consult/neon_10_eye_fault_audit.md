# Job 2: R19 Eye-Camera Fault Audit & Cross-Camera Top 10

*Scope note:* `hollowmere_eye_frames_R19_R18.zip` contained **15 R19 frames** (`HM_Eye_*_Ruined.jpg`) and no R18 fallback frames (`Allotment`, `Bakery`, `BellYard`, `Climb`, `InnFront`, `Lake`, `Mill`, `RingSouth` were absent from the archive). All 15 R19 frames were inspected directly.

## 1. Per-Camera Fault Audit (Most Damaging First)

### 1. `Balcony`
1. **Smooth grey Play-Doh Mawrrow cone mountain (upper right)** clashes sharply with scanned trees and crisp stone foreground — `HollowmereBackdrop.cs` (`Build` / Far layer crest).
2. **Oversized bright lime-green broad-leaf plants** clipping the left and right foreground parapet edges read as low-detail plastic foliage — `HollowmereSquare.cs` / `HollowmereFoliage.cs` (`Put` / parapet planters).
3. **Ruler-straight blocky stone parapet slabs in immediate foreground** with uniform flat top planes and sharp rectangular joints — `HollowmereLanes.cs` / `HollowmereSquare.cs` (terrace parapet).
4. **Blocky, straight-edged ruin shell chimneys and dark unlit stall silhouettes** across the square — `Art/Blender/town/sc_ruin_shells.py` & `HollowmereClutter.cs`.

### 2. `Churchyard`
1. **Bare tan earth hillside on right half** with pasted, isolated rounded boulders and sparse evenly spaced spiky tufts — `HollowmereBellYard.cs` (`KeepBank`, `PutSlope`), `HollowmereGroundBanks.cs` (`BuildBanksT13`), `HollowmereTerrain.cs`.
2. **Clean, sterile cobble terrace walkway & sharp-edged stone benches/low wall** with almost zero moss/dirt seam transition — `HollowmereBellYard.cs` (`PutWall`), `HollowmereLanes.cs` (`Build`).
3. **Crushed-dark shadowed grass lawn around gravestones** (lower right), making the gravestones and tended grave hard to read — `HollowmereBellYard.cs` (`KeepLawn`, `Tended`) & `HollowmereZones.cs`.
4. **Bright white lighthouse spire popping out of the distant left valley** at higher contrast than the hazy hills behind it — `HollowmereBackdrop.cs` (`Build`).

### 3. `HillWest`
1. **Evenly spaced, grid-like rows of small spiky grass tufts** marching up the sunlit left grass slope — `HollowmereTerrain.cs` (detail scatter) & `HollowmereBellYard.cs` (`PutSlope`).
2. **Large flat-faced vertical stone retaining wall (center-right)** reads as a single stepped grey slab with mossy rocks pasted on top and a hard base line on smooth green grass — `HollowmereBellYard.cs` (`BellCrag`) & `HollowmereGroundBanks.cs` (`BuildBanksT13`).
3. **Smooth, featureless lime-green grass carpet** on the lower-left foreground with zero dirt/clover/leaf breakup — `HollowmereTerrain.cs` & `HollowmereFoliage.cs`.

### 4. `JettyBack`
1. **Uniform, machine-clean running-bond fieldstone revetment wall** with a razor-straight foot seam where it meets the grass bank — `HollowmereRevetment.cs` (`Build`, `BuildProfile`).
2. **Boxy merlon teeth along the town wall top** and clean square-cut `LowerGate` ruin silhouette — `Art/Blender/town/sc_town_wall.py` & `HollowmereTownWall.cs` (`TryBuild`, `PlaceGate`).
3. **Uniform vertical grass carpet stopping abruptly against a bare smooth tan dirt foreground** (bottom 25%) — `HollowmereShore.cs` (`ShoreLine`, `Bank`) & `HollowmereTerrain.cs`.
4. **Dark glossy boat hull** (bottom right) sitting harshly on flat dry sand — `HollowmereShore.cs` (`Build`).

### 5. `LowerLane`
1. **Oversized, flat, bright-white unlit butterfly cards** hovering right in front of the left lens — `HollowmereAmbientBuilder.cs` (`BuildInsects`, `EnsureCardMaterials`).
2. **Mechanical "railroad-track" stone gutter** running straight up the right edge of the cobbles — `HollowmereLanes.cs` (`Build`, `LanePiece.Gutter` / `GutterChoked`).
3. **Boxy merlon teeth on the left curtain wall** and clean straight timber posts poking from `LowerGate` Shell — `Art/Blender/town/sc_town_wall.py` (`wall_pc`, `build_shell`).
4. **Clean cobble lane meeting a sharp dirt verge** with sparse spiky green tufts — `HollowmereTerrain.cs` (`LaneWear`, `LaneEdge`) & `HollowmereStreets.cs`.

### 6. `MarketStreet`
1. **Smooth grey Play-Doh Mawrrow cone mountain** (upper right) and pale washed-out mid-valley floor with toy-like dotted crest trees — `HollowmereBackdrop.cs` (`Build`, `Forest`).
2. **Clean, uniform pale flagstone stair treads** with sharp geometric pier blocks framing both sides — `HollowmereLanes.cs` (`Build`, `AddStoneTread`).
3. **Sparse isolated props** (single tipped pot on left stair, lone handcart on right) with hard shadow contact — `HollowmereStreetClutter.cs` (`Build`, `Clusters`).

### 7. `Meadow`
1. **Clean, tiled running-bond revetment wall** occupying the left third, meeting the ditch/grass in a hard geometric line — `HollowmereRevetment.cs` (`Build`, `BuildProfile`).
2. **Boxy crenellations on the town wall above** — `Art/Blender/town/sc_town_wall.py`.
3. **Uniform yellow-green spiky grass blades** and pale bright-blue water strip with toy-like vertical far-shore tree cards — `HollowmereTerrain.cs`, `HollowmereWater.cs`, `HollowmereBackdrop.cs` (`Forest`).

### 8. `OrchardGap`
1. **Giant low-poly faceted cubes & icosahedron rocks** dominating the breach rubble pile — `Art/Blender/town/sc_town_wall.py` (`breach`, `heap`, `rock`) + `HollowmereOrchard.cs`.
2. **Floating stone block on the right parapet + stray `"L"` artifact** — `Art/Blender/town/sc_town_wall.py` (`wall_pc` coping/merlon mismatch; fixed in Pass A `sc_town_wall.py`).
3. **Smooth, blurry flat-green painted terrain patch** (bottom left) with a glossy grey rock pasted on it and dark crushed foreground shadows — `HollowmereTerrain.cs`, `HollowmereOrchard.cs` (`Build`), `HollowmereT13c.cs` (`GapLift`).

### 9. `Orchard`
1. **Floating cantilevered procedural stone slab** (bottom left) with glossy specular sheen on its flat top — `HollowmereOrchard.cs` / `HollowmereT13c.cs` (`OrchardLaneKit`).
2. **Blocky sharp-edged procedural wooden cart** (bottom right) and oversized bright butterfly cards — `HollowmereOrchard.cs` (`Build`) & `HollowmereAmbientBuilder.cs` (`BuildInsects`).
3. **Razor-sharp horizontal cutoff** between dense dark spiky grass and bare sunlit tan path — `HollowmereOrchard.cs` (`OnPath`) & `HollowmereTerrain.cs`.
4. **Long straight background town wall with uniform boxy merlon teeth** and crushed-black barrel/crate silhouettes in the midground — `Art/Blender/town/sc_town_wall.py` & `HollowmereZones.cs`.

### 10. `Postcard`
1. **Huge empty, high-frequency glossy cobble plaza** covering the bottom 45% of the frame with isolated single props (lone overturned bench + pot left, lone crate right, lone box center) — `HollowmereSquare.cs` (`BuildGround`), `HollowmereB3Ground.cs`, `HollowmereGroundProps.cs` (`Build`).
2. **Plastic specular sheen on cobbles** (unclamped smoothness) amplifying HF noise across the whole square — `HollowmereSquare.cs` / `M_SquareGround`.
3. **Waxy/blobby market stall awnings** clashing with scanned timber and the stone church — `HollowmereClutter.cs` (`PlaceCloth`) / stall kit mesh.

### 11. `RingWest`
1. **Toy-like, overly clean, sharply stepped ruin shell blocks** (right foreground) with uniform bevels, bright yellow-tan lighting, and a hard base line on bare tan dirt — `Art/Blender/town/sc_ruin_shells.py` & `HollowmereRuinShells.cs`.
2. **Mechanical "railroad-track" stone gutter** running straight down the right cobble seam — `HollowmereLanes.cs` (`Build`, `LanePiece.Gutter`).
3. **Clean cobble lane strip** with sparse identical spiky grass tufts poked along the right wall foot — `HollowmereTerrain.cs` (`LaneWear`) & `HollowmereFoliage.cs`.

### 12. `RoadBack`
1. **Evenly spaced spiky grass tufts** lined up in a repeating rhythm along the right fence posts — `HollowmereRoadside.cs` (`BuildRoadside`, `RoadHedges`) & `HollowmereT13c.cs` (`RoadTrees`).
2. **Uniformly smooth, bare tan dirt road surface** down the center with weak wheel-rut / pebble / leaf-litter breakup — `HollowmereRoad.cs` (`Build`) & `HollowmereTerrain.cs` (`CountryRoad`).
3. **Blocky distant ruin chimney silhouettes** on the right horizon — `Art/Blender/town/sc_ruin_shells.py`.

### 13. `RoadOut`
1. **Clean, toy-like rectangular ruin shell blocks** (right foreground) with crisp stepped Lego-like corners and hard ground contact against scanned grass — `Art/Blender/town/sc_ruin_shells.py`, `HollowmereRuinShells.cs`, `HollowmereT13c.cs` (`ShellFoot`).
2. **Smooth grey Mawrrow peak** on the far left and bright white vertical lighthouse glint in the center valley — `HollowmereBackdrop.cs` (`Build`).
3. **Scattered bright white butterfly cards** clashing over the dark ruin shadow — `HollowmereAmbientBuilder.cs` (`BuildInsects`).

### 14. `Spawn`
1. **Glaring rectangular lighter cobble patch + flat sharp-edged blue puddle decal** sitting on a high-frequency glossy cobble sheet (45% of frame) — `HollowmereB3Ground.cs` (`BuildDecals` `SquarePuddle`), `HollowmereSquare.cs` (`BuildGround`), `HollowmereSurfaces.cs` (`SquareMacro`).
2. **Smooth Play-Doh Mawrrow mountain cone** (upper left) right beside a realistic Megascans oak tree — `HollowmereBackdrop.cs` (`Build`).
3. **Waxy/blobby market stall awning** (right foreground) and sparse isolated props across the open plaza — `HollowmereClutter.cs` (`PlaceCloth`), `HollowmereGroundProps.cs` (`Build`).

### 15. `West`
1. **Clean, toy-like smooth stone fountain** (foreground center) with a hard circular green-moss ring pasted around its base on glossy cobbles — `HollowmereSquare.cs` (`BuildDressing`) & `HollowmereB3Ground.cs`.
2. **Glossy, high-frequency cobble sheet** with an isolated lone crate on the far left and boxy stall canopy behind — `HollowmereSquare.cs` (`BuildGround`), `HollowmereGroundProps.cs`, `HollowmereClutter.cs`.
3. **Straight-edged ruin shell stacks & angled beam silhouettes** on the far wall — `Art/Blender/town/sc_ruin_shells.py`.

---

## 2. Cross-Camera Top 10 Fixes (Ranked by Expected `/50` Gain)

| Rank | Fix | Cameras Helped | Source File / Function | Est. Gain |
| :--- | :--- | :--- | :--- | :--- |
| **1** | **Deploy Pass A `sc_town_wall.py` + Megascans rubble/ivy swap** (kills floating parapet block, `"L"`, giant faceted cubes, boxy merlon teeth) | `OrchardGap`, `Orchard`, `LowerLane`, `JettyBack`, `Meadow` | `Art/Blender/town/sc_town_wall.py`, `HollowmereTownWall.cs` (`TryBuild`), `HollowmereOrchard.cs` | **+3.5 to +5.0** |
| **2** | **Calm & break up the square cobble plaza** (clamp `M_SquareGround` smoothness `<= 0.25`, dial back normal scale `0.6×`, remove/feather rectangular bright patch & hard-edged `SquarePuddle`, add edge moss/dirt) | `Spawn`, `Postcard`, `West`, `Balcony` | `HollowmereSquare.cs` (`BuildGround`), `HollowmereB3Ground.cs` (`BuildDecals`), `HollowmereSurfaces.cs` (`SquareMacro`) | **+2.5 to +4.0** |
| **3** | **Dress/replace smooth Mawrrow mountain cone & toy crest trees** (Megascans rock/cliff ridge silhouette + stronger aerial haze; tone down white lighthouse spire) | `Spawn`, `Balcony`, `MarketStreet`, `RoadOut`, `Churchyard`, `Meadow` | `HollowmereBackdrop.cs` (`Build`, `Forest`) | **+2.0 to +3.5** |
| **4** | **Rough up `sc_ruin_shells.py` silhouettes + add Megascans corner/rubble/ivy caps** (breaks Lego-like stepped blocks and hard base lines) | `RingWest`, `RoadOut`, `Balcony`, `Spawn`, `West` | `Art/Blender/town/sc_ruin_shells.py`, `HollowmereRuinShells.cs`, `HollowmereRuinDressing.cs` (`Dress`) | **+2.0 to +3.0** |
| **5** | **Replace uniform/grid grass tufts with 2-level clumped scatter + broad-leaf edge cover** (kills marching rows on slopes/fences & straight grass-to-dirt edges) | `HillWest`, `Churchyard`, `Orchard`, `RoadBack`, `JettyBack`, `Meadow` | `HollowmereTerrain.cs`, `HollowmereBellYard.cs` (`PutSlope`), `HollowmereRoadside.cs` (`BuildRoadside`), `HollowmereOrchard.cs` | **+2.0 to +3.0** |
| **6** | **Break up `HollowmereRevetment` running-bond & base line** (add missing/proud stones, ivy curtains, sunk boulders & shrubs along the wall foot) | `Meadow`, `JettyBack` | `HollowmereRevetment.cs` (`Build`, `BuildProfile`), `HollowmereShore.cs` (`WallFoot`, `Bank`) | **+2.0 to +3.0** |
| **7** | **Tone down/replace the "railroad-track" lane gutters** (bury/irregularize `Gutter`/`GutterChoked` cleats and blend silt/weeds over the seam) | `LowerLane`, `RingWest` | `HollowmereLanes.cs` (`Build`, `GutterSlots`) | **+1.5 to +2.5** |
| **8** | **Fix foreground hero prop clashes & cluster square props** (remove floating slab & blocky cart in `Orchard`; replace waxy stall awnings; group lone square props into wall/stall clusters; add weathering/moss to fountain) | `Orchard`, `Postcard`, `Spawn`, `West`, `MarketStreet` | `HollowmereT13c.cs` (`OrchardLaneKit`), `HollowmereOrchard.cs`, `HollowmereClutter.cs` (`PlaceCloth`), `HollowmereGroundProps.cs` (`Build`), `HollowmereSquare.cs` (`BuildDressing`) | **+1.5 to +2.5** |
| **9** | **Fix oversized bright butterfly cards & lime parapet plants** (shrink butterflies `0.45×`, darken/tint to match scene lighting, push spawn clearance `> 4 m` from eye cameras; swap `Balcony` lime plants for Megascans ivy/ferns) | `LowerLane`, `Orchard`, `RoadOut`, `Balcony` | `HollowmereAmbientBuilder.cs` (`BuildInsects`, `EnsureCardMaterials`), `HollowmereFoliage.cs` | **+1.0 to +2.0** |
| **10** | **Dress bare tan hillside & lift crushed-black midground props** (break up `Churchyard`/`HillWest` bare earth slopes with sunk Megascans outcrops + fern/leaf-litter mats; add subtle sky-bounce fill in `Orchard` & `Churchyard` shadows) | `Churchyard`, `HillWest`, `Orchard`, `OrchardGap` | `HollowmereBellYard.cs` (`KeepBank`, `KeepLawn`), `HollowmereGroundBanks.cs` (`BuildBanksT13`), `HollowmereZones.cs` | **+1.0 to +2.0** |
