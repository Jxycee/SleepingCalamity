# Encounter templates for the 12 Cellars combat rooms (spec step 5a)

**Goal:** For each of the 12 pool combat rooms (`CellarRoomsV2.Pool`, Kind Combat), author which enemy group stands where (template, spawn cells, wave count), scale it for 1-4 players inside the room's FightSpace budget, derive it deterministically from the dive seed, and validate it. Cellars = tier 1 enemies only (Slime, RatKing, Broom; no DoughWarden).

**Spec:** `2026-11-03-hearthdeep-dungeon-design.md` section 7 (one type = breather, two = normal, three = complex; at most 8 live enemies at 4 players; waves at most 2 deep) and 12 step 5; templates from `docs/research/dungeon-deep-dive/08-encounters-arenas-bosses-audio.md` 7.1; `docs/ROOM_METRICS.md` (small-party rooms must be respected).

## Findings that shape the design
- `CellarEncounterPlan` already places authored + extra spawns (cells only, kinds rolled later by `CellarEnemyMix`). This work adds a **template layer** that decides kind, role and wave per spawn; it does not change the existing plan's cells or goldens (own Rng stream `cellars.template`).
- Party scaling today is `CoopScaling.ExtraEnemiesPerRoom` (+1 per extra player). Templates define a solo base group and a per-player add list; total live enemies = `min(base + adds, 8)` and must satisfy `FightSpace.Fits(room, players, live)` (else drop adds from the last one). `CellarRoomsV2.SmallParty` rooms (coal_stoke_chicane: 2 heroes) cap the party size the template is authored for; above it the template keeps its solo-to-max group and the validator only checks the held size.
- Roster per floor comes from `CellarFloorRoster.RosterMask`; a template whose roles need a kind off the roster remaps the role to a roster kind (same rule as spawners), never to DoughWarden.
- Rules to enforce (7.1): max 2 distinct kinds on floors 1-2, 3 from floor 3; <= 8 live; waves <= 2 deep; one Ambush template per floor (and not with a hazard); the carrier/warden spawn cells keep their authored cell.

## Design
New pure-C# files in `Core/Rooms/` (no engine refs):
- `EncounterTemplate` (id, roles with kind + count + wave + anchor (`Door`/`Open`/`Pillar`/`Dais`/`Far`), `PerPlayerAdds`, `WantsAffordance`), and `EncounterTemplates` (the 10 research templates minus Elite arena/Hunter: Pack, Gel swarm, Shield wall, Rush and bombard, Guarded caster, Mixed tier, Ambush, Reinforced).
- `RoomEncounterTable`: static authored map room id -> allowed template ids (2-3 each, chosen by room shape and affordances).
- `EncounterBuilder.Build(RoomDataV2 room, string templateOrNull, uint seed, int floor, int roomIndex, int players, int rosterMask) -> RoomEncounter` (spawn list: cell, EnemyKind, SpawnTier, wave 0/1, role). Picks template via stream `Rng.Derive(seed, "cellars.template", floor<<32|roomIndex)`; anchors resolve to free standable cells (authored spawn cells first, then the room's free cells nearest the anchor class); deterministic, no floats in decisions.
- `EncounterValidator`: every spawn on a standable floor cell, no two on one cell, <= 8 live per wave and per room at the party size, FightSpace fit, kind <= tier 1, kind on roster, kind count per floor rule, waves <= 2, template's `WantsAffordance` present in `room.Affordances`, `EnemyHabitat.Fits` for each kind.

Integration note: this task ships the data + builder + validator and a thin adapter `EncounterBuilder.ToExtraCells(...)` for the existing spawner; wiring the host spawner to use templates is a follow-up request (it touches NetworkBehaviours and would need risk-reviewer) and is out of scope here.


## Tasks (implementer runs; 2-3 tasks each)
Run A: **Task 1** types + the 8 templates (`EncounterTemplate.cs`, `EncounterTemplates.cs`) with `EncounterTemplatesTests` (ids unique, roles within tier 1, waves <= 2, solo base counts match research table, per-player adds respect the 8 cap). **Task 2** `RoomEncounterTable` for the 12 rooms + tests (every combat room has 2-3 templates; every template reaches >= 1 room; Ambush only where the room is empty-looking, i.e. >= 3 free cells).
Run B: **Task 3** `EncounterBuilder` + tests (deterministic per seed, same output twice, players 1-4 monotone non-decreasing live count, <= 8, SmallParty respected, roster remap, carrier/warden cells untouched). **Task 4** `EncounterValidator` + tests incl. one negative case per rule.
Run C: **Task 5** sweep test over all 12 rooms x players 1-4 x floors 1-7 x 200 seeds (fast tier 50 seeds, `[Category("Soak")]` 1000+) asserting validator-clean; doc `docs/ROOM_METRICS.md` addendum listing the table. Then: merge origin/main, `unity.sh gate` while working, `unity.sh soak` (Core/Cellars adjacency) and full `check` on final tip, Sonnet `reviewer`, merge.

## Coordinator decisions (2026-11-06, supersede the text above)
- Scope: Q1 yes, the 12 pool combat rooms (bakery set pieces keep their authored guard). Q2: NO host spawner wiring. Q3: Elite arena and Hunter stay out.
- **Guardrail (a):** floors 1-2 use one-wave templates only (no Reinforced) and at most 2 enemy kinds. The solo live count per room must not exceed today's `CellarEncounterPlan` count for that room (its authored spawn count) by more than 1. Per-player adds must not exceed `CoopScaling.ExtraEnemiesPerRoom` (+1 per extra player). EditMode tests pin both limits (Tasks 1, 3 and 5).
- **Ambush (b):** a template-level Ambush is not rolled once per floor on its own. `EncounterBuilder` uses the Ambush template only for the room the Ambush floor modifier already picks (`CellarEncounterPlan.AmbushRoom`, passed in as `isAmbushRoom`); every other room never gets it. No Ambush template on a floor with a hazard (so the validator rejects it when the floor has any hazard modifier other than the Ambush one that selected the room is moot: the Ambush modifier is itself slot A, so a second hazard cannot exist). Later wiring request: connect `AmbushRoom` to the builder's `isAmbushRoom` flag.
- **Seed key (c):** `((ulong)floor << 32) | (uint)roomIndex` (cast before shifting).

## Open questions for the coordinator
1. Scope: "12 v2 rooms" read as the 12 pool combat rooms (rooms-v2 plan). Bakery set-piece rooms keep their authored guard. OK?
2. Is wiring the host spawner to templates in scope (needs risk-reviewer, Netcode) or a separate request? Plan assumes separate.
3. Elite arena and Hunter templates are excluded (Dough Warden is tier 2+, hunters already handled by `CellarEncounterPlan`). OK?
