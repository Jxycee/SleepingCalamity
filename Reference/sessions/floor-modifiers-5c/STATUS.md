State: planning
Branch: claude/floor-modifiers-5c-4b3b0f (from origin/main 14fa7b94)
Archive: not yet

## Plan (2026-11-15)
Sources read: spec 2026-11-04 floor-card note (table rows 2, 3, 7), plan5 STATUS, `docs/design/dungeon1-cellars.md`.
Conflicts: DC7 "no standing water in Phase 1" vs spec Brimming "water stays high 90 s"; no elite-affix system exists (tier-1 only on dungeon 1); no ambient drip code exists yet (P5 overlays are later presentation work).
Design (all placeholder numbers in FloorModifierTuning, deterministic via the existing "cellars.modifier" stream; new enum values appended, hazards/boons arrays grow):
1. Brimming (hazard, family Healing, so never with Warm Hands): "the cellar is damp": healing points recharge 45 s (30 normally). Reward: each hero gets 1 extra loot roll per room cleared (once per room). No water change (DC7); water high-level waits for step 5b.
2. Fat Larder (hazard, family Enemies): twist FatLarder = exactly one extra Tier1 enemy in one combat room (first with a free cell, as Crowded's guaranteed room). No affix system exists, so "elite" is a single extra tier-1 enemy; keeps the dungeon-1 guardrail. Reward: clearing that room gives each hero 1 extra loot roll.
3. Still Air (boon, new family Ambience): no ambient drips/steam. Pure query `FloorModifierSet.AmbientLife`; presentation reads it when P5 overlays land. Card line only, no reward.
Core: FloorModifiers.cs, FloorCard.cs (title/effect/reward text), CellarEncounterPlan (FatLarder extra), CellarFloorRoster.TwistOf + FloorTwist.FatLarder; EditMode tests per modifier + determinism.
Host wiring (small): DiveHaulSync heal recharge already reads HealRechargeSeconds (Brimming through the same helper); CellarContentsSync PayRoomClear grants extra rolls via GiveExtraRoll (same bag path as Stirred). Touches host loot grants: risk-reviewer, stop merge-ready (needs: coordinator).
Gates: unity.ps1 check + soak (Core/Cellars touched). Docs: PHASE1.md stale lines + roadmap header, QUEUE lines 19 and 26 ticked in merge commit.
