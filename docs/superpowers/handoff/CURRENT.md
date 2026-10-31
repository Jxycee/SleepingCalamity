# CURRENT: Sleeping Calamity running handoff

## Now
- 2026-10-31 ~15:50 UTC: **session 5 is coordinator** (board `claim`; note `C:/Users/jayce/.claude/coordinator/_handoff.md`). Q7 room pool merged `513ac08` (21 rooms, 61 tests). Running: Q8 town wall (15%, `claude/brave-keller-3d91gk`), Q6 garden (r2 self 27/40, Unity placement `a92a1ef`, final run then check). Paused for usage (5 h window 74% at 15:46, resets 17:10): session "Wave 1: Bakery" carrying both the **dungeon corner less-orange polish** (still WIP: `7dd1592`/`247ae4d`; the earlier note that 588dd81 passed check was wrong, 588dd81 is a STATUS commit) and **Q9 keep** (art done `5262394`, integration left). User approval of the dungeon corner ("make it a bit less orange and then I approve") gets recorded here once that polish merges. Archive ready (user, sidebar): Wave 0A, townhouse, stalls, inn, heroes' home, tithe gate, square/churn/room_pool. Weekly 53%.
- ~15:35 UTC: **session 4 handing off** (context 88%; note `C:/Users/jayce/.claude/coordinator/_handoff.md`). Merged since 13:30: square `c66ea1f`, foliage `9c9e891`, Q3 ring 2 `d87f6bf`, Q5 churn fix `2f90e87`, Q4 dungeon corner `9cf4559` (user: "make it a bit less orange and then I approve"; polish pass running). User rules: new job = new session (QUEUE claiming CLOSED `097f916`); archive sessions that say ready. Pending starts (desktop message needed): Q10 lake, Q11 lanes, successor coordinator.
- ~13:30 UTC: merged since 08:00: tithe gate `6260a84`, inn `2c436ed`, heroes' home `fd9714b`, townhouse `bca6ee8`, Q1 kits (cottage + workshop) `3e4c2d8`, Q2 smithy/schoolhouse/mill `89d361d` (all `SC RESULT check PASS`, test-edit 591-594, test-play 32 + 8 skipped). Running: square (94%), foliage (check PASS, merging), Q3 ring 2 fill, Q4 dungeon corner. First 5 h window ended at ~94% (pacing log `C:/Users/jayce/.claude/coordinator/_paused.txt`); window 2 is 12:10-17:10 UTC. Cloud sessions often end their turn after a step and sit idle: nudge on a stall. Subagent handback fails in this session: have agents write summaries to a file.

## Next
1. 17:10 UTC reset: resume the Bakery session (dungeon polish, then Q9 keep integration); pacing cron `4,24,44`, log `C:/Users/jayce/.claude/coordinator/_paused.txt`.
2. On the user's next desktop message: start Q10-Q15 (`Reference/hollowmere/QUEUE.md`) as NEW cloud sessions (env "SleepingCalamity Cloud", one job each), within pacing; Q12 look pass after Q8 merges.
3. Merge Q6 garden and Q8 wall on green check; record the dungeon corner approval. Older history: `history-2026-10-31.md`. The user owes: `USER_TODO.md` (Mixamo clips, 2-player walks).

## Branches and worktrees
- `main` at `6c99120` + this handoff (last code check: Q7 `513ac08`, earlier Q4 `9cf4559` SC RESULT check PASS 609 / 32 + 8). Open cloud branches: `claude/sharp-goldberg-vpyt8c` (dungeon polish WIP + Q9 keep, paused), `claude/ecstatic-bardeen-pr8wbk` (Q6 garden), `claude/brave-keller-3d91gk` (Q8 wall).
- B11 cellar lantern merged by its cloud session (`7f9adc1`, look locked 28.5/40); free asset sources in `Art/Source/`. No open branches besides its merged `claude/practical-wozniak-vk1q0j`.
- 2026-10-30 session 3: 7 merged remote branches deleted (user OK); `claude/combat-depth-276a8c` kept (live session), `claude/determined-thompson-6z2mml` until the bell tower merges.
- Cleanup 2026-10-30: finished agent worktrees and all merged local branches removed. 16 merged remote branches deleted on GitHub (user OK). Session worktrees `determined-boyd-9ac4b8` and `infallible-bassi-581d4e` stay until their sessions are archived.

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
