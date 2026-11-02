Status: implementing done, gate/PlayMode check running (dive-flow group, playtest items 3,4,5,9,10,11,14)

Decisions (user asleep, recommended option taken):
- 3: return dive starts on floor 3 only if checkpoint reached AND no pieces left on floors 1-2 (DiveState.StartFloor); else floor 1. Hub prompt follows NetStartFloor.
- 9/10: every dive picks a fresh seed (SeedOverride still pins). Piece ids are per floor, so pieces stay collectable; chests/healing points no longer replay.
- 4: bank point works once per dive unless unbanked pieces/loot wait (DiveState.CanBankHere); prompt hides when spent; host refuses with a notice.
- 5: ServerTryStair/GoHome/Bank/Descend refuse a downed asker (DiveHaulSync.AskerIsUp); local press also skipped when down.
- 11: NetStairOpen replicated; locked stair prompt reads "The way down is shut (find the gate key)" and sends no press.
- 14: downed countdown pauses while an ally holds revive; hold targets the nearest downed hero.
