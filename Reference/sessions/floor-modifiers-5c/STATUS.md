State: merge-ready (host loot grant = networked, needs: coordinator); check PASS + soak PASS on f51a70e5
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

## Progress (2026-11-15, usage limit hit)
- Tip d9b4c3c9: unity.ps1 check PASS (build PASS, EditMode 1794/1794, PlayMode 287 passed 0 failed 36 skipped), SC MARKER == d9b4c3c9.
- Risk review (no blockers). TODO should-fix: (1) PayExtraRolls / PayEveryone in CellarContentsSync skip clients whose NetworkManager.ConnectedClients[id].PlayerObject == null and use a non-creating bag lookup (PartyBags.For creates bags); (2) add a PlayMode payout test (copy room-clear setup of FloorModifierNetworkTests ~line 131: each client gains exactly `rolls`, re-clear pays nothing, only fat room pays on Fat Larder). Nit: comment says 50000+room, real index 51000+room+1000(k-1).
- Then: rerun check, `unity.ps1 soak` (Core/Cellars touched), tick QUEUE lines 19 and 26 in merge commit, Archive: ready, board needs: coordinator (host loot grant = networked).

## Final (2026-11-15)
Tip f51a70e5 (review fixes applied: payouts skip clients without a hero object, non-creating bag lookup, 3 PlayMode payout tests).
Check: unity.ps1 check PASS (build PASS, EditMode 1794/1794, PlayMode 290 passed 0 failed 36 skipped), SC MARKER == f51a70e5.
Soak: unity.ps1 soak PASS 46/46 on f51a70e5.
origin/main moved (18 commits since my base 14fa7b94, incl. QUEUE.md): merger merges origin/main, checks the new tip, and ticks QUEUE lines 19 (stale docs, done in d50b549a) and 26 (5c) in the merge commit. I did not tick them here to avoid a QUEUE conflict.
Risk review: no blockers; both should-fix items done. Nits left: bonus rolls never in boots band; each Brimming roll feeds stir; full bag gets a notice each room clear.
Archive: ready
