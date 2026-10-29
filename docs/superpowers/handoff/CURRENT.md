# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-29. First session in the new repo (handoff `2026-10-29-new-project-handoff.md` section 6) is done apart from locking the story.
- **Repo setup:**
  - Unity 6000.6.5f1 URP project, built from the URP blank template with the handoff's package pins (`bb22786`).
  - Infrastructure copied from FishGame HEAD with the fg/FishGame names renamed to sc/SleepingCalamity.
  - Assembly skeleton: Core, Data, Runtime, Gameplay, Presentation, UI, App, Networking, Editor, plus the EditMode and PlayMode test assemblies. ArchitectureTests plus a URP smoke test and a PlayMode smoke test.
  - `unity.ps1 check`: `SC RESULT check PASS` (build PASS, test-edit 2/2, test-play 1/1), run on the skeleton tree before its commit.
- **User decisions, 2026-10-29:** all in `CLAUDE.md` "Settled design rules" and `docs/STORY.md`'s header.
  - Players: solo, or 2-4 co-op with difficulty scaling and 4 recorded players per world.
  - Ghidra PS2 extensions: OK to download.
  - Disc dumps ready: DC1, KH1, KH2. Prompt the user for the ISOs once the decomp research is done.
- **Phase 0:**
  - Story: `docs/STORY.md` draft 3 (`897acca`), not locked yet. The user is reading it.
  - Lessons: `docs/design/dark-cloud-lessons.md`.
  - DC2 research from the decomps is done: `docs/research/dc2-combat-weapons.md`, `docs/research/dc2-dungeons-town.md`.

## Next
1. Lock `docs/STORY.md` with the user (section 9 holds its open questions).
2. REA on DC1 is in progress (a subagent writes `docs/research/dc1-rea.md`). Setup is done (2026-10-29):
   - ISOs are in `C:\Users\jayce\Desktop\Games\ps2\`.
   - ELFs are extracted to `C:\Dev\_private\ps2\{dc1,kh1,kh2fm}\`.
   - ghidra-emotionengine-reloaded v2.1.38 is installed in Ghidra 12.1.4. There is no ps2sdk extension to install.
   - **DC1's ELF keeps 4,181 named functions.** KH1 and KH2FM are stripped.
   - Once DC1 is done: KH1/KH2 lock-on and combat feel (stripped, so slower), then the gaps listed at the end of each DC2 note.
3. Build the reference library for Hollowmere and the Undercroft (handoff 3.2). First rewrite `sc-reference` for the new style.
4. Then write the Phase 1 plan, `docs/PHASE1.md` (handoff 3.4 and 3.5).

## Branches and worktrees
- `main` only.

## Blockers and open questions
- The story isn't locked yet (user).
- The asset skills still carry FishGame's Riverlight style rules. Rewrite them from the style bible before any art work.
- Not ported yet, to bring in when the plan reaches them:
  - FishGame code: Networking, App flow, Runtime services, Core utilities, builders and validators.
  - FishGame tools: the unity-mcp package and `.mcp.json`.

## Read next
- `docs/superpowers/handoff/2026-10-29-new-project-handoff.md`: phase plan, the parts bin, risks.
- `docs/STORY.md`, `docs/design/dark-cloud-lessons.md`, `docs/research/`.
- Decomp clones (read-only, never copied into the repo): `C:\Dev\_research\ChronicleTwo`, `C:\Dev\_research\OpenCloud`. OpenCloud turned out to hold almost no DC1 code, so DC1 needs REA.

## Inbox
- (none)
