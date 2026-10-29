# Curved hulls and open vessels: boats, carts, troughs, wells

Part of **sc-asset-craft**: read SKILL.md first; this file adds what is specific to this kind of asset. Learned from the 2026-10-25 asset duo (blind test #9, a clinker rowboat). Numbers measured on one asset are marked as such: treat them as a starting point, not a rule.

- **Generate small boats** with **`Art/Blender/tools/sc_hull.py`**. No trusted source has a small rowboat (Poly Haven has only ships), and the shipped `Millpond_Prop_Rowboat` is a smooth loft, **not clinker**.
  - `Loft([(z, [keel .. sheer points])])`: stations from stern to bow, Catmull-Rom between them, girth by arc length.
  - `strakes(acc, hull, n, key_fn(t, s, kind), lap, thk, chamfer, worn_top)`: clinker planks with dark sloped lands. `kind` is `'plank'`, `'top'` (the worn gunwale) or `'land'`.
  - `skin(acc, hull, key_fn, offset, inside)`: the inner planking.
  - `Buckets(weld=True)` collects faces by (slot, layer, ao, tint) and `flush(g)` adds each bucket. That is the waterline and per-face paint helper, and it skips collapsed faces.
  - The blind boat's 65-line strake code became 2 calls, at the same 1 990 tris and 28% fewer verts. Example: `claude/asset-duo` `Art/Previews/duo/r26/rowboat_hull.py`.
- **Clinker laps must read without shadows.** A real 22 mm stepped lap is invisible in the preview, because its step faces point down and the AO bake misses the overhang. Make each lap a **chamfered strip tinted dark** (the lap's shadow painted in), about 25-30 mm wide. Use 6-7 strakes a side on a 4 m boat, and keep the bow loft smooth enough not to facet: 8 or more sections forward.
- **A waterline or paint boundary across planks** (a tarred bottom under painted topsides) needs a per-face tint. `MG.add` takes one tint per call, so use `sc_hull.Buckets` (it works for any raw geometry, not only hulls).
- **Faded paint** on a hull: take the boat red row of the materials table at about 70% chroma, plus a bare grey band at the gunwale where hands and oars wear it. Add the new colour to `B.PEELING` if it should peel, and vary the planks by value only.
- **Silhouette at 48 px.** A long, low boat is a sliver. Oars laid athwartships, a raised sheer at bow and stern, and a tall stem head add outline breaks; oars laid fore-and-aft add none.
- **Insides.** Run `sc_asset_preview.py --inside 1`, which writes `<out>_inside.png`: a top-down view and a 60-deg-down view, outside the scored sheet. Use it for boats, carts, troughs and wells. On the blind boat it showed at once that the bilge water was a near-black slab over the whole floor. Water in a vessel needs a sheen value and a shallow, partial fill.
