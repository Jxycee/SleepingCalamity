---
name: sc-foliage
description: Build or refine Sleeping Calamity foliage in the locked stylised-real look - the orchard and town trees, ivy, crack weeds, grass clumps, moss tufts and cellar roots (batch B9) - with the card-crown method, a PBR foliage atlas (albedo+alpha, normal, ORM), crown normals and AO, ribbons for small cover, instanced grass, wind and budgets. Use for any tree, bush, ivy, grass, weed, moss or root work, "the trees look like lollipops/blobs/CG", or a foliage batch.
---

# Foliage (trees, ivy, weeds, grass, moss, roots)

**The look:** saturated clean yellow-green by day (PHASE1 3.1: foliage `#6f8a34`, moss `#616e2e` / `#b6ab28`), chunky readable crown masses with light tops and dark undersides, dense dressing in every corner (handoff 2.1). Leaves are real PBR (albedo with alpha, normal, ORM), clean and saturated rather than photo-dull. Phase 1 needs ivy, crack weeds, grass clumps, one orchard tree and moss tufts for the beauty corner, and roots trickling into the cellar (PHASE1 section 4, B9).

Tools: FishGame's tree generator, atlas painter and season machinery were built for its palette-slot toon look and are not ported; build the B9 generator fresh with the methods below. Tool status: sc-asset-craft 0b.

## How a tree crown is built (card crowns)
Each tree is a trunk-and-limbs mesh plus alpha-cut foliage cards. The look comes from four decisions (FishGame's best-reading trees, style-independent):
1. **Real skeleton.** Limbs from a trunk with a root flare. Lobes sit at limb ends, so the crown has gaps you see branches through. A sphere of cards with no visible limbs reads as a lollipop.
2. **Cloud masses, not a ball.** The crown is 4-7 lobes of unequal size, pushed outward and set in two tiers: separate clumped masses with light tops and dark undersides. Chunky and readable, in keeping with the storybook shapes (PHASE1 3.4).
3. **One shading mass.** Blend card normals between the card's lobe direction and the whole crown's, so the crown shades as one soft form with bumps. Normals from the cards' own planes make a noisy, sparkly, CG crown.
4. **AO by height and radius:** dark core to lit rim, and inner core blobs at the limb ends that fill the see-through gaps with shadow colour, not sky.
- **Fewer, larger cards** with clumped masses inside them read better than many small ones; card overdraw, not triangles, is the real cost.
- **Bark** is a T1-style tileable or a baked trunk (sc-texture-paint); root flares and big limbs are geometry.

## The foliage atlas
- **Cells are clusters, not single leaves.** A cell is one clump with its own light-top / dark-underside structure; single-leaf cards read as noise at play distance.
- **Narrow, lanceolate or hanging leaves** (grass blades, weeds, ivy runners) need their own cell: stretching a broadleaf cluster along a stem reads as palm fronds.
- Make it a full PBR set like every other material: albedo with alpha (alpha cut, no sorting), normal and ORM. Source leaves from CC0 scans graded to PHASE1 3.1 (clean, +15-25% chroma; no baked light in the albedo).

## Shrubs and bushes
A bush built like a tree (lobes only at the stem tips) reads as a lollipop on sticks:
- leafy to the ground: a skirt ring of lobes low down, bare stems only through small gaps;
- lobes along the stems at several heights, not only at the tips;
- dome proportions, slightly flattened; hedgerow shrubs wider than tall.

## Small cover: ivy, crack weeds, moss tufts, grass clumps
- **Mesh ribbons or cards.** FishGame's shore plants were two-sided mesh ribbons (strips with a taper and a slight twist, fanned from a base, normals bent toward the clump, darker base and lighter tips); for 10-20 leaves they beat a card's alpha overdraw. Two-sided ribbons cost double their tris. Choose per plant by how it looks.
- **Ivy** follows the wall: runners as thin tubes or ribbons hugging the facade, leaf clusters on cards along them, denser low and in corners where water sits. Place it as its own object over the kit, not baked into the wall.
- **Crack weeds and moss** go where water and shade go: wall feet, step joints, the fountain basin, cobble joints. Moss tufts and moss decals (B3) together; tidy and placed, never a uniform coat.
- **Roots** in the cellars: tapered tubes from the vault cracks, a T3 family (a few sculpted clusters baked once, instanced), root bark from B2.

## Grass at density
`docs/research/urp-stylised-realism.md` section 7: dense grass is normally a dedicated GPU-instanced renderer (indirect-drawn blades or clumps, blades near and cards far, wind in the vertex shader, player-push from a global vector array for up to 4 players), alpha-to-coverage cutout, no sorting. Terrain details are fine for moderate density. Trees, rocks and props instance through the GPU Resident Drawer. Research the proven option before building a custom one; the named Asset Store packs are unverified.
- Many alpha-tested foliage shadows are a common hidden cost: decide per kind whether it casts.

## Wind
Prefer **shader wind**: vertex offset by height times a world-space sine, card tips more than the trunk. It costs no bones and no animation import and stays in sync for all clients. The project has no wind yet; treat it as a design task.

## Budgets
PHASE1 3.3 has no foliage row yet (B9 is tier T2/T3). Size a scattered kind like a T3 piece (50-400 tris, one shared set per family), and the orchard tree against the hero props around it; state the number in the header and ask the controller if it runs high.

## Review
sc-asset-review, with these foliage checks:
- the silhouette against the sky at thumbnail size: clumped masses, gaps, no perfect sphere;
- a dark underside and light top in the shade-side view;
- limbs visible through the gaps;
- colour inside PHASE1 3.1 (yellow-green, not blue-green or photo olive), measured on the mood render;
- in a group with the facade, cobbles and props it dresses: dense, not noisy.
