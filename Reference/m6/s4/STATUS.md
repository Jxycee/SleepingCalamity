Status: DONE (M6 S4 + S5 merged). Archive: ready
Date: 2026-11-01

S4 placement mode: merged 26adb46a (branch claude/m6-s4-placement, check PASS bfdb407f: EditMode 1124, PlayMode 76/0 fail/15 skip; risk-reviewed, findings fixed).
- TownSync dynamic network prefab spawned by DiveDirector. NetTown NetworkVariable<TownNet> (Core TownWire snapshot) is the one town view on every peer; host republishes on TownState change.
- Builder lock NetBuilder (ulong, sentinel MaxValue): freed on release, holder disconnect and dive start (ServerForceRelease). Idle holder auto-releases a late grant.
- 10 server RPCs, sender from RpcParams, holder check, Core TownState re-validation. NetGhost replicated; wishes evaluated locally (WishResult.Detail).
- Placement input row, ghost green/red, four turns, part slotting, pick-up, placement camera, wish panel. Lock release saves (HostWorld.SaveRequested).

S5 baker and service: merged (branch claude/m6-s5-baker, check PASS c19e2769: EditMode 1181, PlayMode 87/0 fail/15 skip; risk-reviewed, findings fixed).
- Core BakerService: 3 loaves/player at dive start iff bakery complete, baker housed, all wishes met (Q4 C); else 0 + notice. DiveLoafLedger gives a mid-dive spawn the same count.
- HeroVitals.NetLoaves (host-written), owner eat RPC heals 30 via ServerHeal.
- First-meet beat latched in DiveDirector.BakerTick, saved in the residents section (Extra preserved), replicated via TownSync.NetBakerMet; toast on a live flip only.

Not verified / follow-ups:
- PlacementMode keys, camera and wish panel never driven in a live editor or walk (S6 walks).
- Eating is a debug-only H DevKey; needs an item input action. Loaves can be eaten in town.
- First-meet gift and Wren's list tick deferred (no Wren list in code). Baker NPC is the existing TownView greybox at the lot.
- Real late client join for mid-dive loaves untested (host-side spawn tested). Far-from-build-point placement exit untested.
- Flaky timing tests seen once each, passed on re-run: CombatNetworkTests.Grace_APerfectDodgeInsideTheWindow_StillReachesTheHost, CombatArenaTests.Solo_SixRatsOnOneHero...
