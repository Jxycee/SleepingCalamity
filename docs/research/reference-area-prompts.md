# Reference-area image prompts: Hollowmere and the Cellars (2026-11-13)

For Nano Banana / GPT Image / Midjourney (see the concept-gen memory: Gemini in the browser pane, save everything generated). Output is concept reference only: it goes to `Reference/private/` (gitignored), feeds Fable's design run and the asset-search ranker as extra reference frames, and is never shipped or traced. Own words; no game names inside the prompts that matter, but naming the *look* is fine. Style numbers come from `docs/PHASE1.md` section 3.

## How to use
1. Paste the **style header** first, then one **shot** block. Generate 2 per shot, keep the better one.
2. Attach 2-3 real frames as image input when the tool allows it: for Hollowmere use `Reference/private/genre2/` town frames + `oot2026/vgc_08_*`, `vgc_10_*`; for the Cellars use `oot2026/vgc_25_*`, `vgc_23_*` and our `Reference/cellars/floor_art2/floor_room.jpg`.
3. Save as `Reference/private/concepts/<area>_<shot>_<nn>.png`; rank against needs with `Tools/asset_search/rank_style.py` or hand them straight to Fable.
4. Ask for 16:9, 2K. If the result drifts to photoreal or to cartoon, add the matching correction line below.

## Style header (always first)
> Stylised realism game concept frame, third-person action-RPG, storybook proportions: chunky rounded forms, thick oak timbers about 1.4x real, oversized chimneys and roof overhangs, doors taller than a person, props slightly oversized. Real PBR materials with tidy placed wear (plaster, granite cobble, adzed oak, red clay tile, wrought iron), no photo noise. Slightly muted, moody stone and plaster, saturated painted wood and cloth accents. Layered depth with atmospheric haze, soft volumetric light, a few drifting leaves or dust motes. Looks like a 2026 high-end remake of a 2000s whimsical dungeon-crawler JRPG. 16:9, no text, no UI, no characters unless asked.

Corrections: *too photoreal* -> "hand-painted-feeling storybook forms, simplified shapes, clean silhouettes". *too cartoon/plastic* -> "real material response, subtle roughness variation, grime in crevices". *too dark/muddy* -> "clean separation between lit stone, mid tone and shadow, no mass pure black".

## Hollowmere (starting town, day; hilltop above a lake, ruined and shabby but loved)
Palette: warm cream plaster, red-terracotta roofs, dark timber, grey-warm cobble, yellow-green foliage, slate-teal shadows. One warm sun about 5200 K, cool sky fill.

- **H1 wide square, midday:** "A hilltop village square about 40 x 30 m around a dry sandstone fountain, half-timbered cottages with cream plaster and red tile roofs on old stone foundations, some shabby with missing shutters and scaffolds, lanes leaving the square as narrow stubs, a church with a tall bell tower and a sealed iron gate on the north side, worn granite cobbles with moss in the joints, a lake glimpsed far below under haze, warm midday sun, cool blue-teal shadows, pollen drifting."
- **H2 same, low sun:** same square, low golden sun from the left, long shadows across the cobbles, warm rim light on timbers, haze lit orange, a few lit windows.
- **H3 cobble ground close-up:** "Close view of worn granite cobblestone street, warm and cool grey stones, moss and small weeds in the joints, a puddle reflecting a timber wall, a cart-wheel groove, fallen leaves, shallow depth of field."
- **H4 bakery (ruined, to be rebuilt):** "A small corner bakery, front elevation, stone ground floor, plaster upper floor, a bricked-in oven chimney, the door and painted sign missing, bare timber frame where the roof was, scaffold poles, flour sacks and a broken peel in the doorway, chunky storybook shapes."
- **H5 church and bell tower:** "A squat stone church fused to a tall square bell tower with a red tile spire, ivy on the lower walls, a small trapdoor-sized cellar hatch in the porch floor, worn steps, birds."
- **H6 heroes' home:** "The four friends' shared home: a crooked two-storey cottage, patched roof, mismatched shutters in faded blue and ochre, wood pile, laundry line, a barrel for rain, a cat on the sill, welcoming but shabby."
- **H7 market corner:** "Market stalls with canvas awnings in ochre and madder red, crates, baskets, barrels, hanging lanterns, painted signs left blank, empty stalls with a few goods, cobbles underfoot."
- **H8 orchard edge:** "Gnarled apple trees at the village edge, dry-stone wall with moss, a path to a lake overlook, yellow-green leaves, dappled sun, a small fishing jetty far below."

## Cellars (Undercroft, floors 1-4: warm firelight, clean, not muddy, no teal)
Palette: lamp gold, lit stone `#c9aa76`, tan and orange brick, shadows deep warm umber to plum, small red ember accents. Crisp value steps, saturated gold, little fog.

- **C1 corridor:** "Arched brick cellar corridor, old tan and orange brick, lime wash flaking, flagstone floor with worn centre, iron wall lamps casting rich gold light pools, deep warm umber shadows (no blue or green), clear shapes, a few dust motes in the lamp shafts."
- **C2 floor-1 larder:** "A cellar larder room, stone shelves with glazed crocks and jars, hanging dried herbs and sausages, oak barrels with iron hoops, sacks, a rat hole, gold lamplight."
- **C3 floor-2 wine vault:** "A vaulted wine vault, long rows of racked bottles and big oak casks, brick barrel vault with white salt stains, a central lantern, damp sheen on flagstones, warm gold with deep plum shadows."
- **C4 floor-3 coal cellar:** "Coal cellar, black coal heaps, soot-dark brick, a cracked furnace door with ember glow, rails and a tipped cart, orange emissive cracks, gold and ember red only."
- **C5 checkpoint room:** "A small safe room under the stairs, a table with a lit candle and a bedroll, banked chests, a chalk mark on the wall, calm gold light, an iron door."
- **C6 entry from church:** "The cellar trapdoor seen from below: stone steps rising to a square of cool daylight, ladder, cobweb, damp steps, the room beyond lit warm."
- **C7 floor-4 oven maze:** "Giant brick bread-oven vaults, arched oven mouths glowing red-gold, flour dust drifting like fog, iron peels and trays the size of doors, a sleeping baker's apron on a stool, ember accents."
- **C8 combat room:** "A square cellar combat room 5 x 4 cells, pillars, barrels as cover, lamps on chains, flagstone floor, three doorways, readable floor and clear lanes, no clutter in the centre."
- **C9 monsters (rat-king, Dough Warden):** "Storybook cellar monsters: a rat-king, a stack of fused vermin wearing a crown of rusted tin lids; a Dough Warden, a hulking living dough golem with an iron oven door for a chest, flour smoke from its joints; chunky shapes, clear silhouette, warm lamplight, on a grey backdrop, front and three-quarter views."

## Fable's use
Treat generated frames as mood and layout ideas, the genre screenshots as the quality bar, and the Megascans/Fab sheets as the material source. When a prompt result is picked, note the file in the area's design doc so later rounds can be compared with it.
