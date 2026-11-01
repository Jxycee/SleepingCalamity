# Things for the user to do

These are the steps only the user can take. Claude keeps this list current: when you finish an item, tick it or tell Claude.

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
