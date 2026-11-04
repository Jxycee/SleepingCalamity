# Floor card, modifiers and the two new objectives (2026-11-04)

Design note for step 4 of the Phase 1 build order (`2026-11-03-hearthdeep-dungeon-design.md`, section 12): "Objective variety (two-valve seal, hold the pump) and the named floor card with two modifiers." It expands section 6 items 2, 5 and 8. Everything here is a *proposal*; every number is `(placeholder)` until a walk tunes it. Water half only (floors 1-5 and the Phase 1 rough floors); the heat-half twins (damper seal, hold the hearth) reuse the same types with different dressing.

## 1. Rules this note must respect
- Nothing missable: every floor goal is deterministic and visible on the map once its room is seen (spec section 7, "side goals are deterministic and never missable").
- No required N-player action and no feature only a full group can trigger. Every objective works solo and scales by effort, not by headcount.
- No hidden random penalties. Whatever a modifier does, the card says before the party commits to the floor, and the reward is printed beside it.
- Rooms cleared and left stay cleared (no respawn after leaving a room).
- Host-authoritative; keys, pieces and people stay single shared items; loot is per player (decision 39).

## 2. The floor card
Shown for a few seconds on entering a generated floor (and re-openable from the map screen, so it is never missable):
- the floor's name (the bundle name, for example "The Smoking Larder"), floor number of the dungeon;
- the objective, one line, with a progress line that updates (`Valves 0/2`, `Pump 0%`);
- the two modifiers, each with its effect and its reward;
- pieces and people still to find on this floor and the ones already found;
- nothing else: no enemy list, no secret hints.

The card is built once from the host seed and sent to clients as a plain data snapshot, so everyone reads the same text. Name comes from a per-dungeon name table keyed by sub-theme and hazard flavour (about 20 names for the water half, placeholder). Authored floors (1, 3, 5, boss) carry hand-written cards.

## 3. Modifiers: two per floor
Spec section 6 says "at most one per floor"; step 12.4 says two. Proposal that reconciles them: each floor has two **slots**, either of which may be empty.
- **Slot A, a hazard** that makes the floor harder, always paired with a stated reward.
- **Slot B, a boon or a quirk** that is free (a comfort or a cosmetic twist), no cost, no reward line needed.

Rules: never two modifiers from the same family; floors 1, 3, 5, the boss floor and the first rough floor after the bank carry none (guaranteed calm, as the spec already asks for some floors); from floor 6 on both slots can fill. Hazards are rolled from the seed, never mid-floor.

### Candidate list (about 8, all placeholder)
| # | Modifier | Slot | Effect | Reward |
|---|---|---|---|---|
| 1 | Stirred | A | +10 stir on entry (placeholder) | double chest count in vault and ordinary chests (placeholder) |
| 2 | Brimming | A | water stays at the high level for the first 90 s (placeholder), then settles to the player-set level | +1 bonus material roll per room cleared |
| 3 | Fat Larder | A | +1 elite (one affix) from the roster | elite drops one extra roll per player |
| 4 | Thin Light | A | darker rooms, shorter pickup glints (never hides a secret's tell) | +10% chest luck (placeholder) |
| 5 | Crowded | A | enemy count +20% (placeholder) before co-op scaling | +15 gold per clear, per player |
| 6 | Hurried | A | collapse timer factor 1.5 down to 1.25 (placeholder) | stir from the floor counted 1.5x for the push-on reward |
| 7 | Still Air | B | no ambient drips or steam bursts; calm floor | none |
| 8 | Warm Hands | B | mending springs on this floor heal 25% more (placeholder) | none |

### How they stack with co-op scaling
Co-op scaling (spec 4.7: count 1.0/1.45/1.85/2.2, HP 1.0/1.15/1.3/1.45) is applied **first**; modifiers act as multipliers on the scaled result and are capped:
- total enemy count never exceeds the section 7 cap of 8 live enemies at once; extra spawns queue as waves of at most two deep;
- modifiers never change damage numbers or bosses, only count, an elite slot, timers, stir and loot rolls;
- reward lines are per player (rolled per player like all loot) and not multiplied by party size, so a solo player is never short-changed relative to the hazard;
- stir effects feed the existing sqrt-of-players stir formula; the +10 is added to the meter, not scaled again.

## 4. Objective: two-valve seal (water half)
Goal: open the stair by operating two valves in different rooms.
1. The stair room is sealed by a flooded sluice (visible from the start, on the map).
2. Valve A sits in a room reached from the entrance side; valve B is on the far side of the floor (farthest dead end that is not the vault), so the two rooms are never adjacent. The generator places them with the same farthest-first pass used for dead-end jobs.
3. Turning a valve takes one interaction (a short hold, 2 s (placeholder)) and persists; the room's water state flips as the gimmick already does. Both valves can be turned in any order.
4. When both are open the sluice drains and the stair unlocks. Progress shows on the card.

Players: any one player can do everything, in sequence. In a party the valves can be turned in parallel but nothing requires it. A downed or disconnected player is not needed.
Fail / solo-friendly fallback: there is no failing; a valve room is a normal combat room with its exits locking as usual (exits only, dead ends never lock). If the party wipes, the dive fails by the normal rule and the next dive regenerates. A valve's room, once cleared, stays cleared; the valve stays turned.
Bags and shared state: valves are world state held by the host, shared. They hand out no item and need no key, so per-player bags do not matter. The sluice key of floor 1 is separate and unaffected.

## 5. Objective: hold the pump (water half)
Goal: defend a pump while it fills the stair channel, then the stair opens.
1. A pump room (a combat arena with the pump in the centre) is placed in the middle third of the floor path.
2. A player starts the pump (one interaction). It runs for 75 s solo (placeholder) while a bounded wave sequence attacks (at most 3 waves, two deep, respecting the 8 live enemy cap).
3. Progress fills while at least one living player is within the pump ring; it pauses (does not reset) if everyone leaves, and enemies do not reset either.
4. At 100% the stair channel fills and the stair opens. The pump keeps running (no damage to it that can fail the floor).
Players: duration does **not** shrink or grow with party size; wave size follows co-op scaling. One player is enough. More players only make the fight easier to share.
Fail / solo-friendly fallback: nothing in this fight can end the floor early. If the pump takes damage it only slows (to 50% rate (placeholder)) while an enemy is attacking it, shown on the progress bar. If the party is overwhelmed and retreats, the pump pauses; coming back resumes. A one-time "start whenever you are ready" prompt guarantees the party chooses the moment. Solo gets the self-revive from spec 4.7 and, as a fallback, a 25% (placeholder) slower enemy spawn rate so the solo hold feels fair.
Bags and shared state: pump progress and stair state are host-held shared state. The fight drops per-player loot like any other room. Keys, pieces and people stay single shared items; neither objective gives or consumes one.

## 6. Core types that would carry this (names only)
- `CellarObjective` (an enum or small value type for key carrier / two-valve seal / hold the pump / swarm) and an objective plan chosen in `CellarFloorGenerator`, with the never-twice-in-a-row rule;
- `FloorObjectiveState` (host state: valve flags, pump progress) and `FloorObjectivePlan` (where the valves and the pump room go);
- `FloorCard` (name, objective text, modifier list, pieces and people status) plus `FloorCardNames` (name table);
- `FloorModifier` and `FloorModifierSet` (the two slots), with `FloorModifierTuning` for the placeholder numbers;
- touched, not new: `CellarFloorValidator` (check both valves reachable, pump room present, objective satisfiable solo), `CellarEncounterPlan`, `CellarFloorRoster`, `StirMeter`, `CellarLootMix`, `CellarSpecialRoom`.
All pure Core (no engine types), so EditMode tests cover generation; presentation and net layers read snapshots.

## 7. Open questions
1. One or two modifiers per floor: spec section 6 says at most one, step 12.4 says two. Is the hazard-plus-quirk split in section 3 the right reconciliation?
2. Should hazard modifiers be skippable (a "decline" choice at the stair) or always taken? Declining would remove surprise but may split parties.
3. Pump duration and wave count at 75 s / 3 waves: tune in the M7 walk against the time target of decision 29.
4. Do valves in the heat half (damper seal) keep the same 2 s hold or use the bellows tool?
5. Should hold the pump be allowed on floor 4-5 where the water is already bubbling, or reserved for 2 and 4 only?
6. Does a modifier's reward count toward stir and the bank's "unbanked valuables" input, or only as ordinary loot?
7. Name table: authored by hand or generated from sub-theme words? How many names before repeats feel stale?
