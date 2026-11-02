# Things for the user to do

These are the steps only the user can take. Claude keeps this list current: when you finish an item, tick it or tell Claude.

## Tonight (2026-11-02): manual test plan, in order
Main will be checked and quiet before you start (the coordinator holds Asset merges while you test).
1. **3c solo, the M6 square loop** (~40 min). Covers the dive too (3b's floors 1-4), now with **tier 1 enemies only** in the Cellars. Note how the solo dive feels: too hard, too easy, too long.
2. **Look while you walk:** Terrace and Keep stair flights (should read as steps, no black strip), butterflies/bees, chimney smoke, the square.
3. **2-player items with Multiplayer Play Mode** (two editors, no second person needed): 3c steps 13-17, then 3b's netcode-only checks. Item 3 (real Relay with a friend) can wait.
4. **Read and answer:** the 6 open questions in `docs/design/m7-proposal.md` section 3 (they shape M7 S2-S8).
5. **If time:** the Mixamo clips (item 1).
Also check (stair hand-over fix, just merged): take the F1->F2->F3 stairs as host and as client, go home, abandon at F8, a wipe, and a client joining mid-dive. Nobody should sink or fall at a stair; dodge/sprint may be blocked for a split second right after arriving.
In the 2-player run, also try standing on the other hero's head: if you can perch there, say whether that's fine or should be blocked.
Tip: **F4** toggles the dev overlay (dive time, damage taken, heals, loaves, stir, per-room clear times; editor/dev builds only). Note anything it shows that feels off.
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
3b. **The M5 Cellars walk, floors 1-4 (2 players; spine `e525a51` + contents `c75c33f8`; an automated host+client run of this script passes).** About 20-30 minutes. The town and the Undercroft stair come in M6, so the dive starts from the Arena_Dive hub. The bottom of the screen shows `[E] ...` whenever an action is in reach; the stir meter sits on screen during a dive.
   1. Both machines: turn on the dev panel toggle "Start in the Cellars dive hub" (or editor menu `SleepingCalamity/Dev/Dive Hub`, or a build with `-dev-dive`).
   2. Host: press "Host online (Relay)" and read out the join code. Second player: "Join by code", enter it. Host: "Start session". Both land on the hub pad.
   3. Walk into the stair mouth to the violet column: `[E] Go down the Undercroft stair`. Either player presses E. Both of you appear at floor 1's entry.
   4. Floor 1: walk into rooms (enemies spawn when someone first steps into a combat room). One enemy carries the gate key: kill it and a gold cube drops; walk over it. Only now does the stair open.
   5. On the way: press E at chests (loot goes into the shared haul; a key chest gives the room key, then walk up to the locked gate or press E there to open it). Walk over the bakery pieces (spheres) to pick them up; the "pieces left" count drops for both of you.
   6. At the stair column: `[E] Take the stair down`. Repeat 4-6 on floors 2 and 3.
   7. Floor 2 checkpoint (lamp nook): `[E] Bank the haul at the checkpoint`. Banked pieces are safe and the party heals. A healing point may also stand on some floors (walk onto it; 30 s recharge).
   8. Watch the stir meter climb as you clear floors and carry pieces: at 40 rooms get one extra enemy, at 70 a big rat-king hunts you, at 100 you have 90 s to reach a bank point.
   9. Floor 4, the bakery: fight past the Dough Warden in the antechamber to the baker's room. `[E] Bank the haul with the baker`, then `[E] Take the party home` at the green column. You are back on the hub pad.
   10. Optional fail test: start a second dive (the stair offers floor 3 now, as a return dive), pick up a piece, and let the party fall (or host presses F8 to abandon). You return to the hub and the unbanked piece is back on its floor next time.
   Debug keys (host, dev builds): F7 starts a dive from anywhere, F8 abandons it. Tell Claude what broke, what felt wrong, and whether floors felt samey.

3c. **The M6 square-loop walk (S6), solo then 2 players.** About 30-40 minutes each. An automated host+client run of this loop (`SquareLoopWalkQaTests`) passes; this walk checks what it cannot: keys, prompts, camera, look and feel. Hollowmere is the default start scene now, so do NOT turn on the dive-hub dev toggle. Bottom-of-screen prompts read `[E] ...`; key names below are the defaults (check yours in the controls screen).
   **Solo**
   1. Press Solo / Auto Solo. You spawn in Hollowmere by the cistern house. Check the town stands as saved (rebuilt buildings, the ruined bakery lot) and nothing blocks you at the start.
   2. Walk to the cistern-house stair. `[E] Go down the Undercroft stair` appears. Press E: you land on floor 1.
   3. Dive as in 3b (steps 4-9): collect bakery pieces (floors 1-3), bank at the floor-2 checkpoint, fight to the floor-4 heart, `[E] Bank the haul with the baker`, then `[E] Take the party home` (green column).
   4. You are back at the stair top in Hollowmere. The haul should now be town stock: the bakery pieces, loot and the baker. Note anything missing.
   5. Walk to the bakery lot's build point. `[E] Open placement mode`. Press E: the camera lifts to a top-down view, the hero stands still.
   6. Choose a lot (move keys / stick move to the neighbouring lot; E cycles to the next free lot). Ghost is green when allowed, red when not. Space turns it a quarter turn. Put it where it would cut a lane or block the path: it must read red and left-click (primary) must do nothing.
   7. Put it on a legal lot and left-click: the frame appears. Read the three baker wishes in plain words; they tick live as you move the ghost between lots. Try one lot where a wish fails, then settle on one where all three tick.
   8. Left-click again for each part: oven, door, sign (each appears as it lands). Right-click (secondary) picks up the last thing placed; place it again.
   9. Left-click once more to place the baker. He appears at his door. All wishes ticked: the service opens.
   10. Esc leaves placement mode (the world saves). Walk away; the lock is released.
   11. Go to the stair again and start a dive: you should carry **3 loaves**. Take some damage, eat one (check the key in the controls), HP rises.
   12. Quit to the main menu, start Solo again (same world). The bakery and baker still stand, the banked stock is the same, the loaves/dive state are sane.
   **2 players** (two editors with Multiplayer Play Mode, or editor + dev build; Relay as in item 3)
   13. Host + join by code, Start session. Both land in Hollowmere by the cistern house.
   14. Host places the bakery frame; the client must see the same ghost, the wish ticks and the placed building live (a "Someone is building" prompt shows for the other player). The client cannot start placing while the host holds the lock.
   15. Client builds the parts and places the baker; host watches. Try placing the frame in a path-blocking spot from the client: refused.
   16. Both dive: each gets 3 loaves. Go through bank and home with both. A downed player must not be able to take the stair.
   17. Host quits; restart as host with the same world and let the client rejoin: town, stock, baker and both players' weapon growth survive.
   Tell Claude every step that surprised you, any prompt that lied, and whether placement felt clear.

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
