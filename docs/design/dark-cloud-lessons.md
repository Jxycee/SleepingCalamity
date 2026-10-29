# Dark Cloud lessons: what to keep, what to fix

User, 2026-10-29: "We must ensure we nail these items and do not make the same mistake the original game did." Must-keep feelings (user): **rebuilding home**, **the weapon bond**, **dungeon dive tension**.

Source: the user's own summary of player complaints about Dark Cloud 1 and how Dark Cloud 2 / Dark Chronicle answered them. The decomp research (`docs/research/`) adds the numbers. Each row says what we do about it; "Story" means `docs/STORY.md` carries it, "Plan" means `docs/PHASE1.md` must answer it.

## Weapons and progression
| Complaint | DC2 | Our answer |
|---|---|---|
| Weapons break constantly; babysitting durability and repair powder | softened, still there | Durability never destroys a weapon. Wear lowers output; a cheap field fix (or a co-op ally) restores it. Plan: decide whether wear exists at all after the combat greybox. |
| Upgrade/build-up paths opaque and grindy, no in-game explanation | better, still murky | A visible weapon tree: every build-up target, its requirements and how far along you are, shown on the weapon. |
| Heavy fodder farming | still there | Growth comes from use (kills, combos, bosses), not fodder. Materials come from targeted sources the journal names. |
| Weapons ruined by bad choices; stats hard to read | still there | No irreversible bad choice: stat previews before every attach/build-up, and a way to undo or re-route. |

## Dungeons
| Complaint | DC2 | Our answer |
|---|---|---|
| Random floors repetitive and samey | **persistent** | Hand-authored room pools per dungeon, biome sub-themes per floor band, set-piece floors and floor events; generation assembles, design authors. This is the biggest content cost (Plan). |
| Backtracking and re-clearing is a slog | partly | Floors stay cleared; shortcuts open back to town; return dives start at the deepest reached checkpoint. |
| Long dungeons, few checkpoints; one death costs a lot | improved | Checkpoint every few floors; death costs the dive's unbanked haul, not hours. Tension comes from carrying loot, not from losing time. |
| Hunger and thirst interrupt the flow | removed | None. |

## Town building
| Complaint | DC2 | Our answer |
|---|---|---|
| Placement requirements cryptic; players needed a guide | improved, still cryptic | Every resident's wishes are shown in-game in plain words, with a live preview score while placing. |
| Missable things; builds that block progress | still there | Nothing permanently missable; a tracker shows what's left; any placement can be moved. |
| Fiddly placement camera and controls | improved | Modern placement mode: snapping, free camera, undo. |

## Combat and controls
| Complaint | DC2 | Our answer |
|---|---|---|
| Shallow, button-mashy; little enemy variety per dungeon | **persistent** | Dodge, combo routes, charge and finisher; enemies telegraph and need different answers; each dungeon gets a varied roster plus floor-band variants. Plan: prove combat feel in greybox before content. |
| Camera in tight spaces; awkward lock-on | improved | Modern third-person camera with collision handling and a soft/hard lock-on (solved problem; use common practice). |
| Character switching adds friction; weaker ranged play | changed | Each of the four heroes plays fully on their own; solo swaps at town, co-op has one per player. |

## Story and presentation
| Complaint | DC2 | Our answer |
|---|---|---|
| Thin, cliché story; flat characters | better delivery, standard beats | Personal stakes (the heroes' own families were taken), character arcs per chapter, a dark undertow that pays off (Story). |
| Late-game difficulty spikes, padded final dungeons | **persistent** | Every dungeon has its own identity and a set length; no filler floors; difficulty curve planned per chapter (Plan). |

## Systems and UX
| Complaint | DC2 | Our answer |
|---|---|---|
| Tight inventory, clunky menus | still there | Generous storage, sorting, search, stat compare. |
| Key mechanics poorly signposted | improved | Every system introduced in play and listed in the journal. |
| Side content not rewarding | uneven | Each side activity feeds the main loop (materials, town wishes, weapon growth), never just a score. |
