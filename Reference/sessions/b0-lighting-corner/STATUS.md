State: BLOCKED (Fab EULA: the pane is signed in to Fab but the Fab End User License Agreement must be accepted by the user once; T01, C01, C02, C15 wait on it. Poly Haven K01 fetched, builders in progress)
Branch: claude/b0-lighting-corner-4098b7
Task: build-plan row B0 (docs/superpowers/plans/2026-11-13-area1-dungeon1-build.md)

## Concept palette (sc_ref_palette.py --k 7, 2026-11-14; value = max-channel sRGB; warmth = lights minus darks, same metric the bands use)
| img | p10 | p50 | p90 | warmth |
|---|---|---|---|---|
| H1 square midday | 0.16 | 0.48 | 0.91 | +0.07 |
| H2 square low sun | 0.23 | 0.53 | 0.99 | +0.18 |
| H3 cobble close | 0.18 | 0.44 | 0.76 | +0.14 |
| C1 corridor | 0.07 | 0.24 | 0.74 | +0.52 |
| C2 larder | 0.07 | 0.23 | 0.49 | +0.35 |
| C3 wine vault | 0.06 | 0.40 | 0.78 | +0.46 |
| C4 coal cellar | 0.05 | 0.19 | 0.42 | +0.30 |
| C5 checkpoint | 0.11 | 0.24 | 0.58 | +0.34 |
| C6 church entry | 0.05 | 0.14 | 0.45 | +0.15 |
| C7 oven maze | 0.10 | 0.35 | 0.73 | +0.38 |
| C8 combat room | 0.11 | 0.36 | 0.65 | +0.42 |

DC5 reading (proposal, decided only when the user chooses): no concept reaches p10 0.12, so neither band fits as written. Lit rooms (C3, C7, C8): p50 0.35-0.40, p90 0.65-0.78 (the brighter band) with p10 0.06-0.11. Corridors and dark rooms (C1, C2, C4, C5, C6): p50 0.14-0.24 (PHASE1's darker band). Proposed split: lit rooms floors 1-5 take brighter p50/p90 with p10 0.06-0.11; corridors, floors 6-7 and rolled dark floors take the darker band. Town: H1 warmth +0.07 sits below the +0.12 band; H2/H3 inside it.
Archive: no
