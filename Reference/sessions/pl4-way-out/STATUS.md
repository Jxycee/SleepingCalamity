Status: IN PROGRESS
Started: 2026-11-13 (cloud launcher)
Step: 4.1, 4.2, 4.3 and the 4.4 walk script done; PL-4 4.4 build: PC needed (the cloud editor has only the Linux player module, no Windows target). Risk and branch review, then check on the tip, still to run.

Done:
- 4.1 pause menu (18cdccde).
- 4.2 floor pick (93939422): the pick defaults to the bank floor while the checkpoint is held; the Lock button (Tab, right-stick click) cycles floors 1..bank; the prompt names the bound button instead of G; the host still validates the pick against the checkpoint. Tests: DiveEntryInputTests ReturnDive_DefaultsToTheBankFloor (replaces the PL-1 pin), FloorPick_Button_CyclesAllowedFloors, Prompt_NamesTheRealButton, FloorPick_NoCheckpoint_OnlyFloor1, FloorPick_AboveBank_Refused; new FloorPickNetworkTests FloorPick_ClientPick_HostValidates, FloorPick_ClientPick_NoCheckpoint_HostRefuses. Each failed first on the old code (4 of the 7 changed or new tests failed; the others pin host rules that already held). PlayMode DiveEntryInputTests 8/8, FloorPickNetworkTests 2/2.
- 4.3: PL-1's only listed break was 4.2; nothing else blocks the walk, so none taken.
- 4.4 walk script: USER_TODO items 3b and 3c replaced with plan section 5 (3a kept) plus the one-PC Relay rehearsal.

Gates: gate1 on 18cdccde PASS (EditMode 1718, 1715 passed, 0 failed; the 2 earlier failures did not reproduce); gate2 on 93939422 see git log / marker.
Note: the real input adapter's binding display strings crash a headless Linux editor; multi-peer tests must install a FakeInputService (done in FloorPickNetworkTests).
Unverified: 4.4 Windows build and title-screen screenshot (PC), two-process Relay walk.
