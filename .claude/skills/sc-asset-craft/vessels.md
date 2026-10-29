# Open vessels and coopered work: barrels, crocks, jars, buckets, troughs, basins

Part of **sc-asset-craft**: read SKILL.md first; this file adds what is specific to round and open vessels (batch B7, the B10 fountain basin). Lessons from FishGame's 2026-10-25 boat and bucket tests and the potion stand; treat numbers as a starting point.

- **Build them the way they are made.** A barrel is coopered: staves with gaps and a bilge, iron hoops standing proud, a chime at each end, end grain on the heads. A crock is thrown: throwing rings, a thicker foot, glaze ending in runs. A full glossy glaze reads as modern plastic; mostly bare clay with glaze over the shoulder reads old.
- **Staves must show.** A smooth lathe reads as one CG cylinder. Model the staves (or bake them from a stave high) so the seams carry in the normal map, with per-stave tone jitter in the material.
- **Turned and hard-surface vessels keep a clean source low** with a Catmull-resampled profile; collapse-decimating their remesh gives facet bands that read as stripes.
- **Open vessels need an inside.** Back faces aren't drawn, so a single wall shows its inside as a hole: give the wall thickness, an inner surface and a contents cap (water, grain, coal) at the fill line. Render one view looking down into it; the outside views miss it.
- **Liquid in a vessel** gets a sheen value and a shallow, partial fill, not a near-black slab. Use the fake-refraction liquid (sc-asset-craft section 1) when it is seen up close.
- **Silhouette:** a closed barrel reads as a blob. A lid ajar, a tap, a ladle, a sack slumped against it, or one on its side in a cradle adds the outline break.
- **Family batch:** barrels, crates, sacks, jars and crocks share one generator and one bake (PHASE1 9). Vary size, wear and contents per item rather than building one hero barrel.
