# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-30, morning after an autonomous overnight run. All four Phase 0 outputs exist in a first version.
- **What exists:**
  - **Locked story:** `docs/STORY.md` (`bf518ff`).
  - **Reference library:** Hollowmere and the Undercroft, 88 CC0/PD/CC BY images with palette targets (`eba7205`).
  - **Research:** DC1 REA, DC2 decomp, Kingdom Hearts, URP look, genre landscape, in `docs/research/`.
  - **Phase 1 plan:** drafted, not yet approved: `docs/PHASE1.md` (`7e1ad0a`).
- **The user owes:** going through `docs/superpowers/handoff/decisions-for-review.md`. It has rows S1-S8, T1-T5 and A1-A6, and A6 points to PHASE1's decisions P1-P51.
- **Last `check`:** `SC RESULT check PASS` (build PASS, test-edit 2/2, test-play 1/1) on the skeleton (`29fa5ce`). No Unity changes since.

## Next
1. Review the decisions with the user, apply their changes to `PHASE1.md` and the other docs, and mark Phase 1 approved.
2. Start Phase 1 at M1 (PHASE1 section 2): port the FishGame code (Networking, App flow, Save, Core utilities) and stand up the networked greybox.
3. Fill the reference-library gaps the next time art comes up:
   - coal cellars, brick sewers, a market square with banners, a hill town above a lake;
   - flat plaster and oak close-ups;
   - a measured OoT-2026 frame (screens are out 5 Nov 2026), kept in `Reference/private/`.

## Branches and worktrees
- `main` only.

## Blockers and open questions
- The Phase 1 plan isn't approved yet (user).
- The asset skills other than `sc-reference` still carry FishGame's Riverlight style rules. Rewrite them from PHASE1 section 3 (the style bible) before the art batches.
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
- (none)
