# Tobin hair: XPBD vs verlet, SC_HairCards additions, collider rig
(Neon, exchange 5, 2026-11-22. Summarised by the coordinator. Full text in the Gemini thread.)

**Verdict:** keep `TobinHairSpringChains.cs` and `SC_HairCards` and upgrade both in place. No AetherStrand swap is needed (its presets and audit telemetry are optional extras). The recorded route (HairDeck cards + SC_HairCards + spring chains, 2026-11-10) stands.

## Solver upgrades (TobinHairSpringChains.cs)
- Replace the explicit spring `2400*(target-p)` with an XPBD compliance tether (`alpha = compliance/subDt^2`). Today k*subDt^2 = 0.375 at dt 1/20 with 4 substeps, close to overshoot; XPBD is stable at any stiffness. Velocity comes from `(x_new - x_prev)/subDt`.
- Damping `Mathf.Exp(-drag*subDt)` instead of 0.93 per substep, which damps twice as fast at 120 fps.
- Split head inertia: linear 0.35-0.55x, angular 0.75-1.0x, so sprints don't drag the whole style back.
- Write rotations only to bones 1..NB-1 (no positions), so cards can't stretch.
- Auto zones from each chain's root local position on Head and its length: front/bangs (z > +0.04: stiff, high drag, angle tether), crown (short, stiff), sides/rear (stiff at i=1, fluid at the tip, full collision). Works for any chain count.
- Virtual tip: point N sits one segment length past the last bone.

## Shader upgrades (SC_HairCards, about 85% there)
- Radial head-centre normal blend, 30-50 % toward `normalize(positionWS - _HeadCenterWS)` (MaterialPropertyBlock): the biggest visual gain.
- Core-ribbon AO without a new texture: `isCore = step(1.5, uv0.x)`, multiply ambient and the secondary lobe by 0.55-0.70.
- Check that both lobes shift along the bitangent with G + uv1.x: primary exp 80-140, light-tinted, toward the root; secondary exp 20-40, albedo-tinted, toward the tip.
- AlphaToMask only helps with MSAA on. With TAA/SMAA, add an IGN dither in Forward (`clip(alpha - lerp(0.42, ign, _DitherStrength))`) and keep the 0.42 clip in DepthOnly/ShadowCaster.
- Bake GrooveAO into `_StrandTex.R`; still one texture sample.

## Colliders (6 analytic, H = head height chin to crown, 0.22-0.24 m)
| Collider | Shape | Bone | Centre / ends (H) | Radius (H) |
|---|---|---|---|---|
| Cranium | sphere | Head | (0, 0.52, -0.04) | 0.42 |
| Face/jaw | capsule | Head | (0, 0.46, 0.12) to (0, 0.10, 0.18) | 0.30 |
| Neck | capsule | Neck | (0,0,0) to (0, 0.42, 0.02) | 0.24 |
| Upper chest | capsule | Spine2/Chest | (-0.45, 0.15, -0.05) to (0.45, 0.15, -0.05) | 0.42 |
| Shoulder L/R | capsule | clavicle | root to +0.55 H toward the upper arm | 0.26 |
Plus a 0.012 m card margin, position-only projection (no bounce) and about 15 % tangential friction.
