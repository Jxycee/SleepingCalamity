# Creatures and prop-creatures: Cellars enemies, animals

Part of **sc-asset-craft**: read SKILL.md first; this file adds what is specific to creatures. The lessons came from FishGame's 2026-10-25 creature tests (a heron, a moorhen); treat any number as a starting point.

**Which pipeline** (PHASE1 section 5): prop-like enemies (the sweeping broom, the jam-jar slime) are fine to build as generators in Blender. Organic creatures (the rat-king) and humanoids go through **sc-ai-model-pipeline** or a base mesh, not primitives. In Phase 1 the enemies and the baker stay greybox; this file is for when their art starts.

- **References first** (sc-reference): photos set the proportion and the value pattern (a rat's body-to-tail length, where its ears sit, the darker back over a paler belly). Paintings rarely help for animals.
- **Proportion:** storybook, not chibi, the same as the heroes (PHASE1 3.6). Real material on a slightly stylised form: a rat-king is a believable rat pushed in shape (bigger head and paws, a crown of junk), not a toy. A prop-creature keeps its object's real construction (the broom's bound bristles and turned handle, the jar's glass and lid) and gets its character from pose and silhouette.
- **Read by profile.** Check the thumbnail read at three-quarter and side-on and design for the better one. Each enemy must read from silhouette alone at combat distance, like the heroes (PHASE1 3.6).
- **Don't trade silhouette for budget.** A creature trimmed to save tris lost its silhouette grade in the FishGame A/B. Spend the budget (PHASE1 3.3: enemy 25k tris, one 2K set) on the head, limbs and tail outline.
- **Markings and bands stay soft and broken.** A stripe or wet band as one hard slab reads as a sticker or an inflatable ring. Break its edge, vary value inside every dark mass in patches, and give the body a waist so it isn't a sausage.
- **Smooth lofted bodies** need enough loft sides on neck and body (14+) that the outline doesn't facet.
- **Moving parts are separate objects** with their pivot on the axis (a lid, a jaw, a tail), named in the header, so rigging or code can drive them.
