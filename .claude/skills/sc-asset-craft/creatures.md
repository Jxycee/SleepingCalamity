# Creatures: birds, fish, insects

Part of **sc-asset-craft**: read SKILL.md first; this file adds what is specific to this kind of asset. Learned from the 2026-10-25 asset duo (blind test #10, a grey heron). Numbers measured on one asset are marked as such: treat them as a starting point, not a rule.

- **The family is `sc_millpond_life.py`** (heron, mallard hen, duckling, kingfisher, swan, dragonfly, swarms). Its header lists each model's origin and pose, and `millpond_life.json` holds the attach points.
  - Origins: at the feet on the bed for a wader, at the waterline for a swimmer, at the centre for a swarm. The model faces +z.
  - A new pose of an existing animal is a new stem (`Life_HeronResting` beside the shipped hunting `Life_Heron`).
  - Don't use the prop skeleton (`B.Bldg`, `to_base`): build `B.MG` groups with the family's `loft(g, spine, rads, tint_fn, sides, slot, layer, ao)`, `lathe_t`, `cone`, `eye` and `leg`.
- **References first** (sc-reference): field-guide photos set the proportion (the resting heron's 20 deg body lean, the leg at about 40% of the height, the wing tips at 0.2 H) and the value pattern (flank stripes, neck streaks, the crest stripe running from above the eye). Paintings rarely help for animals; skip them.
- **Multi-slot plumage:** `loft(..., slot_fn=lambda t, a: slot)` sets a slot per face (t along the spine, a around it), so one loft can mix Snow whites and Rock greys.
- **Plumage and fur colours:**
  - White and pale feathers on **Snow (11)** with a grey tint of about 0.55-0.75, so they keep headroom. Pale on Rock needs tint 2.5 and has none.
  - Greys and blacks on Rock, with tints under about 1.8.
  - Bills and legs on Sand.
  - Animal colours never change with the season.
- **Loose verts in the family helpers:** pass `caps=True` to `eye()`, `leg()` and `lathe_t()` in new code. The default still closes on the axis and leaves loose vertices (shipped Heron 8, MallardHen 4), so shipped exports stay byte-identical until a local re-export flips the default (+172 / +108 tris).
- **Read by profile.** Check the 48 px read twice, at the default three-quarter view and side-on (`sc_asset_preview.py --play-yaw 55` turns only the 48 px camera; the scored sheet is unchanged), and design for the better one. A folded-neck resting heron reads best three-quarter (p64 vs p44 side-on); a hunting heron with its neck out reads best in profile. Its wear beat is natural, not man-made: a wet leg, ruffled breast plumes, a fish in the bill. Pick one the water won't hide.
- **Don't trade silhouette for budget.** In the A/B test the skill-built moorhen trimmed to 1 576 tris and its silhouette fell to grade 2 (p25), against 4 for a 2 944-tri bird. It won by eye only because it had the right species markings. A creature can go up to about 2x the median (3 000) if the extra tris go into the head, neck and tail outline.
- **Budget:** audit and score with **`--class creature`**. The per-model median is **1 480 tris**, from the 11 shipped animals; tris don't scale with size (the 0.2 m kingfisher has 1 360). The ceiling is 4 000, with no small-object cap and density warn 9 000. The MallardHen passes there at efficiency 4.
- **Markings and bands stay soft and broken.** A wet waterline band or flank stripe as one hard slab reads as an inflatable ring or a sticker (A/B run 2: "sausage body, inflatable-ring lower half, slab white flank stripes"). Break their edges per face, vary the feather value inside every dark mass (±10-15%, in patches), and give the body a waist (narrower at the neck base and tail) so it isn't a sausage.
- **Smooth lofted bodies:** the paint pass's edge band has nothing to bite on, so the brush variation carries the paint. Put the value pattern in the per-face tints (stripes, streaks), and keep the outline faceting down with 14+ loft sides on the neck and body.

## Fish (user, 2026-10-25: adult audience, ~150 species, Millpond first)
- **Realistic anatomy, never childlike.** No googly or oversized eyes, no cartoon mouths or faces, no toy proportions. Eyes sit in the head at the species' real size; mouths, fin rays, scale rows, lateral lines and countershading follow field-guide photos (sc-reference). The old fish (Perch, Carp and the rest) are restyled the same way.
- **Invented species are welcome** (a phoenix fish, a shooting star fish) when believable: fantastical in colour, pattern and lore, real in anatomy.
- **Each species needs a signature** that survives a silhouette-only sheet: head and mouth profile, body cross-section, dorsal position, anal fin length, tail shape, fin thickness, eye size and iris colour, scale size and gloss.
- The generator lives in `Art/Blender/fish/` (extracted from the .blend files); species are data entries; skins are painted in the Riverlight look (R102), not flat toon.
- **The quality bar comes from other games' fish** (user, 2026-10-25): `docs/superpowers/design/2026-10-25-fish-quality-standard.md` (Red Dead Redemption 2, The Angler, Final Fantasy XIV/XV, Sea of Thieves, Dredge). Their images are copyrighted: view them for the standard, keep only links and written notes, never commit or trace them.
