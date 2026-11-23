State: DONE (A/B not won: 5 of 6 cameras lose after two rounds; check running, line below)
Branch: claude/pass-a-wall-kit-c12454 (worktree .claude/worktrees/pl1-loop-proof-23a335), from claude/ground-blend-4d3fa9 (merged 237d9ecf). Pushed, not merged.
needs: coordinator (decision on the A/B result)

| Commit | What |
|---|---|
| 9442d5c1 | Neon sc_town_wall.py v1 deployed, 12 wall/gate FBX re-exported |
| f9916403 | Round 1: scanned breach heap (HollowmereOrchardBreach.cs), Orchard foreground cluster + path-edge feather, smaller dimmer butterflies |
| 996517de | R21 frames |
| 5e31e29a | Round 2: jagged ruined tops + foot rubble in sc_town_wall.py, matte breach rocks, lit ivy, larger butterflies (0.17 m, 24/flock), anchored trough cluster |
| eb2e11b4 | Round 2 breach heap angular only; R22 frames |
| 1144f3dd | Scoped build output (scenes, terrain, matte/lit material variants) |

## Per-camera (HF whole frame before/after R22; blind A/B after vs before, /50, round 1 R21 then round 2 R22)
| Camera | HF before/after | R21 | R22 |
|---|---|---|---|
| OrchardGap | .0485 / .0556 | 32 vs 37 | 33.5 vs 37 |
| Orchard | .1445 / .1440 | 34.5 vs 37 | 35.5 vs 37 |
| LowerLane | .0979 / .0899 | 33.5 vs 35.5 | 34.5 vs 36 |
| JettyBack | .2125 / .2171 | 35.5 vs 38.5 | 32.5 vs 34.5 |
| Meadow | .1482 / .1555 | 33.5 vs 36 | 35.5 vs 34.5 (win) |
| RoadOut | .0815 / .0813 | 36 vs 37 | 35.5 vs 36.5 |
Before = R20 (OrchardGap/Orchard/Meadow) or R19. Reviewers read the card from the main checkout (docs/art/scene-judging.md is untracked, not in worktrees). Single reviewer per pair, noise about 1-2 points.

Why it loses (reviewer themes): the old kit's crenellated tops and rubble foot gave silhouette, density and ground contact that Neon's cleaner wall lacks (round 2 restored jagged tops and foot rubble; helped, not enough); butterflies score as Life cues, near-lens ones were the audit fault but fewer/smaller cards cost Life; scanned breach rocks and ivy read smeared at 1080p; floating dark blob on the LowerLane cobbles (shadow without caster, check HollowmereLanes/arch); neon-green flat ground patch left of OrchardGap (HollowmereTerrain, not touched).
Neon feedback: neon-feedback.md. Needs-downloads: needs-downloads.md (descriptions only, no Fab names).
Not verified: wall wear=0 frame, east gate/shells that reuse the modules.

Archive: ready
