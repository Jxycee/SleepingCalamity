Status: MERGED d6154e73 (branch claude/objective-goldberg-ff8a1f tip 499f85db, check PASS marker == tip). Next: solo balance (coordinator task)

Decisions (user asleep, recommended option taken):
- 3: return dive starts on floor 3 only if checkpoint reached AND no pieces left on floors 1-2 (DiveState.StartFloor); else floor 1. Hub prompt follows NetStartFloor.
- 9/10: every dive picks a fresh seed (SeedOverride still pins). Piece ids are per floor, so pieces stay collectable; chests/healing points no longer replay.
- 4: bank point works once per dive unless unbanked pieces/loot wait (DiveState.CanBankHere); prompt hides when spent; host refuses with a notice.
- 5: ServerTryStair/GoHome/Bank/Descend refuse a downed asker (DiveHaulSync.AskerIsUp); local press also skipped when down.
- 11: NetStairOpen replicated; locked stair prompt reads "The way down is shut (find the gate key)" and sends no press.
- 14: downed countdown pauses while an ally holds revive; hold targets the nearest downed hero.

Grace flake: root cause = -nographics frames ~1 ms, so transport latency spent the 48-frame grace window; test now paces grace-bound waits at 16 ms/frame (PacedFrame/PacedWait) and waits for AllowsDodge instead of a fixed 20 frames. 4 isolated passes + full check PASS. Not a gameplay regression.
Follow-up: CellarContentsSync.cs:172 piece spots < pieces per floor means extra pieces are unreachable (now also blocks floor-3 return dive).
