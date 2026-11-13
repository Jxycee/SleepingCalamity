# Area 1: Hollowmere (design)

**Status:** proposal from Fable Run 3, 2026-11-13. Not approved. The user approves this before any build session starts. Decisions here are current direction, not locks (CLAUDE.md, "Decisions have leeway").
**Builds on:** `docs/PHASE1.md` section 3 (style bible), `docs/STORY.md`, `docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md`, the town as built (`Core/Town/HollowmereLayout.cs`, `Editor/Builders/Hollowmere*.cs`).
**References, in order:** the user's concepts H1-H8 (`Reference/private/concepts/`), then `Reference/private/genre2/`, `oot2026/`, `totk/`, and our approved corner (`Reference/hollowmere/approved_corner/`).
**Assets:** every code in square brackets (for example [T01]) is a row in `docs/design/area1-dungeon1-needs.md`, which holds the listing, URL, licence note and contact sheet. "ours" means a piece already under `Assets/_Project/Content/Art/`.
**IP:** nothing of Dark Cloud's names, characters, text or assets.

## 1. Where the town stands today

Seen in the latest captures (`Reference/sessions/ambient-life/after/HM_Overview_Ruined.png`, `Reference/hollowmere/square/captures/HM_HomeDoor_Ruined_Midday.jpg`, `Reference/sessions/stairs-traversal-3/round3/HM_KeepStairs_Ruined.png`). Some are two weeks old, so the first build session re-shoots them before changing anything.

- The layout, lanes, wall, terrace, church group and stalls are sound. Keep them.
- The ruined square reads as an empty dig site: flat brown pads, no verticals, nothing moving near the player.
- The sky is a hard blue-to-green gradient with no cloud or haze. This is the largest single gap to the concepts.
- The cobbles are fan-pattern setts. The concepts (H1, H3) show rounded, uneven cobbles with moss in the joints.
- Grass tufts stand in rows on open cobbles and are too large. Butterflies and birds read as flat shapes.
- Round 4 of town polish scored about 19 to 22 out of 40 (`CURRENT.md`).

## 2. Six ideas

1. **The chimneys stayed.** Every ruined lot keeps its hearth and chimney stack above the footing. The square at the start is a ring of lonely chimneys and doorsteps. It gives the ruin height and a silhouette, says "homes stood here", and ties the town to the Hearthdeep. When a house is rebuilt, smoke rises from that same chimney. (Decision H1: STORY says "foundations and doorsteps".)
2. **Wild to tended.** Ten years of growth fill the ruin: saplings inside the lots, ivy on the stacks, meadow in the joints, all in the concepts' yellow-green. Rebuilding tames it lot by lot. Wildness is the progress bar.
3. **Three anchors, one short walk.** Home (rest), the bakery lot (build) and the bell yard (dive) sit within 40 m. Each has its own tall shape, its own sound and one thing that moves.
4. **A sky worth looking at.** A painted, hazy sky, drifting cloud shadows on the cobbles, and lake haze beyond the wall. Cheap in URP and it changes every frame.
5. **Life without a crowd.** The residents are gone until rescued, so life comes from wind, cloth, smoke, birds, a cat, hens, and the heroes themselves standing at their own spots. Each rescued resident switches on a "life kit" for their building.
6. **Evening home.** Leave for a dive in morning light. Come back at golden hour with lit windows. Two light states tied to the loop, no clock. (Decision H2.)

## 3. Layout

Schematic, north up, not to scale. Coordinates in the table are metres from the fountain, +z north, taken from code. The square shelf is y 0, the keep terrace y +4, the Ring Lane y about -3 on the flanks and -6 on the south, the lake y -12.

```
                         hill rises north
  z 30                        D L                    D trapdoor   L landing
  z 25                 [ CHURCH ][T][Ax]             T bell tower  Ax annex
  z 20     ginnel   ======terrace wall==G=======     G tithe gate (sealed)
  z 15       st/    b                      \st       st stairs up   b terrace bench
  z 10   Sm  :  [F04][ BAKERY ][F01][ HOME ]  :
  z  5   |   :  +-------------------------S-+[F10]   S spawn
  z  0   R   :[F03]     c        F    p     |  :  R
  z -5   i ==West Lane==                    [INN]  i   F fountain  c p t stalls
  z-10   n   :  |            t              |  :  n
  z-15   g   :[F05]                         ==East Lane==
  z-20       :  +----------+  M  +----------+  :
  z-25   L   :   [F06]     |  a  |  [F07][F08] :  L
  z-35   a   :.............|  r  |.............:  a
  z-45   n  Sc             |  k  |   al    O      n
  z-50    \==============Ring Lane=====al==O==/     al allotments  O orchard
  z-55       \ Lower Lane                  O  \ Mill Lane
  z-65  ==wall==LG=========wall=======..gap..==EG    Mi mill
  z-70    Saltwake Rd <--/      meadow
  z-80             J ~~~~~~~~~~~~~~~~~~~~~~~~~~      J jetty
  z-100         ~~~~~~~~~~~~  LAKE  ~~~~~~~~~~~~
```

| Place | Position (x, z) | Source |
|---|---|---|
| Fountain | (0, 0) | `HollowmereLayout.cs:25` |
| Square | x -20..20, z -21..7 | same |
| Spawn, at the home door | (12.5, 3.75) | `HollowmereHub.cs:43-48` |
| Heroes' home | (12.5, 11.25), door (12.5, 6.75) | landmark rows |
| Bakery lot F02 (build point at its doorstep) | (-12.5, 12.7) | `HollowmereHub.cs:125-129` |
| Stalls: produce, cloth, tools | (4.5, 4.5), (-7, -3), (5.5, -8.5) | landmark rows |
| Inn lot F09 | (26.5, -6) | ring 1 rows |
| Tithe gate | (5.6, 19.6) | landmark rows |
| Bell tower / church (keep hall) | (5.6, 24) / (-3, 24.5), base y 4 | landmark rows |
| Trapdoor, landing | (5.6, 29.2), (8.4, 29.2), y 4 | `HollowmereLayout.cs:31` |
| Market Street | x 0, z -21 to -49 | lane rows |
| Lower Gate, East Gate | (-21, -65), (57, -46) | `HollowmereLayout.cs:301-304` |
| Jetty root | about (-22, -82) | `HollowmereLakeArt.cs:8` |
| Orchard (new) | wall side of the Ring Lane's south-east bend, about x 34..44, z -54..-44 | free strip between R2_18 and Mill Lane; the build session confirms it against the lot table |

### Routes
- **Spawn to build point:** 26 m west along the north row. Flat.
- **Spawn to dive:** north-east corner of the square, Keep Lane, the east stair (by `Cap_HM_StairSide2`), west along the keep walk to the landing and trapdoor. About 40 m.
- **Square to lake:** Market Street, Ring Lane, Lower Lane, Lower Gate, shore, jetty. About 110 m, all downhill. This is the long calm walk.
- **Square to orchard:** East Lane, Ring Lane south. About 60 m.

### Sight lines
| From | What is in view | What we do with it |
|---|---|---|
| Spawn, facing south-west (set the spawn yaw toward (-4, -6)) | fountain at 13 m, cloth stall, the west row's chimneys, Market Street's gap with sky and haze | first frame: fountain centred-left, haze gap on the left third |
| Spawn, a quarter turn right | bakery lot at 26 m (F01 is a low ruin at the start, so nothing hides it) | Tobin hammering there is the sound that pulls the player over |
| Spawn, behind | bell tower 17 m tall above the home roof | pigeons and the wind tone say "up there" |
| Build point, facing south | fountain 15 m, stock pile 4 m, cloth stall, home's smoke to the east, inn across the square | the placement camera already sits above F02 |
| Terrace edge at (0, 20.6), eye y 5.65 | the whole square, the fountain on axis, Market Street straight ahead, sky over the wall | the "progress balcony": a bench here, passed on every walk to the dive |
| Trapdoor | tower door, landing, annex, the hill bank. No view of the square | on purpose: a sheltered pocket before the descent |
| Market Street mouth (0, -21), facing north | fountain, north row, terrace wall, church and tower above | the postcard view (H1). Seen on every return from the lake |

The lake surface cannot be seen from the square over the town wall (eye 1.65 m, wall top about y -2 at z -64). The lake belongs to the orchard gap, the Lower Gate and the shore. From the square the south reads as haze and far ridges.

## 4. Light and atmosphere (URP)

One recipe for the whole town in two states. Numbers are starting points for the lighting test corner (build plan B0), which tunes them against H1, H2, H3 and the day bands (p10/p50/p90 0.21-0.25 / 0.44-0.50 / 0.80-0.86, warmth +0.12 to +0.20).

| Setting | Morning (default) | Evening (after a dive) |
|---|---|---|
| Sun | elevation 42, yaw -32 (sun in the south-south-east). Test 54, 46, 38 | elevation 12, yaw +62 (sun in the west-south-west, as H2) |
| Sun colour, intensity | (1.00, 0.91, 0.83), 4.5 | (1.00, 0.72, 0.48), 3.2 |
| Sky | HDRI, partly cloudy [K01], turned so its sun matches | HDRI, low sun [K01] |
| Ambient | from the skybox, with an APV bake per state (replaces the trilight) | same |
| Fog | Exponential (not squared), density 0.0048, colour sampled from the sky's horizon (start #b9c6cf) | density 0.006, start #e9b98a |
| Cloud shadows | a tiling cookie on the sun, 220 m tile, 1.2 m/s along the wind, sun dips 35% | dips 20% |
| Shadows | 4 cascades, 100 m, 4096, soft | same |
| Post | Neutral tonemap; exposure retuned after the sky swap (start -0.3); contrast 20; saturation 0; white balance +6; shadows tinted to #2f3a44; bloom 0.25 at threshold 1.0; vignette 0.18; SSAO radius 0.35 m | exposure -0.2; white balance +14; bloom 0.35 |
| Lights | none | window glow #d6a24a on rebuilt houses and the home; lantern posts lit (range 7, shadows on the nearest two) |

Why Exponential: exp-squared at 0.012 hides everything past 100 m. Plain exponential at 0.0048 keeps the square crisp (75% at 60 m) and softens the far shore (38% at 200 m).

Local accents:
- **Tithe gate:** an ember point light, #c2412a, range 5, pulsing 0.8 to 1.4 over 4 s, plus a faint shimmer. The only warm light in Morning.
- **Bell yard:** in the tower's shade all Morning. Cooler and quieter. One lit brazier in Evening.
- **Bakery, once rebuilt:** warm window, flour motes in the door's light shaft.
- **Orchard:** dappled light from real leaf cards, plus the cloud cookie.
- **Lake:** the brightest haze in the scene. Keep the water's specular path toward the sun.

No day-night cycle in Phase 1 (unchanged). The two states are presets (`SC_CAPTURE_SUN` already has Midday and LowSun).

## 5. The town's life

**Wind.** One wind for everything, blowing from the lake up Market Street into the square. Base 2.2 m/s, a gust every 9 to 14 s. It drives the foliage shader, cloth sway (bunting, washing, awnings, banners), smoke drift, sign swing (about 6 degrees), cloud shadow travel, and a puff of leaves on each gust.

**Smoke, cloth, water.**
- Smoke rises from the heroes' home from the start, and from each rebuilt building.
- Cloth: one torn bunting span and bleached banners at the start. Six spans and dyed banners when ring 1 is rebuilt. Washing at the home always. All cloth uses four dyes: madder red, ochre, woad blue, undyed.
- The fountain is dry, with leaves in the basin. Water returns when ring 1 is complete.
- A few puddles sit in cobble dips after the storm that opens the story.

**Animals.**
| Animal | Where | Behaviour | Source |
|---|---|---|---|
| Tuppence the cat (STORY) | home sill, fountain rim, the bakery step once it is warm | follows the player in town; can be petted | [A02] |
| Pigeons, 10 | tower ledges, church ridge | flush when the player comes within 6 m, circle, return in 20 s | [A03] |
| Crows, one per three ruined lots | on the chimney stacks | leave a lot for good when it is rebuilt | ours (rook cards) |
| Sparrows, 6 | bakery step and stalls, once rebuilt | hop, scatter | [A03] |
| Hens, 4 and a rooster | allotments and orchard | wander, flee at 2 m | [A01] |
| Butterflies, bees | planters, orchard, allotments | exist; shrink to true size and add more near flowers | ours |
| Fish rings, gulls | lake | exist | ours |
| Inn dog | inn door, arrives with the innkeeper | lies down, follows her | [A05] |

**People.**
- The heroes are the first residents. In solo the three not in use stand in town (the swap already happens here). In co-op, absent heroes fill the spots. Tobin works at the bakery lot's bench, Wren at her list wall, Pip sits on the fountain rim, Ash sorts the haul at the tools stall. Looped idle clips from the animation sets we own. (Decision H3.)
- Rescued residents get two or three anchor points and walk between them slowly: Hollis Rye (baker) at the oven, the counter and the fountain; Marta Fenn (innkeeper) at the inn door, a bench and the produce stall. No schedule system.

**Sound.** Nothing for the town exists yet. Sources: [S01].
| Layer | Content |
|---|---|
| Bed, everywhere | hill wind, far lake lap, leaves |
| Bell yard | wind through the empty belfry (a low hollow tone), pigeons, the frayed bell rope tapping |
| Tithe gate | a slow warm breath, faint crackle |
| Bakery lot | Tobin's hammer and plane; when rebuilt, oven fire and a door bell |
| Home | chimney draw, washing flap, cat |
| Square | sign creak, bunting snap on gusts, footsteps on cobble |
| Orchard, allotments | bees, hens, an apple dropping now and then |
| Lake | water on shingle, a jetty creak, gulls |
| Evening | fewer birds, swallows, a far owl |

## 6. Ruined, framed, rebuilt

| Stage | What stands | Dressing |
|---|---|---|
| 0. Hearth and doorstep | ruin footing (ruin kit), the chimney stack, the doorstep | two saplings on M and L lots, brambles, tall grass, ivy up the stack, roof tiles and beam ends in the grass [T05], one crow |
| 1. Frame up (the frame is placed) | timber frame and part of the roof, as H4 | scaffold on two faces [T04], ladder, tarp, a tool crate, growth cut back to stumps |
| 2. Home again (parts placed) | the whole building | the life kit: smoke, a lit window in Evening, one cloth item, a flower box [L04], a door sound, the resident's anchors, one animal |

The square as a whole gains fountain water and full bunting when ring 1 is complete.

## 7. Spots

Budget classes used in the tables:
- **H** hero prop: up to 30k triangles, up to two 2K sets.
- **M** medium: up to 6k triangles, one 2K set (shared where possible).
- **S** small: up to 1.5k triangles (400 if scattered more than 20 times), 1K or a shared 2K atlas.
- Megascans imports take the LOD that fits the class, with textures at 2K at most (1K for S).

### S1. Home door (spawn)
Concept H6. Cameras `Cap_HM_HomeDoor`, `Cap_HM_HeroesHome`, `Cap_HM_PX_Annex`. Rest, hero swap, Wren's list. It should feel lived in from the first second.
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Wren's list wall | ours, new painted decal (rows of name-like marks, no readable text) | 1, 3 x 2 m | home's west wall | story anchor, Wren's spot | 2K decal |
| Woodshed lean-to | ours, new, from the oak and roof-tile sets | 1 | east wall | H6 silhouette | M |
| Log pile, firewood | [T06] | 2 piles, 6 loose | in and by the shed | warmth | M, S |
| Washing line | ours WashingLine | 1, five garments in the four dyes | shed post to wall | cloth in the wind | S |
| Rain barrel under a downpipe | ours RainBarrel | 1 | south-east corner | drip sound | S |
| Cart wheel, crates | [T16] wheel, ours Crate_A/B | 1, 3 | by the door | clutter | S |
| Bench | [T16] | 1 | left of the door | a hero sits here in Evening | M |
| Sill with the cat | ours | 1 | ground-floor window | Tuppence | - |
| Shutters in woad blue and ochre | ours, material tint | all | windows | H6 colour | - |
| Flower box, pots | ours FlowerBox, [L04] | 1, 3 | sill, step | tended | S |
| Ivy | ours Ivy_Facade, [T12] | east gable | | softens the corner | S |
| Chimney smoke | ours | 1 | always on | life | - |

### S2. Fountain and the square floor
Concepts H1, H3. Cameras `Cap_HM_Fountain`, `Cap_HM_SquareSouth`, `Cap_HM_SquareWest`, `Cap_HM_KeepWall`.
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Cobbles | [T01] Cobblestone Floor | whole square | | the H1 ground (Decision H4) | 2K tile, 1024 px/m |
| Fan ring | ours FlagRing | 6 m ring | round the fountain | keeps the approved motif | - |
| Gutters, edge band | [T01] River Cobblestone | gutter lines, 1.5 m band at the walls | | H3 channel | 2K tile |
| Moss in joints | ours B3 moss, [C03] moss decals | heavier in the north shade and at the fountain foot | | age | decals |
| Puddles | wet decal (ours drip set, [C03]) | 5 | cobble dips, one by the gutter as H3 | sky reflections | decals |
| Cart rut | ours, normal decal | 1 line | Market Street mouth to the bakery | leads the eye to the build point | decal |
| Leaf drifts | [T15] | 12 patches | against kerbs and the lee side of footings | autumn | decals |
| Grass | [T12] grass clumps, 0.25 to 0.4 m | edges, footings, fountain foot only | none in the open middle | fixes the rows | S, instanced |
| Fountain | ours DryFountain, leaves in the basin | 1 | | Pip's spot | - |
| Benches | ours Bench_A, Bench_Broken | 2 and 2 | facing the fountain | rest | - |
| Lantern posts | ours | 9 | as placed | lit in Evening | - |
| Banner poles, bunting | ours | 4, 1 torn span | as placed | cloth | - |

### S3. Bakery lot (build point)
Concept H4. Cameras `Cap_HM_Bakery`, `Cap_HM_Ruins`.
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Oven stack, standing alone | ours, the bakery's oven and chimney as a separate piece | 1 | east gable line | stage 0 landmark, as H4's brick oven | M |
| Debris | [T05] roof tiles, rubble, planks | 12, 2, 4 | inside the footing | ruin | S |
| Tobin's bench | [T16] table, [T14] toolbox, plane; ours sawhorses | 1 set | 3 m south-west of the step | Tobin's spot, build-point marker | M |
| Stock pile | ours crates, [T04] boards, rope edge | 3 x 3 m | beside the bench | carried pieces wait here in view | S |
| Scaffold (stage 1) | [T04] | two bays, two lifts, west and south faces | | H4 | M per bay |
| Flour sacks, peel (stage 1 on) | [C11] | 3, 1 | by the door | H4 | S |
| Bread, baskets (stage 2) | ours Basket_Bread, [T08] bread | 2, 6 | sill board | goods | S |
| Sign (stage 2) | ours ShopSign_Bakery | 1 | bracket over the door | swings | - |
| Smoke, warm window, sparrows (stage 2) | ours, [A03] | | | life kit | - |

### S4. Market row and the inn front
Concept H7. Cameras `Cap_HM_SquareWest`, `Cap_HM_TownhouseSide`, `Cap_HM_Clutter`.
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Stalls | ours Ring1/Stalls (ruined and rebuilt sets) | 3 | as placed | no free listing beat ours (sheet T07) | - |
| Produce | [T08] cabbage, pumpkin, potato, apple; twig baskets | 6, 3, 1 sack, 1 crate, 3 | produce stall | H7 | S |
| Cloth piles | [T09] | 2 | cloth stall | colour | S |
| Tools | [T14] tongs, [T16] rake, shovel, scythe | 1 each | tools stall | Ash's spot | S |
| Stall lanterns | ours Stall_Lantern | 3 | | Evening | - |
| Crate, barrel, sack clusters | ours B7 | 3 clusters of 5 | stall backs, inn wall | mass | - |
| Inn front (stage 2) | ours Inn and sign; [T16] benches; [C07] barrel on stand | 1, 2, 1 | F09 | the dog lies here | M |

### S5. Tithe gate and terrace wall
Cameras `Cap_HM_TitheGate`, `Cap_HM_KeepWall`, `Cap_HM_J09`.
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Gate | ours TitheGate | 1 | | sealed, breathing | - |
| Breath | ember light, shimmer, a leaf puff every few seconds | 1 | gate | "something below" | effect |
| Scorch, dry ground | ours grime decal; no moss within 2 m | | threshold | a warm dry patch against the damp wall | decal |
| Ivy, ferns | ours Ivy_Facade, Lintel_Ivy, [T12] | wall face and foot, away from the gate | | H5 green | S |
| Terrace bench | [T16] | 1 | (0, 20.6) on the terrace | the progress balcony | M |

### S6. Bell yard (dive)
Concepts H5 and C6. New cameras `Cap_HM_BellYard` (from the landing, looking west) and `Cap_HM_TerraceView` (from (0, 20.6, 5.65), looking south).
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Trapdoor | ours, new: oak hatch 1.6 x 1.2 m, iron ring, sigils on the underside that glow faintly when it opens | 1 | (5.6, 29.2), replaces the grey slab | the way down | M |
| Tower door | [C13] castle door | 1 | tower's north face | H5 | M |
| Porch lamp | [C15] hanging iron lamp | 1 | over the door | lit in Evening | S |
| Dive kit | rope coil (ours, new), [C12] small chest, lantern crate (ours) | 1 each | by the hatch; the chest marks the landing | "we go down from here" | S |
| Brazier | [T16] stand brazier | 1 | landing | Evening fire | S |
| Bell rope | ours, new, frayed | 1 | hangs in the doorway | sways; the bell is gone | S |
| Yard gate | [C13] iron gate | 1, open | east end by the annex | frames the yard | M |
| Ivy, ferns, moss | ours, [T12] | north wall and corners | | always in shade | S |
| Pigeons | [A03] | 10 | ledges | sound and motion | - |

### S7. Market Street and the Lower Gate
Cameras `Cap_HM_LaneMouth`, `Cap_HM_MarketStreet`, `Cap_HM_J03`, `Cap_HM_LowerGate`.
- River cobbles [T01] with a centre gutter (ours lane kit).
- Weeds and ferns at the wall feet only.
- A broken cart [T16] at the lane mouth and a milestone (ours) by the gate.
- Keep the lot at the foot of the street (R2_16) low in Phase 1 so the street ends in sky.

### S8. Orchard corner (new)
Concept H8. New camera `Cap_HM_Orchard`. STORY places an orchard on the south slope; none is built. (Decision H5.)
| Prop | Asset | Count | Where | Role | Budget |
|---|---|---|---|---|---|
| Apple trees | ours, new, via `sc-foliage` (gnarled trunk, card crown, yellow-green). No free apple tree exists on Fab (sheet T10) | 7, in 2 shapes, 4.5 to 6 m, one fallen | the strip | H8 | H each, one shared 2K atlas |
| Dry-stone wall | [T02] | 22 m, with a gap for the gate | lane side | H8 foreground | M per 4 m |
| Gate, rail fence | ours Fence_Gate, Fence_Rail | 1, 12 m | wall gap; across the town-wall gap | H8 | - |
| Town-wall gap | ours Wall_Crumbled x3, Wall_Breach x1, fixed by design | | south of the trees | the lake view. Stays open as a low parapet when rebuilt | - |
| Bench | ours Bench_A | 1 | facing the gap | sit and look | - |
| Windfalls, baskets, ladder | [T08] apples, twig baskets; [T04] ladder | 20, 2, 1 | under the trees | harvest | S |
| Ferns, long grass | [T12] | wall foot, between trees | | H8 | S, instanced |
| Bees, butterflies, hens | ours, [A01] | | | life | - |

### S9. Allotments (R2_07, R2_18)
Cameras `Cap_HM_Allotment`, `Cap_HM_Yard`. Ours Garden kit as built. Add a wheelbarrow, pen wall and tools from [T16], and the hens.

### S10. Lake shore and jetty
Cameras `Cap_HM_LakeShore`, `Cap_HM_LakeJetty`. Ours LakeShore as built. Add one boat pulled up on the shingle and one moored [T13], three boulders [T11], and gull cards.

### Ring 2 lanes (rules, not a prop list)
- Ground: river cobbles on lanes, packed soil on yards (ours).
- Each ruined lot follows stage 0.
- One clutter cluster per 15 m of lane, always against a wall.
- Growth sits at wall feet and in lots, never in the walking line.
- One landmark per lane so they can be told apart: the smithy's anvil outside [T14], the schoolhouse's low bell post (ours, new), a cart [T16] in Wall Ginnel.

## 8. Decisions for the user

- **H1. Chimneys stand on ruined lots.** STORY 151 says "foundations and doorsteps". Reason: the ruin needs height and a readable silhouette, and the hearth is the game's own image. Small story wording change.
- **H2. Evening state on return from a dive.** Reason: a warm homecoming for no clock system. Needs one line in the return flow to pick the preset.
- **H3. Heroes stand in town as the first residents.** Reason: life on day one, and the swap gets a place. Uses the mannequin until hero models exist.
- **H4. Swap the square's fan setts for the concept cobbles** (keep the fan ring at the fountain). The fan setts were part of the corner approved 2026-10-30; the concepts of 2026-11-13 show rounded cobbles.
- **H5. Orchard at the south-east bend, with a fixed gap in the town wall for the lake view.** Needs `TownWallPlan` to accept fixed variants (a small Core change with a test).
- **H6. New sky, fog mode and exposure.** This replaces the look of the approved corner, so B0 ends with a new reference row for approval.
- **H7. Animal models.** No free rigged hen, cat or pigeon on Fab fits (sheets A01 to A03). `Reference/sessions/ambient-life` notes say the user planned to supply animal models. Otherwise the local AI pipeline makes them. Until then: card birds, and a static cat on the sill.
- **H8. Spawn yaw** toward the fountain and the haze gap (first-frame composition).

## 9. Not in this design
Interiors, a day-night cycle, shops and their UI, residents beyond the baker and innkeeper, the mill and smithy services, ring 3 beyond the shore.
