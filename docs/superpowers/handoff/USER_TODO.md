# Things for the user to do

These are the steps only the user can take. Claude keeps this list current: when you finish an item, tick it or tell Claude.

## Now (unblocks Phase 1 work)
1. **Download the Mixamo clips** for M2/M3. The list and download settings are in `docs/design/mixamo-clips.md`: Y Bot plus about 22 movement clips and the combat sets, saved into `C:\Dev\_private\mixamo\`. Needs your Adobe account. *Unblocks: M2 animation (movement code can start without it).*
2. **Link Unity Gaming Services** so online co-op can be tested:
   1. Open the project in Unity, go to Project Settings > Services, link it to a Unity Cloud project, and sign in.
   2. On the Unity Cloud dashboard, enable Authentication with anonymous sign-in.
   3. On the same dashboard, enable Relay and Lobby (Multiplayer Services / Sessions).
   4. Close the editor afterwards, because Claude's batch runs need it closed.

   *Unblocks: the manual 2-player Relay walk, the last step of M1.*
3. **Do the 2-player Relay walk** once item 2 is done (about 5 minutes). Use two editors with Multiplayer Play Mode, or one editor plus a development build:
   1. On one, press "Host online (Relay)" and note the join code.
   2. On the other, press "Join by code" and enter it.
   3. When both players show, press "Start session" on the host. Both players should land in Arena_Greybox.

   Tell Claude what happened.

## Soon
4. **Pick Tobin's concept** from the options Claude generates (M10). Claude will ask you when the options are ready.
5. **Optional: dump your PS2 BIOS** into `C:\Dev\_private\ps2\bios\`, using your own console and a BIOS dumper. It lets Claude capture real Dark Cloud town and dungeon frames in PCSX2, which are scarce online.

## When Claude asks
6. **Playtests at the milestone gates:**
   - M3: play a 5-minute combat arena and say whether the feel is right.
   - M7: play the full 2-player loop.
   - M9: approve the town and dungeon corners on captures.
   - M10: approve Tobin.
7. **Go through the open decisions** in `docs/superpowers/handoff/decisions-for-review.md`. Claude asks the important ones directly.
