# Things for the user to do

These are the steps only the user can take. Claude keeps this list current: when you finish an item, tick it or tell Claude.

## Tonight (2026-11-02): manual test plan, in order
Main will be checked and quiet before you start (the coordinator holds Asset merges while you test).
1. **3b solo, the loop walk** (~30 min; the PL-4 release build). Covers the dive too. Note how the solo dive feels: too hard, too easy, too long.
2. **Look while you walk:** Terrace and Keep stair flights (should read as steps, no black strip), butterflies/bees, chimney smoke, the square.
3. **2-player items with Multiplayer Play Mode** (two editors, no second person needed): 3b's two-player steps. Item 3 (real Relay with a friend) can wait.
4. **Read and answer:** the 6 open questions in `docs/design/m7-proposal.md` section 3 (they shape M7 S2-S8).
5. **If time:** the Mixamo clips (item 1).
Also check (stair hand-over fixes, incl. the floor-collider sync `d6eaddbc` that fixed heroes sinking up to 1.27 m): take the F1->F2->F3 stairs as host and as client, go home, abandon at F8, a wipe, and a client joining mid-dive. Nobody should sink or fall at a stair; dodge/sprint may be blocked for a split second right after arriving.
In the 2-player run, also try standing on the other hero's head: if you can perch there, say whether that's fine or should be blocked.
Tip: **F4** toggles the dev overlay (dive time, damage taken, heals, loaves, stir, per-room clear times; editor/dev builds only). Note anything it shows that feels off (heals now count one per loaf eaten, `2ff6e1f2`).
Tell the coordinator every surprise, a lying prompt, a stuck spot or a crash (paste the console line).

## Now (unblocks Phase 1 work)
1. **Download the remaining Mixamo clips** for M2/M3 (Y Bot and 4 idles done; Quaternius CC0 and the Kevin Iglesias packs cover most). The list and settings are in `docs/design/mixamo-clips.md`: about 12 clips left (the Kevin Iglesias packs cover the rest), saved into `C:\Dev\_private\mixamo\`. Needs your Adobe account. *Unblocks: M2 animation (movement code can start without it).*
2. ~~Link Unity Gaming Services~~ (done 2026-10-30, `7e9c6c7`).
3. **Waiting for a second player (user, 2026-10-30): the 2-player Relay walk.** Also waiting: the M2 2-player checks and the M7 2-player loop. Solo testing goes ahead without them.
   **Do the 2-player Relay walk** once item 2 is done (about 5 minutes). Use two editors with Multiplayer Play Mode, or one editor plus a development build:
   1. On one, press "Host online (Relay)" and note the join code.
   2. On the other, press "Join by code" and enter it.
   3. When both players show, press "Start session" on the host. Both players should land in Arena_Greybox.

   Tell Claude what happened.
3a. **Audit checks for the same 2-player Relay walk (2026-11-12, `docs/superpowers/audits/2026-11-12-save-netcode-audit.md`).** These need real Relay and two machines; no test can run them. About 10 minutes.
   1. **Crash and rejoin (A1).** Mid-dive, the second player ends the game from Task Manager, relaunches at once and joins by the same code within about 10 seconds. Expected today: the join fails or the player is dropped a few seconds after arriving, and a second try works. Say what the screen showed and how long each step took.
   2. **Host quits (A10).** Mid-session the host presses Alt+F4. Time how long the second player's game takes to return to the title, and what it says.
   3. **Join while the host is loading (A4).** The host presses "Start session"; the second player joins by code during the host's loading screen. Does the second player get a hero in the hub?
3b. **The loop walk on the PL-4 release build (replaces the old 3b M5 and 3c M6 walks; plan `docs/superpowers/plans/2026-11-11-playable-loop.md` section 5).** On the build, no dev panel, no dev keys, no flags. If a step needs any of them, the milestone is not done. The build folder and a title-screen screenshot are named in `Reference/sessions/pl4-way-out/STATUS.md`. Write surprises in `docs/playtest/NOTES.md` and tell Claude every one (a prompt that lied, a stuck spot, a crash: paste the console line).
   **Solo, about 30 minutes**
   1. Start the game. A title screen, not a grey box. Choose to play alone, make a new world.
   2. You stand in Hollowmere. A line on screen tells you to find the stair, and you can see where it is.
   3. At the trapdoor a prompt appears. Press Interact. You are on floor 1 with a floor card.
   4. Play down. On floor 2 you find the bakery frame; the pieces-left count drops.
   5. Come home either way: fall, or reach the bank room on floor 5 and choose to go home. A card says what came home and what was lost.
   6. The line now points at the bakery lot. Walk there, press Interact, place the frame on a lot that reads green. Try a spot that blocks a lane: refused, in words.
   7. Press Pause to leave placement. Press Pause again: a menu. Read the controls. Leave to the title.
   8. Continue the same world. The frame stands. Quit from the menu.
   9. Stretch: dive again for the oven, door and sign, wake the baker on floor 3, finish the bakery, place the baker, and start the next dive with 3 loaves. Take a hit and eat one with the button the HUD names. After going home from the bank, the trapdoor offers the bank floor first; the prompt names the button that changes the floor (Tab, or right-stick click on a pad).
   **Two players, about 40 minutes.** A second person on their own PC. Before that, a ten-minute **one-PC Relay rehearsal**: the editor and the build side by side on two profiles (or two builds); this is the first time Relay carries two players.
   1. Host: play with friends, new multiplayer world. The lobby shows a join code and one name. Start is greyed out and says it needs two players.
   2. Guest: join, type the code. Guest sees "waiting for the host", host sees two names. Host starts. Both stand in Hollowmere.
   3. Either player presses Interact at the trapdoor: both are on floor 1. Each finds a piece. One falls; the other revives them with a hold.
   4. Reach the bank room. One chooses to go home, the other confirms. Each sees their own result card.
   5. Host places the frame while the guest watches the ghost move. Host leaves placement; guest takes over and places a part. Guest tries a lane-blocking spot: refused.
   6. Guest opens the pause menu and leaves. Host keeps playing. Guest joins again with the same code and is back in town.
   7. Host leaves from the pause menu after the warning. Guest sees "the host ended the session" in words and lands on the title.
   8. Host cannot continue the world alone (a multiplayer world needs two): the lobby says so. Guest joins, host starts: the frame, the part, both players' weapon growth and bags are as they were.
   Extra for the two-player run: take the trapdoor as the guest with a held checkpoint and change the floor with the pick button; the host starts on the floor the guest picked. Standing on the other hero's head: say whether that is fine or should be blocked.

## Cloud sessions (so a cloud session can work like the PC)
- **Set up the cloud environment** (title-bar environment menu > Edit; details in `docs/superpowers/handoff/cloud-environment.md`):
  1. Setup script: paste the whole of `Tools/cloud/setup.sh`.
  2. Network access: Full.
  3. Environment variables `UNITY_EMAIL` and `UNITY_PASSWORD` (your Unity account; it must not use two-factor sign-in). The cloud activates its own Personal licence with them. Without them it can compile and test Core but can't launch Unity.
  4. Optional: a second environment, "SleepingCalamity Art", with the same settings and `SC_PROFILE="${SC_PROFILE:-art}"` in its script, for live Blender.

## Done: M3 retest
- Answered 2026-10-30: yes to all 8 (`docs/design/m3-combat-greybox.md`).

## Soon
4. **Pick Tobin's concept** from the options Claude generates (M10). Claude will ask you when the options are ready.
5. ~~Dump your PS2 BIOS~~ (done 2026-10-30: PCSX2 runs DC1, KH1 and KH2 for measurements).

## When Claude asks
6. **Playtests at the milestone gates:**
   - M3: done 2026-10-30 (solo arena, all 8 yes).
   - M7: play the full 2-player loop.
   - M9: approve the town and dungeon corners on captures.
   - M10: approve Tobin.
7. **Go through the open decisions** in `docs/superpowers/handoff/decisions-for-review.md`. Claude asks the important ones directly.
