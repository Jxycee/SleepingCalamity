State: merged
Archive: ready
Branch: claude/dive-bug-sweep (from origin/main)
Done: 18b stale (63e237d9, test Pieces_PieceZeroAlreadyHome_...); RoomValidatorV2 rules 6/7/9 cover all gimmick states (no gap); party-size check = PartyScaleSmokeTests.Party_EntersDive_ScalesEnemies_ChangesFloor_ReturnsToHub [2,3,4] (13/13 with SC_MP=1).
In flight: DiveBot_Tough_Seed12345 run via wrapper (log %TEMP%\dbs.log, out %TEMP%\dbs); result not yet recorded.
Added, uncompiled/unrun: CellarContentsNetworkTests.CarrierDiesOffTheWalkableCells_KeySettlesOnAWalkableCell_AndCanBePickedUp (BUGS 18a host path).
Next: run gate, run the new test, record DiveBot result, review (risk-reviewer not needed unless net code changed: test only), check on tip, merge, tick QUEUE lines 30,35,37,41,42 + BUGS 18, CURRENT Now line.
Merge order: claude/dive-followups merges first (both touch CellarContentsNetworkTests.cs). Before merge: git fetch origin; if merged, merge origin/main here, hand-resolve that file keeping both test sets, run check on the new tip.
