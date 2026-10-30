# Style targets from the first reference library (2026-10-29)

Measured with `Art/Blender/tools/sc_ref_palette.py` (k-means in Lab, k=7, pooled per set) on the 1024 px free-licence photos and public-domain paintings in `Reference/hollowmere/` and `Reference/undercroft/` (88 images, each folder has a `REFS.md` with source and licence). Value is sRGB max channel (0-1), p10 / p50 / p90; warmth is the lights minus the darks (positive = lights warmer).

These are **photo and painting numbers, not game numbers.** OoT-2026 frames were measured on 2026-10-29 (section "Measured OoT 2026 frames"; the frames are copyrighted and live only in gitignored `Reference/private/`, numbers only here). Photos were not cropped to the subject, so sky, people and vehicles leak into some masses (marked); read masses as relations, not exact hexes.

## Hollowmere (day)

| Set (n) | Main colour masses (hex, share) | Value p10 / p50 / p90 | Warmth |
|---|---|---|---|
| Architecture: half-timber, stone, plaster (14) | mid stone-grey `#5c5953` 21%, near-black timber and shadow `#282321` 21%, light grey `#999591` 21%, plaster white `#d6dbde` 20%, red-brown timber and roof `#78423c` 8%, sky `#79a4de` 5% (leak), foliage `#6a793d` 4% | 0.17 / 0.51 / 0.92 | cooler (-0.14) |
| Market square and fountain (6) | grey paving `#979694` 27%, brown stone `#6d605a` 23%, shadow `#32302e` 21%, white `#e9ebea` 18%, foliage `#4b6028` 5% | 0.22 / 0.53 / 0.95 | neutral (-0.03) |
| Bell tower and gate arches (5) | stone `#5e5854` 24%, `#8e8a88` 24%, shadow `#292522` 17%, sky `#83a9db` 12% (leak), sandstone `#a8866e` 12%, white `#e2e4ee` 9% | 0.20 / 0.53 / 0.88 | cooler (-0.24) |
| Materials: cobble, tile, slate, stone (11) | pale stone `#b7b0aa` 31%, grey `#817a7b` 28%, dark `#352e28` 13%, terracotta `#d19270` 10%, olive `#6e5e3c` 9%, tile red-brown `#784430` 7% | 0.28 / 0.59 / 0.82 | warmer (+0.12) |
| Landscape: hill town, lake, orchard (3) | foliage `#3b451b` 24%, shadow `#201b19` 19%, lake haze `#a5b5c4` 14%, roof brown `#8d715f` 13%, sky `#d3dadf` 11% | 0.14 / 0.37 / 0.87 | cooler (-0.09) |
| Mill (3) | shadow `#1e1b1d` 19%, wet timber `#4e4844` 18%, stone `#81827f` 16%, sky `#cdd5df` 15%, red-brown `#9b6c55` 12%, plaster cream `#dcccb1` 12% | 0.15 / 0.55 / 0.96 | cooler (-0.22) |
| Paintings (Richter, Friedrich, Spitzweg) (6) | umber `#4e3c25` 23%, near-black `#201913` 21%, pale sky `#d2d3d0` 20%, ochre `#7c6039` 14%, grey haze `#898c8c` 9%, straw `#afa584` 9% | 0.12 / 0.45 / 0.87 | neutral (0.00) |

Combined town set with the sky masses excluded (25 images): stone and paving greys `#958f8b` 25% and `#575651` 21%, plaster `#dadde0` 19%, shadow `#282321` 18%, red-brown `#7e4b42` 7%, foliage `#6e813d` 4%; value 0.19 / 0.52 / 0.92, spread 73.

## Undercroft (dark)

| Set (n) | Main colour masses (hex, share) | Value p10 / p50 / p90 | Warmth |
|---|---|---|---|
| Cellars: barrel vaults, wine niches (7) | stone `#6a685f` 24%, black `#211f1b` 22%, brown `#4d3924` 21%, pale stone `#aeada8` 15%, lamplit ochre `#856839` 12%, bright lamp stone `#c9aa76` 6% | 0.13 / 0.40 / 0.72 | warmer (+0.13) |
| Cisterns: Basilica Cistern and similar (6) | black-brown `#20110a` 29%, `#471d0c` 20%, burnt orange `#78300d` 17%, olive `#73501d` 11%, lamp orange `#b15816` 10%, lit stone `#ab914e` 8%, `#e3dda3` 6% | 0.10 / 0.37 / 0.75 | much warmer (+0.38) |
| Roots and ruins: Ta Prohm (8) | olive moss `#484431` 21%, shadow `#1b1711` 20%, grey stone `#6e6a64` 18%, `#a1a099` 16%, moss-tan `#8d845c` 11%, sky and haze `#dadcdc` 11% (leak), gold `#d8c16d` 4% | 0.11 / 0.44 / 0.84 | slightly warmer (+0.05) |
| Crypts and catacombs (4) | dark brown `#463e35` 30%, `#292120` 29%, stone `#716757` 18%, `#a09b91` 10%, lamp gold `#816a38` 6% | 0.15 / 0.30 / 0.65 | warmer (+0.07) |
| Stairs (3) | pale `#d1d2cd` 23%, shadow `#2d292b` 16%, grey `#7c7d7a` 15%, lamp yellow `#c0a035` 14%, brown `#5f4422` 14%, amber `#8d630d` 12% | 0.22 / 0.58 / 0.88 | warmer (+0.09) |
| Materials: moss on brick, old brick, roots (8) | brick-tan `#9d7d69` 26%, shadow `#312a20` 21%, moss green `#616e2e` 17%, violet-grey `#5d5762` 16%, pale `#ccc7c6` 10%, moss yellow `#b6ab28` 7%, brick orange `#bf7a38` 4% | 0.20 / 0.50 / 0.81 | warmer (+0.21) |
| Paintings: lamplit cellars, moonlit river (4) | black `#26201d` 38%, brown `#3a2a19` 28%, `#5e3e1d` 12%, `#5e4b37` 10%, stone `#8d7558` 6%, lamp ochre `#956b2d` 5% | 0.12 / 0.23 / 0.52 | warmer (+0.23) |

## What the numbers say

1. **Day-scene value structure.** Photos of half-timbered towns span p10 0.17 to p90 0.92 (spread 73) with a middle of 0.51: a big pale mass (plaster, sky), a big dark mass (timber, shadow), little between. Public-domain Romantic and storybook landscapes are lower and tighter at the top (p50 0.45, p90 0.87) because the painter keeps a warm umber mass at 23% and a near-black at 21%. The painter's masses are the readable structure to copy: 3-4 masses, one pale, one dark, one warm mid.
2. **The dark scenes have no teal in any photo.** Cistern and cellar light is lamp orange (warmth +0.13 to +0.38, olive and brown masses). The target look is teal shadows with gold light, so the teal comes from the style bible's grade and fog colour, not from references. Measured dark-scene value to start from: p10 0.10-0.15, p50 0.30-0.40, p90 0.65-0.75 (paintings go darker still: p50 0.23, p90 0.52). The cistern photos lose 49% of their area to the two darkest browns, so the game should lift shadows enough that shapes read.
3. **Material relations to keep.** Plaster is the lightest mass (about `#d6dbde`, warm it toward cream); timber is the darkest (about `#282321` to `#4e4844`); roof and brick terracotta sits in a narrow band (`#784430` to `#b3622f`); moss is olive-yellow (`#616e2e`, `#b6ab28`) against cool grey stone; cobble and masonry greys run `#817a7b` to `#b7b0aa` with warm lights (+0.12). In the town set lights are cooler than darks (-0.14 to -0.24) because of sky and overcast, so warm the lit faces in-game.

## Measured OoT 2026 frames (2026-10-29)

Source: 25 official Nintendo Life screenshots and 48 stills of the 4K reveal trailer (Video Games Chronicle), resized to 1024 px; same tool, k=7 pooled per set. Only scene frames were used (portraits and menus excluded; the one HUD frame, Castle Town market, cropped below its top bar). These are trailer or press grades, not raw engine captures, and sky and characters leak into masses; read relations, not exact hexes. Warmth is lights minus darks.

| Set (n) | Main masses (hex, share) | Value p10 / p50 / p90 | Warmth |
|---|---|---|---|
| Town day: Castle Town market, Kakariko aerial, Gerudo fortress, ruin arch (5) | cool blue-slate shadow `#2f3a44` 34%, grey-green stone `#5d6660` 20%, pale haze stone `#99a7ac` 16%, sandy plaster `#b7a680` 13%, red-brown timber and roof `#714b3a` 9%, terracotta `#bd8466` 8% | 0.23 / 0.44 / 0.78 | warmer (+0.19) |
| Forest: Kokiri path, Deku Tree vista (5) | grey-green mid `#55554f` 39%, near-black green `#272921` 26%, hazy light `#908c81` 24%, dark moss `#433f1b` 7%, lit moss olive `#746c29` 4% | 0.15 / 0.33 / 0.56 | slightly warmer (+0.05) |
| Dungeon dark: water, flooded door, cavern, pool, ghost ship (8) | deep teal-black `#1a2724` 39%, teal `#376268` 25%, olive stone `#57523f` 18%, sand stone `#93856a` 16% | 0.12 / 0.32 / 0.59 | warmer (+0.07); the teal frames alone are cooler (-0.10 to -0.24) |
| Interiors: castle corridor, boss room, torch halls (5) | near-black olive `#191509` 43%, dark brown `#3b1f13` 26%, brown `#594130` 17%, ember `#884125` 6%, lit plaster `#b59c91` 5% | 0.07 / 0.20 / 0.50 | warmer (+0.15) |

## Measured comparison with the photo sets (replaces the earlier proposal)

1. **The game is darker and lower-contrast than the photos, not brighter.** Town day p50 0.44 and p90 0.78, against photo towns at 0.51 and 0.92. No mass is near-white: the lightest town masses are `#99a7ac` and `#b7a680` (value about 0.65-0.72). The earlier plan to keep plaster near-white at p90 0.92 is wrong for this look. New day target: p10 0.22-0.24 (matches the earlier lift), p50 about 0.44, p90 about 0.78-0.80, with highlights carried by small bright accents rather than a big white mass.
2. **Shadows are cool slate-teal, never brown-black.** Day shadow mass `#2f3a44` at a third of the frame; dungeon darks `#1a2724` and teal `#376268`. Photo shadows (`#282321`) are warm near-black. Target: cool blue-green shadows in every set; the sunlit side carries the warmth. Lights are warmer than darks by +0.19 in town (photos -0.14), so the plan to warm lit faces stands and is stronger than proposed (+0.15 to +0.20, not +0.05 to +0.15).
3. **Masonry and plaster are tan, not white.** Plaster reads `#b7a680` to `#afa99f` and warm stone `#bd8466` terracotta, with roofs and timber `#714b3a` (earlier proposal `#9c4a34` was more saturated than any measured town mass). Keep roof red-brown around `#714b3a` to `#bd8466`; saturation comes from the sky blue and foliage, not the roofs.
4. **Forest and haze are heavy.** Forest p50 0.33, p90 0.56: the whole frame sits mid-low with a hazy grey-green `#55554f` mass and olive moss `#433f1b` to `#746c29`. Distance fog and light shafts do the lifting, so depth fog colour should be a warm grey `#908c81` rather than pure sky.
5. **Dungeons are teal-and-amber with value 0.12 / 0.32 / 0.59.** Dark-scene p10 0.12 matches the earlier 0.12-0.16 target; p90 is 0.59 (photo cisterns 0.75), so lamp highlights should stay modest. Interiors are darker still (p50 0.20, p90 0.50) with ember `#884125` accents: reserve p50 0.20-0.30 for corridors and halls.
6. **Unchanged.** Dark-scene lifted blacks, tidy placed wear, chunkier forms, and the 3-mass day structure (cool dark, mid stone, warm tan). The earlier +15-25% chroma raise is not supported by town masses (they are muted: chroma of stone and plaster is low); raise chroma on foliage, sky and accent colours only.

## Library gaps and licence notes

- Openverse was down (504s) on 2026-10-29, so the library was fetched from Wikimedia Commons with a fallback script (`CC0`, public domain and `CC BY` only; licence read from Commons file metadata). `REFS.md` rows carry source URL, creator and licence; the CC BY credit lives there.
- Weak or missing: coal cellars (no usable free photo found), flooded brick cisterns other than Basilica Cistern (its lighting is sodium orange), Victorian brick sewers, a Rothenburg-style market square with banners, a clean hill-town-above-a-lake photo (Meersburg roofscape stands in), and flat-on plaster and oak material close-ups (the plaster stand-in is a weathered wall). A search with different sources or a person-supplied set would fill these.
- The Basilica Cistern, Ta Prohm and crypt photos carry tourists' lighting and some saturation; use them for form and light placement, not exact colour.
