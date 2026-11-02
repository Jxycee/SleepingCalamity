# Town visual review and open-world plan (2026-11-01)

From the user's two trailer fly-throughs (build `84dfd742`, 2026-11-01):
- Trim1 (35 s): Hollowmere's square Ruined, then Rebuilt, then the heroes' home stairs.
- Trim2 (39 s): the cottage, the stair shaft, the Cellars.

Every frame was measured by script; key frames were judged by eye against the style bible (`docs/PHASE1.md` section 3). Frame sheets are in `frames/`; times below are seconds into each clip.

**Review only. Nothing here is fixed yet (user, 2026-11-01).**

## Verdict

The town reads as polished but empty. There are good pieces:
- the half-timbered houses
- the stalls once rebuilt
- the light-shaft room in the Cellars

They sit on very large, uniform surfaces, with nothing alive and nothing on the horizon to look at. Beyond the walls is haze, flat bands of card trees, a flat lake and blank fields: the Shadow of the Colossus feeling the user described, without that game's landmarks.

Most items below are density, variety and life, not broken art.

## A. Town: visual fix list (ranked)

| # | What | Where (clip, time, sheet) | Why it matters |
|---|---|---|---|
| A1 | **The square is a big, empty cobble field.** Buildings and stalls hug the edges; the centre is bare. The fountain is small for the space. | Trim1 4-24 s (`a1`, `a2`) | The main read of the town is "parking lot". Scale and mid-ground dressing (well, carts, benches, planters, a tree, worn paths) would fix it. Dressing the whole square is the vertical slice after Phase 1 (PHASE1 section 1): this feeds that plan. |
| A2 | **The cobble tileable repeats and has one tone.** The radial fan pattern is visible across the whole square. There are no wear paths, no gutters or kerbs at building edges, and no darker or damp strips along walls. | Trim1 throughout, Trim2 1-6 s (`a1`, `b1`) | Style bible 3.2: tidy placed wear over clean base. Today it is clean base only, at a scale that shows repetition. Needs a macro variation layer and edge trims. |
| A3 | **Crack weeds are an even sprinkle of identical tufts,** the same density Ruined and Rebuilt. | Trim1 0-24 s (`a1`, `a2`) | They should cluster in cracks, at walls and in corners. Rebuilt should show far fewer, as a sign of care. |
| A4 | **Ivy reads as flat lime cards** in evenly spaced columns, with the same silhouette repeated and too saturated. | Trim1 4-11.5 s (ruined walls, `a1`), Trim2 6.5 s (`b1`) | Palette (3.1, muted day colour) and shape. Needs value and saturation down, size and spacing variety, and a hanging or trailing form. |
| A5 | **Flat, untextured colour blocks** read as placeholders: a bright red square on the annex's upper storey; a blue bar and a red rectangle on the cottage front; a very saturated red shutter. | Trim1 26.5-34 s (`a3`), Trim2 4-6.5 s (`b1`) | They look unfinished next to PBR neighbours. Texture them (painted wood, a sign) or tone them down. |
| A6 | **Stone walls look puffy** (pillow-like, oversized stones) on the heroes'-home gable and the cottage. | Trim1 29-34 s (`a3`), Trim2 1-4 s (`b1`) | Normal or height strength and texel scale. The stones read as bread rolls rather than dressed stone. |
| A7 | **A hard-edged dark green rectangle on the ground** in front of the heroes' home, with grass on it and drain grates. | Trim1 31.5-34 s (`a3`) | A decal or ground patch with straight edges and no blend. |
| A8 | **The earth bank right of the home stairs** is a raw terrain slope with a stretched, low-res texture. | Trim1 26.5-29 s (`a3`) | Needs a rock face, a retaining wall or a blended slope material. |
| A9 | **The lower stair reads as a textured ramp:** tread and riser shapes are lost in the brick pattern. | Trim1 26.5-29 s, foreground (`a3`) | Also a gameplay read: players can't see the steps. |
| A10 | **Distant ring-2 houses look like plain boxes** (flat facades, few windows) in clear air. | Trim1 14.5-18 s, left (`a2`) | LOD or detail at mid-distance, or more haze at that range (3.5). |
| A11 | **Props sit "pasted" on the cobbles:** sacks, crates and stall legs have little contact darkening. | Trim1 14.5-24 s (`a2`) | Grounding: SSAO strength or radius (3.5), contact decals. |
| A12 | **The backdrop is flat:** tree lines and ridges read as cardboard bands in haze; the lake is a flat sheet; the horizon has no landmark. | Trim1 4-21 s (`a1`, `a2`) | See part C. |
| A13 | **Rebuilt has no life:** no chimney smoke, nothing moving, the same weeds as Ruined. | Trim1 13-25 s (`a2`) | The reward of rebuilding should feel alive. See C1. |
| A14 | **The rebuild swap stalls and smears.** Switching Ruined to Rebuilt freezes the game three times: 3 frames at 12.3 s, 3 at 12.47 s, 9 frames (0.3 s) at 13.6 s. The half-timbered house fades in as a blurry ghost for about 0.3 s. | Trim1 12-14 s (`rebuild_swap_12-14s.jpg`) | It shows in trailers and will show in play when a building appears. Likely first-time loading of the Rebuilt meshes, textures and shaders; worth a warm-up or preload. |

## B. Cellars (seen in Trim2, short list)

| # | What | Where | Note |
|---|---|---|---|
| B1 | **Dust motes render as hard white squares.** | 22 s (`b2`) | The particle texture is missing or the material is wrong. |
| B2 | **Greybox leftovers in the undercroft:** flat white unlit discs on the floor, a red glowing strip under a beam, a dark box. | 35 s (`b2`) | Marker discs and greybox pieces visible in a trailer shot. |
| B3 | **The plaster room before the stair is flat and blotchy,** and its door is a dark slab. | 12-12.5 s (`b2`) | Low detail compared with the corridor beyond it. |
| B4 | **The stair shaft's rock is stretched and oversized,** and the camera runs very close to it. | 13.8-15.7 s (`b2`, `stair_shaft_11-16s.jpg`) | Texel density (3.3). |
| B5 | **The corridor walls wash out to near-white** for about 0.4 s after the dark room, and there are two dips to black (11.7 s, 15.9 s). | 13.3-13.7 s | Exposure or transition. Check whether the dips are intended. |
| B6 | **The undercroft is very dark,** with pillars as black silhouettes. | 32-38 s | May be intended mood. Compare against the approved dungeon corner. |

## C. The open world: from "empty but polished" to "abandoned and reclaimed"

**The story gives the answer.** Only the town was dragged away on the Long Night (`docs/STORY.md`). The land around it has been left alone for ten years.

So the emptiness should read as **abandoned by people and taken back by nature**, not as unfinished:
- Ruined Hollowmere: no human life, plenty of wild life.
- Rebuilt Hollowmere: human life returns (smoke, laundry, sound).

That contrast is part of the reward of rebuilding.

Shadow of the Colossus gets away with emptiness because every view has a landmark and something moving (birds, wind, light). Hollowmere has neither yet.

### C1. Ambient life (no gameplay; fits Phase 1 scope, no residents needed)

- **Birds:**
  - crows on ruin tops and the bell tower
  - swallows over the square
  - a flock that lifts when the hero comes close
  - gulls toward the lake
- **Small creatures:** butterflies and bees over the meadows; a few rabbits, a fox or deer at field edges that run from the hero.
- **The lake:** fish rings, reed sway, a heron.
- **Wind:** wind waves through long grass and fallow wheat; moving cloud shadows across the valley; leaves and pollen (pollen exists in town).
- **Smoke from the heroes' home chimney** in the Ruined state: the four live there. Rebuilt adds smoke from every rebuilt house.

### C2. Dressing the countryside (what ten years of neglect looks like)

- Old field boundaries: dry-stone walls and hedgerows. Fields gone fallow with wildflowers; overgrown wheat; scrub creeping in.
- Abandoned farmsteads and a barn just outside the walls, roofs caving: the farms survived, their people fled.
- An overgrown orchard; lone oaks; grey, rotted haystacks; a leaning scarecrow; wild bee skeps; a sheepfold with a few feral sheep.
- The old tithe road (Saltwake road; milestones, the shrine and a cart already exist), with overturned tithe carts and a broken bridge over the stream.
- Mid-distance variety, so the terrain isn't one material: meadow, ploughed field, scrub, rock outcrops.

### C3. Landmarks on the horizon (give every view something to look at)

- **The mountain where Mawrrow sleeps:** a dark silhouette always on the horizon, with warm haze or steam rising. It ties to the tithe gate that "breathes warm air" (STORY 8.2).
- **The way to Saltwake:** the road winding west to the horizon, a glint of the grey-green estuary, a far lighthouse (the next town).
- A ruined windmill or watchtower on a ridge line.
- Replace the flat card ridges with real low-poly silhouettes and layered haze (3.5).

### C4. Gameplay outside the walls (needs the user's decision; Phase 1 excludes it)

Candidates for `docs/PARKING_LOT.md`:
- gathering spots (synthesis materials)
- fishing spots (fishing opens in Saltwake)
- small finds and journal scraps
- a viewpoint
- resident wish targets that use the land ("where I can see the lake", STORY 8.3)

## Decisions (user, 2026-11-01)

1. **A closed ring, Nier Replicant style.** The land around Hollowmere is a walkable valley ring bounded by natural edges, not an open world that runs to the terrain's edge. Possible bounds: a cliff or ravine line, steep wooded slopes, the lake shore, the stream. Exits stay shut until the story opens them; the Saltwake road opens with ring 3.
   - This also removes today's walk-off-the-world edge at x ≈ -150 (BUGS.md item 17).
   - Everything past the ring is horizon: silhouettes, haze, landmarks.
2. **Ambient life goes in, if easy and good models that match the style can be found** (see the sourcing plan below).
3. **The Mawrrow mountain goes on the horizon, but it must not be the whole background.** It is one landmark among several: the Saltwake road and a far lighthouse glint to the west, a ruined windmill or watchtower on a ridge, and real tree and ridge silhouettes in between.
4. **No people in Hollowmere until a rebuild unlocks someone.** Ruined Hollowmere has wildlife only, plus the heroes' own chimney smoke. Later towns may reveal a survivor (parked).

## Ambient life: sourcing plan

Rule: no paid assets. Free means CC0 or CC BY from a trusted source, or a free Asset Store item the user adds (`.claude/rules/art-assets.md`, `sc-asset-adopt`). The finding that shapes the plan: most free animal sets are flat-shaded low poly or cartoon, which clashes with our PBR stylised realism up close. So pick the cheapest route per creature, by how close the camera gets.

| Creature or effect | Camera distance | Route | Why |
|---|---|---|---|
| Distant flocks, circling crows, swallows, gulls over the lake | 20 m and more | **VFX:** GPU particles or vertex-animated cards with a wing-flap shader, or a tiny bird mesh from a Blender generator | No model quality question at that size; cheap; reproducible from text |
| Butterflies, bees, pollen, fish rings, chimney smoke, wind waves, cloud shadows | any | **VFX / shader only** | No models needed |
| Crows perched on ruins and the bell tower (idle, peck, fly off) | 5-15 m | One crow from our own Blender generator (a simple shape, a few bones), or trial the free Asset Store "Living Birds" (Dinopunch; dated 2018 and built for Unity 4.5, so it needs a material and quality check) | The closest common creature; one model covers all perches |
| Rabbits, a fox, deer at field edges; a heron at the lake; feral sheep in a fold | 10-40 m | **A/B before choosing:** (a) Quaternius's CC0 animated animals reshaped and re-textured through `sc-asset-adopt`, vs (b) the AI pipeline (Tripo image-to-3D plus auto-rig, `sc-ai-model-pipeline`) in our style. Score both with `sc-asset-review` | Needs a rig and animation; the low-poly source risks the style, the AI route risks the rig |

Order:
1. VFX-only life first. No style risk, and the biggest change to the "empty" feel.
2. Then one crow, and one deer or rabbit through the A/B above.
3. Only then the rest of the fauna set.

Every adopted asset gets a SOURCES.md row with its licence. Quaternius's pack contents and licence page must be confirmed on its own site before adopting (the pack page could not be fetched today).

## Open questions for the user (answered 2026-11-01, see Decisions)

1. How far should the hero walk beyond the walls? Today the terrain is walkable to its edge, and the hero can walk off the world at x ≈ -150 (BUGS.md item 17). Backdrop-only, a small ring of fields, or out to the road?
2. Ambient life (C1) now as Phase 1 polish, or with the after-Phase-1 square dressing?
3. Is the Mawrrow mountain on the horizon OK story-wise (it reveals where the demon sleeps from minute one)?
4. Keep the Ruined state free of human life (smoke only from the heroes' home) and save human life for Rebuilt? (Recommended.)

## Suggested order once fixes start

1. **Cheap, high read (one town polish session):** A5 placeholder colours, A7 ground patch, A3 weed clustering, A4 ivy palette and variety, B1 square dust, B2 greybox leftovers.
2. **Surfaces:** A2 cobble variation and edge trims, A6 wall relief, A8 earth bank, A9 stair read (`sc-texture-paint`).
3. **Life and horizon:** C1 ambient life plus C3 landmarks and the backdrop rebuild (A12): the biggest change to the "empty" feel.
4. **Countryside dressing (C2)** and square dressing (A1), sized with `sc-asset-budget`.
5. **The rebuild swap stalls (A14):** a warm-up or preload when the session starts.
