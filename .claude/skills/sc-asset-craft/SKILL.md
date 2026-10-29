---
name: sc-asset-craft
description: Build or refine a SleepingCalamity 3D model (prop, building, boat, rock, tree part) as a procedural Blender generator in the Riverlight painterly style (spec R102). Use for any new Millpond/region asset, a detail or convergence pass on an existing one, or when a board card says "model", "prop", "detail pass" or "refine toward the concept". Pairs with sc-asset-budget (numbers), sc-asset-review (scoring) and sc-asset-adopt (external meshes).
---

# Building a SleepingCalamity model

Models are Python generators under `Art/Blender/` that build meshes from code, bake AO and export FBX. Never hand-model in a .blend and never commit a .blend as the source. The generator is the source; the FBX is its output.

These rules come from an overnight hillclimb on 2026-10-25: fresh agents built test assets from the skill, and what tripped them was fixed. That made builds faster and removed API mistakes; it has **not yet been checked in Unity**, and the grades come from the Blender preview and `sc_asset_score.py`. The log is `docs/superpowers/handoff/2026-10-25-asset-skills-duo.md`.

## 0. Before code (5 minutes, saves hours)
1. **Find the target.**
   - If the asset appears in a concept frame (`Art/Design/Concepts/Millpond/*_concept_1.jpg`), crop it (from two frames if possible) and note its silhouette, three value masses (light / mid / dark), colours and wear state (rundown or restored).
   - **If it isn't in any painting** (most small props), get form and construction from references (**sc-reference**: licensed photos and public-domain paintings, measured with `sc_ref_palette.py`). Take the light, palette and wear state from the frame of its *setting*: the sluice, mill and weir `mill_weir`; the landing and village `landing_rundown` / `landing_restored`; the tug and reeds `dawn_tug`; the shallows and ford `shallows`; the pasture, hills and anything else `overview` (palette only), and take target colours from the hex column of the materials table in section 2. Review against those with `sc_concept_match.py --targets`.
2. **Check it doesn't exist.** If the requested stem is taken by a different shipped asset (a 3.1 m smooth rowboat afloat vs a 4 m clinker boat on the slip), don't overwrite it: pick a stem that says the difference (`Rowboat_ClinkerSlip`) and note the clash in your report. Run `ls Assets/_Project/Content/Models/Millpond/*/ | grep -i <name>` and grep the generators. Extend an existing generator before starting a new file; one family goes in one file (crates with crates). When the family file uses an older pattern (no `to_base`, no paint pass), add your asset to it **using this skill's conventions** inside that file: the skeleton's conventions win, the family file is where they live. A standalone building of a kind that already appears inside a group model (the privy inside `sc_millpond_yards.py`'s yards) is a new stem with its own function in that family file, not a copy in a new file. Scratch work, blind tests and experiments write a standalone generator in the scratchpad; move it into the family file when it lands.
3. **Claim it** on the Millpond Work Board (its note `tp-claim-protocol`: one generator file, one owner) before starting real work. Scratch tests and experiments don't need a claim.
4. **Write the header comment first.** Copy the pattern of `sc_millpond_logs.py`'s header (lines 1-15): what it is, concept source, run line, output paths, real size, origin and axes, encoding, and a dated line per detail pass. The header is the asset's README (the HF study found the best AI-built assets ship one inside the file). Keep it current on every pass.

## 0b. Craft library
Generic Blender production notes (MIT, arjun988/blender-skills) are in `library/` (see `library/README.md`). Read one only when you need it: characters and retopology, rigging, props, Unity export, LODs and colliders, VFX. The house rules in this skill win over them.

## 1. Reuse the kit, don't reinvent
Load the modules with this block. It works from a generator in `Art/Blender/millpond/`; a scratch generator sets `SC_REPO=<repo root>` in the environment:
```python
import importlib.util, os, sys
R = os.environ.get('SC_REPO') or os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))   # the repo root
def _load(name, *rel):
    s = importlib.util.spec_from_file_location(name, os.path.join(R, *rel)); m = importlib.util.module_from_spec(s); s.loader.exec_module(m); return m
B  = _load('sc_millpond_buildings', 'Art', 'Blender', 'millpond', 'sc_millpond_buildings.py')
H  = B.H                                    # the buildings module's own sc_harbour: one instance, shared state (loading it again makes a second)
LK = _load('sc_millpond_landing', 'Art', 'Blender', 'millpond', 'sc_millpond_landing.py')
HM = _load('sc_handmade', 'Art', 'Blender', 'tools', 'sc_handmade.py')
PP = _load('sc_paint_pass', 'Art', 'Blender', 'tools', 'sc_paint_pass.py')
import bpy; bpy.ops.wm.read_homefile(use_empty=True)
```
The module aliases:
- `H` = `Art/Blender/sc_harbour.py` (primitives, palette slots `H.S` incl. Snow 11 to FoliageAlt 15, env layers `H.L`, AO, export)
- `B` = `Art/Blender/millpond/sc_millpond_buildings.py` (the Millpond building kit)
- `LK` = `sc_millpond_landing.py` (the landing props; its `LK.B` is `B` and `LK.H` is `H`)
- `T` = `sc_millpond_textures.py` (`T.ML`, the Millpond layers)
- `HM` = `Art/Blender/tools/sc_handmade.py`, `PP` = `Art/Blender/tools/sc_paint_pass.py`

**The prop skeleton (runnable; copy it):**
```python
import os, sys
OUT = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else os.path.join(H.ROOT, 'Assets', '_Project', 'Content', 'Models', 'Millpond', 'Props')
B.MILLPOND_LAYERS = True                   # every Millpond model draws on M_Millpond_Base / _Tri (MillpondBuilder art.Toon): layers 16+ OK
B.PEELING = ()                             # small props (< 1 m parts): no auto PeelPaint (the kit applies it to any GREEN_PAINT/FRAME-like tint)
b = B.Bldg('MyProp')                       # b.base -> Millpond_Bldg_MyProp.fbx; b.tri -> ..._Tri.fbx (stone/brick/slate/tin, world triplanar)
b.base.name = 'Millpond_Prop_MyProp'; b.tri.name = 'Millpond_Prop_MyProp_Tri'
r = B.rng('myprop.boards')
b.base.add(HM.wobble(B.box((0.7, 0.03, 0.14)), r, 1.0, 0.004), H.S['Rock'], H.L['Wood'], 0.45, tint=(0.68, 0.64, 0.56))   # MG.add(geo, slot, layer, ao_floor, tint=RGB)
shift, size = LK.to_base(b)                # origin to the base centre; KEEP shift: anything placed later in final coordinates needs it
objs = B.realize(b)                        # AO bake (bake_ao2) + objects + per-face tint into Col
for ob in objs:
    PP.paint_pass(ob, seed=1.7, **PP.WOOD)  # rule 9
    if '--no-export' not in sys.argv:
        os.makedirs(OUT, exist_ok=True); print(H.export_if_changed(ob, os.path.join(OUT, ob.name + '.fbx')))
```
**A building or outbuilding** (privy, shed, kiln, pump house, cottage) uses the same skeleton with three changes:
- Keep the kit's `Millpond_Bldg_<Stem>` names (drop the rename line).
- Write to `B.OUT_DIR` (`.../Millpond/Buildings`), not `Props`.
- Keep `B.PEELING` as it is when the building has painted parts of 1 m or more (doors, shutters, boards): they take the PeelPaint grain. Set `B.PEELING = ()` only when every painted part is under 1 m. On a narrow painted part (a door or shutter under about 0.8 m wide) the PeelPaint grain shows one camo blotch: set `B.PEEL_UV_SCALE = 2.5` (finer peel) before building it. `B.PEEL_MIN_SIZE` skips PeelPaint on parts whose *largest* dimension is under it, so it doesn't catch a tall narrow door.

Stone, slate, brick and tin go on `b.tri` (`_Tri.fbx`, world triplanar); timber, paint and glass go on `b.base`. Score it with `--class outbuilding` (sc-asset-review).

A prop with no stone/tin parts leaves `b.tri` empty: `realize` skips it and no `_Tri` FBX is written, which MillpondBuilder handles. Run it with `-- --out <scratch>` while iterating: the audit needs an FBX, and the scratchpad keeps the repo clean. Give your generator `--only`, `--no-export` and `--out`, as the duo generators do. Not every kit generator has `--preview`; use `sc_asset_preview.py` on the exported FBX instead. `geo` is the kit's `(V, N, F, UV)` tuple from `H.rbox`, `B.box`, `H.lathe(...)[0]`, `H.tube` or `H.blob`, placed with `H.xform(geo, centre, rot)`.

**Kit costs** (measured with `blender -b --factory-startup --python Art/Blender/tools/sc_kit_costs.py`; `sc_kit_costs.tris(geo)` counts any part in your generator):

| Primitive | Tris |
|---|---|
| `B.box` | 12 |
| `H.rbox` segs 1 / **2 (default)** / 3 | 44 / **108** / 188 |
| `H.prism` square, segs 1 / 2 | 44 / 108 |
| `H.lathe(..., sides)[0]` | 2 x sides per profile segment (+ sides per cap): 8 sides x 2 segments = 32 (+16 with caps) |
| `H.tube(pts, r, sides)` | 2 x sides per segment: 6 sides, 4 segments = 48 |
| `H.blob` subdiv 1 / 2 / **3 (default)** | 20 / 80 / **320** |

| Need | Use |
|---|---|
| rounded box (chunky, hand-made) | `H.rbox(size, r, segs=1)` for small parts (44), `segs=2` (108) only for hero edges |
| flat board, slate, plank, tooth, bolt | `B.box(size)`: 12 tris |
| turned or round part | `geo, _ = H.lathe(profile, sides)` (**it returns a tuple**): posts and handles 8, lanterns 10, buckets, barrels and buoys 12, hero round things 16-24 |
| open vessel (pail, bucket, tub, trough, planter) | an outer lathe, plus an inner lathe of the profile at a smaller radius run top-down (or `H.mirror_fix` on it) so its faces point inward, plus a dark contents cap (soil, water, feed) at the fill line. Back faces aren't drawn, so a single wall shows its inside as a hole |
| rope, pipe, rail, branch | `H.tube(pts, radius, sides)` (6 sides); sagging rope `H.rope_line(a, b, sag)`; coil `H.rope_coil` |
| lumpy organic (moss cushion, sack, bush core) | `H.blob(radius, r, subdiv=1 or 2)`: the default 3 is 320 tris |
| a profile swept along a path (gutters, mouldings, hull rails) | `H.sweep_profile(path, prof)` |
| stone courses, slate roof, window, door, chimney, gutters, plank wall, post, tin roof | `B.stone_walls`, `B.slate_roof`, `B.window`, `B.door`, `B.chimney`, `B.gutters`, `B.plank_wall`, `B.post`, `B.tin_roof` |
| place/rotate | `H.xform(geo, centre, rot)`, `H.rx/ry/rz(deg)` |
| mirror a part (a left/right pair, a fish head-down) | `H.xform` with a reflection, then `H.mirror_fix(geo)` (reverses faces so they still face out) |
| a part along a segment (rail, strut, handle, shaft) | `HM.along(geo, a, b)`: build it along +z at the segment's length |
| hanging cloth (apron, sack, tarp, washing) | a thin `H.rbox`, then `HM.pleat(geo, along=0, depth, count)` and `HM.sag`: a flat panel reads as a board |
| hinged part (door, lid, gate) | `HM.hinge(geo, pivot, axis, deg)`: check one corner after the swing; a +z-facing door hinged at x-min opens outward with a negative deg about y |
| a wall or gable facing x (ridge along x) | `B.plank_wall` / `B.boards_gable` only face z: build in a scratch `B.Bldg`, `B.rotate(scratch, 90)`, shift its V, `B.merge(real.base, scratch.base)` (the duo's net shed). `boards_gable` is for a ridge along **z**, whatever its comment says |
| tin roof | `B.tin_roof(b, W, D, eave, ridge, seed, cap='flat', rust_tint=None)`: `eave` and `ridge` are the heights of the sheets' mid-plane at the wall line and the ridge; the underside at the wall line sits at `eave - 0.03`, so pass **`eave` = the top of the wall plate + 0.03**, or a gap shows. New roofs use **`cap='bent', rust_tint=B.RUSTY_TIN`**: the default flat 0.5 m cap reads as a floating plank (it stays only so shipped roofs export unchanged), and the default `RUST` reads terracotta with no headroom. `cap=None` for a roof you cap yourself |
| cheap real brickwork on a prop | an `H.rbox` mortar core, then one `B.box` per brick keeping only its outward faces, 6 mm proud: real joints at 4 tris a brick (the duo's kiln). `B.stone_walls` is for buildings |
| trees, cards, foliage atlas | `sc_millpond_trees.py` and the **sc-foliage** skill |

## 2. The encoding (what the shader reads)
- **Axes:** author in **Unity axes** (y up, front +z), metres, real size. The origin sits at the bottom centre (props, buildings), the trunk foot (trees) or as the header states. Blender = (-x, -z, y). Export with the kit's `export_fbx` / `export_if_changed` (-Z forward, Y up, bake_space_transform) and never change these settings.
- **Material:** none is exported (the audit shows `mats: 0`). MillpondBuilder assigns `M_Millpond_Base` to `<name>.fbx` and `M_Millpond_Tri` (world triplanar) to `<name>_Tri.fbx`. The harbour uses `M_Toon_Base` / `M_Toon_Tri` the same way.
- **UV0** `UVMap`: metres along the grain (wood u along the board), or the atlas cell for cards.
- **UV1** `Layer`: x = **palette slot** (`H.S`), y = **texture layer** (`H.L` env 0-7; Millpond `T.ML` 16 Stone, 17 Rust, 18 Slate, 19 PeelPaint, 20 Limewash, 21 Granite). Integral values only.
- **Vertex colour** `Col` (linear, CORNER): **0.5 x ao x tint**, clamped to 1. The drawn albedo is `lin(slot) x ao x tint`, so **`tint = lin(target) / (lin(slot) x ao)`**.
  - Don't guess the tint. `python3 Art/Blender/tools/sc_tint.py tint '<#hex>' --ao 0.8 --lit` ranks the slots for a target colour, and `sc_tint.py color Rock 0.68,0.64,0.56` shows what a tint renders. README and painting hexes render at about 1.0x their albedo in the preview (calibrated), so `--lit` barely moves them.
- **Headroom:** the paint pass can lighten a worn edge only up to Col 1. Keep each part's Col under about 0.85 (the audit WARNs "no headroom"). That's why the table below prefers a lighter slot over a big tint on Wood. `--prefer` a material's own slot only when it keeps headroom.
- **Layer scale:** layers tile at 0.6 repeats per metre (one tile is about 1.7 m). On a part under 1 m, PeelPaint, Stone or Limewash show one blown-up blotch. Use them on parts of 1 m or more; on small props use Wood (3) / Rock (6) or none, and put the wear in the paint pass.
- **Silent relayering** (`B.MILLPOND_LAYERS = True`, as every Millpond model sets): the kit's `MG.add` remaps some (slot, layer) pairs.
  - (Rock slot, Rock layer 6) becomes **Stone 16**.
  - (Rock slot, Sand layer) becomes **Limewash 20**.
  - Any Wood-layer part whose tint points the way of a `B.PEELING` colour (green paint, frames) becomes **PeelPaint 19**.
  - Inside `B.tin_roof`, Roof or Rock on the Rock layer or none becomes **Rust 17**.
  - Every other pair passes through unchanged. **(Roof, Rock) stays env Rock 6**, the cobble look: that's what `B.chimney`'s brick and anything on `B.BRICK` draws today. Brick on Rock + Sand gets Limewash's plaster craze; on Rock + Rock it gets Stone's flagstone cracks. `B.BRICK_LAYER = True` (opt-in) moves every brick part to Brick 22. It stays off until a local session builds layer 22 into the array and re-exports; see the duo log.

  So a small Rock-slot part that should keep the plain rock grain needs another layer: `H.L['none']` or Wood. Check the audit's layer list if a part's grain looks wrong.

**Materials** (hex = the albedo it renders at ao 0.8, from `sc_tint.py color`; use these hexes as `--targets` for props outside the paintings):

| Material | Slot + tint | Hex | Layer |
|---|---|---|---|
| weathered grey timber | Rock + (0.68, 0.64, 0.56) | #6a675d | Wood 3 |
| scrubbed / pale oak (restored tops) | Sand + (0.54, 0.53, 0.63) | #9a8a6c | Wood 3 |
| creosoted / tarred timber (a dark mass) | WoodDark + (0.45, 0.70, 1.18) | #3e332a | Wood 3 |
| dark painted frames | Dock + `B.FRAME` (0.29, 0.48, 0.81) | #4a473e | Wood 3 |
| faded green paint | Rock + (0.42, 0.71, 0.48) | #546c56 | Wood 3 |
| restored teal (doors, rails) | Rock + (0.36, 0.81, 0.59) | #4e735f | Wood 3 |
| boat / mill red | Roof + (0.89, 0.90, 0.64) | #99412b | Wood 3 |
| cream paint | Sand + (0.71, 0.52, 0.55) | #ae8965 | Wood 3 |
| mustard | Wood + (1.18, 1.25, 0.67) | #a47a38 | Wood 3 |
| white paint | Snow 11 + a grey tint (about 0.6) | | none |
| stone walls (Tri) | Rock + `B.STONE` (0.78, 0.73, 0.55) | #716d5c | Stone 16 (parts >= 1 m) |
| slate (Tri) | Rock + `B.SLATE` (0.34, 0.45, 0.53) | #4c575a | Slate 18 |
| limewash render (Tri) | Rock + `B.LIMEWASH` (1.3, 1.27, 1.25) | #8f8d87 | Limewash 20 |
| cast iron (black) | Rock + `B.IRON` (0.30, 0.31, 0.33) | #474848 | none |
| rusty iron | Rock + `B.IRON_RUST` (0.42, 0.30, 0.22) | #54473a | none (Rust 17 on sheets) |
| rusty corrugated tin (Tri) | Roof + `B.RUSTY_TIN` (0.30, 1.13, 1.17), via `tin_roof(rust_tint=...)`. The README's lit `#915544` used as an albedo renders salmon | #5c493b | Rust 17 (tin_roof sets it) |
| galvanised / lead | Rock + (0.66, 0.74, 0.82) / `B.LEAD` | #686e6f | none |
| brass | Sand + (0.42, 0.42, 0.30) (Accent can't: it stays orange) | #897c4b | none |
| verdigris | Sand + (0.20, 0.42, 0.55) | #617c65 | none |
| moss (seasonal) | Reed + `B.MOSS` (0.31, 0.29, 0.48) | #525839 | none |

The kit's older constants `B.GREY_TIMBER` and `TEAL` on Wood give the same colours with **no headroom** (Col 0.93-0.97); the census reports them as legacy. Use the rows above for new work. (`CREAM_WOOD` moved to the Sand slot with headroom on 2026-10-25.)

- **Seasons:** colour comes from the palette slot (`Assets/_Project/Content/Art/ArtPalette.asset`). Slots 0 Grass, 1 Foliage, 2 FoliageDark, 7 Reed, 14 Blossom and 15 FoliageAlt **change with the season**: anything living (grass, leaves, reeds, moss, ivy) goes on one of them, with a tint. Slots 3-6, 8-10, 11 Snow (white all year) and 12/13 PineLight/PineDark **never change**. Slot 14 is tree blossom (pink in spring, green otherwise): never use it for flowers.
- **Flowers and washing** are man-made garden colour: petals and clothes on Rock with a tint (`BLOOMS`, `CLOTHES` in `sc_millpond_restored.py`); their stems on a seasonal slot. A bed that vanishes in winter is a builder placement rule, not a colour.

## 3. Riverlight look rules (R102)
The game lights everything with a soft wrapped sun, a cool sky-coloured shade and a painterly world-space drift (`_FGPaint`). The model supplies **shape, value structure and local colour**. Each rule says what it fixes on the rubric (sc-asset-review).

1. **Silhouette first** (line 1). At 48 px tall (the preview's play-distance panel) the asset must still read as what it is.
   - Spend triangles on **outline breaks**: a leaning post, a sagging roofline, a lid ajar, a rope loop, a chimney pot, a handle that stands off.
   - **A break counts only if it is at least 2 px at 48 px, about 4% of the model's height.** A broom handle or a line under that is invisible to the player and to `sc_asset_score.py`.
   - **Negative space:** the library's best-reading props let you see through them (crayfish pot, net rack, benches, gate, hay rack, stall); closed one-value boxes (nest box, crate stacks) read worst.
   - A naturally solid object gets its silhouette from what sits on or around it: a lid open, handles, a tool leaning on it, legs or skids lifting it.
2. **Mass, then openness** (lines 1-2). Every prop needs **at least one solid, heavy mass**, about a quarter of its silhouette or more: a thick top, a chest, a cupboard under the bench, a stone base. Openwork alone reads as scaffolding (the blind test's airy tool rack). **Openness is daylight between masses, not parts sticking out.** The silhouette score counts dents against the outline's hull. A lifted sheet or a door swung flat against the wall *fills* a dent, while a gap adds one: a butt standing 0.2 m off the wall, a lean-to, a woodpile with daylight under it, a deep eave over a recessed wall (no outward change moved a privy from 3 to 4). Make it slightly top-heavy: the top piece thicker and wider than what carries it.
3. **Chunky sections** (lines 2, 6). Real sizes, then a little heavier. Nothing structural goes below about 1.5% of the model's height, except lines (rope, net, wire).

| Part | Section |
|---|---|
| posts and uprights over 1 m | 90-120 mm square |
| furniture legs, small posts | 60-80 mm |
| rails, stretchers, cleats | 45-70 x 70-100 mm |
| tops, seats, benches | 35-50 mm thick, overhanging the frame by 40-60 mm |
| boards, planks, lids | 25-40 mm |
| door / frame stiles | 45-70 mm |
| iron straps, bands | 6-10 mm thick x 40-60 mm wide |
| bolt and peg heads | 20-30 mm, standing 10-15 mm proud |
| rope | 15-25 mm (6-sided tube) |

4. **Form vocabulary** (line 6). Rounding alone doesn't give form; joinery does. Use 3-5 of these per prop:
   - tops overhang 40-60 mm
   - legs splay 1-2 deg
   - cleats or battens under tops and across doors
   - through-tenons and peg heads standing proud
   - chamfered or capped post tops
   - a plinth or skid at the foot
   - visible joints: straps, bolts, pegs
   - `rbox` rounding only on the edges that catch the eye (lids, top edges, outer corners, handles). The rest stays flat `box`: rounding every board of a 0.7 m chest cost +650 tris and a density WARN.

   **For buildings and outbuildings,** the joinery is architectural. Use 3-5 of these:
   - corner posts and door jambs as `rbox` segs 1 (44 tris each) that stand 20-30 mm proud of the cladding
   - a sole plate or plinth that is wider than the wall
   - a soft roof edge: barge boards, or the sheets' overhang dipping 1-2 deg at the ends
   - a bent ridge cap (see the tin roof row)
   - a door with ledges and a brace (the Z), set 20 mm into its frame
   - a step, slab or a pair of bricks at the threshold
   - an eave gutter on brackets
   - boards that lap, so each one casts a line
   A closed hut reads as a box at 48 px (duo r10 privy, silhouette 3). Rule 2's openness for a building is an open or ajar door onto a dark inside, an overhang deep enough to shade, and things leaning on or fixed to the walls: a butt, a ladder, a bench, a pipe. Give it at least two.
5. **Three value masses and a contrast pair** (line 3). **Contrast goes BETWEEN masses; brush variation stays WITHIN each.** Don't fix value with large flat dark areas: they flatten the brush band and cost a painterly grade (blind test #2: the feed bin traded painterly 4 for value). A dark mass still carries per-board tint and the paint pass. Decide light / mid / dark per asset and keep tints inside each mass within about ±12% value. Give every asset one **contrast pair**: a pale element against a dark one (a pale sign on a black post, pale hay in dark iron, scrubbed oak on creosoted legs). An all-timber prop has no natural dark mass, so make one: a tarred base band, iron hardware, the shadowed inside of an open box, a wet band at the foot. **Break a band's top edge board by board** (each board's tar or damp line at its own height, ±40-80 mm), never one straight split: the scorer rewards a straight band, but a blind grader called it "hard horizontal bands that look CG" and preferred plain board-by-board timber (A/B run 2).
6. **Paint the colour in patches, not per face** (line 5). The concepts drift warm and cool *inside* a material.
   - Vary the tint per **patch** (a board, a course, a roof area): ±6-10% value and ±0.04 hue, warm toward up-facing faces and cool or greener toward the bottom and the damp side. (In the game the sun moves with the day, so "warm" means up, not a fixed side.)
   - Draw from `rng(stream)`. Per-face random tint reads as confetti, and the scorer penalises it.
   - **Painted parts: value-only jitter.** The kit picks PeelPaint by the tint's *direction* (`B._painted`), so hue jitter silently drops a painted board to bare wood. Scale painted tints by one scalar. If you add a paint colour that should peel, append it to `B.PEELING`.
   - Opening things up moves painterly more than paint does: an open lid showing a tray, a rack of fish, a coil of line.
   - **Contents must be recognisable objects, not blocks.** A tool reads by its handle plus its head (a crowbar's hook, a spade's blade, a rod's taper), at 3-5 parts each. In the A/B test, the no-skill chest's real crowbar, spade and rod beat the skill-built chest's "floating blocks" by eye, though the scorer preferred the tidy one.
7. **Hand-made, not machined** (line 6). Nothing man-made in the concepts is perfectly straight, flush or repeated. Every board, slat, stave, slate and post gets its own deterministic irregularity from `HM`:
   - `wobble(geo, r, deg, shift)`: boards 0.5-1.5 deg and 2-5 mm, posts 1-3 deg, never above 4 deg unless broken
   - `sag(geo, axis, amount)`: a 1.2 m board sags about 6-10 mm
   - `taper`: split posts, worn steps
   - `jitter_length`: ±4% on slats

   This habit alone moves a prop from "game asset" to "made by someone".
8. **AO carries form.** The kit's bake (`bake_ao2`) with per-part floors (wood 0.45, rock 0.62, props 0.5): crevices darken but never go black, and contact shadows come from the ground occluder.
9. **The painted finish** (lines 5, 6). After `B.realize(b)`, run `PP.paint_pass(ob, seed=<per asset>, **preset)` on every object.
   - What it paints: a worn, lighter band on every bevel (the strips of `rbox`), darker concave fillets, warm tops and cool undersides, and brush variation inside every part (`brush=0.35`, ±17% value, warm/cool at 0.22 m; the cool side is blue-green). It works per face corner, so the band is exactly the bevel.
   - It shows only where there are bevels and headroom.
   - Presets `WOOD`, `STONE`, `METAL`. Layers without a preset are left alone: rope (7) and PeelPaint (19) need their own entry in `presets_by_layer` (rope: `PP.WOOD` with a smaller brush; peel: `PP.WOOD`). A mixed object takes one preset per **texture layer** with `presets_by_layer={0: PP.METAL, 3: PP.WOOD}`: timber carries the Wood layer and iron none, while both sit on the Rock slot in the headroom recipes, so `presets_by_slot` can't separate them.
   - `HM.split_long` adds nothing on boards narrower than about 0.4 m.
10. **Round parts get staves** (line 5). A barrel, bucket, tub or round tower reads as one smooth CG cylinder unless its staves show.
    - Build the lathe with `sides` = 2 x the stave count.
    - After realize, call `PP.stave_bands(ob, centre, count, value=0.2, seam=0.4, r_range=..., h_range=...)`. Give the centre in final coordinates (the authored axis minus `shift`).
    - Use the ranges so the lid, tap, hoops and pots stay unbanded.
11. **Storytelling wear, readable at 10 m** (line 7). **Make every noun in the brief readable from the three-quarter view** (a pump house needs its well: a stone ring, a cover, a bucket), **and deliver every wear word in the brief** ("faded paint", "one replaced plank", "iron corners"), each visible from the three-quarter view, before polishing anything else. Clean budgets and good scores don't make up for a missing story beat: that's how the skill-built chest lost the blind A/B to a messier one. Spend triangles up to about 1.5x the class median on story beats; efficiency only drops a grade past that. Choose 2-3 beats. **At least one must change the value pattern at 48 px**: a light new board in a dark run, a dark wet or tarred base band, a missing board showing the dark inside, or an accent object of 0.3 m or more. Fresh paint counts only if it changes the value pattern.
    - **Rundown:** faded paint (tint toward the slot grey), moss on up-facing edges and the shaded side, rust on fixings, missing or slipped pieces, a lean. Concentrate wear where water and weather go (bottoms, edges, joints, the waterline). "No photo noise, no grime."
    - **Restored is not showroom:** a tool left out (watering can, bucket, broom, a coil of new rope), one replaced board lighter and warmer, a fresh paint edge over older paint, chalk marks or a stencilled number, a cloth on a hook, flowers, a clean worn patch where hands go, moss scrubbed off the front but left at the back.
12. **Real proportions** (line 2). Use real dimensions (a door 0.95 x 1.95 m, a crate 0.5-0.6 m, a rowboat 3.6-4.2 m, a bollard 0.4-0.6 m, a workbench top at 0.85-0.9 m), then make them a little heavier and top-heavy. Check with `sc_asset_preview.py --ref human,door`. Never shrink to "cute".

### Asset-specific notes (read the one that matches)
- **Creatures** (birds, fish, insects; `sc_millpond_life.py`): [creatures.md](creatures.md)
- **Curved hulls and open vessels** (boats, carts, troughs, wells; `sc_hull.py`): [hulls.md](hulls.md)
- **Stonework and modular runs** (walls, fences, hedges; class `run`): [stonework.md](stonework.md)
- **Trees, bushes, reeds, shore plants:** the **sc-foliage** skill. **External meshes:** **sc-asset-adopt**.

## 4. Workflow loop
Copy this checklist into your notes and tick it off:
```
- [ ] 0  target found (concept crop or sc-reference), name checked, header written
- [ ] 1  blockout: silhouette and masses, flat tints; preview at 512
- [ ] 2  encode: slots/layers/tints by value mass; headroom kept
- [ ] 3  detail: outline breaks, sections, joinery, contrast pair, patches, hand-made, wear; paint pass
- [ ] 4  audit PASS -> preview -> score (percentiles) -> rubric by eye
- [ ] 5  top 3 defects fixed, re-measured (loop until the review passes)
- [ ] 6  determinism (two audits equal); export (cloud: scratch only)
```
1. **Blockout:** silhouette and masses only, flat tints. Export to the scratchpad (`--out`) and preview at `--size 512`. That's the size the scorer needs: one size throughout, about 6 s a render.
2. **Encode:** slots, layers, tints by value mass (table above, `sc_tint.py`); AO.
3. **Detail:** outline breaks, sections, form vocabulary, the contrast pair, patch colour, hand-made irregularity (rule 7), wear beats; after realize, the paint pass (rule 9) and staves (rule 10).
4. **Measure:**
   - `sc_asset_audit.py` (budgets, encoding, headroom)
   - `sc_asset_preview.py` (look)
   - `sc_asset_score.py` (objective lines 1/3/5/8 against the shipped library)
   - `sc_concept_match.py --targets` (colour)
   - then the sc-asset-review rubric
5. Fix the top 3 defects and re-measure. Stop when the review passes; don't polish past the play-distance read.
6. **Export to the repo** with `export_if_changed` (unchanged FBX stay byte-identical, no LFS churn). Commit the generator and the FBX (plus `.meta` only if Unity made it).

Blender in a fresh cloud box (30 s): `mkdir -p ~/apps && curl -sL https://download.blender.org/release/Blender4.5/blender-4.5.3-linux-x64.tar.xz | tar -xJ -C ~/apps`, then use `~/apps/blender-4.5.3-linux-x64/blender`. **A cloud session commits the generator, not the FBX.** Exports go to the scratchpad (`--out`), because the user's PC runs Blender 5.2 and its bytes differ (`docs/superpowers/handoff/cloud-environment.md`). The local session runs the generator to write `Assets/` and the Unity check. Locally it's on the PATH.
```
blender -b --factory-startup --python Art/Blender/millpond/<gen>.py -- --out <scratch> [--only <stem>]
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py   -- <scratch>/audit.json --class prop <scratch>/*.fbx
blender -b --factory-startup --python Art/Blender/tools/sc_asset_preview.py -- <scratch>/sheet.png --size 512 <scratch>/X.fbx+<scratch>/X_Tri.fbx
python3 Art/Blender/tools/sc_asset_score.py <scratch>/sheet.png --class prop --audit <scratch>/audit.json
```

## 5b. Tripo-style construction (kit research, 2026-10-28)
A teardown of an AI-generated cottage (7.8k tris, 256 parts) showed why its shapes beat our clean primitives: every part is **deformed** (bent, tapered, twisted, sagging, chamfered), parts are **pushed into each other** (230 of 256 touch a neighbour) so the silhouette is a compound, faces hidden inside or against another part are **deleted**, it is all smooth-shaded, and the texture/paint carries the fine detail (no bevel loops, no greebles). Use it for hero silhouettes (chimneys, dormers, barrels, roofs, window frames); the helpers are in `HM` (`sc_handmade.py`, section "Tripo-style construction"):
1. **Deform every part.** `tbox(size, chamfer, bend_deg, twist_deg, taper_amt, seg, r, warp_amp)` (chamfered, cut, bent box), `bend(geo, axis, deg, toward, seg)`, `twist(geo, axis, deg, seg)`, `warp(geo, r, amp, size)` (smooth noise offset, deterministic via `r`), `cut(geo, axis, n)` (slice a long part so it has vertices to bend). Nothing straight, no two alike (one `r` draw per part).
2. **Overlap into compounds.** Sink a cap 3 cm into its shaft, a frame into a hood, a hood into the roof; then `compound(parts, crease=...)` culls the buried and flush faces (`cull_buried`, works between any closed solids, not just axis boxes) and smooth-shades (`renormal`; `crease` 70-100 keeps hard edges where a cap meets a shaft). Occluders must be closed solids.
3. **Builders:** `extrude(loop, depth, segs)` (any 2D outline), `roof_slab(w, d, rise, thick, sag, flare)` (one thick roof, ridge sagging, eave corners flared, ~140-190 tris), `arch_ring(w, h, bar, depth)` (hollow arch window/door frame, ~70 tris), `eyebrow(w, rise, depth, thick, fall)` (eyebrow-dormer hood). `merge(parts)` joins geos. `loft(rings, sides)` (a stack of squared or round rings, each with an offset and turn: kinked chimney, flared cap, recessed top, barrel; 8 tris per ring step at 4 sides) and `kink(geo, axis, steps)` (piecewise shear: a zig-zag silhouette, not bend's even arc) are the cheapest way to match an AI part's measured profile; measure the original's rings first.
4. **Let the texture carry detail.** Few tris per part (a chimney is 58 in the original, about 130 here); smooth normals instead of bevel loops; paint pass for wear.
Proof and contrast sheet: `Art/Blender/tools/experimental/sc_tripo_style_demo.py` (chimney, dormer, barrel: Tripo / new / old). Existing helpers (`wobble`, `sag`, `taper`, `cull`) are unchanged.

## 5. Determinism and hygiene
- **Gate on the audit JSON, not the FBX bytes.** FBX headers carry a timestamp. Export twice to the scratchpad (`--out`) and diff the two `sc_asset_audit.py` JSONs (the same in sc-asset-review).
- **Tapered tubes** (stems, saplings, roots, horns): `HM.tube_taper(pts, r0, r1, sides, caps)`, with the same frames and winding as `H.tube`.
- **Lathe caps, not r = 0 points.** A profile point on the axis plus a cap leaves loose vertices (an audit WARN). End the profile at a small radius and use `cap0` / `cap1`. The cork floats in `sc_millpond_landing.py` still use the old pattern; don't copy it.
- **Splitting a lathe** (the wall on Base, the cap on Tri, or a separate tint per band): passing the full vertex list with only some of its faces leaves the rest as loose vertices (75 in duo r10). Build two lathes from the two parts of the profile, or compact the vertex list to the faces you keep.
- **A lean or a sink** (a rundown hut leaning 2-3 deg): rotate about the base edge on the low side, so that corner stays at y 0, and state the lean in the header (the review's y = 0 gate accepts "as the header states"). **Check the physics for hulls and barrels:** a flat-floored boat on hard ground rests at about 16 deg, so a smaller heel needs chocks or a keel block under it.
- **Collision comes from placement, not the model.** MillpondBuilder gives a placed model either a `Box` collider from its render bounds or a `Mesh` collider from the render mesh itself (`TripoCollider` on its placement row). So a wide attachment (a stake ahead of a bow, spilled stones, an oar) inflates a box collider into an invisible wall, and a dense mesh collider costs physics. Keep blocking attachments as separate placements, or say in the header which collider the asset needs.
- **Origin with attachments:** `LK.to_base` centres on the bounding box, so an attachment (a stake and painter ahead of the bow, a ladder, a spill) shifts the origin off the main body. Centre on the body, or state the offset in the header so the builder can place it.
- Every random draw comes from `random.Random(SEED ^ crc32(stream))` (`B.rng('name.part')`). Never `random.random()` or time. `mathutils.noise` is position-only, so it's safe.
- **Winding and normals:** the kit's helpers orient faces outward. `MG.add` re-orients every face from its **vertex normals**, so for raw geometry the normals decide everything and winding alone doesn't. The `oriented()` pattern in `sc_millpond_logs.py` sets both.
- **Degenerate faces:** `MG.add` drops zero-area faces (overlaps collapsing at a stem or a point) and leaves their vertices behind, which the audit reports as loose verts. It prints `FGKIT <group>: dropped N degenerate face(s)`: read the generator's output for it, then weld or skip those faces. One shipped model triggers it: `Millpond_Bldg_houseboat`.
- **Split vertices are normal:** the kit gives every face its own vertices (flat shading and per-face Col), so the vertex count runs at about 2.7x the triangles. The audit doesn't flag it, and it isn't a defect.
- No loose verts, no zero-area faces, no internal faces hidden inside other parts (they cost tris and confuse the AO).
- Name objects `Millpond_<Kind>_<Name>[_Tri]` (Prop, Bldg, Tree, Rock, Life, Cover, Gear, Item, Board): the name is the FBX stem MillpondBuilder places. Season variants add a suffix (`_Bare`, `_Flowers`, `_Pods`, `_Winter`).
