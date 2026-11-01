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
3b. **The M5 Cellars walk, floors 1-4 (2 players; spine merged `e525a51`).** The town and the Undercroft stair are not wired in yet (M6), so the dive starts from the Arena_Dive hub:
   1. Turn on the dev panel toggle "Start in the Cellars dive hub" (or the editor menu `SleepingCalamity/Dev/Dive Hub`, or launch a build with `-dev-dive`), then host (solo or "Host online (Relay)") and have the second player join by code.
   2. In the hub, walk to the violet column in the stair mouth and press **E** to start the dive (host picks the seed). Debug keys, host only, dev builds: **F7** starts a dive from anywhere, **F8** abandons it.
   3. On floors 1-3 press **E** at the stair column to go down; on floor 2 press **E** at the bank column (checkpoint: banks the haul and heals); on floor 4 press **E** at the bank in the baker's room, then **E** at the green column to go home.
   4. Expect: both players land on each floor's entry together and see the same floor. Enemies, keys and chest loot come with the contents session's merge; until it lands the stairs are always open. Tell Claude what felt wrong or samey.

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
