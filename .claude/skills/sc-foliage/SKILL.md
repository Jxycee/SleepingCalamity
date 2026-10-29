---
name: sc-foliage
description: Build or refine SleepingCalamity trees, bushes, hedges, reeds and other foliage in the Riverlight painterly style - the card-crown method, the painted foliage atlas, crown normals and AO, seasonal colour, wind, and budgets. Use for any tree/bush/reed/grass/ivy work, "the trees look like lollipops/blobs/CG", seasonal foliage bugs, or a foliage convergence card.
---

# Foliage (trees, bushes, reeds)

Reference implementation: `Art/Blender/millpond/sc_millpond_trees.py` (the six Millpond kinds: PineA, PineB, RoundA oak, RoundB alder, Willow, Birch, plus bare winter trees), its atlas painter (`Canvas`, `paint_cluster`, `paint_twig`, `paint_bough`, `paint_whorl`), and `sc_tree_preview.py`. Bushes reuse it (`sc_millpond_bushes.py`, the hazel shares the broadleaf tints). Read the header (lines 1-60) before any change.

## Before you build: units, outputs, integration (duo blind test #6)
- **Units.** A scatter tree (one of `MillpondScatter.TreeKinds`) is authored at **height scale 1**: unit height, origin at the trunk foot. MillpondBuilder scales each placement to its height (`TreeModelHeight`, measured from _Tri + _Cards). A bush or a placed one-off tree uses real metres, like any prop. State which in the header.
  - Knock-on: the audit's density check skips objects under 1.5 m^2, so a scale-1 tree is never density-checked. Judge it by its tris instead.
- **Outputs.** Loading `sc_millpond_trees.py` as a module leaves its atlas and FBX paths aimed at the repo, and `build_atlas` writes there.  In scratch work pass `--out <dir>`, set `SC_OUT`, or call `T.set_out(dir)` (duo r18). `SC_REPO` and `SC_ROOT` both set the root. The paint pass (sc-asset-craft rule 9) works on a `T.make_object` tree.
- **Integration trap.** `MillpondBuilder.HasRiverlightTrees` returns false if **any** listed kind's `_Tri` or `_Cards` FBX is missing, and then **every** Millpond tree falls back to the harbour's code trees. Never add a kind to `MillpondScatter.TreeKinds` before its FBX land in the same commit. That needs a C# change and a local `check`.
- **Seasons in the preview.** Render every season with `sc_asset_preview.py --season summer|autumn|winter|spring`. It reads the season palettes from `ArtPalette.asset` and exits with an error for an unknown season. Before duo r18 it silently showed summer.
- **One season model per audit.** The scorer sums every object in one audit. Audit the leafy model (_Tri + _Cards) and the `_Bare` winter model separately, or the tris double and efficiency drops to 1. The tree median is about 1 090 tris per model (_Tri + _Cards); the 700 below is the _Tri alone.

## How a painterly tree is built
Each tree is two FBX:
- `_Tri` (trunk, limbs, the dark inner crown core) on `M_Toon_Tri`
- `_Cards` (alpha-cut foliage cards) on `M_Millpond_Foliage`

The look comes from four decisions:
1. **Real skeleton.** Limbs from a trunk (`skeleton`) with a root flare (`rfn`). Lobes sit at limb ends, so the crown has gaps you see branches through, as in the concepts. A sphere of cards with no visible limbs reads as a lollipop.
2. **Cloud masses, not a ball.** The crown is 4-7 **lobes** of unequal size, pushed outward (`spread 1.3`) and set in two tiers (`tier 0.2`). The concepts read as separate clumped masses with light tops and dark undersides.
3. **One shading mass.** Card normals are blended between the card's lobe direction and the whole crown's (`nfn`), so the crown shades as one soft form with bumps. Normals from the cards' own planes make a noisy, sparkly, CG crown.
4. **AO by height and radius** (`aofn`): dark core (0.38) to lit rim (about 1.0). The inner `crown_core` blobs at the limb ends fill the see-through gaps with shadow colour, not sky.

## Recipe 2: whip crowns (pollards, willows, coppice; duo r18)
> **Not shippable yet.** Recipes 2 and 3 draw on the wide atlas (lance, clump and tress cells), which the game doesn't load until a local session does the steps under "The wide atlas" below. A tree built with them and exported before that renders with the wrong leaves in Unity. Prototype freely in scratch; don't commit their FBX until those steps are done.

For a crown of long straight shoots with narrow leaves, use `T.whip_crown(tri, cards, r, heads, whips, crown_c, crown_r, tint, tint_dark, tint_silver, tint_core, per_whip, core, slot, silver_slot, silver_share)` on the **lance cell**. The lance cell needs the wide atlas (below).
- **Whips:** the caller draws them as tubes, so the summer and `_Bare` models share them. Lance cards (`card(..., anchor='base')`, width 0.7 x length) start along each whip's outer 75%.
- **Droop** rises from about 15% to 70% toward the tip, and each card is rolled about its whip. Cards below the crown centre lean dark, with ±20% value jitter per card.
- **Core:** per head, a `crown_core` blob plus about 12 short lance cards fanning out of it. That inner mass is what reads as a willow head rather than a frond fan.
- **Traps:**
  - The lance cell's leaves were tried paired at 15-35 deg (a wheat ear) and at 28-70 deg (a fern); scattered in 3 ranks with 1 in 5 hanging is what reads as willow.
  - Upright cards seen broadside still look a little frondy: keep the topmost whips' cards drooping.
- **Clumps for painterly** (duo r19): `clump_share 0.5-0.6, clump_scale ~1.6` turns the outer-half cards into **clump cards**: the wide atlas's u 1.5-2.0 cell, six hanging tresses, each with a lit top, a dark underside and silver on the lit side. They lift painterly from p15 to p46.
  - Use pure lance cards (`clump_share 0`) only for an airy osier or withy bed.
  - **Why it works:** the shipped top-15% trees (pines, oak, alder) all use big cards (1.5-1.85x the lobe radius) with painted masses inside, few lobes, dark core blobs and low confetti (0.30-0.41). Thin strands score low (the shipped weeping willow: 140 strands, p-low, confetti 0.60). Painterly needs value masses of about card size; leaf-scale detail is averaged away.
  - **The trade:** clumps narrow the value spread (value 3 -> 2), because their mid-tone masses fill p10-p90. Get it back with brighter lit tops, which needs tint headroom: leave room under the clamp on the lit side.
  - **The clump cell is stored flipped** (lit top at v 0), because whip and curtain cards hang from v 0. Before duo r20's fix every clump card showed its lit top at the bottom. That fix alone took the pollard from value 2 to 3 and painterly 3 to 4 (p62).
  - `core=True` adds 12 upright lance sprigs per head, which read as fern on top of a dome. On a domed or weeping crown use `core='blob'` (no sprigs).
  - In full leaf, clumps hide the whips: the head reads as a rounded weeping mass. That's fair for a pollard in leaf; the `_Bare` model carries the pollard read.
- The rebuilt pollard (r19, clump_share 0.6 / scale 1.6): _Tri 1 464 + _Cards 648, painterly 3 (p46), silhouette 5 (p92), value 2 (p23); colour 3, form 4 by eye. Its 1.93x tris are the trunk's (1 464 alone), so keep a bole under about 800 tris for efficiency 4.

## Recipe 3: weeping curtains (weeping willow, weeping birch; duo r20-r21)
`whip_crown` assumes upright whips; for hanging curtains use the kit's recipe 3:
- **`T.curtain_paths(r, crown_c, crown_r, tresses=17, per=(4, 5), hem=0.3, hem_inland=(0.3, 1.3), water, partings, parting_lift)`:** strands fall from the crown shell, gathered into tresses, with a hem height on the water side and partings lifted so the trunk shows through.
- **`T.curtain_crown(cards, r, paths, ..., clump_max=0.6, card_len, hem_len, aspect=(0.45, 0.65))`:**
  - clump cards overlapping by a third, capped at 60%, with lance cards between them and at the hem;
  - per card: aspect jitter, a u flip on half (`card(flip_u=True)`), a roll, and ±15% value.
- **`T.curtain_bare(g, r, paths, ...)`:** the winter model, tapered 3-sided strands on every 2nd path.
- **The arching limbs are species shape:** your generator draws them; the recipe takes any shell. Use `whip_crown(core='blob')` for the crown mass without the fern-like sprigs.
- **Aspect trade:**

  | aspect | form | painterly |
  |---|---|---|
  | 0.45-0.65 (default) | 3: the clumps still read as grape ovals | 4 (p62) |
  | 0.3-0.42 | 4: streaming tresses | 3 (p46) |

  | `tress_share 0.3` (duo r22) | 4: streaming tresses mixed with lobes; value 4 (p62) | 3 (p31) |

  The tress cell (u 1.75-2.0 of the wide atlas) is three hanging streaks of narrow leaves. The scorer and the eye disagree on curtains: the brush-scale band-pass rewards blob-sized patches, and a streak's masses are long, thin and mostly edge. **For curtain trees, treat the painterly grade as advisory** (as silhouette already is for spiky ones) and judge form by eye. Use ovals for a tree seen mostly from far off, and tress 0.3 for one seen up close.
- **Result (r21 willow, aspect 0.3-0.42):** silhouette p100, value p54, painterly p46, form 4 by eye. Summer 2 172 tris, winter Bare 1 972 (the blind run's was 3 054). With tints 10% under the clamp, the Col-clip WARN went away.

## Shore plants and ground cover (iris, reeds, marigold; duo blind test #11)
- **How the shipped ones are made:** two-sided **mesh ribbons**, not cards.
  - `Millpond_Prop_FlagIris` and `MarshMarigold` live in `sc_millpond_margins.py`; the reeds are built in C# (`MillpondReeds.cs`, `Millpond_Reeds.asset`, no FBX).
  - `_Cards` load only for TreeKinds and BushKinds, so a cover plant is mesh.
  - Name it `Millpond_Cover_<Name>` for instanced scatter, or `Prop` for one placed by json.
- **The ribbon recipe:**
  - strips with a taper and a slight twist, fanned from a base, bent normals toward the clump (as `nfn` for crowns);
  - a darker base and lighter tips;
  - no paint-pass edge band (`presets_by_layer={0: dict(PP.WOOD, edge=0)}`): flat ribbons have no edges to wear.
- **Two-sided costs double:** `M_*_Base` culls back faces, so each ribbon is 2x its tris. A card instead needs an atlas cell and adds alpha overdraw. At the instanced ceiling of **300 tris per season model** (sc-asset-budget), ribbons win for 10-20 leaves.
- **Leaf slot: Reed (7), not FoliageDark.** FoliageDark's autumn colour is red (#c23d26), right for a beech, wrong for a sedge. Reed goes blue-green in summer and olive-tan in autumn.
- **Audit and score with `--class cover`** (FAIL above 300 tris per season model, no smear WARN). The cover median is small (137), so a 1 m clump grades efficiency 2-3. Use `sc_asset_preview.py --seasons 'spring=a.fbx;summer=a.fbx+a_Flowers.fbx;autumn=a.fbx+a_Pods.fbx;winter=a_Winter.fbx'` to see all four seasons on one sheet.
- **Seasonal parts on instanced cover have no engine path yet.**
  - Flowers, pods and the winter collapse belong in separate season models: `_Flowers` (summer), `_Pods` (autumn), `_Winter`.
  - But `AddGroundCover` instancing has no season mask, and `SeasonalVisibility` works only on placed GameObjects. Until a C# mask per cover kind lands (local; the proposal is `docs/superpowers/notes/2026-10-25-cover-season-mask.md`: a `Seasons` byte per `GroundCover.KindMesh`, 0 meaning every season), seasonal parts on instanced cover would show all year.
  - Don't put flowers on a fixed slot: on Rock they bloom in winter, which is the shipped FlagIris's bug.
  - The engine has four seasons, so "early summer" means all summer.
- **Winter:** a collapsed tangle of brown leaves bent over at the base, not upright stubs. The blind iris's upright winter model read as a starburst, and the scorer rewarded the spikes, so judge it by eye.

## Bushes and hedgerow shrubs (not small trees)
A bush built like a tree (lobes only at the stem tips) reads as a lollipop on bare sticks (duo E8, round 6). For shrubs:
- **Leafy to the ground:** a skirt ring of lobes at y 0.1-0.3 m, so no bare stems show at the base except through small gaps.
- **Lobes along the stems**, at three heights on every stem, not only at the tips.
- **Dome proportions:** rx about equal to ry, a slightly flattened top. Hedgerow shrubs are wider than tall.

Species character at bush scale. The atlas has one broadleaf leaf-cluster cell, so tint and form are the main levers:
- density (how many cards, and how small the gaps)
- twig colour and spikiness in the `_Tri`: blackthorn's dark, spiny, straight twigs; hawthorn's lighter, zigzag ones; bramble's arching canes on a low mound
- crown outline: blackthorn dense and flat-topped, hawthorn lumpy and rounded, hazel open and multi-stemmed
- tint: blackthorn much darker and bluer than hazel; hawthorn mid

A truly distinct leaf needs another atlas cell. That's an atlas change (four cells, 1024 cap) for its own card.

**Fruit and berries** (sloes, haws, blackberries) are seasonal, but the `_Tri` sits on a fixed slot, so fruit there would show all year. Until the builder can hide a `_Fruit` object out of season, leave fruit off. Note the wish on the board.

## The painted atlas (`T_MillpondFoliage.png`, 1024, RGBA)
- Four cells: spruce bough, spruce whorl, leaf cluster, leafy twig. Painted at 2x and box-filtered down. Grey centred on 0.5 (colour from the palette slot), alpha cut at 0.5.
- **Paint clusters, not leaves.** A cell is one clump of 7 lobes, each with its own light direction and sun-caught leaves on top. Single-leaf cards at play distance read as noise.
- Leaves are lit across their width from one light direction (`leaf(..., light=(-0.6, 0.8))`) with a darker midrib. That hand-painted value structure is what makes the cards read as brushwork.
- **Narrow, lanceolate or hanging leaves** (willow, reed, iris) use the **lance cell**. Stretching the broadleaf cluster or the 40-deg twig cell along a whip reads as palm fronds or agave (duo r17). The 2 x 2 atlas has no free cell: every card maps its whole cell, so an empty corner can't be used.
- **The wide atlas** (opt-in, duo r18) is 2048 x 1024. In scratch work, one call `T.use_wide(out)` sets it up, builds `<out>/T_MillpondFoliage.png` and returns its path. `sc_asset_preview.py` then picks it up from `SC_OUT` (or `--atlas`) and prints which atlas it used.
  - Its left half is the shipped atlas, texel for texel. The lance cell sits at authored u 1.0-1.5, and u 1.5-2.0 is free.
  - Shipped card UVs stay 0..1; the material takes `_BaseMap_ST (0.5, 1, 0, 0)`.
  - Preview with `sc_asset_preview.py --atlas <wide.png>`.
  - **Local before shipping:**
    1. Set the ST in `RenderMaterialLibrary`.
    2. Let `EnsureCardAtlasImport` allow 2048 wide. At its 1024 cap the shipped cells lose half their texels.
    3. Rebuild with `--wide` and byte-check the left half against the old PNG.
- Atlas size stays 1024: `RenderMaterialLibrary.EnsureCardAtlasImport` caps it, and a 512 cell already out-resolves the screen at play distances.

## Colour and seasons
- **Slots by season** (from ArtPalette, duo r18 `season_table.txt`), for a silver-sage summer and a yellow autumn:
  - Only **Reed** reaches silver-sage, at tint (0.8, 0.82, 2.1), but its autumn is a dull tan.
  - **FoliageAlt** gives a clear yellow autumn, but its summer caps at a cool green.
  - Foliage, FoliageDark and Grass turn orange or red.
  - The best compromise is the leaf mass on FoliageAlt (1.25, 1.0, 2.2), plus about 40% of the upper cards as silver flashes on Reed (0.8, 0.82, 1.9) (`silver_slot`, `silver_share`).
  - **Costs:** the Reed silver turns **blue-grey in autumn**, so keep `silver_share` near 0.25 on trees seen mostly in autumn. These tints sit at the clamp, which leaves no room to brighten the lit tops. For value, take the tint about 10% under the clamp and get the brightness from the lit-top AO.
  - A true silver-to-yellow slot would be a palette edit: a design call for the user.
- **What you're tuning:** the drawn leaf colour = lin(the slot's colour **in that season**) x ao x tint x atlas grey. One tint serves every season, so pick the slot whose seasonal colours land near the species in summer *and* in autumn, then tint for summer. A silver-green willow that should yellow in autumn is a slot choice; the tint can't do both (duo r18 tabulates the slots).
- **Two colour checks can disagree.** A target match against README hexes (94.6) and a concept crop match (43) gave opposite verdicts on the r17 willow. Trust the crop when the species is in a painting. Otherwise, check by eye that the hue reads: silver-green, olive or dark blue-green.
- Foliage sits on **seasonal slots**: broadleaves on Reed (7) or Foliage (1/2), conifers on PineLight/PineDark (12/13, which stay green all year), blossom on 14. Never a fixed colour.
- `TINT` moves the slot to the concept's measured colour (README palette). It is tuned **from Unity captures**, not from Blender (the lit crown read 2x too bright and yellow until measured; see `CROWN_SCALE`). Measure the crowns in a concept-camera capture with `sc_concept_match.py` before and after any tint change.
- Autumn comes from the slot's season colour. Winter uses the bare models (`build_bare`: trunk, limbs and twigs, no cards).

## Wind (when animating)
The HF tree-sway study (agent-built, validated) did it with a small armature (root, trunk, branches, foliage bone collections), F-curve cycles modifiers for a seamless 8 s loop (frame 1 = rest, the last frame duplicates it), base vertices pinned, and branch tips kept inside the crown. In our pipeline prefer **shader wind** (vertex offset by height x a world-space sine, the card tips more than the trunk). It costs no bones and no animation import and stays in sync for all clients. Treat it as a design task: spec plus a board card. The project has no tree wind yet.

## Budgets (sc-asset-budget)
- `_Tri` up to 9k tris (median 700), `_Cards` up to 4k tris. 40-60 clusters plus 10 twig cards per broadleaf. Card overdraw is the real cost, not the triangles: prefer fewer, larger, well-painted cards.
- **Hero trees.** The tree median (about 1 090 tris) comes from scale-1 scatter trees. A placed hero tree (a 9 m weeping willow seen from the stands) is allowed up to 2.5x the median per season model. Say so in the header and on the board card, so efficiency 3 there is expected. The blind willow came to 2 176 + Bare 3 054.
- Backdrop forests (`sc_millpond_farwoods.py`) use far cheaper massing and are judged only at distance.

## Review
- Preview: `sc_asset_preview.py` on `<tree>_Tri.fbx+<tree>_Cards.fbx`. `*_Cards` objects are drawn with the foliage atlas (x2 gain, alpha cut at 0.5), as `M_Millpond_Foliage` draws them. The generator's own `--preview` (`sc_tree_preview.py`) also works.
- The audit's smear WARN is expected on cards and thin twig tubes (cards and tubes are smooth-shaded on purpose); the audit no longer raises it for class `tree`.
- Checks:
  - the silhouette against the sky at 48 px (clumped masses, gaps, no perfect sphere)
  - a dark underside and light top in the shade-side view
  - limbs visible through the gaps
  - the species reads (spruce: drooping tiers; oak: wide clumpy; alder: narrow oval; willow: curtains; birch: white trunk, light crown)
