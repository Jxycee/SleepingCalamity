# Ring 1 market stalls (Wave 1): sources, states, pivots

The three fixed stalls of the Hollowmere square (layout rows Stall_1 produce, Stall_2 cloth, Stall_3 tools). Placed by `HollowmereBuilder` (stall rows) at each landmark row including its pad height; the Ruined preset keeps the `Ruined` child, Rebuilt keeps `Rebuilt`.

## Generator
`Art/Blender/town/sc_market_stall.py` (procedural, reproducible from text; extends the approved B8 MarketStall, which it still builds unchanged without `--variant`). Per variant `<v>` (produce, cloth, tools), cloud Blender 4.5.3, 2026-10-31:

    blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch> --variant <v> --state ruined  --stage bake --res 2048 --export --no-render
    blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch> --variant <v> --state rebuilt --stage bake --res 2048 --export --no-render
    blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch> --variant <v> --combine
    python3 Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/maps Stall_<V>_<State> <this folder>/<V>      (for each state)

## Files per variant folder (`Produce/`, `Cloth/`, `Tools/`)
- `Stall_<V>.fbx`: root `Stall_<V>` with two mesh children, `Ruined` and `Rebuilt`, one material slot each, UV0 = that state's bake atlas. -Z forward, Y up, metres, transforms baked; pivot at the ground centre of the 2.8 x 1.8 m footprint, counter facing local +Z. The awning overhangs the front by about 0.3 m; the ruined crates and fallen pieces stay within about 0.6 m of the footprint.
- `Stall_<V>_<State>_BaseColor.png` (sRGB), `_Normal.png` (tangent, OpenGL Y+), `_ORM.png` (R occlusion, G roughness, B metallic), 2048 px, one set per state.
- Collider (builder): a box over counter and frame, 2.7 x 1.6 x 1.6 m; the awning has none.

## What each state shows
- Rebuilt: fresh paint and awning in the variant's colours (produce ochre/madder, approved B8 look; cloth slate-blue frame, cream/indigo awning; tools dark-stained frame, cream/sage awning), pennants, an iron lantern on the hook, goods (produce: apples, pears, cabbages, squash, loaves, jars; cloth: folded stacks, bolts, two lengths hung from the front bar to near the ground, drapes down the counter front, wool skeins; tools: a tool wall on a pale back board, planes, chisels, horseshoes, a nail tray, copper pots, a coil and shears on the front bar, a barrel of long tools).
- Ruined: paint flaked to grey oak, a bleached torn awning with mould, empty slatted crates, weeds at the legs; each fails its own way: produce = front post snapped, the awning frame down on the counter; cloth = only the bare ribs and rags; tools = the frame leaning hard, its side panel fallen flat.

## Texture inputs
The shared B1 tileables `Art/Textures/B1_town/` (painted_wood, oak_timber, canvas, iron, sandstone), graded from Poly Haven scans, CC0 1.0 (record: `Art/Textures/B1_town/SOURCES.md`), plus procedural masks. No other external assets. References (study only): `Reference/hollowmere/stalls/REFS.md`.
