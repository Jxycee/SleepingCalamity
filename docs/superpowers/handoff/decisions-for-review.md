# Decisions for review

Claude made these calls while working on its own (user, 2026-10-29: "Any design decisions please note and list at the end. We will go through them."). Each one is written so it can be changed easily.

- **Placeholder** means only a simple swap is needed, such as a name or a number.
- **Call** means a design choice the user should confirm.

Change a line in place, or mark it OK.

## Story (from the locked STORY.md, items Claude proposed)
| # | Kind | Decision | Where |
|---|---|---|---|
| S1 | Placeholder | The Bargain was made about **60 years** before the game, and the Long Night happened **10 years** before. | STORY 2 |
| S2 | Call | A few survivors outside the towns (a hermit, a trader, a shepherd) can be met on the road. Claude kept these when the story was locked. | STORY 2 |
| S3 | Call | The four heroes' unclaimed status is explained on Undercroft floor 8, and it is why the Gleaners hunt them. | STORY 2, 8.4 |
| S4 | Call | Each heirloom's legendary form is earned in the chapter where its original owner comes home. | STORY 3 |
| S5 | Placeholder | The Tithe-Keeper is named **Elder Corwin Vane**, and his servants are called **the Gleaners**. | STORY 4 |
| S6 | Call | The final fight has four stages (waking, the fight in the open with the towns helping, inside the maw, the end). The more of the Vale is rebuilt, the more help the player gets in stage 2. | STORY 7 |
| S7 | Placeholder | The Undercroft has 12 floors in 3 bands plus a boss floor. The Phase 1 plan may change these counts. | STORY 8.4 |
| S8 | Placeholder | Supporting names: the Tithe Feast, the Long Night, the Gleaners, the Bell-Warden, and the dungeon names. | STORY 6, 8 |

## Setup (session 1)
| # | Kind | Decision |
|---|---|---|
| T1 | Call | Packages left out for now: unity-mcp, the Tripo bridge, VFX Graph, terrain-tools and `com.unity.pipeline`. Each can be added back when the plan needs it. |
| T2 | Call | Kept the Data assembly and skipped FishGame's Editor.TestMenu. |
| T3 | Call | Turned the REA MCP server back on in `.claude/settings.json`; FishGame had it denied. |
| T4 | Call | Research files now live in fixed folders outside the repo instead of the per-session scratchpad: `C:\Dev\_research\` and `C:\Dev\_private\ps2\`. |
| T5 | Call | Did not port the `fun-formula` memory (user: skip). Ported `reverse-engineering-tools` as a reference. |

## Autonomous run (night of 2026-10-29)
Claude adds lines here as it works.
| A1 | Call | Proposed URP starting stack:<br>• Forward+ with the GPU Resident Drawer.<br>• 4 shadow cascades, about 100 m.<br>• Adaptive Probe Volumes for the towns. The dungeons are generated at runtime, so they can't use baked GI.<br>• SSAO.<br>• The Neutral tonemapper plus a grade (compare ACES before locking).<br>• The free MIT CristianQiu volumetric-light package for god rays.<br>To be tested on the beauty corner. Source: `docs/research/urp-stylised-realism.md`. |
| A2 | Flag | Early press describes the OoT 2026 remake's mood as darker and more grounded than our brief, which reads it as saturated daylight. The brief stays as the user's picture; worth checking against the official screens once it launches (5 Nov 2026). |
