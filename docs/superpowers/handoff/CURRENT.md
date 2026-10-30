# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-30, session 2 (controller on the PC). Phase 1 underway.
- **Merged to main:** M1, M2, M4, M8 (session 1) and now **M3 combat greybox** (`af71ed0`, `SC RESULT check PASS` EditMode 484, PlayMode 29 + 1 skipped). Risk review, fix re-check and branch review all passed; the fixes: unique host-wide enemy attack serials, spell pip spent at cast, finisher prior from the owner, host-written weapon swap, deterministic netsim tests (0% loss: every hit within one round trip), downed heroes can't jump, out heroes stand up at wave clear. Sounds are Kenney CC0 (`docs/audio/SOURCES.md`).
- **M3 is NOT done:** waits for the user's 5-minute arena verdict and the 2-player netsim walk (no second player yet).
- **UGS linked** (`7e9c6c7`): Authentication, Relay and Lobby on the dashboard. Packages kept per the user: Microsoft GDK, IAP 5.4.4, patch bumps (`b4563ac`, `c2fd91e`).
- **Animation sources gathered** (decisions A25): Quaternius UAL1/UAL2 free (CC0) in `C:\Dev\_private\quaternius\`; Kevin Iglesias FREE packs (Basic Motions, Archer, Spellcasting, Crafting, Throwing, Skeleton) in the Asset Store cache `%APPDATA%\Unity\Asset Store-5.x\Kevin Iglesias\`; Mixamo Y Bot + 4 idles in `C:\Dev\_private\mixamo\`. About 12 Mixamo clips still owed (`docs/design/mixamo-clips.md`). Research: `docs/research/animation-sources.md`, `docs/research/libraries.md`.
- **Decisions this session:** `decisions-for-review.md` A25-A30. PCSX2 free cam (PINE) waits for M9 look work (user).
- **The user owes:** `docs/superpowers/handoff/USER_TODO.md` (arena playtest, remaining Mixamo clips).

## Next
1. **M3 retest (user):** readability pass merged (`d60b4b8`; `SC RESULT check PASS` on `1c44f60`: EditMode 519, PlayMode 31 + 2 skipped). Bow fixed (1-frame window skipped under 60 fps, dropped buffered clicks, ray over short enemies), hit flash/hitstop/whiff/X marker, Core `Telegraph` footprints matching the hit test, shorter enemy reaches, Tier1Atk x0.65, gentler waves 1-2. The user answers the 8-question checklist in `docs/design/m3-combat-greybox.md`. Known: wave 4 to 5 is a x3.5 attack step; retune after the retest. Combat timing research done: DC1 (`4d9bbca`) and KH1/KH2 (`671c211`) measured live in PCSX2, in `docs/research/combat-timings.md` (gaps listed there); use it for the post-retest retune. Bot playtester merged (`ea42243`, check PASS on `93c4948`; test-only `ArenaBot` + `BotPlaytestTests`, `SC_BOT=1`); its 2 s wave-1 stall was the bot walking into a pillar, fixed (`cf6a6e5`, check PASS on `5c4bced`).
2. **Hero animation merged** (`e53369b`, `SC RESULT check PASS` EditMode 502, PlayMode 30 + 2 skipped; the skips are Explicit capture tools). Y Bot plays Quaternius/Kevin Iglesias/Mixamo clips via `AnimatorHeroDriver`; owed Mixamo clips drop into `Content/Animation/Mixamo/` by name hint (`docs/animation/SOURCES.md`; build log prints `SC ANIM placeholder`). Rough: bow aim pose, guard shield placement.
3. **M9** (up to 3 art agents, P51): beauty corner first (B4+B6, B7+B8, B10, B9), then the dungeon corner (B5, B11) with the cellar toward the user's reference image (A24).
4. **M5** room-pool authoring alongside; **M10** Tobin after.
5. Open: M2/M3/M7 2-player walks wait for a second player. M4/M6 persistence must key growth by the persistent player id, not the session client id (`CombatHost.cs:95`).

## Branches and worktrees
- `main` at `cf6a6e5` (green). Merged this session: `m3-combat`, `m2-hero-anim`, `m3-readability`.
- Merged: `m1-foundation`, `m2-hero-camera`, `m3-combat`, `m4-*`, `m8-*`. Worktrees `agent-a3f4b7d611d7e2923` (M3), `agent-a884291bf0ad9ef08` (M8), `agent-ad25d53219a200b3c` (M4) are finished; their imported Libraries can be reused.

## Blockers and open questions
- The asset skills were rewritten on 2026-10-30. Some legacy FishGame tools still need rework before the art batches; see the tool table in `sc-asset-craft` section 0b.
- `sc-walk` documents WalkDriver helpers that don't exist here yet; they arrive with the App/Dev port.

## Read next
- `docs/PHASE1.md`, then `docs/superpowers/handoff/decisions-for-review.md`.
- `docs/STORY.md`, `docs/design/dark-cloud-lessons.md`, `docs/research/` (start with each note's summary).
- `docs/superpowers/handoff/2026-10-29-new-project-handoff.md`: the founding plan and the parts bin.
- Private research material, outside the repo:
  - `C:\Dev\_private\ps2\` holds the extracted ELFs and the DC1 Ghidra project and decompile (setup in `docs/research/dc1-rea.md`).
  - `C:\Dev\_research\` holds the ChronicleTwo, OpenCloud, OpenKH and KH2FM decomp clones.
  - The ISOs are in `C:\Users\jayce\Desktop\Games\ps2\`.

## Inbox
- Trial ramenvr/autopilot (AI playtester in the editor) as an automated smoke tester once M3 combat settles; evaluated 2026-10-30 by a side session, nothing installed.
