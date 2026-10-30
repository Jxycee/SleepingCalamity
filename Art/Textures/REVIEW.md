# Texture batch reviews

## B1-B3 first pass (controller review, 2026-10-30, P50)
Contact sheets: `Art/Textures/B1_town/sheet.jpg`, `B2_cellar/sheet.jpg`, `B3_trims/sheet.jpg`.

**Scores:** B1 6/10, B2 6.5/10, B3 4/10. A solid scan base; the plaster hue matches the bible.

**Worst three (fix first):**
1. **Canvas reads as flat CG yellow with painted stripes.** It needs a visible weave, slub and sun fade, a muted ochre, and stripes that are dyed into the cloth rather than flat paint.
2. **Saturation is above the muted OoT bible (3.1, updated 2026-10-30).** Painted wood is cyan-blue and should be a faded, chalky blue-grey with wear to bare wood. Roof tile is a hot orange-red and should sit at or toward `#9c4a34`, with more variation from tile to tile and less high-frequency noise at gameplay distance.
3. **The decals barely read.** Crack and drip are almost invisible at sheet scale and need stronger normal and albedo contrast. Moss is a camouflage blotch: it needs clumped olive-to-yellow cushions with soft edges (`#616e2e` / `#b6ab28`) and real height.

**Also:**
- Oak timber is too grey and cool. Half-timber beams should be a dark, warm brown in `#2e2520` to `#4e4844`, with a visible grain direction.
- Root bark reads as planks with knots, not bark. It needs fibrous, twisting ridges and a damp sheen.
- Old brick leans pink-orange. Pull it toward a muted red-brown with varied bricks and soot.
- Plaster is flat. Add subtle trowel undulation and edge grime, keeping the hue.
- Limewash blotches read as a pattern. Break up their repetition.
- Iron is very dark without a reflection probe. Recheck it in the Unity look-test before changing it.
- Cobble repeats at 2 m. Fix with a detail-noise or macro variation layer.

**Next:** fix these, then measure in the Unity look-test scene against the 3.1 scene bands (M8 part 2).
